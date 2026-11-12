using System.Diagnostics;
using System.IO;
using NyangShinobi.Contracts;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.App.Diagnostics;

/// <summary>화면 읽기만 수행한다. 게임 캡처/프레임 스케줄러에 등록하지 않는다.</summary>
public sealed class CaptureBenchmark(IAdbCommandRunner runner, IImageFrameDecoder pngDecoder, IGameClock clock,
    VisionOptions visionOptions)
{
    public const int DefaultSamples = 10;
    public const int DefaultWarmups = 1;

    public Task<CaptureBenchmarkResult> RunAsync(string adbPath, string serial, IProgress<string>? progress,
        CancellationToken cancellationToken, int samples = DefaultSamples, int warmups = DefaultWarmups) =>
        Task.Run(() => RunCoreAsync(adbPath, serial, progress, samples, warmups, cancellationToken), cancellationToken);

    private async Task<CaptureBenchmarkResult> RunCoreAsync(string adbPath, string serial, IProgress<string>? progress,
        int count, int warmups, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbPath);
        AdbDeviceClient.ValidateSerial(serial);
        if (count is < 1 or > 100 || warmups is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(count));
        // 장치 확인은 측정 밖에서 한 번만 수행한다. 모든 실제 캡처는 명시한 -s를 사용한다.
        var devices = await new AdbDeviceClient(runner).ListDevicesAsync(token).ConfigureAwait(false);
        if (devices.Count(device => device.Serial == serial && device.State == "device") != 1 ||
            devices.Count(device => device.Serial == serial) != 1)
            throw new InvalidOperationException("선택한 serial의 준비된 장치가 없습니다. 다시 조회하고 직접 선택하세요.");
        var rawDecoder = new RawBenchmarkDecoder(visionOptions);
        var all = new List<CaptureBenchmarkSample>();
        var states = new[] { new MethodState(BenchmarkCaptureFormat.Png), new MethodState(BenchmarkCaptureFormat.Raw) };
        var canceled = false;
        long frameId = 0;
        try
        {
            for (var phase = 0; phase < 2; phase++)
            {
                var rounds = phase == 0 ? warmups : count;
                for (var round = 1; round <= rounds; round++)
                {
                    var order = round % 2 == 1 ? states : states.Reverse().ToArray();
                    foreach (var state in order)
                    {
                        token.ThrowIfCancellationRequested();
                        if (state.Error is not null) continue;
                        progress?.Report($"{(phase == 0 ? "예열" : "측정")} {round}/{rounds} · {state.Format}");
                        try
                        {
                            var started = Stopwatch.GetTimestamp();
                            string[] args = state.Format == BenchmarkCaptureFormat.Png
                                ? ["-s", serial, "exec-out", "screencap", "-p"] : ["-s", serial, "exec-out", "screencap"];
                            var response = await runner.RunAsync(args, token).ConfigureAwait(false);
                            var received = Stopwatch.GetTimestamp();
                            token.ThrowIfCancellationRequested();
                            if (response.ExitCode != 0) throw new IOException($"ADB 실패 ({response.ExitCode}): {response.StandardError}");
                            var conversionStarted = Stopwatch.GetTimestamp();
                            ImageFrame frame;
                            if (state.Format == BenchmarkCaptureFormat.Png)
                            {
                                frame = await pngDecoder.DecodePngAsync(response.StandardOutput, ++frameId, clock.LocalNow, token).ConfigureAwait(false);
                                state.Layout = "PNG → BGRA32";
                            }
                            else
                            {
                                var raw = await rawDecoder.DecodeAsync(response.StandardOutput, ++frameId, clock.LocalNow, token).ConfigureAwait(false);
                                frame = raw.Frame;
                                state.Layout = $"header={raw.HeaderBytes} · format={raw.Format} · color={raw.ColorSpace?.ToString() ?? "없음"} · stride={frame.Stride} → BGRA32";
                            }
                            token.ThrowIfCancellationRequested();
                            var completed = Stopwatch.GetTimestamp();
                            if (frame.PixelFormat != FramePixelFormat.Bgra32 || frame.Stride != checked(frame.Width * 4) ||
                                frame.Pixels.Length != checked(frame.Stride * frame.Height))
                                throw new InvalidDataException("최종 ImageFrame의 BGRA32/stride/길이가 올바르지 않습니다.");
                            state.Support = BenchmarkSupport.Verified;
                            state.LastFrame = frame; // 표본마다 픽셀을 누적하지 않고 방식별 마지막 한 장만 보관한다.
                            if (phase == 1)
                            {
                                var sample = new CaptureBenchmarkSample(state.Format, round, Stopwatch.GetElapsedTime(started, received),
                                    response.StandardOutput.LongLength, Stopwatch.GetElapsedTime(conversionStarted, completed),
                                    Stopwatch.GetElapsedTime(started, completed), frame.Width, frame.Height, frame.Stride);
                                state.Samples.Add(sample);
                                all.Add(sample);
                            }
                        }
                        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or TimeoutException
                            or global::OpenCvSharp.OpenCVException)
                        {
                            state.Error = $"{exception.GetType().Name}: {exception.Message}";
                            if (exception is NotSupportedException) state.Support = BenchmarkSupport.Unsupported;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { canceled = true; }
        return new(adbPath, serial, count, warmups, canceled,
            states.Select(state => new BenchmarkMethodResult(state.Format, state.Support, state.Error, state.Layout,
                state.LastFrame, state.Samples.ToArray())).ToArray(), all.ToArray());
    }

    private sealed class MethodState(BenchmarkCaptureFormat format)
    {
        public BenchmarkCaptureFormat Format { get; } = format;
        public BenchmarkSupport Support { get; set; }
        public string? Error { get; set; }
        public string Layout { get; set; } = "미확인";
        public ImageFrame? LastFrame { get; set; }
        public List<CaptureBenchmarkSample> Samples { get; } = [];
    }
}
