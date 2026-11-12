using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NyangShinobi.App.Updates;

/// <summary>같은 설치 폴더의 GUI 중복 실행과 파일 교체 중 새 GUI 진입을 막는다.</summary>
public static class InstallRootGuard
{
    private static string Name(string root) => "Local\\NyangShinobi-" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())));
    public static IDisposable EnterGui(string root, bool verifiedRestart)
    {
        var mutex = new Mutex(false, Name(root));
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new InvalidOperationException("같은 설치 폴더의 프로그램이 이미 실행 중입니다.");
            if (!verifiedRestart)
            {
                var path = Path.Combine(root, ".updates", "install.lock");
                UpdateFiles.RejectReparseParents(root, path);
                if (File.Exists(path)) { using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
            }
            return new OwnedMutex(mutex);
        }
        catch { if (acquired) mutex.ReleaseMutex(); mutex.Dispose(); throw; }
    }
    public static FileStream EnterInstaller(string root)
    {
        var path = Path.Combine(root, ".updates", "install.lock");
        UpdateFiles.RejectReparseParents(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try { using var noGui = EnterGui(root, true); return stream; }
        catch { stream.Dispose(); throw; }
    }
    private sealed class OwnedMutex(Mutex mutex) : IDisposable
    { public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); } }
}
