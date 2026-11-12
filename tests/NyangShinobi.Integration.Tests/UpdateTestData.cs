using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NyangShinobi.App.Updates;

namespace NyangShinobi.Integration.Tests;

internal static class UpdateTestData
{
    public static string DirectoryPath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "NyangShinobi.slnx"))) root = root.Parent;
        var path = Path.Combine(root?.FullName ?? Path.GetTempPath(), "artifacts", "update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
    public static void WriteInstallation(string root, string version, bool next = false, bool conflict = false, bool nested = false)
    {
        Directory.CreateDirectory(root);
        var app = nested ? Path.Combine(root, "app") : root;
        Directory.CreateDirectory(app);
        if (nested)
        {
            File.WriteAllText(Path.Combine(root, PortableLayout.Launcher), "launcher-" + version);
            File.WriteAllText(Path.Combine(root, PortableLayout.Guide), "guide-" + version);
        }
        File.WriteAllText(Path.Combine(app, "NyangShinobi.App.exe"), "app-" + version);
        File.WriteAllText(Path.Combine(app, "core.dll"), "core-" + version);
        File.WriteAllText(Path.Combine(app, next ? "added.dll" : "old-only.dll"), "owned-" + version);
        if (conflict) File.WriteAllText(Path.Combine(app, "settings.v0.1.json"), "must-not-overwrite");
        UpdateFiles.WriteJson(Path.Combine(app, BuildMetadata.FileName), new BuildMetadata(nested ? 2 : 1, BuildMetadata.Identity, version, "win-x64", "test-commit", "v" + version));
        WriteManifest(root);
    }
    public static void WriteManifest(string root)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != InstallManifest.FileName)
            .Select(path => new ManagedUpdateFile(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).ToArray();
        UpdateFiles.WriteJson(PortableLayout.ManifestPath(root), new InstallManifest(BuildMetadata.Read(root).SchemaVersion, BuildMetadata.Identity, files));
    }
    public static (byte[] Bytes, UpdateRelease Release) Package(string version = "0.1.0-rc.2", bool nested = false)
    {
        var root = DirectoryPath(); WriteInstallation(root, version, true, nested: nested);
        var name = $"NyangShinobi-v{version}-win-x64.zip";
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                using var entry = zip.CreateEntry(name[..^4] + "/" + Path.GetRelativePath(root, path).Replace('\\', '/')).Open();
                entry.Write(File.ReadAllBytes(path));
            }
        }
        var bytes = memory.ToArray();
        return (bytes, Release(version, bytes.Length));
    }
    public static UpdateRelease Release(string version, long size = 10)
    {
        var tag = "v" + version; var name = $"NyangShinobi-{tag}-win-x64.zip";
        var prefix = $"https://github.com/{GitHubUpdateClient.Repository}/releases/download/{tag}/";
        return new(tag, version, "업데이트 내용", new($"https://github.com/{GitHubUpdateClient.Repository}/releases/tag/{tag}"),
            new(name, new(prefix + name), size), new(name + ".sha256", new(prefix + name + ".sha256"), 64));
    }
    public static string ReleasesJson(params (string Version, bool Prerelease, bool Draft)[] versions) => JsonSerializer.Serialize(versions.Select(item =>
    {
        var release = Release(item.Version);
        return new { tag_name = release.Tag, prerelease = item.Prerelease, draft = item.Draft, body = release.Notes,
            assets = new[] { release.Zip, release.Hash }.Select(asset => new { name = asset.Name, size = asset.Size, browser_download_url = asset.Url.AbsoluteUri }) };
    }));
    public static HttpResponseMessage Response(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new ByteArrayContent(bytes) };
    public static HttpResponseMessage Response(string text, HttpStatusCode status = HttpStatusCode.OK) => Response(Encoding.UTF8.GetBytes(text), status);
    public sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add(request.RequestUri!); return respond(request, cancellationToken); }
    }
}
