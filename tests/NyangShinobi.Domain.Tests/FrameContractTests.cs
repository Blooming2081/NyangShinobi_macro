using NyangShinobi.Domain;

namespace NyangShinobi.Domain.Tests;

public sealed class FrameContractTests
{
    [Fact]
    public void FrameOwnsSnapshotAndKeepsIdentity()
    {
        byte[] pixels = [1, 2, 3, 4];
        var now = DateTimeOffset.UtcNow;
        var frame = new ImageFrame(42, now, 1, 1, 4, pixels);
        pixels[0] = 99;

        Assert.Equal(1, frame.Pixels.Span[0]);
        Assert.Equal(42, frame.FrameId);
        Assert.Equal(now, frame.CapturedAt);
        Assert.Equal(FramePixelFormat.Bgra32, frame.PixelFormat);
    }

    [Theory]
    [InlineData(2, 1, 4, 4)]
    [InlineData(1, 2, 4, 4)]
    [InlineData(0, 1, 4, 4)]
    public void InvalidBufferGeometryIsRejected(int width, int height, int stride, int length) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            new ImageFrame(1, DateTimeOffset.UtcNow, width, height, stride, new byte[length]));

    [Theory]
    [InlineData(-0.01, 0.5)]
    [InlineData(0.5, 1.01)]
    [InlineData(double.NaN, 0.5)]
    [InlineData(0.5, double.PositiveInfinity)]
    public void InvalidNormalizedPointIsRejected(double x, double y) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NormalizedPoint(x, y));
}
