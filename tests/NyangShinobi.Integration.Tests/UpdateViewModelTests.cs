using System.IO;
using System.Net.Http;
using NyangShinobi.App.Diagnostics;
using NyangShinobi.App.Updates;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Application;
using NyangShinobi.Device.Adb;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.Integration.Tests;

public sealed class UpdateViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task StartupChecksOnceLaterDefersAndManualCheckRemainsAvailable()
    {
        using var fixture = new Fixture();
        await fixture.Update.CheckOnceAsync(); await fixture.Update.CheckOnceAsync();
        Assert.Equal(1, fixture.Coordinator.Checks); Assert.True(fixture.Update.HasUpdate);
        Assert.Equal("0.1.0-rc.1", fixture.Update.CurrentVersion);
        fixture.Update.LaterCommand.Execute(null); Assert.False(fixture.Update.HasUpdate);
        await fixture.Update.CheckAsync(); Assert.Equal(2, fixture.Coordinator.Checks); Assert.True(fixture.Update.HasUpdate);
        Assert.Contains("업데이트", fixture.Update.Notes);
    }
    [Fact]
    public async Task NetworkFailureDoesNotBlockNormalStart()
    {
        using var fixture = new Fixture(); fixture.Coordinator.FailCheck = true;
        await fixture.Main.InitializeAsync(Token);
        await fixture.Update.CheckOnceAsync();
        Assert.Contains("확인 실패", fixture.Update.Status); Assert.False(fixture.Update.IsBusy);
        Assert.True(fixture.Main.CanStart);
        fixture.Main.StartCommand.Execute(null); Assert.True(fixture.Main.IsRunning);
        await fixture.Main.StopAsync();
    }
    [Fact]
    public async Task RunningMacroAndUnconfirmedInstallNeverDownloadStopOrClose()
    {
        using var fixture = new Fixture(); await fixture.Main.InitializeAsync(Token); await fixture.Update.CheckAsync();
        fixture.Main.StartCommand.Execute(null);
        await fixture.InstallAsync(true);
        Assert.Equal(0, fixture.Coordinator.Prepares); Assert.True(fixture.Main.IsRunning); Assert.False(fixture.Closed);
        fixture.Main.StopCommand.Execute(null); await fixture.Main.Completion;
        await fixture.InstallAsync(false); Assert.Equal(0, fixture.Coordinator.Prepares);
        await fixture.Main.StopAsync();
    }
    [Fact]
    public async Task ActiveAdbDiagnosticIsProtected()
    {
        using var fixture = new Fixture(); await fixture.Main.InitializeAsync(Token); await fixture.Update.CheckAsync();
        await fixture.Diagnostics.RefreshDevicesAsync();
        fixture.Diagnostics.SelectedDevice = Assert.Single(fixture.Diagnostics.Devices);
        var capture = fixture.Diagnostics.CaptureAsync();
        await fixture.Adb.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        await fixture.InstallAsync(true);
        Assert.Equal(0, fixture.Coordinator.Prepares); Assert.True(fixture.Diagnostics.IsBusy); Assert.False(fixture.Closed);
        fixture.Diagnostics.CancelCommand.Execute(null); await capture;
        await fixture.Main.StopAsync();
    }
    [Fact]
    public async Task DownloadCancellationNeverStartsHelper()
    {
        using var fixture = new Fixture(); await fixture.Main.InitializeAsync(Token); await fixture.Update.CheckAsync();
        fixture.Coordinator.HoldDownload = true;
        var install = fixture.InstallAsync(true);
        await fixture.Coordinator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        fixture.Update.CancelCommand.Execute(null); await install;
        Assert.Equal(0, fixture.Coordinator.Launches); Assert.False(fixture.Closed); Assert.True(fixture.Main.CanStart);
        Assert.Contains("취소", fixture.Update.Status); await fixture.Main.StopAsync();
    }
    [Fact]
    public async Task MacroStartedDuringDownloadPreventsInstallation()
    {
        using var fixture = new Fixture(); await fixture.Main.InitializeAsync(Token); await fixture.Update.CheckAsync();
        fixture.Coordinator.HoldDownload = true;
        var install = fixture.InstallAsync(true);
        await fixture.Coordinator.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        fixture.Main.StartCommand.Execute(null); fixture.Coordinator.Continue.TrySetResult(); await install;
        Assert.True(fixture.Main.IsRunning); Assert.Equal(0, fixture.Coordinator.Launches); Assert.False(fixture.Closed);
        await fixture.Main.StopAsync();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandoffOccursOnlyAfterQuiescenceAndFailureRestoresControls(bool failure)
    {
        using var fixture = new Fixture(); await fixture.Main.InitializeAsync(Token); await fixture.Update.CheckAsync();
        fixture.Coordinator.FailLaunch = failure;
        fixture.Coordinator.OnLaunch = () => { Assert.False(fixture.Main.CanStart); Assert.False(fixture.Diagnostics.CanEdit); Assert.False(fixture.Main.IsRunning); };
        await fixture.InstallAsync(true);
        Assert.Equal(1, fixture.Coordinator.Launches);
        Assert.Equal(!failure, fixture.Closed);
        if (failure) { Assert.True(fixture.Main.CanStart); Assert.True(fixture.Diagnostics.CanEdit); }
        else Assert.False(fixture.Main.CanStart);
        await fixture.Main.StopAsync();
    }
    [Fact]
    public void ChannelPreferencesAreSeparateFromGameSettings()
    {
        var root = UpdateTestData.DirectoryPath(); var preference = Path.Combine(root, "updates.json");
        var gameSettings = Path.Combine(root, "settings.v0.1.json"); File.WriteAllText(gameSettings, "user-game-settings");
        using (var view = new UpdateViewModel(new FakeCoordinator(), preference)) view.Channel = UpdateChannel.Stable;
        using var restored = new UpdateViewModel(new FakeCoordinator(), preference);
        Assert.Equal(UpdateChannel.Stable, restored.Channel); Assert.Equal("user-game-settings", File.ReadAllText(gameSettings));
    }
    [Theory]
    [InlineData(0, UpdateChannel.Development, "Pre-release (테스트)")]
    [InlineData(1, UpdateChannel.Stable, "Stable")]
    public void ExistingChannelValuesKeepTheirMeaningAndOnlyTwoChoices(int stored, UpdateChannel expected, string label)
    {
        var preference = Path.Combine(UpdateTestData.DirectoryPath(), "updates.json");
        File.WriteAllText(preference, $"{{\"Channel\":{stored}}}");
        using var view = new UpdateViewModel(new FakeCoordinator(), preference);
        Assert.Equal(expected, view.Channel); Assert.Equal(2, view.Channels.Count);
        Assert.Equal(new[] { 0, 1 }, view.Channels.Select(value => (int)value));
        var converter = new UpdateChannelLabelConverter();
        Assert.Equal(label, converter.Convert(view.Channel, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal($"{{\"Channel\":{stored}}}", File.ReadAllText(preference));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemplateRepository templates = new(AppContext.BaseDirectory, new VisionOptions());
        private readonly OpenCvVisionEngine vision;
        public FakeCoordinator Coordinator { get; } = new();
        public HoldingAdb Adb { get; } = new();
        public UpdateViewModel Update { get; }
        public MainViewModel Main { get; }
        public DiagnosticsViewModel Diagnostics { get; }
        public bool Closed { get; private set; }
        public Fixture()
        {
            vision = new(templates, new VisionOptions());
            Diagnostics = new(new CaptureDiagnostics(new AdbOptions(), new PngFrameDecoder(new VisionOptions()), new SystemGameClock(), vision, templates, _ => Adb));
            Update = new(Coordinator, Path.Combine(UpdateTestData.DirectoryPath(), "updates.json"));
            Main = new(new MacroSession(), new JsonSettingsStore(Path.Combine(AppContext.BaseDirectory, "data", "settings.default.v0.1.json")), Diagnostics, Update);
        }
        public Task InstallAsync(bool confirmed) => Update.InstallAsync(confirmed, () => Main.IsIdleForUpdate, Main.PrepareForUpdateAsync,
            Main.ResumeAfterUpdateFailure, () => Closed = true);
        public void Dispose() { Update.Dispose(); vision.Dispose(); templates.Dispose(); }
    }
    private sealed class FakeCoordinator : IUpdateCoordinator
    {
        public BuildMetadata Current { get; } = new(1, BuildMetadata.Identity, "0.1.0-rc.1", "win-x64", "test", "v0.1.0-rc.1");
        public int Checks { get; private set; } public int Prepares { get; private set; } public int Launches { get; private set; }
        public bool FailCheck { get; set; } public bool HoldDownload { get; set; } public bool FailLaunch { get; set; }
        public Action? OnLaunch { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<UpdateRelease?> CheckAsync(UpdateChannel channel, CancellationToken token)
        { Checks++; if (FailCheck) throw new HttpRequestException("offline"); return Task.FromResult<UpdateRelease?>(UpdateTestData.Release("0.1.0-rc.2")); }
        public async Task<UpdatePlan> PrepareAsync(UpdateRelease release, IProgress<DownloadProgress>? progress, CancellationToken token)
        {
            Prepares++; Started.TrySetResult();
            if (HoldDownload) await Continue.Task.WaitAsync(token);
            return new(1, "unused", "unused", release.Version, 1, 1, Guid.NewGuid().ToString("N"));
        }
        public Task LaunchHelperAsync(UpdatePlan plan, CancellationToken token)
        { Launches++; OnLaunch?.Invoke(); if (FailLaunch) throw new IOException("helper launch failed"); return Task.CompletedTask; }
    }
    private sealed class HoldingAdb : IAdbCommandRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            if (arguments.SequenceEqual(new[] { "devices", "-l" })) return new("List of devices attached\nselected device\n"u8.ToArray(), "", 0);
            Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); throw new InvalidOperationException();
        }
    }
}
