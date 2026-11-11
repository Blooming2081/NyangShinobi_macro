using Microsoft.Extensions.DependencyInjection;
using NyangShinobi.App.ViewModels;
using NyangShinobi.Application;
using NyangShinobi.Contracts;
using NyangShinobi.Device.Adb;
using NyangShinobi.Features;
using NyangShinobi.Vision.OpenCvSharp;

namespace NyangShinobi.App;

// 구체 어댑터를 아는 유일한 조립 지점. 외부 라이브러리 타입은 UI에 전달하지 않는다.
public static class DependencyInjection
{
    public static IServiceCollection AddNyangShinobi(this IServiceCollection services,
        string defaultSettingsPath, string? userSettingsPath = null)
    {
        services.AddSingleton<ISettingsStore>(new JsonSettingsStore(defaultSettingsPath, userSettingsPath));
        services.AddSingleton<IGameClock, SystemGameClock>();
        services.AddSingleton<IScreenCapture, AdbScreenCapture>();
        services.AddSingleton<IInputController, AdbInputController>();
        services.AddSingleton<IVisionEngine, OpenCvVisionEngine>();
        services.AddSingleton<IEventRegistry, EventRegistry>();
        services.AddSingleton<MacroSession>();
        services.AddFeatureModules();
        // 이벤트 모듈은 여기에서 AddGameEventHandler<T>()로 선택 등록한다.
        // 날짜 변경/일반 이벤트 실행 및 JSON 로딩은 Task 08/09에서 연결한다.
        services.AddSingleton<MainViewModel>();
        return services;
    }
}
