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
                    using var services = new ServiceCollection().AddNyangShinobi(Path.Combine(AppContext.BaseDirectory,
                        "data", "settings.default.v0.1.json")).BuildServiceProvider();
                    var viewModel = services.GetRequiredService<MainViewModel>();
                    await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
                    var window = new MainWindow(viewModel);
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
}
