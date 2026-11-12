using System.ComponentModel;
using System.Runtime.CompilerServices;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using NyangShinobi.App.Diagnostics;
using NyangShinobi.App.Updates;

namespace NyangShinobi.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MacroSession session;
    private readonly ISettingsStore settingsStore;
    private bool initialized;
    private bool isRunning;
    private bool stopping;
    private bool preparingUpdate;
    private GameMode selectedMode;
    private string status = "설정을 불러오는 중입니다.";
    private string settingsSummary = "";

    public MainViewModel(MacroSession session, ISettingsStore settingsStore, DiagnosticsViewModel diagnostics, UpdateViewModel? updates = null)
    {
        this.session = session;
        this.settingsStore = settingsStore;
        Diagnostics = diagnostics;
        Updates = updates;
        StartCommand = new RelayCommand(Start, () => CanStart);
        StopCommand = new RelayCommand(session.Stop, () => IsRunning);
        Diagnostics.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DiagnosticsViewModel.IsBusy))
            { Changed(nameof(CanStart)); Changed(nameof(CanInstallUpdate)); StartCommand.Refresh(); }
        };
        if (Updates is not null) Updates.PropertyChanged += (_, _) => Changed(nameof(CanInstallUpdate));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<GameMode> AvailableModes { get; } = Enum.GetValues<GameMode>();
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public UpdateViewModel? Updates { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public bool IsRunning => isRunning;
    public bool CanStart => initialized && !stopping && !preparingUpdate && !isRunning && !Diagnostics.IsBusy;
    public bool CanInstallUpdate => initialized && !stopping && !preparingUpdate && !isRunning && !Diagnostics.IsBusy && Updates?.HasUpdate == true && !Updates.IsBusy;
    public bool IsIdleForUpdate => initialized && !stopping && !preparingUpdate && !isRunning && !Diagnostics.IsBusy;

    public async Task PrepareForUpdateAsync()
    {
        if (!IsIdleForUpdate) throw new InvalidOperationException("실행 중인 세션/ADB 작업을 먼저 중지하세요.");
        preparingUpdate = true; RefreshState();
        try
        {
            session.Stop();
            await Completion.WaitAsync(TimeSpan.FromSeconds(15));
            await Diagnostics.Completion.WaitAsync(TimeSpan.FromSeconds(15));
            if (session.IsRunning || Diagnostics.IsBusy) throw new InvalidOperationException("세션/ADB 작업 종료를 확인하지 못했습니다.");
        }
        catch { ResumeAfterUpdateFailure(); throw; }
    }
    public void ResumeAfterUpdateFailure() { preparingUpdate = false; RefreshState(); }
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
        status = "준비됨 · Task 02 · 골격 세션";
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
        stopping = true;
        RefreshState();
        session.Stop();
        await Completion;
        await Diagnostics.StopAsync();
    }

    private void RefreshState()
    {
        Diagnostics.SetAvailable(!isRunning && !stopping && !preparingUpdate);
        Changed(nameof(IsRunning));
        Changed(nameof(CanStart));
        Changed(nameof(CanInstallUpdate));
        Changed(nameof(Status));
        StartCommand.Refresh();
        StopCommand.Refresh();
    }

    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
