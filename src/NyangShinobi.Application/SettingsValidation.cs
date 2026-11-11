using System.IO;
using NyangShinobi.Contracts;

namespace NyangShinobi.Application;

public static class SettingsValidation
{
    public static void Validate(GameSessionSettings settings)
    {
        if (settings.SchemaVersion != 1 || settings.AppVersion != "0.1.0" || settings.Platform != "windows-x64")
            throw new InvalidDataException("v0.1 Windows x64 설정만 지원합니다.");
        _ = settings.GetGameMode();
        if (settings.Adb.AutoRecoveryEnabled || settings.Safety.AllowGameRestart ||
            settings.Safety.AllowAdbAutoReconnect || settings.Safety.EnableStallRecovery)
            throw new InvalidDataException("v0.1은 복구/재시작/자동 재연결을 허용하지 않습니다.");
        if (settings.Safety.OnUnknownScreen != "needs_user_action" ||
            settings.NekoNinja.AllowUnknownScreenTaps || !settings.NekoNinja.StartRequiresKnownScreen)
            throw new InvalidDataException("알 수 없는 화면에서 입력하는 설정은 허용하지 않습니다.");
        if (settings.Capture.DefaultIdleIntervalMs <= 0 || settings.Capture.PostTapVerifyDelayMs <= 0 ||
            settings.NekoNinja.MaxTenPullCycles < 0)
            throw new InvalidDataException("검사 시간은 양수, 뽑기 횟수는 0 이상이어야 합니다.");
    }
}
