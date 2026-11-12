using System.Buffers.Binary;
using System.IO;
using System.Text;
using NyangShinobi.App.Diagnostics;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Application;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;
using OpenCvSharp;

namespace NyangShinobi.Integration.Tests;

public sealed class CaptureBenchmarkTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(12, 1)]
    [InlineData(16, 1)]
    [InlineData(12, 2)]
    [InlineData(16, 2)]
    [InlineData(12, 5)]
    [InlineData(16, 5)]
    public async Task RawKnownLayoutsConvertToOwnedBgraFrames(int header, uint format)
    {
        var bytes = Raw(header, format);
        var decoded = await new RawBenchmarkDecoder(new VisionOptions()).DecodeAsync(bytes, 7, DateTimeOffset.UnixEpoch, Token);
        Assert.Equal(header, decoded.HeaderBytes);
        Assert.Equal(format, decoded.Format);
        Assert.Equal(header == 16 ? (uint?)1 : null, decoded.ColorSpace);
        var frame = decoded.Frame;
        Assert.Equal(FramePixelFormat.Bgra32, frame.PixelFormat);
        Assert.Equal(2, frame.Width); Assert.Equal(2, frame.Height); Assert.Equal(8, frame.Stride);
        Assert.Equal(new byte[] { 30, 20, 10, format == 2 ? (byte)255 : (byte)40 }, frame.Pixels.Span[..4].ToArray());
        Array.Fill(bytes, (byte)0);
        Assert.Equal(30, frame.Pixels.Span[0]);
    }

    [Theory]
    [InlineData("short-header")]
    [InlineData("zero-width")]
    [InlineData("zero-height")]
    [InlineData("huge-width")]
    [InlineData("pixel-limit")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("padded-stride")]
    [InlineData("overflow")]
    public async Task RawRejectsMalformedDimensionsStrideAndLength(string error)
    {
        var bytes = Raw();
        var options = new VisionOptions();
        switch (error)
        {
            case "short-header": bytes = new byte[11]; break;
            case "zero-width": BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0); break;
            case "zero-height": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0); break;
            case "huge-width": BinaryPrimitives.WriteUInt32LittleEndian(bytes, uint.MaxValue); break;
            case "pixel-limit": options = options with { MaxPixels = 3 }; break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = [.. bytes, 0]; break;
            case "padded-stride": bytes = [.. bytes, .. new byte[8]]; break;
            case "overflow":
                options = options with { MaxPixels = long.MaxValue };
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, int.MaxValue);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), int.MaxValue);
                break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => new RawBenchmarkDecoder(options).DecodeAsync(bytes, 1, DateTimeOffset.UnixEpoch, Token));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(22)]
    [InlineData(43)]
    [InlineData(999)]
    public async Task RawDoesNotGuessUnsupportedPixelFormats(uint format) =>
        await Assert.ThrowsAsync<NotSupportedException>(() => new RawBenchmarkDecoder(new VisionOptions())
            .DecodeAsync(Raw(16, format), 1, DateTimeOffset.UnixEpoch, Token));

    [Fact]
    public async Task RawRejectsUnknownColorSpaceAndObservesCancellation()
    {
        var bytes = Raw();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 999);
        var decoder = new RawBenchmarkDecoder(new VisionOptions());
        await Assert.ThrowsAsync<NotSupportedException>(() => decoder.DecodeAsync(bytes, 1, DateTimeOffset.UnixEpoch, Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decoder.DecodeAsync(Raw(), 1, DateTimeOffset.UnixEpoch, cancellation.Token));
    }

    [Fact]
    public async Task BothMethodsWarmUpAlternateAndMeasureOnlyExplicitDevice()
    {
        using var fixture = new Fixture();
        var result = await fixture.Service.BenchmarkAsync("chosen-adb.exe", "chosen:5555", null, Token);
        Assert.Equal("chosen-adb.exe", fixture.SeenPath);
        Assert.Equal("chosen-adb.exe", result.AdbPath); Assert.Equal("chosen:5555", result.Serial);
        Assert.False(result.Canceled);
        Assert.Equal(20, result.Samples.Count);
        Assert.Equal(22, fixture.Adb.CaptureCalls);
        var captures = fixture.Adb.Commands.Where(command => command[0] == "-s").ToArray();
        Assert.Equal(new[] { true, false, true, false, false, true }, captures.Take(6).Select(command => command.Contains("-p")));
        Assert.All(captures, command => Assert.Equal(new[] { "-s", "chosen:5555", "exec-out", "screencap" }, command.Take(4)));
        Assert.Equal(1, fixture.Adb.MaxActive);
        Assert.Equal(8, result.Summary.Count);
        Assert.All(result.Methods, method =>
        {
            Assert.Equal(BenchmarkSupport.Verified, method.Support);
            Assert.Null(method.Error); Assert.Equal(10, method.Samples.Count);
            Assert.Equal(FramePixelFormat.Bgra32, method.LastFrame!.PixelFormat);
        });
        Assert.All(result.Samples, sample =>
        {
            Assert.InRange(sample.Round, 1, 10); Assert.Equal(2, sample.Width); Assert.Equal(2, sample.Height);
            Assert.True(sample.AdbMs >= 0 && sample.ConversionMs >= 0);
            Assert.True(sample.TotalMs + 0.001 >= sample.AdbMs + sample.ConversionMs);
            Assert.Equal(sample.Format == BenchmarkCaptureFormat.Raw ? Raw().LongLength : fixture.Adb.Png.LongLength, sample.TransferBytes);
        });
    }

    [Theory]
    [InlineData("raw-format")]
    [InlineData("raw-header")]
    [InlineData("raw-exit")]
    [InlineData("raw-timeout")]
    [InlineData("png-header")]
    [InlineData("png-payload")]
    public async Task FailuresAreVisibleAndDoNotProduceSuccessfulSamples(string failure)
    {
        using var fixture = new Fixture();
        fixture.Adb.Failure = failure;
        var result = await fixture.Service.BenchmarkAsync("chosen-adb.exe", "chosen:5555", null, Token);
        var bad = result.Methods.Single(method => method.Format == (failure.StartsWith("raw", StringComparison.Ordinal)
            ? BenchmarkCaptureFormat.Raw : BenchmarkCaptureFormat.Png));
        Assert.Empty(bad.Samples); Assert.NotNull(bad.Error);
        Assert.Equal(failure == "raw-format" ? BenchmarkSupport.Unsupported : BenchmarkSupport.NotVerified, bad.Support);
        Assert.Equal(10, result.Methods.Single(method => method != bad).Samples.Count);
        Assert.Contains("오류:", result.Description);
        if (failure == "raw-timeout") Assert.Contains(nameof(TimeoutException), bad.Error);
        if (failure == "raw-exit") Assert.Contains("42", bad.Error);
        Assert.Equal(0, fixture.Adb.Active);
    }

    [Theory]
    [InlineData("List of devices attached\n")]
    [InlineData("List of devices attached\nother device\n")]
    [InlineData("List of devices attached\nchosen:5555 offline\n")]
    [InlineData("List of devices attached\nchosen:5555 device\nchosen:5555 device\n")]
    public async Task MissingOfflineOrAmbiguousSerialNeverCaptures(string devices)
    {
        using var fixture = new Fixture();
        fixture.Adb.Devices = devices;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.BenchmarkAsync("chosen-adb.exe", "chosen:5555", null, Token));
        Assert.Equal(0, fixture.Adb.CaptureCalls);
    }

    [Fact]
    public async Task ViewPublishesMetadataWithoutRequiringEqualImagesAndClearsStaleResults()
    {
        using var fixture = new Fixture();
        var raw = Raw()[..20];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), 1);
        fixture.Adb.RawData = raw; // PNG는 2x2, RAW는 1x1: 서로 다른 화면도 정상 측정한다.
        var view = fixture.View;
        await view.RefreshDevicesAsync();
        view.SelectedDevice = view.Devices.Single(device => device.Serial == "chosen:5555");
        await view.BenchmarkAsync();
        Assert.Equal(8, view.BenchmarkRows.Count); Assert.Equal(20, view.BenchmarkSamples.Count);
        Assert.Contains("chosen-adb.exe", view.BenchmarkSummary); Assert.Contains("chosen:5555", view.BenchmarkSummary);
        Assert.Contains("2×2", view.BenchmarkSummary); Assert.Contains("1×1", view.BenchmarkSummary);
        Assert.All(view.BenchmarkResult!.Methods, method => Assert.Equal(BenchmarkSupport.Verified, method.Support));
        Assert.Null(view.Preview); Assert.False(view.CanSave);
        view.SelectedDevice = view.Devices.Single(device => device.Serial == "other:7777");
        Assert.Null(view.BenchmarkResult); Assert.Empty(view.BenchmarkSamples); Assert.Empty(view.BenchmarkRows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BenchmarkBlocksDiagnosticsAndMacroAndCancelsOrClosesCleanly(bool close)
    {
        using var fixture = new Fixture();
        var view = fixture.View;
        var main = new MainViewModel(new MacroSession(), new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory,
            "data", "settings.default.v0.1.json")), view);
        await main.InitializeAsync(Token);
        main.StartCommand.Execute(null);
        await view.BenchmarkAsync();
        Assert.Empty(fixture.Adb.Commands);
        main.StopCommand.Execute(null);
        await main.Completion;
        await view.RefreshDevicesAsync();
        Assert.Null(view.SelectedDevice);
        Assert.False(view.BenchmarkCommand.CanExecute(null));
        view.SelectedDevice = view.Devices.Single(device => device.Serial == "chosen:5555");
        fixture.Adb.HoldAt = 4; // 예열 두 번 + 첫 측정 하나 다음에 취소한다.
        var benchmark = view.BenchmarkAsync();
        await fixture.Adb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(view.IsBusy);
        Assert.False(main.StartCommand.CanExecute(null));
        Assert.False(view.CaptureCommand.CanExecute(null)); Assert.False(view.MatchCommand.CanExecute(null));
        Assert.False(view.DevicesCommand.CanExecute(null)); Assert.False(view.AssetsCommand.CanExecute(null));
        Assert.False(view.BenchmarkCommand.CanExecute(null)); Assert.False(view.CanSave);
        main.StartCommand.Execute(null); Assert.False(main.IsRunning);
        view.AdbPath = "ignored"; view.SelectedDevice = null;
        Assert.Equal("chosen-adb.exe", view.AdbPath); Assert.NotNull(view.SelectedDevice);
        await view.CaptureAsync(); await view.RefreshDevicesAsync(); await view.BenchmarkAsync();
        if (close) await view.StopAsync(); else view.CancelCommand.Execute(null);
        await benchmark.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.False(view.IsBusy); Assert.Equal(0, fixture.Adb.Active); Assert.Equal(4, fixture.Adb.CaptureCalls);
        Assert.Null(view.Preview);
        if (close) Assert.Null(view.BenchmarkResult);
        else
        {
            Assert.True(view.BenchmarkResult!.Canceled); Assert.Single(view.BenchmarkSamples);
            Assert.Contains("취소", view.BenchmarkSummary);
            Assert.True(main.CanStart);
            view.AdbPath = "new-adb.exe";
            Assert.Null(view.BenchmarkResult); Assert.Empty(view.BenchmarkRows);
        }
        await main.StopAsync();
    }

    [Fact]
    public async Task DiagnosticServiceSerializesBenchmarkAndNormalCapture()
    {
        using var fixture = new Fixture();
        fixture.Adb.HoldAt = 1;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var benchmark = fixture.Service.BenchmarkAsync("chosen-adb.exe", "chosen:5555", null, cancellation.Token);
        await fixture.Adb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        var capture = fixture.Service.CaptureAsync("chosen-adb.exe", "chosen:5555", Token);
        Assert.False(capture.IsCompleted);
        cancellation.Cancel();
        Assert.True((await benchmark).Canceled);
        Assert.Equal(2, (await capture).Frame.Width);
        Assert.Equal(1, fixture.Adb.MaxActive);
    }

    [Fact]
    public void StatisticsUseMedianAndNearestRankP95()
    {
        var stats = BenchmarkStatistics.From([4, 1, 3, 2]);
        Assert.Equal(2.5, stats.Mean); Assert.Equal(2.5, stats.Median);
        Assert.Equal(1, stats.Minimum); Assert.Equal(4, stats.Maximum); Assert.Equal(4, stats.P95);
        Assert.Equal(19, BenchmarkStatistics.From(Enumerable.Range(1, 20).Select(value => (double)value)).P95);
    }

    private static byte[] Raw(int header = 16, uint format = 1)
    {
        var bytes = new byte[header + 16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), format);
        if (header == 16) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 1);
        for (var offset = header; offset < bytes.Length; offset += 4)
        {
            bytes[offset] = format == 5 ? (byte)30 : (byte)10;
            bytes[offset + 1] = 20;
            bytes[offset + 2] = format == 5 ? (byte)10 : (byte)30;
            bytes[offset + 3] = 40;
        }
        return bytes;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemplateRepository templates = new(AppContext.BaseDirectory, new VisionOptions());
        private readonly OpenCvVisionEngine vision;
        public FakeAdb Adb { get; } = new();
        public CaptureDiagnostics Service { get; }
        public DiagnosticsViewModel View { get; }
        public string? SeenPath { get; private set; }
        public Fixture()
        {
            vision = new OpenCvVisionEngine(templates, new VisionOptions());
            Service = new(new AdbOptions { ExecutablePath = "chosen-adb.exe" }, new PngFrameDecoder(new VisionOptions()),
                new SystemGameClock(), vision, templates, options => { SeenPath = options.ExecutablePath; return Adb; });
            View = new(Service);
        }
        public void Dispose() { vision.Dispose(); templates.Dispose(); }
    }

    private sealed class FakeAdb : IAdbCommandRunner
    {
        public FakeAdb()
        {
            using var image = new Mat(2, 2, MatType.CV_8UC3, new Scalar(30, 20, 10));
            Cv2.ImEncode(".png", image, out var png);
            Png = png;
        }
        public byte[] Png { get; }
        public byte[] RawData { get; set; } = Raw();
        public string Devices { get; set; } = "List of devices attached\nchosen:5555 device\nother:7777 device\n";
        public string? Failure { get; set; }
        public int HoldAt { get; set; }
        public int CaptureCalls { get; private set; }
        public int Active { get; private set; }
        public int MaxActive { get; private set; }
        public List<string[]> Commands { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToArray());
            if (arguments.SequenceEqual(new[] { "devices", "-l" })) return new(Encoding.UTF8.GetBytes(Devices), "", 0);
            var png = arguments.SequenceEqual(new[] { "-s", "chosen:5555", "exec-out", "screencap", "-p" });
            Assert.True(png || arguments.SequenceEqual(new[] { "-s", "chosen:5555", "exec-out", "screencap" }));
            CaptureCalls++; Active++; MaxActive = Math.Max(MaxActive, Active);
            try
            {
                if (CaptureCalls == HoldAt)
                {
                    Started.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                if (!png && Failure == "raw-timeout") throw new TimeoutException("fake ADB command timeout");
                if (!png && Failure == "raw-exit") return new([], "fake unsupported capture", 42);
                var bytes = png ? (Failure == "png-header" ? new byte[4] : Failure == "png-payload" ? Png[..33] : Png) :
                    Failure == "raw-format" ? Raw(16, 4) : Failure == "raw-header" ? new byte[4] : RawData;
                return new(bytes, "", 0);
            }
            finally { Active--; }
        }
    }
}
