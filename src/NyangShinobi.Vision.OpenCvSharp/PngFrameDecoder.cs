using System.Buffers.Binary;
using System.Runtime.InteropServices;
using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using global::OpenCvSharp;

namespace NyangShinobi.Vision.OpenCvSharp;

public sealed class PngFrameDecoder : IImageFrameDecoder
{
    private readonly VisionOptions options;

    public PngFrameDecoder(VisionOptions options)
    {
        options.Validate();
        this.options = options;
    }

    public async Task<ImageFrame> DecodePngAsync(ReadOnlyMemory<byte> png, long frameId,
        DateTimeOffset capturedAt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.OperationTimeout);
        try
        {
            return await Task.Run(() => Decode(png, frameId, capturedAt, timeout.Token), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("PNG 디코딩 제한 시간을 초과했습니다."); }
    }

    private ImageFrame Decode(ReadOnlyMemory<byte> png, long frameId, DateTimeOffset capturedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePng(png.Span, options.MaxPixels);
        using var decoded = Cv2.ImDecode(png.Span, ImreadModes.Color);
        if (decoded.Empty()) throw new InvalidDataException("PNG 디코딩에 실패했습니다.");
        using var bgra = new Mat();
        Cv2.CvtColor(decoded, bgra, ColorConversionCodes.BGR2BGRA);
        var width = bgra.Width;
        var height = bgra.Height;
        var stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        for (var row = 0; row < height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Marshal.Copy(bgra.Ptr(row), pixels, row * stride, stride);
        }
        return new ImageFrame(frameId, capturedAt, width, height, stride, pixels);
    }

    internal static void ValidatePng(ReadOnlySpan<byte> png, long maxPixels)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 33 || !png[..8].SequenceEqual(signature) || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("올바른 PNG 헤더가 없습니다. RAW/텍스트 응답은 캡처로 사용하지 않습니다.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(20, 4));
        if (width == 0 || height == 0 || (ulong)width * height > (ulong)maxPixels)
            throw new InvalidDataException("PNG 해상도가 설정된 제한을 초과했습니다.");
    }
}
