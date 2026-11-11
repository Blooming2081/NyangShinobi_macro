using System.IO;
using System.Text;
using NyangShinobi.App.Diagnostics;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Application;
using NyangShinobi.Device.Adb;
using NyangShinobi.Vision.OpenCvSharp;
using OpenCvSharp;

namespace NyangShinobi.Integration.Tests;

public sealed class DiagnosticsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("selected device")]
    [InlineData("selected device\nother device")]
    [InlineData("selected offline")]
    public async Task DiscoveryNeverSelectsDeviceOrCapturesAutomatically(string devices)
    {
        using var fixture = new Fixture(devices);
        var view = fixture.View;
        Assert.Empty(fixture.Adb.Commands);
        await view.RefreshDevicesAsync();
        Assert.NotEmpty(view.Devices);
        Assert.Null(view.SelectedDevice);
        Assert.False(view.CaptureCommand.CanExecute(null));
        await view.CaptureAsync();
        Assert.Null(view.Preview);
        Assert.Single(fixture.Adb.Commands);
        Assert.Equal(new[] { "devices", "-l" }, fixture.Adb.Commands[0]);
    }

    [Fact]
    public async Task CaptureMatchAndExplicitSaveReuseFrameWithoutAnyInput()
    {
        using var fixture = new Fixture();
        var view = fixture.View;
        var path = Path.Combine(Path.GetTempPath(), $"nyang-diag-{Guid.NewGuid():N}.png");
        try
        {
            await view.RefreshDevicesAsync();
            view.SelectedDevice = Assert.Single(view.Devices);
            await view.LoadAssetsAsync();
            view.SelectedAsset = view.Assets.Single(asset => asset.TemplateId == "assets/images_nav/mission.png");
            await view.CaptureAsync();
            var preview = view.Preview;
            Assert.NotNull(preview);
            Assert.True(preview.IsFrozen);
            Assert.Equal(480, preview.PixelWidth);
            Assert.Equal(1320, preview.PixelHeight);
            Assert.Contains("480×1320", view.CaptureSummary);
            Assert.Contains("디코딩", view.CaptureSummary);
            Assert.True(view.CanSave);
            Assert.False(File.Exists(path));
            await view.MatchAsync();
            Assert.Contains("임계값 미적용", view.MatchSummary);
            Assert.Contains("인식", view.MatchSummary);
            Assert.Same(preview, view.Preview);
            Assert.Equal(1, fixture.Adb.Captures);
            Assert.False(File.Exists(path));
            await view.SaveAsync(path);
            Assert.True(File.Exists(path));
            var saved = await new PngFrameDecoder(new VisionOptions()).DecodePngAsync(await File.ReadAllBytesAsync(path, Token),
                1, DateTimeOffset.UnixEpoch, Token);
            Assert.Equal(480, saved.Width);
            Assert.Equal(1320, saved.Height);
            var expectedPixels = new byte[480 * 1320 * 4];
            preview.CopyPixels(expectedPixels, 480 * 4, 0);
            Assert.Equal(expectedPixels, saved.Pixels.ToArray());
            Assert.DoesNotContain(fixture.Adb.Commands, command => command.Contains("input"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TargetChangesDiscardStalePreviewAndResults()
    {
        using var fixture = new Fixture("selected device\nother device");
        var view = fixture.View;
        await view.RefreshDevicesAsync();
        view.SelectedDevice = view.Devices[0];
        await view.CaptureAsync();
        Assert.NotNull(view.Preview);
        view.SelectedDevice = view.Devices[1];
        Assert.Null(view.Preview);
        Assert.False(view.CanSave);
        Assert.Equal("캡처 없음", view.CaptureSummary);
        view.AdbPath = "other-adb.exe";
        Assert.Empty(view.Devices);
        Assert.Null(view.SelectedDevice);
        Assert.Equal(1, fixture.Adb.Captures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusyDuplicateCaptureAndCancelOrCloseCannotLeaveActiveWork(bool close)
    {
        using var fixture = new Fixture();
        var view = fixture.View;
        await view.RefreshDevicesAsync();
        var device = Assert.Single(view.Devices);
        view.SelectedDevice = device;
        fixture.Adb.HoldCapture = true;
        var run = view.CaptureAsync();
        await fixture.Adb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(view.IsBusy);
        Assert.False(view.CaptureCommand.CanExecute(null));
        Assert.False(view.CanSave);
        view.SelectedDevice = null;
        Assert.Same(device, view.SelectedDevice);
        await view.CaptureAsync();
        if (close) await view.StopAsync();
        else view.CancelCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.False(view.IsBusy);
        Assert.Null(view.Preview);
        Assert.Equal(1, fixture.Adb.Captures);
        Assert.Equal(!close, view.CanEdit);
    }

    [Fact]
    public async Task InvalidRoiAndRefreshFailureDoNotPublishOldResults()
    {
        using var fixture = new Fixture();
        var view = fixture.View;
        await view.RefreshDevicesAsync();
        view.SelectedDevice = Assert.Single(view.Devices);
        await view.LoadAssetsAsync();
        view.SelectedAsset = view.Assets.Single(asset => asset.TemplateId == "assets/images_nav/mission.png");
        await view.CaptureAsync();
        var preview = view.Preview;
        await view.MatchAsync();
        var previousMatch = view.MatchSummary;
        Assert.Contains("임계값 미적용", previousMatch);
        view.RoiLeft = "0.9";
        view.RoiRight = "0.1";
        await view.MatchAsync();
        Assert.Contains("진단 실패", view.Status);
        Assert.NotEqual(previousMatch, view.MatchSummary);
        Assert.DoesNotContain("frame ", view.MatchSummary);
        Assert.Same(preview, view.Preview);
        fixture.Adb.FailDiscovery = true;
        await view.RefreshDevicesAsync();
        Assert.Empty(view.Devices);
        Assert.Null(view.SelectedDevice);
        Assert.Null(view.Preview);
        Assert.Contains("진단 실패", view.Status);
    }

    [Fact]
    public async Task MissingPreviewCannotCreateFile()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(Path.GetTempPath(), $"nyang-no-frame-{Guid.NewGuid():N}.png");
        await fixture.View.SaveAsync(path);
        Assert.False(File.Exists(path));
        Assert.False(fixture.View.CanSave);
    }

    [Fact]
    public async Task NormalSessionAndDiagnosticsCannotRunTogether()
    {
        using var fixture = new Fixture();
        var main = new MainViewModel(new MacroSession(), new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory,
            "data", "settings.default.v0.1.json")), fixture.View);
        await main.InitializeAsync(Token);
        main.StartCommand.Execute(null);
        try
        {
            Assert.False(fixture.View.CanEdit);
            await fixture.View.RefreshDevicesAsync();
            Assert.Empty(fixture.Adb.Commands);
            main.StopCommand.Execute(null);
            await main.Completion.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await fixture.View.RefreshDevicesAsync();
            fixture.View.SelectedDevice = Assert.Single(fixture.View.Devices);
            fixture.Adb.HoldCapture = true;
            var capture = fixture.View.CaptureAsync();
            await fixture.Adb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.False(main.StartCommand.CanExecute(null));
            main.StartCommand.Execute(null);
            Assert.False(main.IsRunning);
            fixture.View.CancelCommand.Execute(null);
            await capture.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.True(main.CanStart);
        }
        finally { await main.StopAsync(); }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemplateRepository repository = new(AppContext.BaseDirectory, new VisionOptions());
        private readonly OpenCvVisionEngine engine;
        public FakeAdb Adb { get; }
        public DiagnosticsViewModel View { get; }

        public Fixture(string devices = "selected device")
        {
            engine = new OpenCvVisionEngine(repository, new VisionOptions());
            using var template = Cv2.ImRead(Path.Combine(AppContext.BaseDirectory, "assets", "images_nav", "mission.png"));
            using var image = new Mat(1320, 480, MatType.CV_8UC3, new Scalar(15, 30, 45));
            using (var roi = new Mat(image, new Rect(20, 100, template.Width, template.Height))) template.CopyTo(roi);
            Cv2.ImEncode(".png", image, out var png);
            Adb = new FakeAdb(png) { Devices = devices };
            var service = new CaptureDiagnostics(new AdbOptions { ExecutablePath = "fake-adb" },
                new PngFrameDecoder(new VisionOptions()), new SystemGameClock(), engine, repository, _ => Adb);
            View = new DiagnosticsViewModel(service);
        }

        public void Dispose() { engine.Dispose(); repository.Dispose(); }
    }

    private sealed class FakeAdb(byte[] png) : IAdbCommandRunner
    {
        public List<string[]> Commands { get; } = [];
        public string Devices { get; init; } = "selected device";
        public bool HoldCapture { get; set; }
        public bool FailDiscovery { get; set; }
        public int Captures { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToArray());
            if (arguments.SequenceEqual(new[] { "devices", "-l" }))
                return new(Encoding.UTF8.GetBytes("List of devices attached\n" + Devices + "\n"), "fake failure", FailDiscovery ? 1 : 0);
            if (arguments.SequenceEqual(new[] { "-s", "selected", "exec-out", "screencap", "-p" }))
            {
                Captures++;
                Started.TrySetResult();
                if (HoldCapture) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new(png, "", 0);
            }
            throw new InvalidOperationException("진단에서 허용하지 않는 ADB 명령: " + string.Join(' ', arguments));
        }
    }
}
