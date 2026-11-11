using System.ComponentModel;
using System.Windows;
using NyangShinobi.App.ViewModels;

namespace NyangShinobi.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private bool closing;
    private bool mayClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (mayClose)
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        base.OnClosing(e);
        if (closing) return;
        closing = true;
        await viewModel.StopAsync();
        mayClose = true;
        // OnClosing 스택 안에서 Close를 재진입하지 않도록 UI 큐에 예약한다.
        await Dispatcher.InvokeAsync(Close);
    }
}
