using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App.ViewModels;
using NyangShinobi.App.Views;

namespace NyangShinobi.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
