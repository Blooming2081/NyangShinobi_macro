using System.Buffers.Binary;
using System.IO;
using NyangShinobi.Domain;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.App.Diagnostics;

public sealed record RawBenchmarkFrame(ImageFrame Frame, int HeaderBytes, uint Format, uint? ColorSpace);

/// <summary>AOSP screencap의 packed RAW만 지원하는 개발자 벤치마크 전용 변환기.</summary>
public sealed class RawBenchmarkDecoder(VisionOptions options)
{
    public async Task<RawBenchmarkFrame> DecodeAsync(ReadOnlyMemory<byte> data, long id, DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.OperationTimeout);
        try
        {
            return await Task.Run(() => Decode(data.Span, id, capturedAt, timeout.Token), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("RAW 픽셀 변환 제한 시간을 초과했습니다."); }
    }

    private RawBenchmarkFrame Decode(ReadOnlySpan<byte> data, long id, DateTimeOffset capturedAt, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (data.Length < 12) throw new InvalidDataException("RAW 헤더가 12바이트보다 짧습니다.");
        var width = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var height = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        var format = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue ||
            (ulong)width * height > (ulong)options.MaxPixels)
            throw new InvalidDataException($"RAW 해상도가 유효하지 않거나 제한을 초과했습니다: {width}×{height}");
        if (format is not (1 or 2 or 5))
            throw new NotSupportedException($"RAW 픽셀 형식 {format} 미지원 (RGBA=1, RGBX=2, BGRA=5만 지원).");

        // AOSP는 GPU stride를 헤더에 기록하지 않고 각 행의 width*Bpp만 전송한다.
        // 임의의 패딩/stride를 추정하지 않는다. 12/16-byte layout은 정확한 총 길이로 구분한다.
        var stride64 = (ulong)width * 4;
        var pixels64 = stride64 * height;
        if (stride64 > int.MaxValue || pixels64 > int.MaxValue - 16)
            throw new InvalidDataException("RAW stride/픽셀 버퍼가 지원 범위를 초과했습니다.");
        var header = data.Length - (long)pixels64;
        if (header is not (12 or 16))
            throw new InvalidDataException($"RAW 길이/stride 불일치: bytes={data.Length}, packedStride={stride64}, height={height}; 패딩/독자 헤더는 지원하지 않습니다.");
        uint? colorSpace = header == 16 ? BinaryPrimitives.ReadUInt32LittleEndian(data[12..]) : null;
        if (colorSpace is > 2) throw new NotSupportedException($"RAW 색공간 코드 {colorSpace} 미지원.");
        var source = data[(int)header..];
        var stride = (int)stride64;
        var pixels = new byte[(int)pixels64];
        for (var row = 0; row < (int)height; row++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = row * stride; x < (row + 1) * stride; x += 4)
            {
                pixels[x] = source[x + (format == 5 ? 0 : 2)];
                pixels[x + 1] = source[x + 1];
                pixels[x + 2] = source[x + (format == 5 ? 2 : 0)];
                pixels[x + 3] = format == 2 ? (byte)255 : source[x + 3];
            }
        }
        token.ThrowIfCancellationRequested();
        return new(new ImageFrame(id, capturedAt, (int)width, (int)height, stride, pixels), (int)header, format, colorSpace);
    }
}
