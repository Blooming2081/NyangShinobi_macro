namespace NyangShinobi.Domain;

public readonly record struct NormalizedPoint
{
    public NormalizedPoint(double x, double y)
    {
        if (!double.IsFinite(x) || x is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y) || y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(y));
        X = x;
        Y = y;
    }

    public double X { get; }
    public double Y { get; }
}

public readonly record struct NormalizedRegion(NormalizedPoint TopLeft, NormalizedPoint BottomRight);
