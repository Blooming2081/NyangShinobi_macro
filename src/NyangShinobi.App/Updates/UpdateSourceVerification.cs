using System.IO;

namespace NyangShinobi.App.Updates;

/// <summary>실제 공개 Releases의 조회/다운로드 검증용 명시적 CLI. 설치는 하지 않는다.</summary>
public static class UpdateSourceVerification
{
    public static async Task<object> RunAsync(CancellationToken token)
    {
        using var client = new GitHubUpdateClient();
        var release = await client.CheckAsync("0.0.0", UpdateChannel.Development, token).ConfigureAwait(false);
        if (release is null) return new { Repository = GitHubUpdateClient.Repository, Found = false };
        var work = Path.Combine(AppContext.BaseDirectory, ".updates", Guid.NewGuid().ToString("N"));
        var zip = await client.DownloadAsync(release, work, null, token).ConfigureAwait(false);
        var installable = false; string? error = null;
        try { await UpdatePackage.ExtractAsync(zip, Path.Combine(work, "package"), release, token).ConfigureAwait(false); installable = true; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException)
        { error = exception.Message; }
        var result = new { Repository = GitHubUpdateClient.Repository, Found = true, release.Version, release.Tag,
            Asset = release.Zip.Name, Size = new FileInfo(zip).Length, Sha256Verified = true, InstallableMetadata = installable,
            ValidationError = error, WorkDirectory = work, Installed = false };
        UpdateFiles.WriteJson(Path.Combine(work, "source-verification.json"), result);
        return result;
    }
}
