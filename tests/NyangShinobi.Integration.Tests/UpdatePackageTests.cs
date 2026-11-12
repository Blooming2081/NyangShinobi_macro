using System.IO;
using System.IO.Compression;
using NyangShinobi.App.Updates;

namespace NyangShinobi.Integration.Tests;

public sealed class UpdatePackageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task ValidPackageChecksActualVersionAndManagedHashes()
    {
        var (bytes, release) = UpdateTestData.Package();
        var work = UpdateTestData.DirectoryPath(); var zip = Path.Combine(work, "input.zip");
        await File.WriteAllBytesAsync(zip, bytes, Token);
        var extracted = await UpdatePackage.ExtractAsync(zip, Path.Combine(work, "package"), release, Token);
        Assert.Equal(release.Version, BuildMetadata.Read(extracted).Version);
        Assert.NotEmpty(InstallManifest.Read(extracted).Files);
    }
    [Theory]
    [InlineData("../outside.exe")]
    [InlineData("..\\outside.exe")]
    [InlineData("/outside.exe")]
    [InlineData("C:/outside.exe")]
    [InlineData("file.exe:stream")]
    [InlineData("CON.txt")]
    [InlineData("COM¹.txt")]
    [InlineData("CONIN$")]
    [InlineData("trailing. ")]
    [InlineData(".updates/plan.json")]
    [InlineData("nyangshinobi.app.EXE")]
    [InlineData("link")]
    public async Task ArchiveRejectsTraversalAlternateStreamsReservedNamesDuplicatesAndLinks(string attack)
    {
        var (bytes, release) = UpdateTestData.Package();
        var work = UpdateTestData.DirectoryPath(); var zipPath = Path.Combine(work, "input.zip");
        await File.WriteAllBytesAsync(zipPath, bytes, Token);
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry(release.Zip.Name[..^4] + "/" + attack);
            if (attack == "link") entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var stream = entry.Open(); stream.WriteByte(1);
        }
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => UpdatePackage.ExtractAsync(zipPath, Path.Combine(work, "package"), release, Token));
        Assert.False(File.Exists(Path.Combine(work, "outside.exe")));
    }
    [Theory]
    [InlineData("bad-zip")]
    [InlineData("wrong-version")]
    [InlineData("damaged-file")]
    [InlineData("unlisted-file")]
    public async Task CorruptionAndMismatchedVersionsNeverBecomeInstallable(string kind)
    {
        var (bytes, release) = UpdateTestData.Package();
        var work = UpdateTestData.DirectoryPath(); var zipPath = Path.Combine(work, "input.zip");
        await File.WriteAllBytesAsync(zipPath, kind == "bad-zip" ? new byte[4] : bytes, Token);
        if (kind == "wrong-version") release = release with { Version = "0.1.0-rc.3" };
        if (kind is "damaged-file" or "unlisted-file")
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
            var name = release.Zip.Name[..^4] + (kind == "damaged-file" ? "/core.dll" : "/extra.dll");
            zip.GetEntry(name)?.Delete();
            using var stream = zip.CreateEntry(name).Open(); stream.WriteByte(0);
        }
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => UpdatePackage.ExtractAsync(zipPath, Path.Combine(work, "package"), release, Token));
    }
}
