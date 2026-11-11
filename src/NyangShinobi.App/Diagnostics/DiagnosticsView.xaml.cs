using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NyangShinobi.App.Diagnostics;

public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView() => InitializeComponent();

    private void BrowseAdb(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsViewModel viewModel || !viewModel.CanEdit) return;
        var dialog = new OpenFileDialog { Title = "사용 중인 MuMu/BlueStacks ADB 실행 파일 선택", Filter = "실행 파일 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) viewModel.AdbPath = dialog.FileName;
    }

    private async void SaveCapture(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DiagnosticsViewModel viewModel || !viewModel.CanSave) return;
        var dialog = new SaveFileDialog
        {
            Title = "사용자가 선택한 캡처를 PNG로 저장", Filter = "PNG 이미지 (*.png)|*.png", DefaultExt = ".png",
            AddExtension = true, OverwritePrompt = true, FileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) await viewModel.SaveAsync(dialog.FileName);
    }
}
