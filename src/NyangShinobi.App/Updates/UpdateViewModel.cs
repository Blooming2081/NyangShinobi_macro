using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using NyangShinobi.App.ViewModels;

namespace NyangShinobi.App.Updates;

public sealed class UpdateViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IUpdateCoordinator coordinator;
    private CancellationTokenSource? cancellation;
    private bool startupChecked;
    private bool deferred;
    private UpdateChannel channel = UpdateChannel.Development;
    private readonly string preferences;
    public UpdateViewModel(IUpdateCoordinator coordinator, string? preferencesPath = null)
    {
        this.coordinator = coordinator;
        preferences = preferencesPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NyangShinobi", "updates.json");
        try { if (File.Exists(preferences)) channel = UpdateFiles.ReadJson<UpdatePreferences>(preferences).Channel; }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { Status = "업데이트 설정을 읽지 못해 Pre-release (테스트) 채널을 사용합니다."; }
        if (!Enum.IsDefined(channel)) channel = UpdateChannel.Development;
        CheckCommand = new RelayCommand(() => Completion = CheckAsync(), () => !IsBusy);
        LaterCommand = new RelayCommand(() => { deferred = true; NotifyAll(); }, () => HasUpdate && !IsBusy);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => IsBusy && !HandingOff);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public RelayCommand CheckCommand { get; }
    public RelayCommand LaterCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public IReadOnlyList<UpdateChannel> Channels { get; } = Enum.GetValues<UpdateChannel>();
    public string CurrentVersion => coordinator.Current.Version;
    public UpdateRelease? Release { get; private set; }
    public bool HasUpdate => Release is not null && !deferred;
    public bool IsBusy => cancellation is not null;
    public bool CanEdit => !IsBusy;
    public bool HandingOff { get; private set; }
    public double Progress { get; private set; }
    public string Badge => HasUpdate ? "업데이트 • 새 버전" : "업데이트";
    public string LatestVersion => Release?.Version ?? "확인 전/새 버전 없음";
    public string Notes => Release?.Notes ?? "GitHub Releases의 업데이트 내용을 여기에 표시합니다.";
    public string InstallationResult { get; private set; } = "";
    public string Status { get; private set; } = "시작 시 한 번 확인하며, 이후에는 수동으로 확인합니다.";
    public UpdateChannel Channel
    {
        get => channel;
        set
        {
            if (IsBusy || !Enum.IsDefined(value) || value == channel) return;
            channel = value; Release = null; deferred = false;
            try { Directory.CreateDirectory(Path.GetDirectoryName(preferences)!); UpdateFiles.WriteJson(preferences, new UpdatePreferences(channel)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Status = $"채널 설정 저장 실패: {exception.Message}"; }
            NotifyAll();
        }
    }
    public Task CheckOnceAsync()
    {
        if (startupChecked) return Task.CompletedTask;
        startupChecked = true;
        return Completion = CheckAsync();
    }
    public void ReadInstallationResult(string planPath)
    {
        try
        {
            var plan = UpdateFiles.ReadJson<UpdatePlan>(planPath);
            var root = System.IO.Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            if (!root.Equals(plan.TargetRoot, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(plan.Nonce, "N", out _) ||
                !plan.TransactionRoot.Equals(System.IO.Path.Combine(root, ".updates", plan.Nonce), StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFullPath(planPath).Equals(plan.PlanPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("복원 결과 경로가 앱의 작업 폴더와 다릅니다.");
            UpdateFiles.RejectReparseParents(root, plan.TransactionRoot);
            InstallationResult = UpdateFiles.ReadJson<InstallOutcome>(System.IO.Path.Combine(plan.TransactionRoot, "result.json")).Message;
        }
        catch (Exception exception) { InstallationResult = $"업데이트 결과 확인 실패: {exception.Message}"; }
        Notify(nameof(InstallationResult));
    }
    public async Task CheckAsync()
    {
        if (IsBusy) return;
        using var source = new CancellationTokenSource(); cancellation = source;
        Release = null; deferred = false; Status = "GitHub Releases 확인 중…"; NotifyAll();
        try
        {
            Release = await coordinator.CheckAsync(Channel, source.Token);
            Status = Release is null ? "선택한 채널에 더 새로운 Windows x64 배포가 없습니다." : "새 버전이 있습니다. 업데이트 또는 나중에를 선택하세요.";
        }
        catch (OperationCanceledException) { Status = "업데이트 확인 취소/시간 초과. 프로그램은 계속 사용할 수 있습니다."; }
        catch (Exception exception) { Status = $"업데이트 확인 실패: {exception.Message} · 프로그램은 계속 사용할 수 있습니다."; }
        finally { cancellation = null; NotifyAll(); }
    }
    public Task InstallAsync(bool confirmed, Func<bool> isIdle, Func<Task> prepare, Action resume, Action close) =>
        Completion = InstallCoreAsync(confirmed, isIdle, prepare, resume, close);

    private async Task InstallCoreAsync(bool confirmed, Func<bool> isIdle, Func<Task> prepare, Action resume, Action close)
    {
        if (!confirmed || IsBusy || !HasUpdate) return;
        if (!isIdle()) { Status = "매크로/진단 작업을 먼저 중지한 뒤 업데이트하세요."; NotifyAll(); return; }
        using var source = new CancellationTokenSource(); cancellation = source;
        var prepared = false;
        var handedOff = false;
        Status = "업데이트 다운로드 및 무결성 검사 중…"; Progress = 0; NotifyAll();
        try
        {
            var progress = new Progress<DownloadProgress>(value => { if (ReferenceEquals(cancellation, source)) { Progress = value.Percent; Notify(nameof(Progress)); } });
            var plan = await coordinator.PrepareAsync(Release!, progress, source.Token);
            source.Token.ThrowIfCancellationRequested();
            if (!isIdle()) throw new InvalidOperationException("실행 중인 매크로/진단이 있어 설치하지 않습니다.");
            HandingOff = true; NotifyAll();
            await prepare(); prepared = true;
            await coordinator.LaunchHelperAsync(plan, source.Token);
            Status = "작업 종료 확인 완료 · 도우미에 설치를 맡기고 앱을 종료합니다.";
            handedOff = true;
            close();
        }
        catch (OperationCanceledException) { Status = "다운로드/준비를 취소했습니다. 기존 버전은 그대로입니다."; }
        catch (Exception exception) { Status = $"업데이트 준비 실패: {exception.Message}"; }
        finally
        {
            if (prepared && !handedOff) resume();
            HandingOff = false; cancellation = null; NotifyAll();
        }
    }
    private void NotifyAll()
    {
        foreach (var name in new[] { nameof(Channel), nameof(Status), nameof(CurrentVersion), nameof(LatestVersion), nameof(Notes), nameof(IsBusy), nameof(CanEdit), nameof(HasUpdate), nameof(Badge), nameof(HandingOff) }) Notify(name);
        CheckCommand.Refresh(); LaterCommand.Refresh(); CancelCommand.Refresh();
    }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public void Dispose() { cancellation?.Cancel(); }
    private sealed record UpdatePreferences(UpdateChannel Channel);
}
