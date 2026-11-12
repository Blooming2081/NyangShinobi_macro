using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App;
using NyangShinobi.App.ViewModels;
using NyangShinobi.App.Views;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using NyangShinobi.App.Diagnostics;
using NyangShinobi.Device.Adb;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.Integration.Tests;

public sealed class WindowLifecycleTests
{
    [Fact]
    public async Task XamlCommandsBindAndClosingCancelsActiveSession()
    {
        // 창을 표시하거나 앱플레이어를 띄우지 않고 실제 WPF XAML/Dispatcher를 검증한다.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var registrations = new ServiceCollection().AddNyangShinobi(Path.Combine(AppContext.BaseDirectory,
                        "data", "settings.default.v0.1.json"));
                    var diagnosticAdb = new ListOnlyAdb();
                    registrations.AddSingleton(provider => new CaptureDiagnostics(provider.GetRequiredService<AdbOptions>(),
                        provider.GetRequiredService<IImageFrameDecoder>(), provider.GetRequiredService<IGameClock>(),
                        provider.GetRequiredService<IVisionEngine>(), provider.GetRequiredService<TemplateRepository>(), _ => diagnosticAdb));
                    using var services = registrations.BuildServiceProvider();
                    var viewModel = services.GetRequiredService<MainViewModel>();
                    await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
                    var window = new MainWindow(viewModel);
                    var vision = services.GetRequiredService<IVisionEngine>();
                    var tiny = new ImageFrame(1, DateTimeOffset.UnixEpoch, 1, 1, 4, new byte[4]);
                    Assert.Throws<InvalidOperationException>(() => vision.Detect(tiny, new("unused", new(new(0, 0), new(1, 1)), 0.9)));
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var start = FindButton(window, "StartButton");
                    var stop = FindButton(window, "StopButton");
                    Assert.Same(viewModel.StartCommand, start.Command);
                    Assert.Same(viewModel.StopCommand, stop.Command);
                    Assert.True(start.IsEnabled);
                    Assert.False(stop.IsEnabled);
                    start.Command.Execute(null);
                    Assert.True(viewModel.IsRunning);
                    Assert.False(start.IsEnabled);
                    Assert.True(stop.IsEnabled);
                    stop.Command.Execute(null);
                    await viewModel.Completion;
                    var tabs = Assert.IsType<TabControl>(window.Content);
                    tabs.SelectedIndex = 1;
                    await viewModel.Diagnostics.RefreshDevicesAsync();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var selector = Descendants(window).OfType<ComboBox>().Single(item =>
                        AutomationProperties.GetAutomationId(item) == "DiagnosticDeviceSelector");
                    Assert.Single(selector.Items.Cast<object>());
                    Assert.Null(selector.SelectedItem);
                    Assert.False(FindButton(window, "DiagnosticCapture").IsEnabled);
                    Assert.False(FindButton(window, "DiagnosticBenchmark").IsEnabled);
                    Assert.Same(viewModel.Diagnostics.BenchmarkCommand, FindButton(window, "DiagnosticBenchmark").Command);
                    Assert.False(FindButton(window, "DiagnosticSave").IsEnabled);
                    Assert.Same(viewModel.Diagnostics.DevicesCommand, FindButton(window, "DiagnosticDevices").Command);
                    selector.SelectedIndex = 0;
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Assert.True(FindButton(window, "DiagnosticCapture").IsEnabled);
                    Assert.True(FindButton(window, "DiagnosticBenchmark").IsEnabled);
                    viewModel.Diagnostics.AdbPath = "changed-adb.exe";
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    Assert.Null(selector.SelectedItem);
                    Assert.False(FindButton(window, "DiagnosticCapture").IsEnabled);
                    Assert.Equal(1, diagnosticAdb.Calls);
                    start.Command.Execute(null);
                    var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.Closed += (_, _) => closed.TrySetResult();
                    window.Close();
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                    Assert.False(services.GetRequiredService<MacroSession>().IsRunning);
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    private static Button FindButton(DependencyObject parent, string id)
    {
        foreach (var button in Descendants(parent).OfType<Button>())
            if (AutomationProperties.GetAutomationId(button) == id) return button;
        throw new InvalidOperationException($"XAML 버튼이 없습니다: {id}");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class ListOnlyAdb : IAdbCommandRunner
    {
        public int Calls { get; private set; }
        public Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Assert.Equal(new[] { "devices", "-l" }, arguments);
            Calls++;
            return Task.FromResult(new AdbCommandResult("List of devices attached\nselected device\n"u8.ToArray(), "", 0));
        }
    }
}
