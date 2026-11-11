using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Device.Adb;

// 전송 완료는 Pending이다. 화면 반영 여부는 Application에서 새 프레임으로 검증한다.
public sealed class AdbInputController(AdbDeviceClient client) : IInputController
{
    public Task<InputOutcome> TapAsync(string deviceSerial, NormalizedPoint point, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var size = client.GetGeometry(deviceSerial);
        return SendAsync(deviceSerial, ["shell", "input", "tap", Pixel(point.X, size.Width), Pixel(point.Y, size.Height)], cancellationToken);
    }

    public Task<InputOutcome> SwipeAsync(string deviceSerial, NormalizedPoint from, NormalizedPoint to,
        TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (duration <= TimeSpan.Zero || duration.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(duration));
        var size = client.GetGeometry(deviceSerial);
        return SendAsync(deviceSerial, ["shell", "input", "swipe", Pixel(from.X, size.Width), Pixel(from.Y, size.Height),
            Pixel(to.X, size.Width), Pixel(to.Y, size.Height), ((int)Math.Ceiling(duration.TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture)], cancellationToken);
    }

    private static string Pixel(double normalized, int length) =>
        Math.Clamp((int)Math.Round(normalized * length, MidpointRounding.AwayFromZero), 0, length - 1)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    private async Task<InputOutcome> SendAsync(string serial, IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        await client.InputGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await client.ExecuteAsync(serial, command, cancellationToken).ConfigureAwait(false);
            if (result.StandardOutput.Length != 0 || !string.IsNullOrWhiteSpace(result.StandardError))
                throw new IOException("ADB 입력이 예상하지 않은 출력을 반환했습니다. 재입력하지 마세요.");
            return InputOutcome.Pending;
        }
        finally { client.InputGate.Release(); }
    }
}
