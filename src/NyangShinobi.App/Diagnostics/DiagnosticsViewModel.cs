using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Device.Adb;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.App.Diagnostics;

public sealed class DiagnosticsViewModel : INotifyPropertyChanged
{
    private readonly CaptureDiagnostics diagnostics;
    private CancellationTokenSource? cancellation;
    private bool stopping;
    private bool available = true;
    private string adbPath;
    private AdbDeviceInfo? selectedDevice;
    private TemplateDescriptor? selectedAsset;
    private ImageFrame? frame;
    private string roiLeft = "0", roiTop = "0", roiRight = "1", roiBottom = "1";

    public DiagnosticsViewModel(CaptureDiagnostics diagnostics)
    {
        this.diagnostics = diagnostics;
        adbPath = diagnostics.DefaultAdbPath;
        DevicesCommand = new RelayCommand(() => Completion = RefreshDevicesAsync(), () => CanEdit);
        AssetsCommand = new RelayCommand(() => Completion = LoadAssetsAsync(), () => CanEdit);
        CaptureCommand = new RelayCommand(() => Completion = CaptureAsync(), () => CanEdit && SelectedDevice?.State == "device");
        BenchmarkCommand = new RelayCommand(() => Completion = BenchmarkAsync(), () => CanEdit && SelectedDevice?.State == "device");
        MatchCommand = new RelayCommand(() => Completion = MatchAsync(), () => CanEdit && frame is not null && SelectedAsset is not null);
        CancelCommand = new RelayCommand(() => cancellation?.Cancel(), () => IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public RelayCommand DevicesCommand { get; }
    public RelayCommand AssetsCommand { get; }
    public RelayCommand CaptureCommand { get; }
    public RelayCommand BenchmarkCommand { get; }
    public RelayCommand MatchCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public IReadOnlyList<AdbDeviceInfo> Devices { get; private set; } = [];
    public IReadOnlyList<TemplateDescriptor> Assets { get; private set; } = [];
    public BitmapSource? Preview { get; private set; }
    public string Status { get; private set; } = "ADB 경로를 확인하고 장치 목록을 조회한 뒤 직접 선택하세요. 입력 명령은 전송하지 않습니다.";
    public string CaptureSummary { get; private set; } = "캡처 없음";
    public string MatchSummary { get; private set; } = "임계값을 적용하지 않고 일치율만 측정합니다.";
    public string BenchmarkSummary { get; private set; } = "예열 1회 후 PNG/RAW 각 10회 · 회차마다 순서 교대 · 기본 PNG 캡처는 유지합니다.";
    public CaptureBenchmarkResult? BenchmarkResult { get; private set; }
    public IReadOnlyList<BenchmarkSummaryRow> BenchmarkRows { get; private set; } = [];
    public IReadOnlyList<CaptureBenchmarkSample> BenchmarkSamples => BenchmarkResult?.Samples ?? [];
    public bool IsBusy => cancellation is not null;
    public bool CanEdit => available && !IsBusy && !stopping;
    public bool CanSave => CanEdit && Preview is not null;

    public string AdbPath
    {
        get => adbPath;
        set
        {
            if (!CanEdit || value == adbPath) return;
            adbPath = value;
            Devices = [];
            selectedDevice = null;
            ClearCapture();
            Changed(); Changed(nameof(Devices)); Changed(nameof(SelectedDevice));
            Refresh();
        }
    }

    public AdbDeviceInfo? SelectedDevice
    {
        get => selectedDevice;
        set
        {
            if (!CanEdit || Equals(selectedDevice, value)) return;
            selectedDevice = value;
            ClearCapture();
            Changed(); Refresh();
        }
    }

    public TemplateDescriptor? SelectedAsset
    {
        get => selectedAsset;
        set { if (CanEdit) { selectedAsset = value; ClearMatch(); Changed(); Refresh(); } }
    }

    public string RoiLeft { get => roiLeft; set => SetRoi(ref roiLeft, value); }
    public string RoiTop { get => roiTop; set => SetRoi(ref roiTop, value); }
    public string RoiRight { get => roiRight; set => SetRoi(ref roiRight, value); }
    public string RoiBottom { get => roiBottom; set => SetRoi(ref roiBottom, value); }

    public Task RefreshDevicesAsync() => RunAsync(async token =>
    {
        // 조회 실패 시에도 과거 목록이나 캡처를 새 장치의 결과처럼 표시하지 않는다.
        Devices = []; selectedDevice = null; ClearCapture();
        Changed(nameof(Devices)); Changed(nameof(SelectedDevice));
        var devices = await diagnostics.ListDevicesAsync(AdbPath, token);
        token.ThrowIfCancellationRequested();
        Devices = devices;
        Changed(nameof(Devices));
        Status = $"장치 {Devices.Count}개 · 직접 선택하세요. offline/unauthorized 장치는 캡처할 수 없습니다.";
    });

    public Task LoadAssetsAsync() => RunAsync(async token =>
    {
        var assets = await diagnostics.GetAssetsAsync(token);
        token.ThrowIfCancellationRequested();
        Assets = assets;
        Changed(nameof(Assets));
        Status = $"템플릿 {Assets.Count}개 · 검사할 에셋을 직접 선택하세요. 전체 화면 참조는 제외됩니다.";
    });

    public Task CaptureAsync() => RunAsync(async token =>
    {
        var device = SelectedDevice;
        if (device?.State != "device") throw new InvalidOperationException("준비된 장치를 직접 선택하세요.");
        ClearCapture();
        var captured = await diagnostics.CaptureAsync(AdbPath, device.Serial, token);
        var preview = await DiagnosticImages.CreatePreviewAsync(captured.Frame, token);
        token.ThrowIfCancellationRequested();
        frame = captured.Frame;
        Preview = preview;
        CaptureSummary = $"{device.Serial} · {frame.Width}×{frame.Height} · {frame.CapturedAt:HH:mm:ss.fff} · frame {frame.FrameId} · " +
            $"캡처/ADB {Math.Max(0, (captured.TotalDuration - captured.DecodeDuration).TotalMilliseconds):F1} ms · " +
            $"디코딩 {captured.DecodeDuration.TotalMilliseconds:F1} ms · 전체 {captured.TotalDuration.TotalMilliseconds:F1} ms";
        Changed(nameof(Preview)); Changed(nameof(CaptureSummary));
        Status = "메모리 캡처 완료 · 파일은 저장하지 않았습니다.";
    });

    public Task MatchAsync() => RunAsync(async token =>
    {
        ClearMatch();
        var current = frame ?? throw new InvalidOperationException("먼저 화면을 캡처하세요.");
        var asset = SelectedAsset ?? throw new InvalidOperationException("에셋을 선택하세요.");
        var roi = new NormalizedRegion(new(Parse(RoiLeft), Parse(RoiTop)), new(Parse(RoiRight), Parse(RoiBottom)));
        if (roi.TopLeft.X >= roi.BottomRight.X || roi.TopLeft.Y >= roi.BottomRight.Y)
            throw new ArgumentException("ROI의 오른쪽/아래 값은 왼쪽/위보다 커야 합니다.");
        var measured = await diagnostics.MatchAsync(current, asset, roi, token);
        token.ThrowIfCancellationRequested();
        if (measured.Result.FrameId != current.FrameId) throw new InvalidOperationException("다른 프레임의 인식 결과입니다.");
        var evidence = measured.Result.Evidence.SingleOrDefault();
        MatchSummary = evidence is null
            ? $"측정 가능한 위치 없음 · 인식 {measured.Duration.TotalMilliseconds:F1} ms · ROI/템플릿 크기를 확인하세요."
            : $"일치율 {evidence.Score:P2} ({evidence.Score:F6}) · 중심 ({evidence.Center.X:F4}, {evidence.Center.Y:F4}) · " +
              $"인식 {measured.Duration.TotalMilliseconds:F1} ms · frame {current.FrameId} · 임계값 미적용";
        Changed(nameof(MatchSummary));
        Status = "선택한 메모리 프레임의 점수 측정 완료 · 재캡처/게임 입력 없음. 최초 로딩 또는 캐시 시간이 포함됩니다.";
    });

    public Task BenchmarkAsync() => RunAsync(async token =>
    {
        var device = SelectedDevice;
        if (device?.State != "device") throw new InvalidOperationException("준비된 장치를 직접 선택하세요.");
        ClearCapture();
        BenchmarkSummary = $"ADB: {AdbPath} · serial: {device.Serial} · 벤치마크 준비 중 · PNG/RAW 지원 미확인";
        Changed(nameof(BenchmarkSummary));
        var source = cancellation;
        var progress = new Progress<string>(message =>
        {
            // 이전 작업에서 늦게 도착한 Dispatcher 알림은 새 작업 상태를 덮어쓰지 않는다.
            if (!token.IsCancellationRequested && ReferenceEquals(cancellation, source))
            { Status = message; Changed(nameof(Status)); }
        });
        BenchmarkResult = await diagnostics.BenchmarkAsync(AdbPath, device.Serial, progress, token);
        BenchmarkRows = BenchmarkResult.Summary;
        BenchmarkSummary = BenchmarkResult.Description;
        Changed(nameof(BenchmarkResult)); Changed(nameof(BenchmarkRows)); Changed(nameof(BenchmarkSamples)); Changed(nameof(BenchmarkSummary));
        Status = BenchmarkResult.Canceled ? "벤치마크 취소 · 완료된 표본만 메모리에 유지합니다." :
            "벤치마크 종료 · 결과는 메모리에만 있습니다. 실패/미완료 표본과 실제 화면 조건을 확인하세요.";
    });

    // View의 SaveFileDialog에서 사용자가 확인한 경로만 전달한다. 자동 저장 호출은 없다.
    public Task SaveAsync(string path) => RunAsync(async token =>
    {
        var image = Preview ?? throw new InvalidOperationException("먼저 화면을 캡처하세요.");
        await DiagnosticImages.SavePngAsync(image, path, token);
        Status = $"사용자가 지정한 PNG 저장 완료: {path}";
    });

    private Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (!CanEdit) return Task.CompletedTask;
        var source = new CancellationTokenSource();
        cancellation = source;
        Status = "진단 작업 중…";
        Refresh();
        return Completion = RunCoreAsync(operation, source);
    }

    private async Task RunCoreAsync(Func<CancellationToken, Task> operation, CancellationTokenSource source)
    {
        using (source)
        {
            try { await operation(source.Token); }
            catch (OperationCanceledException) { Status = "진단 작업을 취소했습니다."; }
            catch (Exception exception) { Status = $"진단 실패: {exception.Message}"; }
            finally { cancellation = null; Refresh(); }
        }
    }

    public async Task StopAsync()
    {
        stopping = true;
        cancellation?.Cancel();
        await Completion;
        frame = null; Preview = null;
        ClearBenchmark();
        Changed(nameof(Preview)); Refresh();
    }

    public void SetAvailable(bool value)
    { available = value; Refresh(); }

    private static double Parse(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private void SetRoi(ref string target, string value, [CallerMemberName] string? name = null)
    { if (CanEdit) { target = value; ClearMatch(); Changed(name); } }
    private void ClearMatch()
    { MatchSummary = "임계값을 적용하지 않고 일치율만 측정합니다."; Changed(nameof(MatchSummary)); }
    private void ClearCapture()
    {
        frame = null; Preview = null; CaptureSummary = "캡처 없음";
        ClearBenchmark();
        ClearMatch(); Changed(nameof(Preview)); Changed(nameof(CaptureSummary));
    }
    private void ClearBenchmark()
    {
        BenchmarkResult = null; BenchmarkRows = [];
        BenchmarkSummary = "예열 1회 후 PNG/RAW 각 10회 · 회차마다 순서 교대 · 기본 PNG 캡처는 유지합니다.";
        Changed(nameof(BenchmarkResult)); Changed(nameof(BenchmarkRows)); Changed(nameof(BenchmarkSamples)); Changed(nameof(BenchmarkSummary));
    }
    private void Refresh()
    {
        Changed(nameof(Status)); Changed(nameof(IsBusy)); Changed(nameof(CanEdit)); Changed(nameof(CanSave));
        DevicesCommand.Refresh(); AssetsCommand.Refresh(); CaptureCommand.Refresh(); MatchCommand.Refresh(); CancelCommand.Refresh();
        BenchmarkCommand.Refresh();
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
