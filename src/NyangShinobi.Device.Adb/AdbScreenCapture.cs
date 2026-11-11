using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using System.Diagnostics;

namespace NyangShinobi.Device.Adb;

public sealed class AdbScreenCapture(AdbDeviceClient client, IImageFrameDecoder decoder, IGameClock clock) : IScreenCapture
{
    private long frameId;

    public async Task<ImageFrame> CaptureAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        AdbDeviceClient.ValidateSerial(deviceSerial);
        await client.CaptureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var png = await client.ExecuteAsync(deviceSerial, ["exec-out", "screencap", "-p"], cancellationToken).ConfigureAwait(false);
            var frame = await decoder.DecodePngAsync(png.StandardOutput, Interlocked.Increment(ref frameId),
                clock.LocalNow, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            client.SetGeometry(deviceSerial, frame.Width, frame.Height);
            return frame;
        }
        finally
        {
            RuntimeDiagnostics.CaptureDurationMs.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            client.CaptureGate.Release();
        }
    }
}
