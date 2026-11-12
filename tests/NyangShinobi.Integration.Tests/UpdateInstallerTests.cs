using System.Diagnostics;
using System.IO;
using NyangShinobi.App.Updates;

namespace NyangShinobi.Integration.Tests;

public sealed class UpdateInstallerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static UpdatePlan Plan()
    {
        var target = UpdateTestData.DirectoryPath();
        UpdateTestData.WriteInstallation(target, "0.1.0-rc.1");
        var nonce = Guid.NewGuid().ToString("N");
        using var parent = Process.GetCurrentProcess();
        var plan = new UpdatePlan(1, target, Path.Combine(target, ".updates", nonce), "0.1.0-rc.2", parent.Id,
            parent.StartTime.ToUniversalTime().Ticks, nonce);
        UpdateTestData.WriteInstallation(plan.PackageRoot, plan.ExpectedVersion, true);
        File.WriteAllText(Path.Combine(target, "settings.v0.1.json"), "personal-settings");
        Directory.CreateDirectory(Path.Combine(target, "screenshots"));
        File.WriteAllText(Path.Combine(target, "screenshots", "personal.png"), "personal-capture");
        return plan;
    }
    [Fact]
    public async Task SuccessfulInstallBacksUpManagedFilesAndPreservesPersonalData()
    {
        var plan = Plan(); var restarted = false;
        var outcome = await UpdateInstaller.InstallAsync(plan, _ => { restarted = true; return Task.CompletedTask; }, Token);
        Assert.True(outcome.Success); Assert.True(restarted);
        Assert.Equal(plan.ExpectedVersion, BuildMetadata.Read(plan.TargetRoot).Version);
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.BackupRoot).Version);
        Assert.False(File.Exists(Path.Combine(plan.TargetRoot, "old-only.dll")));
        Assert.True(File.Exists(Path.Combine(plan.TargetRoot, "added.dll")));
        PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task ManagedFileCaseChangeDoesNotDeleteTheNewFile()
    {
        var plan = Plan();
        var manifest = InstallManifest.Read(plan.PackageRoot);
        UpdateFiles.WriteJson(Path.Combine(plan.PackageRoot, InstallManifest.FileName), manifest with
        { Files = manifest.Files.Select(file => file.Path == "core.dll" ? file with { Path = "CORE.dll" } : file).ToArray() });
        var outcome = await UpdateInstaller.InstallAsync(plan, _ => Task.CompletedTask, Token);
        Assert.True(outcome.Success);
        Assert.True(File.Exists(Path.Combine(plan.TargetRoot, "CORE.dll")));
        PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task RealLockedFileFailureRestoresAlreadyReplacedFiles()
    {
        var plan = Plan();
        using var locked = new FileStream(Path.Combine(plan.TargetRoot, "core.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
        var outcome = await UpdateInstaller.InstallAsync(plan, _ => throw new InvalidOperationException("must not restart"), Token);
        Assert.False(outcome.Success); Assert.True(outcome.RolledBack);
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.TargetRoot).Version);
        Assert.Equal("app-0.1.0-rc.1", await File.ReadAllTextAsync(Path.Combine(plan.TargetRoot, "NyangShinobi.App.exe"), Token));
        Assert.True(File.Exists(Path.Combine(plan.TargetRoot, "old-only.dll")));
        PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task RestartFailureRollsBackRemovedAndAddedFiles()
    {
        var plan = Plan();
        var outcome = await UpdateInstaller.InstallAsync(plan, _ => throw new IOException("restart failed"), Token);
        Assert.False(outcome.Success); Assert.True(outcome.RolledBack);
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.TargetRoot).Version);
        Assert.True(File.Exists(Path.Combine(plan.TargetRoot, "old-only.dll")));
        Assert.False(File.Exists(Path.Combine(plan.TargetRoot, "added.dll")));
        foreach (var file in InstallManifest.Read(plan.TargetRoot).Files) UpdateFiles.VerifyFile(plan.TargetRoot, file);
        PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task StillRunningNewAppPreventsUnsafeRollback()
    {
        var plan = Plan();
        var outcome = await UpdateInstaller.InstallAsync(plan, _ => throw new RestartStillRunningException("still running", new IOException()), Token);
        Assert.False(outcome.Success); Assert.False(outcome.RolledBack);
        Assert.Equal(plan.ExpectedVersion, BuildMetadata.Read(plan.TargetRoot).Version);
        Assert.True(File.Exists(Path.Combine(plan.BackupRoot, "NyangShinobi.App.exe")));
        PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task IncomingPersonalFileCollisionNeverOverwritesIt()
    {
        var plan = Plan();
        File.WriteAllText(Path.Combine(plan.PackageRoot, "settings.v0.1.json"), "new package collision");
        UpdateTestData.WriteManifest(plan.PackageRoot);
        await Assert.ThrowsAsync<IOException>(() => UpdateInstaller.InstallAsync(plan, _ => Task.CompletedTask, Token));
        Assert.False(Directory.Exists(plan.BackupRoot)); PersonalDataUnchanged(plan);
    }
    [Fact]
    public async Task CancellationBeforeInstallationLeavesOriginalUntouched()
    {
        var plan = Plan();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UpdateInstaller.InstallAsync(plan, _ => Task.CompletedTask, cancel.Token));
        Assert.False(Directory.Exists(plan.BackupRoot));
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.TargetRoot).Version); PersonalDataUnchanged(plan);
    }
    [Theory]
    [InlineData("../elsewhere")]
    [InlineData("same-version")]
    public void PlanRejectsWrongBoundaryOrDowngrade(string attack)
    {
        var plan = Plan();
        plan = attack == "same-version" ? plan with { ExpectedVersion = "0.1.0-rc.1" } : plan with { TransactionRoot = Path.Combine(plan.TargetRoot, attack) };
        Assert.Throws<InvalidDataException>(plan.Validate);
    }
    private static void PersonalDataUnchanged(UpdatePlan plan)
    {
        Assert.Equal("personal-settings", File.ReadAllText(Path.Combine(plan.TargetRoot, "settings.v0.1.json")));
        Assert.Equal("personal-capture", File.ReadAllText(Path.Combine(plan.TargetRoot, "screenshots", "personal.png")));
    }
    [Fact]
    public void InstallerLockPreventsAnotherRegularGuiFromOpening()
    {
        var root = UpdateTestData.DirectoryPath();
        using var installing = InstallRootGuard.EnterInstaller(root);
        Assert.Throws<IOException>(() => InstallRootGuard.EnterGui(root, false));
    }
    [Fact]
    public async Task GuiInstanceGuardIsExclusiveAndReleasedByItsOwner()
    {
        var root = UpdateTestData.DirectoryPath();
        using var release = new ManualResetEventSlim(false);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = Task.Run(() =>
        {
            using var instance = InstallRootGuard.EnterGui(root, false);
            held.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10), Token)) throw new TimeoutException("instance guard test timed out");
        }, Token);
        try
        {
            await held.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.Throws<InvalidOperationException>(() => InstallRootGuard.EnterGui(root, false));
        }
        finally { release.Set(); await owner.WaitAsync(TimeSpan.FromSeconds(5), Token); }
        using var next = InstallRootGuard.EnterGui(root, false);
    }
}
