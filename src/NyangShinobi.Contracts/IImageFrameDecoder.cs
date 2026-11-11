using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

// PNG 디코딩의 네이티브 구현을 Device.Adb에 노출하지 않는다.
public interface IImageFrameDecoder
{
    Task<ImageFrame> DecodePngAsync(ReadOnlyMemory<byte> png, long frameId,
        DateTimeOffset capturedAt, CancellationToken cancellationToken);
}
