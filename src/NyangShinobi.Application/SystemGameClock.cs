using System.Diagnostics;
using NyangShinobi.Contracts;

namespace NyangShinobi.Application;

public sealed class SystemGameClock : IGameClock
{
    public DateTimeOffset LocalNow => DateTimeOffset.Now;
    public long GetTimestamp() => Stopwatch.GetTimestamp();
    public TimeSpan GetElapsedTime(long startTimestamp) => Stopwatch.GetElapsedTime(startTimestamp);
}
