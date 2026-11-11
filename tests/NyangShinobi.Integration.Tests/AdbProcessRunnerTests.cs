using System.Diagnostics;
using System.IO;
using NyangShinobi.Device.Adb;

namespace NyangShinobi.Integration.Tests;

public sealed class AdbProcessRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static AdbOptions Options => new()
    {
        // 실제 adb/앱플레이어를 실행하지 않는 OS 프로세스 경계 테스트다.
        ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")
    };

    [Fact]
    public async Task BinaryStdoutAndStderrAreDrainedWithoutTextConversion()
    {
        var runner = new AdbProcessRunner(Options);
        var result = await runner.RunAsync(["-NoProfile", "-NonInteractive", "-Command",
            "[Console]::OpenStandardOutput().Write([byte[]](0,1,255),0,3); [Console]::Error.Write('diagnostic'); exit 7"], Token);
        Assert.Equal(new byte[] { 0, 1, 255 }, result.StandardOutput);
        Assert.Equal("diagnostic", result.StandardError);
        Assert.Equal(7, result.ExitCode);
    }

    [Fact]
    public async Task OutputSizeIsBounded()
    {
        var runner = new AdbProcessRunner(Options with { MaxOutputBytes = 16 });
        await Assert.ThrowsAsync<InvalidDataException>(() => runner.RunAsync(["-NoProfile", "-NonInteractive", "-Command",
            "[Console]::OpenStandardOutput().Write((New-Object byte[] 1024),0,1024)"], Token));
    }

    [Fact]
    public async Task TimeoutAndCancellationReleaseOnlyOwnedProcess()
    {
        foreach (var cancel in new[] { false, true })
        {
            var marker = Path.Combine(Path.GetTempPath(), $"nyang-adb-test-{Guid.NewGuid():N}.pid");
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
            var runner = new AdbProcessRunner(Options with { CommandTimeout = TimeSpan.FromSeconds(2) });
            var command = $"[IO.File]::WriteAllText('{marker.Replace("'", "''")}', [string]$PID); Start-Sleep -Seconds 30";
            var run = runner.RunAsync(["-NoProfile", "-NonInteractive", "-Command", command], cancellation.Token);
            try
            {
                if (cancel)
                {
                    var deadline = Stopwatch.StartNew();
                    while (!File.Exists(marker) && deadline.Elapsed < TimeSpan.FromSeconds(5))
                        await Task.Delay(TimeSpan.FromMilliseconds(10), Token);
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
                }
                else await Assert.ThrowsAsync<TimeoutException>(() => run);
                Assert.True(File.Exists(marker), "가짜 CLI가 시작되어 PID를 기록해야 합니다.");
                var id = int.Parse(await File.ReadAllTextAsync(marker, Token), System.Globalization.CultureInfo.InvariantCulture);
                try { using var process = Process.GetProcessById(id); Assert.True(process.HasExited); }
                catch (ArgumentException) { /* 이미 OS에서 프로세스가 회수됨 */ }
            }
            finally { File.Delete(marker); }
        }
    }
}
