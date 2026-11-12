using NyangShinobi.Domain;

namespace NyangShinobi.App.Diagnostics;

public enum BenchmarkCaptureFormat { Png, Raw }
public enum BenchmarkSupport { NotVerified, Verified, Unsupported }

public sealed record CaptureBenchmarkSample(BenchmarkCaptureFormat Format, int Round, TimeSpan AdbDuration,
    long TransferBytes, TimeSpan ConversionDuration, TimeSpan TotalDuration, int Width, int Height, int Stride)
{
    public string Method => Format == BenchmarkCaptureFormat.Png ? "PNG" : "RAW";
    public double AdbMs => AdbDuration.TotalMilliseconds;
    public double ConversionMs => ConversionDuration.TotalMilliseconds;
    public double TotalMs => TotalDuration.TotalMilliseconds;
    public string Resolution => $"{Width}×{Height}";
}

public sealed record BenchmarkStatistics(double Mean, double Median, double Minimum, double Maximum, double P95)
{
    public static BenchmarkStatistics From(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0) throw new ArgumentException("통계에는 하나 이상의 표본이 필요합니다.", nameof(source));
        var n = values.Length;
        var median = n % 2 == 0 ? (values[n / 2 - 1] + values[n / 2]) / 2 : values[n / 2];
        return new(values.Average(), median, values[0], values[^1], values[(int)Math.Ceiling(n * 0.95) - 1]);
    }
}

public sealed record BenchmarkSummaryRow(string Method, string Metric, double Mean, double Median,
    double Minimum, double Maximum, double P95);

public sealed record BenchmarkMethodResult(BenchmarkCaptureFormat Format, BenchmarkSupport Support, string? Error,
    string Layout, ImageFrame? LastFrame, IReadOnlyList<CaptureBenchmarkSample> Samples);

public sealed record CaptureBenchmarkResult(string AdbPath, string Serial, int RequestedSamples, int Warmups,
    bool Canceled, IReadOnlyList<BenchmarkMethodResult> Methods, IReadOnlyList<CaptureBenchmarkSample> Samples)
{
    public string Description => $"ADB: {AdbPath} · serial: {Serial}\n" +
        string.Join("\n", Methods.Select(method => $"{method.Format}: " +
            $"{(method.Support == BenchmarkSupport.Verified ? "BGRA32 변환 확인" : method.Support == BenchmarkSupport.Unsupported ? "미지원" : "지원 확인 실패/미측정")} · " +
            $"{method.Samples.Count}/{RequestedSamples}회 · 해상도 " +
            $"{(method.LastFrame is null ? "미확인" : $"{method.LastFrame.Width}×{method.LastFrame.Height}")} " +
            $"(측정: {string.Join(", ", method.Samples.Select(sample => sample.Resolution).Distinct())}) · {method.Layout}" +
            (method.Error is null ? "" : $" · 오류: {method.Error}"))) +
        (Canceled ? "\n취소됨 · 완료된 표본만 표시합니다." : "\n종료 · 실패/미완료 방식은 비교 결론에 사용하지 마세요.") +
        "\n예열은 통계에서 제외 · 단위 ms / byte · P95=nearest-rank · 화면 간 픽셀 일치 비교 없음";

    public IReadOnlyList<BenchmarkSummaryRow> Summary => Methods.SelectMany(method =>
    {
        if (method.Samples.Count == 0) return Array.Empty<BenchmarkSummaryRow>();
        var samples = method.Samples;
        return new[] { Row("ADB 수신 (ms)", samples.Select(s => s.AdbMs)),
            Row("변환/프레임 (ms)", samples.Select(s => s.ConversionMs)),
            Row("전체 (ms)", samples.Select(s => s.TotalMs)), Row("전송 크기 (byte)", samples.Select(s => (double)s.TransferBytes)) };
        BenchmarkSummaryRow Row(string name, IEnumerable<double> values)
        {
            var stats = BenchmarkStatistics.From(values);
            return new(method.Format.ToString(), name, stats.Mean, stats.Median, stats.Minimum, stats.Maximum, stats.P95);
        }
    }).ToArray();
}
