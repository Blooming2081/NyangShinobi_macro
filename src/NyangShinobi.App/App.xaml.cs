using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App.ViewModels;
using NyangShinobi.App.Views;
using NyangShinobi.App.Diagnostics;
using System.Text.Json;

namespace NyangShinobi.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
            var defaults = Path.Combine(AppContext.BaseDirectory, "data", "settings.default.v0.1.json");
            var userSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NyangShinobi", "settings.v0.1.json");
            services = new ServiceCollection().AddNyangShinobi(defaults, userSettings)
                .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var viewModel = services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync(CancellationToken.None);
            MainWindow = new MainWindow(viewModel);
            MainWindow.Show();
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
        base.OnExit(e);
    }
}
