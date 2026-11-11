namespace NyangShinobi.Contracts;

public interface IGameClock
{
    DateTimeOffset LocalNow { get; }
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startTimestamp);
}
