using System.IO;
using System.IO.Compression;

namespace NyangShinobi.App.Updates;

public static class UpdatePackage
{
    public static async Task<string> ExtractAsync(string zipPath, string output, UpdateRelease release, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try { return await Task.Run(() => Extract(zipPath, output, release, timeout.Token), timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("ZIP 추출/검사 제한 시간 초과."); }
    }

    private static string Extract(string zipPath, string output, UpdateRelease release, CancellationToken token)
    {
        if (Directory.Exists(output)) throw new IOException("업데이트 추출 폴더가 이미 있습니다.");
        Directory.CreateDirectory(output);
        UpdateFiles.RejectReparseParents(output, output);
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count is < 1 or > 10000) throw new InvalidDataException("ZIP 항목 수 제한 초과.");
        var prefix = release.Zip.Name[..^4] + "/";
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("ZIP의 최상위 폴더가 배포 이름과 다릅니다.");
            var relative = name[prefix.Length..];
            if (relative.Length == 0) continue;
            var directory = relative.EndsWith('/');
            var safe = directory ? relative[..^1] : relative;
            UpdateFiles.ValidateRelativePath(safe);
            if (((entry.ExternalAttributes >> 16) & 0xF000) is 0xA000 ||
                (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("ZIP 링크/재분석 지점은 허용하지 않습니다.");
            var destination = UpdateFiles.InRoot(output, safe);
            if (directory) { Directory.CreateDirectory(destination); continue; }
            if (!files.Add(safe) || entry.Length > 268435456 || (expanded += entry.Length) > 1024L * 1024 * 1024)
                throw new InvalidDataException("ZIP 중복 경로 또는 확장 크기 제한 초과.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var target = new FileStream(destination, FileMode.CreateNew);
            var buffer = new byte[81920]; long written = 0; int count;
            while ((count = input.Read(buffer)) != 0)
            {
                token.ThrowIfCancellationRequested();
                written += count;
                if (written > entry.Length) throw new InvalidDataException("ZIP 항목 길이 불일치.");
                target.Write(buffer, 0, count);
            }
            if (written != entry.Length) throw new InvalidDataException("ZIP 항목이 잘렸습니다.");
        }
        var manifest = InstallManifest.Read(output);
        var expected = manifest.Files.Select(file => file.Path).Append(PortableLayout.ManifestRelativePath(output)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!files.SetEquals(expected)) throw new InvalidDataException("ZIP 내용과 설치 파일 목록이 일치하지 않습니다.");
        foreach (var file in manifest.Files) { token.ThrowIfCancellationRequested(); UpdateFiles.VerifyFile(output, file); }
        var metadata = BuildMetadata.Read(output);
        if (metadata.Version != release.Version || metadata.ReleaseTag != release.Tag)
            throw new InvalidDataException("ZIP의 실제 버전/태그가 선택한 Release와 다릅니다.");
        return output;
    }
}
