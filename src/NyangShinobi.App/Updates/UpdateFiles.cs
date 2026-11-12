using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace NyangShinobi.App.Updates;

public sealed record BuildMetadata(int SchemaVersion, string AppId, string Version, string Runtime, string Commit, string? ReleaseTag)
{
    public const string FileName = "version.json";
    public const string Identity = "NyangShinobi";
    public static BuildMetadata Read(string root)
    {
        var value = UpdateFiles.ReadJson<BuildMetadata>(Path.Combine(PortableLayout.AppDirectory(root), FileName));
        if (value.SchemaVersion is not (1 or PortableLayout.CurrentSchema) || value.AppId != Identity || value.Runtime != "win-x64")
            throw new InvalidDataException("빌드 메타데이터의 앱/플랫폼이 일치하지 않습니다.");
        SemVersion.Parse(value.Version);
        return value;
    }
    public static BuildMetadata Current(string root)
    {
        if (File.Exists(Path.Combine(root, FileName))) return Read(root);
        var version = typeof(BuildMetadata).Assembly.GetName().Version!;
        return new(1, Identity, $"{version.Major}.{version.Minor}.{version.Build}-dev.0", "win-x64", "unpackaged", null);
    }
}

public sealed record ManagedUpdateFile(string Path, long Length, string Sha256);
public sealed record InstallManifest(int SchemaVersion, string AppId, IReadOnlyList<ManagedUpdateFile> Files)
{
    public const string FileName = "install-manifest.json";
    public static InstallManifest Read(string root)
    {
        var manifest = UpdateFiles.ReadJson<InstallManifest>(PortableLayout.ManifestPath(root));
        var nested = !PortableLayout.AppDirectory(root).Equals(root, StringComparison.OrdinalIgnoreCase);
        var prefix = nested ? "app/" : "";
        if (manifest.SchemaVersion != (nested ? PortableLayout.CurrentSchema : 1) || manifest.AppId != BuildMetadata.Identity || manifest.Files.Count is < 1 or > 10000 ||
            BuildMetadata.Read(root).SchemaVersion != manifest.SchemaVersion)
            throw new InvalidDataException("설치 파일 목록이 올바르지 않습니다.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            UpdateFiles.ValidateRelativePath(file.Path);
            if (file.Path.Equals(prefix + FileName, StringComparison.OrdinalIgnoreCase) || !paths.Add(file.Path) || file.Length is < 0 or > 268435456 ||
                !UpdateFiles.IsSha256(file.Sha256)) throw new InvalidDataException("중복/잘못된 설치 파일 항목입니다.");
        }
        if (!paths.Contains(prefix + "NyangShinobi.App.exe") || !paths.Contains(prefix + BuildMetadata.FileName) ||
            (nested && (!paths.Contains(PortableLayout.Launcher) || !paths.Contains(PortableLayout.Guide) ||
                paths.Any(path => !path.StartsWith("app/", StringComparison.OrdinalIgnoreCase) && path != PortableLayout.Launcher && path != PortableLayout.Guide))))
            throw new InvalidDataException("앱 실행 파일 또는 버전 메타데이터가 없습니다.");
        return manifest;
    }
}

public static class UpdateFiles
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static T ReadJson<T>(string path)
    {
        if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("업데이트 메타데이터 크기 초과.");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("업데이트 JSON이 비어 있습니다.");
    }
    public static void WriteJson<T>(string path, T value)
        => WriteText(path, JsonSerializer.Serialize(value, JsonOptions));
    public static void WriteText(string path, string value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, value);
        File.Move(temporary, path, true);
    }
    public static bool IsSha256(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
    public static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 220 || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.EndsWith('/'))
            throw new InvalidDataException($"허용하지 않는 업데이트 경로: {path}");
        foreach (var part in path.Split('/'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(character => char.IsControl(character) || "<>\"|?*".Contains(character)) ||
                part.Equals(".updates", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"위험한 업데이트 경로: {path}");
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                 (stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3')))
                throw new InvalidDataException($"Windows 예약 이름: {path}");
        }
    }
    public static string InRoot(string root, string relative)
    {
        ValidateRelativePath(relative);
        var canonicalRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(canonicalRoot, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        if (!path.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("업데이트 경로가 대상 폴더를 벗어납니다.");
        RejectReparseParents(root, path);
        return path;
    }
    public static void RejectReparseParents(string root, string path)
    {
        var current = System.IO.Path.GetFullPath(path);
        var boundary = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
        while (current.Length >= boundary.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"재분석 지점은 업데이트할 수 없습니다: {current}");
            if (current.Equals(boundary, StringComparison.OrdinalIgnoreCase)) break;
            current = System.IO.Path.GetDirectoryName(current) ?? throw new InvalidDataException("경로 경계 오류.");
        }
    }
    public static void VerifyFile(string root, ManagedUpdateFile file)
    {
        var path = InRoot(root, file.Path);
        if (!File.Exists(path) || new FileInfo(path).Length != file.Length)
            throw new InvalidDataException($"설치 파일 해시/크기 불일치: {file.Path}");
        using var stream = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"설치 파일 해시 불일치: {file.Path}");
    }
}
