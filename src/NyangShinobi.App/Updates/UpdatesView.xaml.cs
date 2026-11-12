using System.Windows;
using System.Windows.Controls;
using NyangShinobi.App.ViewModels;

namespace NyangShinobi.App.Updates;

public partial class UpdatesView : UserControl
{
    public UpdatesView() => InitializeComponent();
    private async void Install(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main || main.Updates is not { } updates || !main.CanInstallUpdate) return;
        var confirmed = MessageBox.Show(Window.GetWindow(this),
            $"{updates.CurrentVersion} → {updates.LatestVersion}\n\n앱을 종료하고 백업·설치 후 재실행합니다. 진행할까요?",
            "업데이트 설치 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        await updates.InstallAsync(confirmed, () => main.IsIdleForUpdate, main.PrepareForUpdateAsync,
            main.ResumeAfterUpdateFailure, () => Window.GetWindow(this)?.Close());
    }
}
