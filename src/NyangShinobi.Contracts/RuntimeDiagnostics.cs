using System.Diagnostics.Metrics;

namespace NyangShinobi.Contracts;

// 구독자가 없으면 수집/저장 스레드가 생기지 않는다. 스크린샷은 기록하지 않는다.
public static class RuntimeDiagnostics
{
    public const string MeterName = "NyangShinobi.Runtime";
    private static readonly Meter Meter = new(MeterName, "0.1.0");
    public static Histogram<double> CaptureDurationMs { get; } = Meter.CreateHistogram<double>("capture.duration", "ms");
    public static Histogram<double> MatchDurationMs { get; } = Meter.CreateHistogram<double>("vision.match.duration", "ms");
    public static Histogram<double> DetectionDurationMs { get; } = Meter.CreateHistogram<double>("vision.detect.duration", "ms");
}
