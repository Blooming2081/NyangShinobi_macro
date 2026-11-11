using NyangShinobi.Contracts;
using NyangShinobi.Domain;

namespace NyangShinobi.Application;

/// <summary>
/// 활성 장치 세션 범위의 단일 캡처 소유자. 자체 루프/스레드는 없다.
/// 동시 관측 요청은 한 프레임으로 합치며, 입력·검증은 같은 게이트에서 직렬화한다.
/// </summary>
public sealed class FrameScheduler(IScreenCapture capture, IGameClock clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private TaskCompletionSource changed = NewSignal();
    private ImageFrame? current;
    private string? boundSerial;
    private CaptureSettings? settings;
    private long lastCapture;
    private long version;
    private bool halted;

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ImageFrame> GetNextAsync(string serial, CaptureSettings profile, CancellationToken cancellationToken)
    {
        var observedVersion = Interlocked.Read(ref version);
        while (true)
        {
            Task signal;
            TimeSpan remaining;
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Bind(serial, profile);
                if (current is not null && version != observedVersion) return current;
                remaining = TimeSpan.FromMilliseconds(profile.DefaultIdleIntervalMs) - clock.GetElapsedTime(lastCapture);
                if (remaining <= TimeSpan.Zero)
                {
                    try { return await CaptureAsync(serial, cancellationToken).ConfigureAwait(false); }
                    catch { Halt(); throw; }
                }
                signal = changed.Task;
            }
            finally { gate.Release(); }

            // 기본 대기는 게이트 밖에서 한다. 입력이 시작되면 즉시 깨어 검증 완료를 기다린다.
            using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = clock.DelayAsync(remaining, delayCancellation.Token);
            var winner = await Task.WhenAny(delay, signal).ConfigureAwait(false);
            if (winner == delay) await delay.ConfigureAwait(false);
            else
            {
                delayCancellation.Cancel();
                try { await delay.ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal async Task<InputOutcome> ExecuteInputAsync(string serial, CaptureSettings profile,
        ImageFrame source, DetectionResult evidence, string expectedScreenId, Func<CancellationToken, Task<InputOutcome>> send,
        Func<ImageFrame, CancellationToken, Task<DetectionResult>> verify, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Bind(serial, profile);
            if (!ReferenceEquals(current, source) || evidence.FrameId != source.FrameId ||
                evidence.Certainty != ScreenCertainty.Detected || evidence.Evidence.Count != 1 ||
                evidence.Evidence.Any(item => item.FrameId != source.FrameId || !double.IsFinite(item.Score) || item.Score is <= 0 or > 1))
                throw new InvalidOperationException("최신 동일 프레임의 명확한 단일 입력 근거가 필요합니다.");
            cancellationToken.ThrowIfCancellationRequested();
            Pulse();
            try
            {
                if (await send(cancellationToken).ConfigureAwait(false) != InputOutcome.Pending)
                {
                    Halt();
                    return InputOutcome.NeedsUserAction;
                }
                // 전송 시작 시간이 아니라 전송 완료 이후부터 센다.
                await clock.DelayAsync(TimeSpan.FromMilliseconds(profile.PostTapVerifyDelayMs), cancellationToken).ConfigureAwait(false);
                var fresh = await CaptureAsync(serial, cancellationToken).ConfigureAwait(false);
                var result = await verify(fresh, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.FrameId != fresh.FrameId || result.ScreenId != expectedScreenId || result.Certainty != ScreenCertainty.Detected ||
                    result.Evidence.Count == 0 || result.Evidence.Any(item => item.FrameId != fresh.FrameId ||
                        !double.IsFinite(item.Score) || item.Score is <= 0 or > 1))
                {
                    Halt();
                    return InputOutcome.NeedsUserAction;
                }
                return InputOutcome.Verified;
            }
            catch { Halt(); throw; }
        }
        finally { gate.Release(); }
    }

    private void Bind(string serial, CaptureSettings profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        if (halted) throw new InvalidOperationException("이 장치 세션은 중단되었습니다. 사용자 조치 후 새 세션이 필요합니다.");
        if (profile.DefaultIdleIntervalMs <= 0 || profile.PostTapVerifyDelayMs <= 0 || profile.Method != "adb_png")
            throw new ArgumentException("양수 캡처/검증 시간과 adb_png 방식이 필요합니다.", nameof(profile));
        if (boundSerial is null)
        {
            boundSerial = serial;
            settings = profile;
            lastCapture = clock.GetTimestamp();
        }
        else if (boundSerial != serial || settings != profile)
            throw new InvalidOperationException("활성 세션의 장치/캡처 설정을 바꿀 수 없습니다.");
    }

    private async Task<ImageFrame> CaptureAsync(string serial, CancellationToken cancellationToken)
    {
        var frame = await capture.CaptureAsync(serial, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        current = frame;
        lastCapture = clock.GetTimestamp();
        Interlocked.Increment(ref version);
        Pulse();
        return frame;
    }

    private void Halt() { halted = true; current = null; Pulse(); }
    private void Pulse() { var previous = changed; changed = NewSignal(); previous.TrySetResult(); }
}
