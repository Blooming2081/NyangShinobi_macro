using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NyangShinobi.App.Updates;

public interface IUpdateCoordinator
{
    BuildMetadata Current { get; }
    Task<UpdateRelease?> CheckAsync(UpdateChannel channel, CancellationToken token);
    Task<UpdatePlan> PrepareAsync(UpdateRelease release, IProgress<DownloadProgress>? progress, CancellationToken token);
    Task LaunchHelperAsync(UpdatePlan plan, CancellationToken token);
}

public sealed class UpdateCoordinator(GitHubUpdateClient client, string? appRoot = null) : IUpdateCoordinator
{
    public string AppRoot { get; } = Path.GetFullPath(appRoot ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
    public BuildMetadata Current => BuildMetadata.Current(AppRoot);
    public Task<UpdateRelease?> CheckAsync(UpdateChannel channel, CancellationToken token) => client.CheckAsync(Current.Version, channel, token);

    public async Task<UpdatePlan> PrepareAsync(UpdateRelease release, IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        var runtime = Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory());
        if (!runtime.StartsWith(AppRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(AppRoot, InstallManifest.FileName)))
            throw new InvalidOperationException("자동 설치는 버전/설치 목록을 포함한 Windows x64 자체 포함 포터블 배포에서만 지원합니다.");
        using var parent = Process.GetCurrentProcess();
        var nonce = Guid.NewGuid().ToString("N");
        var plan = new UpdatePlan(1, AppRoot, Path.Combine(AppRoot, ".updates", nonce), release.Version,
            parent.Id, parent.StartTime.ToUniversalTime().Ticks, nonce);
        plan.Validate();
        Directory.CreateDirectory(plan.TransactionRoot);
        var zip = await client.DownloadAsync(release, plan.TransactionRoot, progress, token).ConfigureAwait(false);
        await UpdatePackage.ExtractAsync(zip, plan.PackageRoot, release, token).ConfigureAwait(false);
        // 런타임/앱의 최상위 파일만 복사한다. 에셋은 도우미 시작에 필요하지 않다.
        await Task.Run(() =>
        {
            Directory.CreateDirectory(plan.HelperRoot);
            var manifest = InstallManifest.Read(AppRoot);
            foreach (var file in manifest.Files.Where(file => !file.Path.Contains('/') &&
                (file.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))))
            {
                token.ThrowIfCancellationRequested();
                UpdateFiles.VerifyFile(AppRoot, file);
                File.Copy(UpdateFiles.InRoot(AppRoot, file.Path), Path.Combine(plan.HelperRoot, file.Path), false);
            }
            File.Copy(Path.Combine(AppRoot, InstallManifest.FileName), Path.Combine(plan.HelperRoot, InstallManifest.FileName), false);
            UpdateFiles.WriteJson(plan.PlanPath, plan);
        }, token).ConfigureAwait(false);
        return plan;
    }

    public async Task LaunchHelperAsync(UpdatePlan plan, CancellationToken token)
    {
        plan.Validate();
        var start = new ProcessStartInfo(Path.Combine(plan.HelperRoot, "NyangShinobi.App.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = plan.HelperRoot };
        start.ArgumentList.Add("--update-helper"); start.ArgumentList.Add(plan.PlanPath);
        using var helper = Process.Start(start) ?? throw new IOException("업데이트 도우미를 시작하지 못했습니다.");
        try
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(30))
            {
                var ready = Path.Combine(plan.TransactionRoot, "ready");
                if (File.Exists(ready) && await File.ReadAllTextAsync(ready, token).ConfigureAwait(false) == plan.Nonce) return;
                if (helper.HasExited) throw new IOException($"업데이트 도우미 시작 실패: exit={helper.ExitCode}. 작업 폴더: {plan.TransactionRoot}");
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            throw new TimeoutException("도우미 준비 확인 시간 초과. 앱은 종료하지 않았습니다.");
        }
        catch
        {
            // 아직 원본 앱이 살아 있어 도우미는 파일 교체를 시작하지 않았다.
            // 인계 실패 시 자신의 도우미만 회수해 나중에 예기치 않게 설치하지 못하게 한다.
            if (!helper.HasExited) helper.Kill(entireProcessTree: false);
            await helper.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            throw;
        }
    }
}
