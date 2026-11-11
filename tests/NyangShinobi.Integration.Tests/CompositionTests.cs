using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Integration.Tests;

public sealed class CompositionTests
{
    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddNyangShinobi(Path.Combine(AppContext.BaseDirectory, "data", "settings.default.v0.1.json"))
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    [Fact]
    public void CompositionResolvesWithNoRegisteredEvents()
    {
        using var services = CreateProvider();
        Assert.NotNull(services.GetRequiredService<IGameClock>());
        Assert.NotNull(services.GetRequiredService<IVisionEngine>());
        Assert.NotNull(services.GetRequiredService<IScreenCapture>());
        Assert.NotNull(services.GetRequiredService<IInputController>());
        Assert.Empty(services.GetRequiredService<IEventRegistry>().Handlers);
        Assert.Equal(3, services.GetServices<IGameFlow>().Select(flow => flow.Mode).Distinct().Count());
        Assert.Same(services.GetRequiredService<MacroSession>(), services.GetRequiredService<MacroSession>());
    }

    [Fact]
    public async Task ViewModelStartStopAndCloseDoNotCallDevicePorts()
    {
        var device = new ForbiddenDevice();
        var registrations = new ServiceCollection().AddNyangShinobi(Path.Combine(AppContext.BaseDirectory,
            "data", "settings.default.v0.1.json"));
        registrations.AddSingleton<IScreenCapture>(device);
        registrations.AddSingleton<IInputController>(device);
        registrations.AddSingleton<IVisionEngine>(device);
        using var services = registrations.BuildServiceProvider();
        var viewModel = services.GetRequiredService<MainViewModel>();
        Assert.False(viewModel.StartCommand.CanExecute(null));
        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        viewModel.StartCommand.Execute(null);
        try
        {
            Assert.True(viewModel.IsRunning);
            Assert.False(viewModel.StartCommand.CanExecute(null));
            Assert.True(viewModel.StopCommand.CanExecute(null));
            viewModel.SelectedMode = GameMode.NekoNinja;
            Assert.Equal(GameMode.Mission, viewModel.SelectedMode);
            viewModel.StopCommand.Execute(null);
            await viewModel.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(viewModel.IsRunning);
            Assert.False(viewModel.StopCommand.CanExecute(null));
            Assert.True(viewModel.StartCommand.CanExecute(null));
            viewModel.StartCommand.Execute(null);
        }
        finally { await viewModel.StopAsync(); }
        Assert.False(services.GetRequiredService<MacroSession>().IsRunning);
        Assert.Equal(0, device.Calls);
    }

    [Fact]
    public async Task UnimplementedAdaptersCannotReportSuccessfulActions()
    {
        using var services = CreateProvider();
        var capture = services.GetRequiredService<IScreenCapture>();
        var input = services.GetRequiredService<IInputController>();
        await Assert.ThrowsAsync<NotSupportedException>(() => capture.CaptureAsync("unused", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() => input.TapAsync("unused", new(0.5, 0.5), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() => input.SwipeAsync("unused", new(0.1, 0.1),
            new(0.5, 0.5), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => input.TapAsync("unused", new(0.5, 0.5), new(true)));
        var frame = new ImageFrame(1, DateTimeOffset.UtcNow, 1, 1, 4, new byte[4]);
        Assert.Throws<NotSupportedException>(() => services.GetRequiredService<IVisionEngine>()
            .Detect(frame, new("unimplemented", new(new(0, 0), new(1, 1)), 0.8)));
    }

    private sealed class ForbiddenDevice : IScreenCapture, IInputController, IVisionEngine
    {
        public int Calls { get; private set; }
        private Exception Unexpected() { Calls++; return new InvalidOperationException("Task 01에서 장치 호출 금지"); }
        public Task<ImageFrame> CaptureAsync(string deviceSerial, CancellationToken cancellationToken) => throw Unexpected();
        public Task<InputOutcome> TapAsync(string deviceSerial, NormalizedPoint point, CancellationToken cancellationToken) => throw Unexpected();
        public Task<InputOutcome> SwipeAsync(string deviceSerial, NormalizedPoint from, NormalizedPoint to,
            TimeSpan duration, CancellationToken cancellationToken) => throw Unexpected();
        public DetectionResult Detect(ImageFrame frame, DetectionRequest request) => throw Unexpected();
    }
}
