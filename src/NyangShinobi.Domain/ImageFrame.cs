namespace NyangShinobi.Domain;

public enum FramePixelFormat { Bgra32 }

/// <summary>
/// 한 캡처의 관리 메모리 스냅샷. 생성 시 복사하여 버퍼를 소유한다.
/// 풀/네이티브 핸들이 없어 Dispose는 필요 없다. 소비자는 같은 프레임을 공유하고
/// 검사 범위를 벗어나 보관하지 않는다. 향후 풀링은 별도 수명 계약이 필요하다.
/// </summary>
public sealed class ImageFrame
{
    public ImageFrame(long frameId, DateTimeOffset capturedAt, int width, int height,
        int stride, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || stride < checked(width * 4))
            throw new ArgumentOutOfRangeException(nameof(stride));
        if (pixels.Length != checked(stride * height))
            throw new ArgumentException("BGRA32 버퍼 크기가 해상도/stride와 다릅니다.", nameof(pixels));
        FrameId = frameId;
        CapturedAt = capturedAt;
        Width = width;
        Height = height;
        Stride = stride;
        Pixels = pixels.ToArray();
    }

    public long FrameId { get; }
    public DateTimeOffset CapturedAt { get; }
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public FramePixelFormat PixelFormat => FramePixelFormat.Bgra32;
    public ReadOnlyMemory<byte> Pixels { get; }
}
