using NyangShinobi.Domain;
using global::OpenCvSharp;

namespace NyangShinobi.Vision.OpenCvSharp;

internal static class RoiMatcher
{
    public static DetectionResult Match(Mat frame, Mat template, ImageFrame source, string templateId,
        DetectionRequest request, VisionOptions options, CancellationToken cancellationToken)
    {
        if (request.MinimumScore is { } minimum && (!double.IsFinite(minimum) || minimum is <= 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(request));
        var region = request.Region;
        if (region.TopLeft.X >= region.BottomRight.X || region.TopLeft.Y >= region.BottomRight.Y)
            throw new ArgumentException("ROI는 양의 면적이어야 합니다.", nameof(request));
        int left = (int)Math.Floor(region.TopLeft.X * frame.Width), top = (int)Math.Floor(region.TopLeft.Y * frame.Height);
        int right = (int)Math.Ceiling(region.BottomRight.X * frame.Width), bottom = (int)Math.Ceiling(region.BottomRight.Y * frame.Height);
        using var roi = new Mat(frame, new Rect(left, top, right - left, bottom - top));
        using var scaled = new Mat();
        var size = new Size(Math.Max(1, (int)Math.Round(template.Width * (double)frame.Width / options.ReferenceWidth)),
            Math.Max(1, (int)Math.Round(template.Height * (double)frame.Height / options.ReferenceHeight)));
        if (size.Width > roi.Width || size.Height > roi.Height || size.Width < 2 || size.Height < 2)
            return new(source.FrameId, request.ScreenId, ScreenCertainty.Uncertain, []);
        Cv2.Resize(template, scaled, size, 0, 0, InterpolationFlags.Linear);
        Cv2.MeanStdDev(scaled, out _, out var deviation);
        if (deviation.Val0 + deviation.Val1 + deviation.Val2 <= double.Epsilon)
            return new(source.FrameId, request.ScreenId, ScreenCertainty.Uncertain, []);
        cancellationToken.ThrowIfCancellationRequested();
        using var scores = new Mat();
        Cv2.MatchTemplate(roi, scaled, scores, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(scores, out _, out var maximum, out _, out var location);
        cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(maximum)) return new(source.FrameId, request.ScreenId, ScreenCertainty.Uncertain, []);
        var score = Math.Clamp(maximum, 0, 1);
        var certainty = request.MinimumScore is not { } threshold ? ScreenCertainty.Uncertain :
            score >= threshold ? ScreenCertainty.Detected :
            score >= threshold - options.UncertaintyMargin ? ScreenCertainty.Uncertain : ScreenCertainty.NotFound;
        var center = new NormalizedPoint((left + location.X + scaled.Width / 2.0) / frame.Width,
            (top + location.Y + scaled.Height / 2.0) / frame.Height);
        return new(source.FrameId, request.ScreenId, certainty, [new(source.FrameId, templateId, score, center)]);
    }
}
