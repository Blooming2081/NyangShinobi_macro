using NyangShinobi.Contracts;
using NyangShinobi.Domain;
using System.Diagnostics;
using System.Runtime.InteropServices;
using global::OpenCvSharp;

namespace NyangShinobi.Vision.OpenCvSharp;

public sealed class OpenCvVisionEngine(TemplateRepository templates, VisionOptions options) : IVisionEngine, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<DetectionRequest, DetectionResult> results = [];
    private ImageFrame? cachedFrame;
    private Mat? cachedBgr;
    private bool disposed;

    public DetectionResult Detect(ImageFrame frame, DetectionRequest request)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            throw new InvalidOperationException("STA/UI 스레드에서는 DetectAsync를 사용해야 합니다.");
        using var timeout = new CancellationTokenSource(options.OperationTimeout);
        try { return DetectCore(frame, request, timeout.Token); }
        catch (OperationCanceledException) { throw new TimeoutException("이미지 인식 제한 시간을 초과했습니다."); }
    }

    public async Task<DetectionResult> DetectAsync(ImageFrame frame, DetectionRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.OperationTimeout);
        try
        {
            return await Task.Run(() => DetectCore(frame, request, timeout.Token), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("이미지 인식 제한 시간을 초과했습니다."); }
        finally { RuntimeDiagnostics.DetectionDurationMs.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private DetectionResult DetectCore(ImageFrame frame, DetectionRequest request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            options.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            if ((long)frame.Width * frame.Height > options.MaxPixels) throw new InvalidDataException("프레임 해상도 제한 초과");
            if (!ReferenceEquals(cachedFrame, frame))
            {
                cachedFrame = null;
                cachedBgr?.Dispose();
                cachedBgr = null;
                results.Clear();
                using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
                if (!MemoryMarshal.TryGetArray(frame.Pixels, out var pixels))
                    throw new InvalidOperationException("프레임에 관리 배열 버퍼가 필요합니다.");
                for (var row = 0; row < frame.Height; row++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Marshal.Copy(pixels.Array!, pixels.Offset + row * frame.Stride, bgra.Ptr(row), frame.Width * 4);
                }
                var bgr = new Mat();
                try { Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR); }
                catch { bgr.Dispose(); throw; }
                cachedBgr = bgr;
                cachedFrame = frame;
            }
            if (results.TryGetValue(request, out var cached)) return cached;
            var started = Stopwatch.GetTimestamp();
            try
            {
                var result = templates.Use(request.ScreenId, request.TemplateId,
                    (id, image) => RoiMatcher.Match(cachedBgr!, image, frame, id, request, options, cancellationToken), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(request, result);
                return result;
            }
            finally { RuntimeDiagnostics.MatchDurationMs.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            cachedBgr?.Dispose();
            cachedBgr = null;
            cachedFrame = null;
            results.Clear();
        }
    }
}
