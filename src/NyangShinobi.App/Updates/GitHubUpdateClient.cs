using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NyangShinobi.App.Updates;

public enum UpdateChannel { Development, Stable }
public sealed record ReleaseAsset(string Name, Uri Url, long Size);
public sealed record UpdateRelease(string Tag, string Version, string Notes, Uri Page, ReleaseAsset Zip, ReleaseAsset Hash);
public sealed record DownloadProgress(long Received, long Total) { public double Percent => Total == 0 ? 0 : Received * 100d / Total; }

public sealed class GitHubUpdateClient : IDisposable
{
    public const string Repository = "Blooming2081/NyangShinobi_macro";
    public const long MaxDownloadBytes = 512L * 1024 * 1024;
    private readonly HttpClient http;
    public GitHubUpdateClient() : this(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10), AutomaticDecompression = DecompressionMethods.None }) { }
    public GitHubUpdateClient(HttpMessageHandler handler)
    {
        http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NyangShinobi-Updater/0.1");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateRelease?> CheckAsync(string currentVersion, UpdateChannel channel, CancellationToken token)
    {
        var current = SemVersion.Parse(currentVersion);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        UpdateRelease? best = null;
        for (var page = 1; page <= 5; page++)
        {
            var bytes = await ReadSmallAsync(new($"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}"), 2 * 1024 * 1024, deadline.Token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(bytes);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("GitHub Releases 응답이 배열이 아닙니다.");
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean()) continue;
                var tag = release.GetProperty("tag_name").GetString() ?? "";
                if (!tag.StartsWith('v') || !SemVersion.TryParse(tag[1..], out var version) || version.CompareTo(current) <= 0) continue;
                if (channel == UpdateChannel.Stable && (version.IsPrerelease || release.GetProperty("prerelease").GetBoolean())) continue;
                var zipName = $"NyangShinobi-{tag}-win-x64.zip";
                var assets = release.GetProperty("assets").EnumerateArray().ToArray();
                var zipMatches = assets.Where(asset => asset.GetProperty("name").GetString() == zipName).ToArray();
                var hashMatches = assets.Where(asset => asset.GetProperty("name").GetString() == zipName + ".sha256").ToArray();
                if (zipMatches.Length != 1 || hashMatches.Length != 1) continue;
                var zip = ParseAsset(zipMatches[0], tag, zipName);
                var hash = ParseAsset(hashMatches[0], tag, zipName + ".sha256");
                if (zip.Size is <= 0 or > MaxDownloadBytes || hash.Size is <= 0 or > 4096) continue;
                var notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                if (notes.Length > 32000) notes = notes[..32000] + "\n(이하 생략)";
                var candidate = new UpdateRelease(tag, version.Value, notes,
                    new($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}"), zip, hash);
                if (best is null || version.CompareTo(SemVersion.Parse(best.Version)) > 0) best = candidate;
            }
            if (json.RootElement.GetArrayLength() < 100) break;
        }
        return best;
    }

    private static ReleaseAsset ParseAsset(JsonElement asset, string tag, string name)
    {
        var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "");
        var expected = new Uri($"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}");
        if (url != expected) throw new InvalidDataException("Release asset 경로가 공식 저장소와 일치하지 않습니다.");
        return new(name, url, asset.GetProperty("size").GetInt64());
    }

    public async Task<string> DownloadAsync(UpdateRelease release, string workDirectory, IProgress<DownloadProgress>? progress, CancellationToken token)
    {
        // 전달 객체도 다시 검증한다. HTTP 리다이렉트는 매 단계 검증한다.
        ValidateAsset(release.Zip, release.Tag, false); ValidateAsset(release.Hash, release.Tag, true);
        Directory.CreateDirectory(workDirectory);
        UpdateFiles.RejectReparseParents(workDirectory, workDirectory);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var checksum = Encoding.ASCII.GetString(await ReadSmallAsync(release.Hash.Url, 4096, deadline.Token).ConfigureAwait(false)).Trim();
        if (!UpdateFiles.IsSha256(checksum)) throw new InvalidDataException("SHA-256 파일 형식이 올바르지 않습니다.");
        var path = Path.Combine(workDirectory, "download.zip");
        var created = false;
        try
        {
            using var response = await GetAsync(release.Zip.Url, deadline.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is { } length && length != release.Zip.Size)
                throw new InvalidDataException("Release의 파일 크기와 응답 크기가 다릅니다.");
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            created = true;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long received = 0;
            int count;
            var lastProgress = System.Diagnostics.Stopwatch.StartNew();
            while ((count = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
            {
                received += count;
                if (received > release.Zip.Size || received > MaxDownloadBytes) throw new InvalidDataException("업데이트 다운로드 크기 제한 초과.");
                hash.AppendData(buffer, 0, count);
                await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token).ConfigureAwait(false);
                if (lastProgress.ElapsedMilliseconds >= 100) { progress?.Report(new(received, release.Zip.Size)); lastProgress.Restart(); }
            }
            if (received != release.Zip.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(checksum, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("업데이트 ZIP 크기 또는 SHA-256이 일치하지 않습니다.");
            await output.FlushAsync(deadline.Token).ConfigureAwait(false);
            progress?.Report(new(received, release.Zip.Size));
        }
        catch { if (created && File.Exists(path)) File.Delete(path); throw; }
        return path;
    }

    private static void ValidateAsset(ReleaseAsset asset, string tag, bool hash)
    {
        if (!tag.StartsWith('v') || !SemVersion.TryParse(tag[1..], out _)) throw new InvalidDataException("Release 태그 오류.");
        var name = $"NyangShinobi-{tag}-win-x64.zip" + (hash ? ".sha256" : "");
        var expected = new Uri($"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}");
        if (asset.Name != name || asset.Url != expected || asset.Size <= 0 || asset.Size > (hash ? 4096 : MaxDownloadBytes))
            throw new InvalidDataException("허용하지 않는 업데이트 asset입니다.");
    }

    private async Task<byte[]> ReadSmallAsync(Uri uri, int limit, CancellationToken token)
    {
        using var response = await GetAsync(uri, token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("네트워크 메타데이터 크기 초과.");
        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        { if (output.Length + count > limit) throw new InvalidDataException("메타데이터 크기 제한 초과."); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
    private async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        for (var redirects = 0; redirects < 6; redirects++)
        {
            if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                uri.Host is not ("api.github.com" or "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com" or "github-releases.githubusercontent.com"))
                throw new InvalidDataException("허용하지 않는 업데이트 HTTPS 주소입니다.");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                uri = location is null ? throw new InvalidDataException("업데이트 리다이렉트 주소 없음.") : new Uri(uri, location);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("업데이트 리다이렉트 횟수 초과.");
    }
    public void Dispose() => http.Dispose();
}
