using System.IO;

namespace NyangShinobi.App.Updates;

/// <summary>v1 평면 배포와 v2 app/ 배포의 설치 경계. 서로 간 자동 변환은 금지한다.</summary>
public static class PortableLayout
{
    public const int CurrentSchema = 2;
    public const string Launcher = "냥냥시노비 실행.cmd";
    public const string Guide = "사용 설명서.txt";
    public const string ManualInstallMessage = "배포 폴더 구조가 변경되어 자동 전환하지 않습니다. Release ZIP을 새 폴더에 압축 해제해 수동 설치하세요. 기존 폴더에 덮어쓰지 마세요. 사용자 설정은 %LocalAppData%/NyangShinobi에 유지됩니다.";

    public static string AppDirectory(string installRoot)
    {
        var flat = File.Exists(Path.Combine(installRoot, BuildMetadata.FileName));
        var nested = File.Exists(Path.Combine(installRoot, "app", BuildMetadata.FileName));
        if (flat && nested) throw new InvalidDataException("평면/하위 폴더 버전 정보가 함께 있어 설치 경계가 모호합니다.");
        return nested ? Path.Combine(installRoot, "app") : installRoot;
    }

    public static string InstallationRoot(string appDirectory)
    {
        var app = Path.GetFullPath(appDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!File.Exists(Path.Combine(app, BuildMetadata.FileName)) || BuildMetadata.Read(app).SchemaVersion == 1) return app;
        var parent = Path.GetDirectoryName(app) ?? throw new InvalidDataException("설치 폴더 경계 오류.");
        if (!Path.GetFileName(app).Equals("app", StringComparison.OrdinalIgnoreCase) ||
            !AppDirectory(parent).Equals(app, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("새 배포는 전체 폴더를 유지해야 합니다. app 폴더만 이동하지 마세요.");
        return parent;
    }

    public static string Executable(string installRoot) => Path.Combine(AppDirectory(installRoot), "NyangShinobi.App.exe");
    public static string ManifestPath(string installRoot) => Path.Combine(AppDirectory(installRoot), InstallManifest.FileName);
    public static string ManifestRelativePath(string installRoot) => Path.GetRelativePath(installRoot, ManifestPath(installRoot)).Replace('\\', '/');
    public static string UpdateDirectory(string installRoot) => Path.Combine(AppDirectory(installRoot), ".updates");

    public static void EnsureCompatible(string currentRoot, string packageRoot)
    {
        if (BuildMetadata.Read(currentRoot).SchemaVersion != BuildMetadata.Read(packageRoot).SchemaVersion)
            throw new InvalidDataException(ManualInstallMessage);
    }
}
