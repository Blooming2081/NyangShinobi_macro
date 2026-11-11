namespace NyangShinobi.Domain;

// 기본값도 성공 판정이 되지 않도록 Uncertain을 첫 값으로 둔다.
public enum ScreenCertainty { Uncertain, Detected, NotFound }
public sealed record ScreenEvidence(long FrameId, string TemplateId, double Score, NormalizedPoint Center);
// MinimumScore=null은 진단용 점수 측정이다. Detected를 반환하지 않아 입력 근거로 쓸 수 없다.
public sealed record DetectionRequest(string ScreenId, NormalizedRegion Region, double? MinimumScore,
    string? TemplateId = null);
public sealed record DetectionResult(long FrameId, string ScreenId, ScreenCertainty Certainty,
    IReadOnlyList<ScreenEvidence> Evidence);
