using System.Diagnostics;
using System.IO;

namespace NyangShinobi.App.Updates;

public sealed record UpdatePlan(int SchemaVersion, string TargetRoot, string TransactionRoot, string ExpectedVersion,
    int ParentId, long ParentStartTicks, string Nonce, bool VerifyRestartOnly = false)
{
    public string PackageRoot => Path.Combine(TransactionRoot, "package");
    public string HelperRoot => Path.Combine(TransactionRoot, "runner");
    public string BackupRoot => Path.Combine(TransactionRoot, "backup");
    public string PlanPath => Path.Combine(TransactionRoot, "plan.json");
    public void Validate()
    {
        if (SchemaVersion != 1 || ParentId <= 0 || ParentStartTicks <= 0 || !Guid.TryParseExact(Nonce, "N", out _))
            throw new InvalidDataException("업데이트 계획의 식별자가 올바르지 않습니다.");
        SemVersion.Parse(ExpectedVersion);
        var target = Path.GetFullPath(TargetRoot).TrimEnd(Path.DirectorySeparatorChar);
        var transaction = Path.GetFullPath(TransactionRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetDirectoryName(target) is null || transaction != Path.Combine(target, ".updates", Nonce))
            throw new InvalidDataException("업데이트 작업 폴더가 앱의 .updates 경계를 벗어납니다.");
        UpdateFiles.RejectReparseParents(target, transaction);
        var current = BuildMetadata.Read(target);
        if (SemVersion.Parse(ExpectedVersion).CompareTo(SemVersion.Parse(current.Version)) <= 0)
            throw new InvalidDataException("동일/이전 버전으로 업데이트할 수 없습니다.");
    }
}

public sealed record InstallOutcome(bool Success, bool RolledBack, string Message, string BackupDirectory);
public sealed class RestartStillRunningException(string message, Exception inner) : Exception(message, inner);

public static class UpdateInstaller
{
    public static Task<InstallOutcome> InstallAsync(UpdatePlan plan, Func<CancellationToken, Task> restartAndVerify,
        CancellationToken token) => Task.Run(async () =>
    {
        plan.Validate();
        var current = InstallManifest.Read(plan.TargetRoot);
        var next = InstallManifest.Read(plan.PackageRoot);
        var metadata = BuildMetadata.Read(plan.PackageRoot);
        if (metadata.Version != plan.ExpectedVersion) throw new InvalidDataException("업데이트 계획과 실제 버전이 다릅니다.");
        foreach (var file in current.Files) { token.ThrowIfCancellationRequested(); UpdateFiles.VerifyFile(plan.TargetRoot, file); }
        foreach (var file in next.Files) { token.ThrowIfCancellationRequested(); UpdateFiles.VerifyFile(plan.PackageRoot, file); }
        var owned = current.Files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incoming = next.Files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in next.Files)
        {
            var target = UpdateFiles.InRoot(plan.TargetRoot, file.Path);
            if (!owned.Contains(file.Path) && (File.Exists(target) || Directory.Exists(target)))
                throw new IOException($"개인/미관리 파일과 충돌하므로 덮어쓰지 않습니다: {file.Path}");
        }
        if (Directory.Exists(plan.BackupRoot)) throw new IOException("이미 사용한 업데이트 작업입니다.");
        Directory.CreateDirectory(plan.BackupRoot);
        foreach (var file in current.Files)
        {
            token.ThrowIfCancellationRequested();
            var backup = UpdateFiles.InRoot(plan.BackupRoot, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(UpdateFiles.InRoot(plan.TargetRoot, file.Path), backup, false);
        }
        File.Copy(Path.Combine(plan.TargetRoot, InstallManifest.FileName), Path.Combine(plan.BackupRoot, InstallManifest.FileName));
        foreach (var file in current.Files) UpdateFiles.VerifyFile(plan.BackupRoot, file);
        var journal = Path.Combine(plan.TransactionRoot, "journal.json");
        UpdateFiles.WriteJson(journal, new { State = "prepared", plan.ExpectedVersion, plan.BackupRoot });
        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // 백업 완료 후 적용은 사용자 취소로 중간에 버리지 않는다. 실패하면 아래에서 복원한다.
            token.ThrowIfCancellationRequested();
            UpdateFiles.WriteJson(journal, new { State = "applying", plan.ExpectedVersion, plan.BackupRoot });
            foreach (var file in next.Files)
            {
                var target = UpdateFiles.InRoot(plan.TargetRoot, file.Path);
                AtomicCopy(UpdateFiles.InRoot(plan.PackageRoot, file.Path), target, owned.Contains(file.Path));
                if (!owned.Contains(file.Path)) created.Add(file.Path);
            }
            foreach (var obsolete in owned.Except(incoming, StringComparer.OrdinalIgnoreCase)) File.Delete(UpdateFiles.InRoot(plan.TargetRoot, obsolete));
            AtomicCopy(Path.Combine(plan.PackageRoot, InstallManifest.FileName), Path.Combine(plan.TargetRoot, InstallManifest.FileName));
            foreach (var file in next.Files) UpdateFiles.VerifyFile(plan.TargetRoot, file);
            await restartAndVerify(CancellationToken.None).ConfigureAwait(false);
            try { UpdateFiles.WriteJson(journal, new { State = "committed", plan.ExpectedVersion, plan.BackupRoot }); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { /* 새 앱 실행 후에는 파일 롤백을 시도하지 않는다. */ }
            return new InstallOutcome(true, false, "업데이트 설치 및 재실행 확인 완료", plan.BackupRoot);
        }
        catch (RestartStillRunningException exception)
        {
            UpdateFiles.WriteJson(journal, new { State = "restartUnconfirmed", Error = exception.Message, plan.BackupRoot });
            return new InstallOutcome(false, false, "새 앱이 종료되지 않아 파일 교체/롤백을 중단했습니다. 백업과 작업 기록을 보존합니다.", plan.BackupRoot);
        }
        catch (Exception installError)
        {
            try
            {
                foreach (var file in current.Files)
                {
                    // 잠겨 있지만 변경되지 않은 파일은 그대로 유지한다.
                    var unchanged = false;
                    try { UpdateFiles.VerifyFile(plan.TargetRoot, file); unchanged = true; }
                    catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) { }
                    if (!unchanged) AtomicCopy(UpdateFiles.InRoot(plan.BackupRoot, file.Path), UpdateFiles.InRoot(plan.TargetRoot, file.Path));
                }
                foreach (var file in created)
                {
                    var target = UpdateFiles.InRoot(plan.TargetRoot, file);
                    if (File.Exists(target)) { UpdateFiles.VerifyFile(plan.TargetRoot, next.Files.Single(item => item.Path == file)); File.Delete(target); }
                }
                AtomicCopy(Path.Combine(plan.BackupRoot, InstallManifest.FileName), Path.Combine(plan.TargetRoot, InstallManifest.FileName));
                UpdateFiles.WriteJson(journal, new { State = "rolledBack", Error = installError.Message, plan.BackupRoot });
                return new InstallOutcome(false, true, $"설치 실패, 기존 버전 복원 완료: {installError.Message}", plan.BackupRoot);
            }
            catch (Exception rollbackError)
            {
                UpdateFiles.WriteJson(journal, new { State = "rollbackFailed", Error = installError.Message, RollbackError = rollbackError.Message, plan.BackupRoot });
                return new InstallOutcome(false, false, $"설치/롤백 실패. 백업을 보존합니다: {rollbackError.Message}", plan.BackupRoot);
            }
        }
    }, token);

    private static void AtomicCopy(string source, string target, bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".updating";
        try { File.Copy(source, temporary); File.Move(temporary, target, overwrite); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static async Task<int> RunHelperAsync(string planPath, CancellationToken token)
    {
        var plan = UpdateFiles.ReadJson<UpdatePlan>(planPath);
        plan.Validate();
        if (!Path.GetFullPath(planPath).Equals(plan.PlanPath, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(plan.HelperRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("도우미는 독립된 작업 복사본에서만 실행할 수 있습니다.");
        foreach (var file in InstallManifest.Read(plan.PackageRoot).Files) UpdateFiles.VerifyFile(plan.PackageRoot, file);
        if (BuildMetadata.Read(plan.PackageRoot).Version != plan.ExpectedVersion) throw new InvalidDataException("도우미 시작 전 버전 검증 실패.");
        var original = InstallManifest.Read(plan.TargetRoot);
        foreach (var file in original.Files) UpdateFiles.VerifyFile(plan.TargetRoot, file);
        var originalPaths = original.Files.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in InstallManifest.Read(plan.PackageRoot).Files)
            if (!originalPaths.Contains(file.Path) && File.Exists(UpdateFiles.InRoot(plan.TargetRoot, file.Path)))
                throw new IOException($"미관리 파일과 충돌합니다: {file.Path}");
        // 살아 있는 다른 프로세스를 PID만으로 기다리거나 종료하지 않는다.
        Process? parent = null;
        try
        {
            try { parent = Process.GetProcessById(plan.ParentId); }
            catch (ArgumentException) { }
            if (parent is not null)
            {
                if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStartTicks ||
                    !string.Equals(parent.MainModule?.FileName, Path.Combine(plan.TargetRoot, "NyangShinobi.App.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("기다릴 원본 앱 프로세스의 신원이 다릅니다.");
            }
            UpdateFiles.WriteText(Path.Combine(plan.TransactionRoot, "ready"), plan.Nonce);
            if (parent is not null) await parent.WaitForExitAsync(token).WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
        }
        finally { parent?.Dispose(); }
        using var installGuard = InstallRootGuard.EnterInstaller(plan.TargetRoot);
        InstallOutcome outcome;
        var originalIntact = false;
        try
        {
            outcome = await InstallAsync(plan, async restartToken =>
            {
                var start = new ProcessStartInfo(Path.Combine(plan.TargetRoot, "NyangShinobi.App.exe"))
                { UseShellExecute = false, WorkingDirectory = plan.TargetRoot };
                start.ArgumentList.Add(plan.VerifyRestartOnly ? "--verify-update-restart" : "--update-restart");
                start.ArgumentList.Add(plan.PlanPath);
                using var app = Process.Start(start) ?? throw new IOException("새 앱 프로세스를 시작하지 못했습니다.");
                try
                {
                    var ack = Path.Combine(plan.TransactionRoot, "restarted");
                    var deadline = Stopwatch.StartNew();
                    while (deadline.Elapsed < TimeSpan.FromSeconds(45))
                    {
                        if (File.Exists(ack) && await File.ReadAllTextAsync(ack, restartToken).ConfigureAwait(false) == plan.Nonce) return;
                        if (app.HasExited) throw new IOException($"새 버전 시작 실패: exit={app.ExitCode}");
                        await Task.Delay(100, restartToken).ConfigureAwait(false);
                    }
                    throw new TimeoutException("새 버전의 시작 완료 응답이 없습니다.");
                }
                catch (Exception error)
                {
                    if (!app.HasExited)
                    {
                        app.CloseMainWindow();
                        try { await app.WaitForExitAsync(restartToken).WaitAsync(TimeSpan.FromSeconds(15), restartToken).ConfigureAwait(false); }
                        catch (Exception exitError) { throw new RestartStillRunningException("새 앱이 살아 있어 안전한 롤백이 불가능합니다.", exitError); }
                    }
                    throw new IOException("새 버전의 재실행 확인에 실패했습니다.", error);
                }
            }, token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try { foreach (var file in original.Files) UpdateFiles.VerifyFile(plan.TargetRoot, file); originalIntact = true; }
            catch (Exception verificationError) when (verificationError is IOException or InvalidDataException or UnauthorizedAccessException) { }
            outcome = new(false, false, $"설치 사전 작업 실패: {exception.Message} · 원본 무결성={originalIntact}", plan.BackupRoot);
        }
        try { UpdateFiles.WriteJson(Path.Combine(plan.TransactionRoot, "result.json"), outcome); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { await Console.Error.WriteLineAsync(outcome.Message).ConfigureAwait(false); }
        if ((outcome.RolledBack || originalIntact) && !plan.VerifyRestartOnly)
        {
            installGuard.Dispose();
            var restart = new ProcessStartInfo(Path.Combine(plan.TargetRoot, "NyangShinobi.App.exe")) { UseShellExecute = false, WorkingDirectory = plan.TargetRoot };
            restart.ArgumentList.Add("--update-result"); restart.ArgumentList.Add(plan.PlanPath);
            Process.Start(restart)?.Dispose();
        }
        return outcome.Success ? 0 : 1;
    }

    public static void AcknowledgeRestart(string planPath)
    {
        var plan = ValidateRestart(planPath);
        UpdateFiles.WriteText(Path.Combine(plan.TransactionRoot, "restarted"), plan.Nonce);
    }

    public static UpdatePlan ValidateRestart(string planPath)
    {
        var plan = UpdateFiles.ReadJson<UpdatePlan>(planPath);
        var root = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!root.Equals(plan.TargetRoot, StringComparison.OrdinalIgnoreCase) ||
            !plan.TransactionRoot.Equals(Path.Combine(root, ".updates", plan.Nonce), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFullPath(planPath).Equals(plan.PlanPath, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(plan.Nonce, "N", out _) || BuildMetadata.Read(root).Version != plan.ExpectedVersion)
            throw new InvalidDataException("재실행 확인 경로/버전이 다릅니다.");
        UpdateFiles.RejectReparseParents(root, plan.TransactionRoot);
        return plan;
    }
}
