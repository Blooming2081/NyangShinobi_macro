using System.Diagnostics;
using System.Text;

namespace NyangShinobi.Device.Adb;

public sealed class AdbProcessRunner : IAdbCommandRunner
{
    private readonly AdbOptions options;

    public AdbProcessRunner(AdbOptions options)
    {
        options.Validate();
        this.options = options;
    }

    public Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        Task.Run(() => RunCoreAsync(arguments, cancellationToken), cancellationToken);

    private async Task<AdbCommandResult> RunCoreAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.CommandTimeout);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(options.ExecutablePath)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        timeout.Token.ThrowIfCancellationRequested();
        process.Start();
        // 두 파이프를 동시에 비워 stderr가 큰 경우에도 교착되지 않는다.
        var stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, timeout.Token);
        var stderr = ReadBoundedAsync(process.StandardError.BaseStream, timeout.Token);
        var exit = process.WaitForExitAsync(timeout.Token);
        try
        {
            // 한 파이프의 크기 초과가 다른 파이프/프로세스 대기에 가려지지 않게 한다.
            var pending = new List<Task> { stdout, stderr, exit };
            while (pending.Count != 0)
            {
                var completed = await Task.WhenAny(pending).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
                pending.Remove(completed);
            }
            return new(await stdout.ConfigureAwait(false), Encoding.UTF8.GetString(await stderr.ConfigureAwait(false)), process.ExitCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"ADB 명령이 {options.CommandTimeout} 안에 완료되지 않았습니다.");
        }
        finally
        {
            timeout.Cancel();
            if (!process.HasExited)
            {
                // 이 호출이 띄운 CLI만 종료한다. 공유 서버/장치 프로세스는 종료하지 않는다.
                process.Kill(entireProcessTree: false);
                await process.WaitForExitAsync().WaitAsync(options.CleanupTimeout).ConfigureAwait(false);
            }
            // 실패 경로의 파이프 읽기도 종료/관찰한 뒤 Process와 스트림을 해제한다.
            try { await Task.WhenAll(stdout, stderr, exit).ConfigureAwait(false); }
            catch (Exception) when (timeout.IsCancellationRequested) { }
        }
    }

    private async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > options.MaxOutputBytes)
                throw new InvalidDataException("ADB 출력이 설정된 최대 크기를 초과했습니다.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
