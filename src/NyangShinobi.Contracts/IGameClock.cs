namespace NyangShinobi.Contracts;

public interface IGameClock
{
    DateTimeOffset LocalNow { get; }
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startTimestamp);
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}
