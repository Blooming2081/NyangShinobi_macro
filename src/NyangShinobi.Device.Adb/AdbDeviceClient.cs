using System.Collections.Concurrent;
using System.Text;

namespace NyangShinobi.Device.Adb;

public sealed record AdbDeviceInfo(string Serial, string State);

public sealed class AdbDeviceClient(IAdbCommandRunner runner)
{
    private readonly ConcurrentDictionary<string, (int Width, int Height)> geometry = new(StringComparer.Ordinal);
    internal SemaphoreSlim CaptureGate { get; } = new(1, 1);
    internal SemaphoreSlim InputGate { get; } = new(1, 1);

    public async Task<IReadOnlyList<AdbDeviceInfo>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        var result = await RunCheckedAsync(["devices", "-l"], cancellationToken).ConfigureAwait(false);
        var lines = Encoding.UTF8.GetString(result.StandardOutput).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("List of devices attached", StringComparison.Ordinal))
            throw new InvalidDataException("ADB 장치 목록 형식이 올바르지 않습니다.");
        return lines.Skip(1).Select(line =>
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) throw new InvalidDataException("잘못된 ADB 장치 항목입니다.");
            return new AdbDeviceInfo(parts[0], parts[1]);
        }).ToArray();
    }

    internal async Task<AdbCommandResult> ExecuteAsync(string serial, IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        ValidateSerial(serial);
        var devices = await ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        var selected = devices.Where(device => device.Serial == serial).ToArray();
        if (selected.Length != 1 || selected[0].State != "device")
            throw new InvalidOperationException("명시한 serial의 준비된 장치가 없습니다. 사용자 확인이 필요합니다.");
        cancellationToken.ThrowIfCancellationRequested();
        return await RunCheckedAsync(["-s", serial, .. command], cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdbCommandResult> RunCheckedAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await runner.RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.ExitCode != 0)
            throw new IOException($"ADB 실패 ({result.ExitCode}): {result.StandardError}");
        return result;
    }

    public static void ValidateSerial(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (serial.StartsWith('-') || serial.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw new ArgumentException("serial에 공백/제어 문자/옵션을 사용할 수 없습니다.", nameof(serial));
    }

    internal void SetGeometry(string serial, int width, int height) => geometry[serial] = (width, height);
    internal (int Width, int Height) GetGeometry(string serial)
    {
        ValidateSerial(serial);
        return geometry.TryGetValue(serial, out var size) ? size :
            throw new InvalidOperationException("같은 장치의 정상 PNG 캡처로 해상도를 먼저 확인해야 합니다.");
    }
}
