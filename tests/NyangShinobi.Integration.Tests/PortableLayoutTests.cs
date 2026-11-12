using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App;
using NyangShinobi.App.Updates;
using NyangShinobi.Device.Adb;

namespace NyangShinobi.Integration.Tests;

public sealed class PortableLayoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedZipVerifiesOuterLauncherAndAppPayloadTogether(bool tamperLauncher)
    {
        var (bytes, release) = UpdateTestData.Package(nested: true);
        var work = UpdateTestData.DirectoryPath(); var zipPath = Path.Combine(work, "input.zip");
        await File.WriteAllBytesAsync(zipPath, bytes, Token);
        if (tamperLauncher)
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            var name = release.Zip.Name[..^4] + "/" + PortableLayout.Launcher;
            zip.GetEntry(name)!.Delete(); using var stream = zip.CreateEntry(name).Open(); stream.WriteByte(0);
        }
        var output = Path.Combine(work, "extracted");
        if (tamperLauncher)
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdatePackage.ExtractAsync(zipPath, output, release, Token));
        else
        {
            await UpdatePackage.ExtractAsync(zipPath, output, release, Token);
            Assert.Equal(2, BuildMetadata.Read(output).SchemaVersion);
            Assert.Equal(output, PortableLayout.InstallationRoot(Path.Combine(output, "app")));
            Assert.Equal("app/install-manifest.json", PortableLayout.ManifestRelativePath(output));
            Assert.False(File.Exists(Path.Combine(output, InstallManifest.FileName))); // 구형 파서는 교체 전 거부한다.
            Assert.Contains(InstallManifest.Read(output).Files, file => file.Path == PortableLayout.Launcher);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NestedInstallAndRollbackManageOuterFilesAndPreserveBothPersonalLocations(bool restartFails, bool lockFile)
    {
        var plan = Plan(true, true);
        var app = PortableLayout.AppDirectory(plan.TargetRoot);
        File.WriteAllText(Path.Combine(plan.TargetRoot, "personal.txt"), "outer-personal");
        File.WriteAllText(Path.Combine(app, "personal.json"), "app-personal");
        using var locked = lockFile ? new FileStream(Path.Combine(app, "core.dll"), FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        var result = await UpdateInstaller.InstallAsync(plan, _ => restartFails ? throw new IOException("restart fixture") : Task.CompletedTask, Token);
        var succeeded = !restartFails && !lockFile;
        Assert.Equal(succeeded, result.Success); Assert.Equal(!succeeded, result.RolledBack);
        var version = succeeded ? plan.ExpectedVersion : "0.1.0-rc.1";
        Assert.Equal(version, BuildMetadata.Read(plan.TargetRoot).Version);
        Assert.Equal("launcher-" + version, File.ReadAllText(Path.Combine(plan.TargetRoot, PortableLayout.Launcher)));
        Assert.Equal("guide-" + version, File.ReadAllText(Path.Combine(plan.TargetRoot, PortableLayout.Guide)));
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.BackupRoot).Version);
        Assert.Equal("outer-personal", File.ReadAllText(Path.Combine(plan.TargetRoot, "personal.txt")));
        Assert.Equal("app-personal", File.ReadAllText(Path.Combine(app, "personal.json")));
        foreach (var file in InstallManifest.Read(plan.TargetRoot).Files) UpdateFiles.VerifyFile(plan.TargetRoot, file);
        Assert.Equal(succeeded, File.Exists(Path.Combine(app, "added.dll")));
        Assert.Equal(!succeeded, File.Exists(Path.Combine(app, "old-only.dll")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LayoutMigrationIsBlockedBeforeBackupOrReplacement(bool fromNested)
    {
        var plan = Plan(fromNested, !fromNested);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(plan, _ => throw new IOException("must not restart"), Token));
        Assert.Contains("수동 설치", error.Message);
        Assert.False(Directory.Exists(plan.BackupRoot));
        Assert.Equal("0.1.0-rc.1", BuildMetadata.Read(plan.TargetRoot).Version);
        foreach (var file in InstallManifest.Read(plan.TargetRoot).Files) UpdateFiles.VerifyFile(plan.TargetRoot, file);
    }

    [Theory]
    [InlineData("missing-launcher")]
    [InlineData("extra-root-file")]
    [InlineData("schema")]
    [InlineData("ambiguous-root")]
    public void NestedManifestRejectsWrongBoundaryAndMissingEntryPoints(string kind)
    {
        var root = UpdateTestData.DirectoryPath(); UpdateTestData.WriteInstallation(root, "0.1.0", nested: true);
        var path = PortableLayout.ManifestPath(root); var manifest = InstallManifest.Read(root);
        if (kind == "missing-launcher") manifest = manifest with { Files = manifest.Files.Where(file => file.Path != PortableLayout.Launcher).ToArray() };
        if (kind == "extra-root-file") manifest = manifest with { Files = manifest.Files.Append(new("rogue.dll", 1, new string('0', 64))).ToArray() };
        if (kind == "schema") manifest = manifest with { SchemaVersion = 1 };
        if (kind == "ambiguous-root") File.Copy(Path.Combine(root, "app", BuildMetadata.FileName), Path.Combine(root, BuildMetadata.FileName));
        UpdateFiles.WriteJson(path, manifest);
        Assert.Throws<InvalidDataException>(() => InstallManifest.Read(root));
    }

    [Fact]
    public void NestedPlanUsesOuterRootAndRefusesAnAppOnlyTransaction()
    {
        var plan = Plan(true, true); plan.Validate();
        var wrong = plan with { TransactionRoot = Path.Combine(plan.TargetRoot, ".updates", plan.Nonce) };
        Assert.Throws<InvalidDataException>(wrong.Validate);
        Assert.Equal(Path.Combine(plan.TargetRoot, "app", "NyangShinobi.App.exe"), PortableLayout.Executable(plan.TargetRoot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BundledAdbUsesAbsoluteContentPathAndExplicitOverrideIsPreserved(bool custom)
    {
        var root = UpdateTestData.DirectoryPath(); File.WriteAllText(Path.Combine(root, "adb.exe"), "fixture");
        var options = custom ? new AdbOptions { ExecutablePath = "custom-adb.exe" } : null;
        using var services = new ServiceCollection().AddNyangShinobi(Path.Combine(root, "data", "settings.default.v0.1.json"), adbOptions: options).BuildServiceProvider();
        Assert.Equal(custom ? "custom-adb.exe" : Path.Combine(root, "adb.exe"), services.GetRequiredService<AdbOptions>().ExecutablePath);
    }

    private static UpdatePlan Plan(bool currentNested, bool nextNested)
    {
        var root = UpdateTestData.DirectoryPath(); UpdateTestData.WriteInstallation(root, "0.1.0-rc.1", nested: currentNested);
        var nonce = Guid.NewGuid().ToString("N"); using var parent = Process.GetCurrentProcess();
        var plan = new UpdatePlan(1, root, Path.Combine(PortableLayout.UpdateDirectory(root), nonce), "0.1.0-rc.2", parent.Id, parent.StartTime.ToUniversalTime().Ticks, nonce);
        UpdateTestData.WriteInstallation(plan.PackageRoot, plan.ExpectedVersion, true, nested: nextNested);
        return plan;
    }
}
