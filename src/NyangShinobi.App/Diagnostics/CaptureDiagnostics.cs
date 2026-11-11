using System.Diagnostics;
using NyangShinobi.Contracts;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.App.Diagnostics;

public sealed record DiagnosticCapture(ImageFrame Frame, TimeSpan TotalDuration, TimeSpan DecodeDuration);
public sealed record DiagnosticMatch(DetectionResult Result, TimeSpan Duration);

/// <summary>수동 개발자 진단 전용. 입력 컨트롤러/게임 흐름을 의존성으로 받지 않는다.</summary>
public sealed class CaptureDiagnostics(AdbOptions defaults, IImageFrameDecoder decoder, IGameClock clock,
    IVisionEngine vision, TemplateRepository templates, Func<AdbOptions, IAdbCommandRunner>? runnerFactory = null)
{
    private string? currentPath;
    private AdbDeviceClient? client;
    private AdbScreenCapture? capture;
    private readonly TimedDecoder timedDecoder = new(decoder);

    public string DefaultAdbPath => defaults.ExecutablePath;

    public Task<IReadOnlyList<TemplateDescriptor>> GetAssetsAsync(CancellationToken cancellationToken) =>
        templates.GetDescriptorsAsync(cancellationToken);

    public Task<IReadOnlyList<AdbDeviceInfo>> ListDevicesAsync(string adbPath, CancellationToken cancellationToken)
    {
        Connect(adbPath);
        return client!.ListDevicesAsync(cancellationToken);
    }

    public async Task<DiagnosticCapture> CaptureAsync(string adbPath, string serial, CancellationToken cancellationToken)
    {
        Connect(adbPath);
        var started = Stopwatch.GetTimestamp();
        var frame = await capture!.CaptureAsync(serial, cancellationToken).ConfigureAwait(false);
        return new(frame, Stopwatch.GetElapsedTime(started), timedDecoder.Duration);
    }

    public async Task<DiagnosticMatch> MatchAsync(ImageFrame frame, TemplateDescriptor asset, NormalizedRegion roi,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        // 새 임계값을 만들거나 설정을 수정하지 않는다. 측정 결과는 입력 근거가 아니다.
        var result = await vision.DetectAsync(frame, new(asset.ScreenId, roi, null, asset.TemplateId), cancellationToken).ConfigureAwait(false);
        return new(result, Stopwatch.GetElapsedTime(started));
    }

    // 한 DiagnosticsViewModel이 한 번에 하나의 작업만 실행한다. 경로 변경 중에는 호출하지 않는다.
    private void Connect(string adbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbPath);
        if (client is not null && currentPath == adbPath) return;
        var options = defaults with { ExecutablePath = adbPath };
        client = new AdbDeviceClient(runnerFactory?.Invoke(options) ?? new AdbProcessRunner(options));
        capture = new AdbScreenCapture(client, timedDecoder, clock);
        currentPath = adbPath;
    }

    private sealed class TimedDecoder(IImageFrameDecoder inner) : IImageFrameDecoder
    {
        public TimeSpan Duration { get; private set; }
        public async Task<ImageFrame> DecodePngAsync(ReadOnlyMemory<byte> png, long frameId,
            DateTimeOffset capturedAt, CancellationToken cancellationToken)
        {
            var started = Stopwatch.GetTimestamp();
            try { return await inner.DecodePngAsync(png, frameId, capturedAt, cancellationToken).ConfigureAwait(false); }
            finally { Duration = Stopwatch.GetElapsedTime(started); }
        }
    }
}
