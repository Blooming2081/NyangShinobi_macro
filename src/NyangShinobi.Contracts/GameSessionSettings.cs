using NyangShinobi.Domain;

namespace NyangShinobi.Contracts;

// JSON 기본 설정의 필드 이름/구조를 그대로 보존한다. required는 누락을 오류로 만든다.
public sealed record GameSessionSettings
{
    public required int SchemaVersion { get; init; }
    public required string AppVersion { get; init; }
    public required string Platform { get; init; }
    public required string Mode { get; init; }
    public required AdbSettings Adb { get; init; }
    public required MissionSettings Mission { get; init; }
    public required MirrorSettings Mirror { get; init; }
    public required CombatSettings Combat { get; init; }
    public required CaptureSettings Capture { get; init; }
    public required SafetySettings Safety { get; init; }
    public required NekoNinjaSettings NekoNinja { get; init; }
    public required DateChangeSettings DateChange { get; init; }
    public required EventSettings Events { get; init; }

    public GameMode GetGameMode() => Mode switch
    {
        "mission" => GameMode.Mission,
        "mirror" => GameMode.Mirror,
        "neko_ninja" => GameMode.NekoNinja,
        _ => throw new ArgumentException($"지원하지 않는 모드: {Mode}")
    };
}

public sealed record AdbSettings(string? DeviceSerial, bool AutoRecoveryEnabled);
public sealed record MissionSettings(string Rank, string CharacterPreset, PresetNumbers AutoPresetNumbers,
    bool NextStage, bool StopAfterClear, bool SwitchToMirrorAfterFailures, int FailureCountThreshold,
    string OnEnergyEmpty, DoubleRewardSettings DoubleReward);
public sealed record PresetNumbers(int Lower, int Upper, int Extreme);
public sealed record DoubleRewardSettings(bool Enabled, int ReturnTimeoutSeconds);
public sealed record MirrorSettings(string Attribute, int Character, string RepeatMode, bool WeekdayAuto,
    IReadOnlyDictionary<string, WeekdayMirrorSettings> WeekdaySchedule, bool StopAfterClear);
public sealed record WeekdayMirrorSettings(string Attr, int Character);
public sealed record CombatSettings(bool UseBigi, int UpperMissionMaxHp);
public sealed record CaptureSettings(int DefaultIdleIntervalMs, int PostTapVerifyDelayMs,
    string Method, string TransientPolicy);
public sealed record SafetySettings(string OnUnknownScreen, bool AllowGameRestart,
    bool AllowAdbAutoReconnect, bool EnableStallRecovery);
public sealed record NekoNinjaSettings(bool StartRequiresKnownScreen, bool AllowUnknownScreenTaps,
    int MaxTenPullCycles, string Note);
public sealed record DateChangeSettings(bool Enabled, double PopupConfidenceCandidate,
    double FollowupCloseConfidenceCandidate, int HomeStableFrames, int HomeReturnTimeoutSeconds,
    int DailyRolloverHourLocal, string OnUnsupportedMode, string Note);
public sealed record EventSettings(bool Enabled, string Mode, string DefinitionsDirectory,
    string UnrecognizedScreen, bool EnableOnlyValidatedEvents, int DedupeCooldownMs,
    int EventSequenceTimeoutSeconds);
