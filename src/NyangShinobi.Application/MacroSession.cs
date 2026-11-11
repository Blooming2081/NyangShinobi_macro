using NyangShinobi.Domain;

namespace NyangShinobi.Application;

/// <summary>
/// 시작/중지 UI용 단일 세션 골격. 캡처/입력/게임 흐름을 실행하지 않는다.
/// Task 02 이후에 실제 오케스트레이션을 연결한다. 대기는 취소 신호만 기다리며
/// 폴링/워치독/백그라운드 스레드를 생성하지 않는다.
/// </summary>
public sealed class MacroSession
{
    private readonly object gate = new();
    private CancellationTokenSource? activeCancellation;
    private GameMode? activeMode;

    public GameMode? ActiveMode { get { lock (gate) return activeMode; } }
    public bool IsRunning { get { lock (gate) return activeCancellation is not null; } }

    public async Task<FlowOutcome> RunAsync(GameMode mode, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource cancellation;
        lock (gate)
        {
            if (activeCancellation is not null)
                throw new InvalidOperationException("이미 실행 중인 세션이 있습니다.");
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            activeCancellation = cancellation;
            activeMode = mode;
        }

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).ConfigureAwait(false);
            return FlowOutcome.Stopped;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return FlowOutcome.Stopped;
        }
        finally
        {
            lock (gate)
            {
                activeCancellation = null;
                activeMode = null;
                cancellation.Dispose();
            }
        }
    }

    public void Stop()
    {
        lock (gate) activeCancellation?.Cancel();
    }
}
