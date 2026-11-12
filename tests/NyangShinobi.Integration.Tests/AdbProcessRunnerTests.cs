using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using NyangShinobi.Device.Adb;

namespace NyangShinobi.Integration.Tests;

public sealed class AdbProcessRunnerTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly TimeSpan ReadinessBudget = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CleanupBudget = TimeSpan.FromSeconds(5);
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

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 3000)]
    [InlineData(true, 3000)]
    public async Task TimeoutAndCancellationReleaseOnlyOwnedProcess(bool cancel, int startupDelayMilliseconds)
    {
        // The runner's deadline includes CLI startup. Reserve a bounded startup phase,
        // then require an actual PID handshake before testing cancellation or timeout.
        var options = Options with { CommandTimeout = ReadinessBudget + TimeSpan.FromSeconds(3) };
        var pipeName = $"nyang-adb-test-{Guid.NewGuid():N}";
        using var readyPipe = CreateReadyPipe(pipeName);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        using var unrelated = new Process
        {
            StartInfo = new ProcessStartInfo(options.ExecutablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 120" })
            unrelated.StartInfo.ArgumentList.Add(argument);

        var command = $$"""
            $ErrorActionPreference = 'Stop';
            Start-Sleep -Milliseconds {{startupDelayMilliseconds}};
            $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', '{{pipeName}}', [IO.Pipes.PipeDirection]::Out);
            try {
                $pipe.Connect({{(int)ReadinessBudget.TotalMilliseconds}});
                $writer = [IO.StreamWriter]::new($pipe);
                try {
                    $writer.WriteLine([string]$PID);
                    $writer.Flush();
                    Start-Sleep -Seconds 120;
                } finally { $writer.Dispose(); }
            } finally { $pipe.Dispose(); }
            """;
        string[] arguments = ["-NoProfile", "-NonInteractive", "-Command", command];
        output.WriteLine($"OS={RuntimeInformation.OSDescription}; architecture={RuntimeInformation.ProcessArchitecture}; .NET={Environment.Version}");
        output.WriteLine($"cancel={cancel}; startupDelayMs={startupDelayMilliseconds}; commandTimeout={options.CommandTimeout}; readinessBudget={ReadinessBudget}");
        output.WriteLine($"executable={options.ExecutablePath}; exists={File.Exists(options.ExecutablePath)}; pipe={pipeName}");
        output.WriteLine($"arguments={string.Join(" | ", arguments)}");

        unrelated.Start();
        Task<AdbCommandResult>? run = null;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            run = new AdbProcessRunner(options).RunAsync(arguments, cancellation.Token);
            var id = await WaitForReadyAsync(readyPipe, run, options.ExecutablePath);
            output.WriteLine($"readyElapsedMs={stopwatch.ElapsedMilliseconds}; ownedPid={id}; unrelatedPid={unrelated.Id}");
            using var owned = Process.GetProcessById(id);
            Assert.NotEqual(unrelated.Id, id);
            Assert.False(owned.HasExited, "가짜 CLI는 준비 신호 후에도 실행 중이어야 합니다.");
            Assert.False(unrelated.HasExited, "비교용 프로세스가 검증 전에 종료됐습니다.");
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            }
            else await Assert.ThrowsAsync<TimeoutException>(() => run);

            output.WriteLine($"completedElapsedMs={stopwatch.ElapsedMilliseconds}; ownedExited={owned.HasExited}; unrelatedExited={unrelated.HasExited}");
            Assert.True(owned.HasExited, "타임아웃·취소된 호출의 CLI가 회수되지 않았습니다.");
            Assert.False(unrelated.HasExited, "다른 호출의 프로세스까지 종료했습니다.");
        }
        finally
        {
            // Also observe/reap the runner when readiness or an assertion fails.
            cancellation.Cancel();
            try
            {
                if (run is not null)
                {
                    try { await run.WaitAsync(CleanupBudget, CancellationToken.None); }
                    catch (TimeoutException) when (!run.IsCompleted) { throw; }
                    catch (Exception exception) { output.WriteLine($"runnerCleanup={exception.GetType().Name}: {exception.Message}"); }
                }
            }
            finally
            {
                // This test created this sentinel; never kill by name or process tree.
                if (!unrelated.HasExited) unrelated.Kill(entireProcessTree: false);
                await unrelated.WaitForExitAsync(CancellationToken.None).WaitAsync(CleanupBudget, CancellationToken.None);
            }
        }
    }

    [Fact]
    public async Task ReadinessReportsProcessStartFailure()
    {
        using var pipe = CreateReadyPipe($"nyang-adb-test-{Guid.NewGuid():N}");
        var missingExecutable = Path.Combine(Path.GetTempPath(), $"nyang-missing-{Guid.NewGuid():N}.exe");
        var run = new AdbProcessRunner(Options with { ExecutablePath = missingExecutable }).RunAsync([], Token);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WaitForReadyAsync(pipe, run, missingExecutable));
        Assert.IsType<Win32Exception>(error.InnerException);
        Assert.Contains("프로세스 시작 실패", error.Message);
        Assert.Contains(missingExecutable, error.Message);
    }

    [Fact]
    public async Task ReadinessReportsEarlyExitWithStderr()
    {
        using var pipe = CreateReadyPipe($"nyang-adb-test-{Guid.NewGuid():N}");
        var run = new AdbProcessRunner(Options).RunAsync(["-NoProfile", "-NonInteractive", "-Command",
            "[Console]::Error.Write('fake-startup-error'); exit 17"], Token);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => WaitForReadyAsync(pipe, run, Options.ExecutablePath));
        Assert.Contains("준비 신호 전 종료", error.Message);
        Assert.Contains("exitCode=17", error.Message);
        Assert.Contains("fake-startup-error", error.Message);
    }

    private static NamedPipeServerStream CreateReadyPipe(string name) =>
        new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task<int> WaitForReadyAsync(NamedPipeServerStream pipe, Task<AdbCommandResult> run, string executable)
    {
        using var reading = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var ready = ReadReadyPidAsync(pipe, reading.Token);
        try
        {
            Task completed;
            try { completed = await Task.WhenAny(ready, run).WaitAsync(ReadinessBudget, Token); }
            catch (TimeoutException exception)
            {
                throw new TimeoutException($"가짜 CLI 준비 대기 시간 초과: budget={ReadinessBudget}; executable={executable}; connected={pipe.IsConnected}; runner={run.Status}", exception);
            }
            if (completed == ready) return await ready;

            AdbCommandResult result;
            try { result = await run; }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException($"가짜 CLI 프로세스 시작 실패: executable={executable}; nativeError={exception.NativeErrorCode}", exception);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !Token.IsCancellationRequested)
            {
                throw new InvalidOperationException($"가짜 CLI 준비 신호 전 실행 실패: executable={executable}; runner={run.Status}; cause={exception.GetType().Name}: {exception.Message}", exception);
            }
            throw new InvalidOperationException($"가짜 CLI 준비 신호 전 종료: executable={executable}; exitCode={result.ExitCode}; stderr={result.StandardError}");
        }
        finally
        {
            reading.Cancel();
            try { await ready; }
            catch (Exception) when (reading.IsCancellationRequested) { }
        }
    }

    private static async Task<int> ReadReadyPidAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        await pipe.WaitForConnectionAsync(cancellationToken);
        using var reader = new StreamReader(pipe, leaveOpen: true);
        var line = await reader.ReadLineAsync(cancellationToken);
        if (!int.TryParse(line, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new InvalidDataException($"가짜 CLI의 준비 응답 PID가 잘못되었습니다: '{line}'");
        return id;
    }
}
