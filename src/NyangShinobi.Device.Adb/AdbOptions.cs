namespace NyangShinobi.Device.Adb;

public sealed record AdbOptions
{
    public string ExecutablePath { get; init; } = "adb.exe";
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan CleanupTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int MaxOutputBytes { get; init; } = 32 * 1024 * 1024;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ExecutablePath);
        if (CommandTimeout <= TimeSpan.Zero || CleanupTimeout <= TimeSpan.Zero || MaxOutputBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(CommandTimeout));
    }
}
