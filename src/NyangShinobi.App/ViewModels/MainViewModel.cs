using System.ComponentModel;
using System.Runtime.CompilerServices;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MacroSession session;
    private readonly ISettingsStore settingsStore;
    private bool initialized;
    private bool isRunning;
    private GameMode selectedMode;
    private string status = "설정을 불러오는 중입니다.";
    private string settingsSummary = "";

    public MainViewModel(MacroSession session, ISettingsStore settingsStore)
    {
        this.session = session;
        this.settingsStore = settingsStore;
        StartCommand = new RelayCommand(Start, () => CanStart);
        StopCommand = new RelayCommand(session.Stop, () => IsRunning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<GameMode> AvailableModes { get; } = Enum.GetValues<GameMode>();
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public bool IsRunning => isRunning;
    public bool CanStart => initialized && !isRunning;
    public string Status => status;
    public string SettingsSummary => settingsSummary;
    public GameMode SelectedMode
    {
        get => selectedMode;
        set
        {
            if (!isRunning && Enum.IsDefined(value))
            {
                selectedMode = value;
                Changed();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        SelectedMode = settings.GetGameMode();
        settingsSummary = $"기본 검사 {settings.Capture.DefaultIdleIntervalMs}ms · 입력 후 검증 {settings.Capture.PostTapVerifyDelayMs}ms";
        initialized = true;
        status = "준비됨 · Task 01 골격";
        Changed(nameof(SettingsSummary));
        Changed(nameof(Status));
        RefreshState();
    }

    private void Start()
    {
        isRunning = true;
        status = "골격 세션 실행 중 · 게임 자동화는 아직 구현되지 않았습니다.";
        RefreshState();
        Completion = RunSessionAsync(selectedMode);
    }

    private async Task RunSessionAsync(GameMode mode)
    {
        try
        {
            await session.RunAsync(mode, CancellationToken.None);
            status = "중지됨 · 추가 게임 입력 없음";
        }
        catch (Exception exception)
        {
            status = $"사용자 조치 필요: {exception.Message}";
        }
        finally
        {
            isRunning = false;
            RefreshState();
        }
    }

    public async Task StopAsync()
    {
        session.Stop();
        await Completion;
    }

    private void RefreshState()
    {
        Changed(nameof(IsRunning));
        Changed(nameof(CanStart));
        Changed(nameof(Status));
        StartCommand.Refresh();
        StopCommand.Refresh();
    }

    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
