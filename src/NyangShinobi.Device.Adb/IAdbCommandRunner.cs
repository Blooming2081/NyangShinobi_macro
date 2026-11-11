namespace NyangShinobi.Device.Adb;

public sealed record AdbCommandResult(byte[] StandardOutput, string StandardError, int ExitCode);

// OS 프로세스 경계. 테스트는 이 포트로 가짜 ADB를 제공한다.
public interface IAdbCommandRunner
{
    Task<AdbCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
