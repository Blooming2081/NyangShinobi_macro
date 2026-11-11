namespace NyangShinobi.Vision.OpenCvSharp;

public sealed record VisionOptions
{
    public int ReferenceWidth { get; init; } = 480;
    public int ReferenceHeight { get; init; } = 1320;
    public long MaxPixels { get; init; } = 16 * 1024 * 1024;
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public double UncertaintyMargin { get; init; } = 0.05;

    internal void Validate()
    {
        if (ReferenceWidth <= 0 || ReferenceHeight <= 0 || MaxPixels <= 0 || OperationTimeout <= TimeSpan.Zero ||
            !double.IsFinite(UncertaintyMargin) || UncertaintyMargin is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ReferenceWidth));
    }
}
