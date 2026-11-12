using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App.ViewModels;
using NyangShinobi.App.Views;
using NyangShinobi.App.Diagnostics;
using System.Text.Json;
using NyangShinobi.App.Updates;

namespace NyangShinobi.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;
    private IDisposable? installationInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0] == "--verify-update-source")
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                var result = await UpdateSourceVerification.RunAsync(deadline.Token);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result)); Shutdown(0);
            }
            catch (Exception exception) { await Console.Error.WriteLineAsync(exception.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--update-helper")
        {
            try { Shutdown(await UpdateInstaller.RunHelperAsync(e.Args[1], CancellationToken.None)); }
            catch (Exception exception)
            {
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "helper-error.txt"), exception.ToString()); }
                catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException) { }
                await Console.Error.WriteLineAsync(exception.ToString()); Shutdown(1);
            }
            return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-update-restart")
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await PortableValidation.VerifyAsync(AppContext.BaseDirectory, deadline.Token);
                UpdateInstaller.AcknowledgeRestart(e.Args[1]); Shutdown(0);
            }
            catch (Exception exception) { await Console.Error.WriteLineAsync(exception.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Length == 1 && e.Args[0] == "--verify-portable")
        {
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var result = await PortableValidation.VerifyAsync(AppContext.BaseDirectory, cancellation.Token);
                await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result));
                Shutdown(0);
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(exception.ToString());
                Shutdown(1);
            }
            return;
        }
        try
        {
            var verifiedRestart = e.Args.Length == 2 && e.Args[0] == "--update-restart";
            if (verifiedRestart) UpdateInstaller.ValidateRestart(e.Args[1]);
            installationInstance = InstallRootGuard.EnterGui(PortableLayout.InstallationRoot(AppContext.BaseDirectory), verifiedRestart);
            var defaults = Path.Combine(AppContext.BaseDirectory, "data", "settings.default.v0.1.json");
            var userSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NyangShinobi", "settings.v0.1.json");
            services = new ServiceCollection().AddNyangShinobi(defaults, userSettings)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var viewModel = services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync(CancellationToken.None);
            MainWindow = new MainWindow(viewModel);
            MainWindow.Show();
            if (e.Args.Length == 2 && e.Args[0] == "--update-restart")
            {
                await MainWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                UpdateInstaller.AcknowledgeRestart(e.Args[1]);
            }
            if (e.Args.Length == 2 && e.Args[0] == "--update-result" && viewModel.Updates is not null)
            {
                viewModel.Updates.ReadInstallationResult(e.Args[1]);
                if (MainWindow.Content is System.Windows.Controls.TabControl tabs) tabs.SelectedIndex = 2;
            }
            if (viewModel.Updates is not null) _ = viewModel.Updates.CheckOnceAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "냥냥시노비 v0.1 시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        installationInstance?.Dispose();
        base.OnExit(e);
    }
}
