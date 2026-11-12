# Task 02 — ADB PNG 캡처·기본 입력·ROI 인식

범위는 [tasks/02_capture_vision.md](../tasks/02_capture_vision.md)입니다. Task 03의 임무·거울 진행 로직은 구현하지 않았습니다. 기존 WPF 시작/중지는 골격 세션만 제어하며 이 문서의 장치 API를 자동 호출하지 않습니다.

후속으로 입력 없는 수동 **개발자 진단** 탭을 추가했습니다. 장치 선택·캡처·에셋 점수 측정·명시적 저장 방법과 최신 검증 결과는 [진단 안내](TASK02_DIAGNOSTICS.md)를 참조하세요. 아래는 Task 02 기반 API 구현 당시의 기록입니다.

## 구현 동작

- `AdbDeviceClient`는 `adb devices -l` 목록을 확인하고 **호출자가 지정한 serial 하나가 `device` 상태일 때만** `-s <serial>` 명령을 전송합니다. 장치가 하나여도 serial을 생략하지 않으며, 여러 장치 중 첫 장치를 고르지 않습니다. 미지정·미등록·중복 serial, offline/unauthorized는 오류로 중단합니다.
- `AdbScreenCapture`는 `exec-out screencap -p`의 stdout을 바이너리로 수신합니다. 같은 client의 캡처는 직렬화하며, 프레임 ID·수신 시각·실제 해상도·stride·BGRA32 버퍼를 반환합니다. PNG 디코딩은 `IImageFrameDecoder`를 통해 Vision 프로젝트가 수행합니다.
- `AdbInputController`는 같은 장치의 정상 캡처로 확인한 해상도로 정규화 탭/스와이프 좌표를 변환합니다. 경계 좌표는 마지막 픽셀에 제한합니다. `shell input tap/swipe` 전송 완료는 `Pending`이며 화면 반영 성공이 아닙니다. 장치 준비 상태를 입력 직전 다시 확인합니다. 회전/해상도 변경 후에는 먼저 새 캡처가 필요합니다.
- `AdbProcessRunner`는 `ArgumentList`, `UseShellExecute=false`, 숨김 CLI 프로세스를 사용합니다. stdout/stderr를 동시에 제한된 버퍼로 읽고, 비정상 종료·취소·타임아웃을 전달합니다. 실패 시 **자신이 시작한 CLI 프로세스만** 종료·회수합니다. ADB 서버/게임/앱플레이어를 종료·재시작하지 않습니다.
- `TemplateRepository`는 최초 로딩 시 manifest의 포함 에셋 크기와 SHA-256을 검사하고 카탈로그의 미등록 경로·중복 경로·assets 밖 경로를 거절합니다. 현재 원본 중 **79개 템플릿**을 캐시합니다. reference/excluded 및 전체 화면 참조는 일반 템플릿 매칭 대상에서 제외합니다. 그 자료 자체는 삭제하지 않습니다.
- `OpenCvVisionEngine`은 명시한 화면/템플릿만 검색합니다. 정규화 ROI를 실제 픽셀 영역으로 변환하고 기준 해상도 480×1320에 맞춰 템플릿 크기를 조정한 뒤 정규화 상관계수 매칭을 수행합니다. 일치율, 정규화 중심, 같은 frameId의 증거와 `Detected / Uncertain / NotFound`를 반환합니다. 이 결과는 **개별 화면 요소의 매칭**이며 임무 성공/거울 완료의 복합 판정이 아닙니다.
- 같은 `ImageFrame`/요청의 검출 결과와 BGR 변환을 재사용합니다. frameId가 같아도 다른 프레임 객체이면 픽셀 캐시를 새로 만듭니다. `ranked_skill` 등 같은 screenId의 여러 원본은 고유한 `TemplateId`(manifest 상대 경로)를 명시해야 합니다. 모호한 후보를 임의 선택하지 않습니다.
- 단색 템플릿과 축소 후 단색이 된 템플릿은 완벽한 일치로 오인하지 않습니다. 일치 기준은 `DetectionRequest.MinimumScore`, 불확실 구간은 `VisionOptions.UncertaintyMargin`으로 지정합니다. 실제 게임 임계값은 이 작업에서 확정하지 않았습니다.

## 프레임·입력 정책

`FrameScheduler`와 `InputSequencer`는 활성 장치 세션의 DI scope 안에서 각각 하나입니다. 최초 요청의 serial과 `CaptureSettings`에 고정하며 실행 중 교체를 거절합니다. 자체 반복 루프나 상시 감시 스레드는 없습니다.

1. 첫 관측은 기본 3초 뒤 캡처합니다. 이후 관측은 직전 캡처 완료부터 3초를 채웁니다. 같은 대기 구간의 동시 요청은 **동일 프레임 객체**를 받습니다.
2. 대기 중 입력이 시작되면 기본 타이머를 깨우고, 입력·검증이 끝날 때까지 다른 캡처를 허용하지 않습니다.
3. 최신 캐시 프레임의 `Detected` 결과와 같은 frameId의 단일 증거가 있어야 그 증거의 중심을 탭합니다. 오래된 프레임, 불확실한 결과, 중복 대기 탭은 거절합니다.
4. 전송 **완료 후** 500ms를 기다려 새 프레임을 촬영하고, 호출자가 명시한 다음 화면을 검증합니다. 다른 화면 ID나 다른 frameId의 증거는 성공이 아닙니다.
5. 불확실/미검출 검증은 `NeedsUserAction`으로 끝내며, 오류/입력 중 취소도 세션을 중단시킵니다. 추가 캡처/입력과 자동 재터치를 차단합니다. 사용자 조치 뒤 새 scope를 만드는 것은 호출자의 책임입니다.

3초/500ms 값은 원본 `data/settings.default.v0.1.json`의 `capture` 설정에서 가져옵니다. RAW는 지원 경로로 노출하지 않으며 `adb_png` 이외의 설정을 거절합니다.

## 사용·수명·취소 조건

- 사용자가 실행·연결해 둔 장치와 ADB 실행 파일이 필요합니다. 기본 실행 파일명은 `adb.exe`이며 App 구성 루트의 `AddNyangShinobi(..., adbOptions: new AdbOptions { ExecutablePath = ... })`로 지정할 수 있습니다. ADB를 다운로드하거나 자동 연결하지 않습니다.
- 장치 serial은 사용자 설정의 `adb.deviceSerial` 또는 명시적인 API 인자로 전달합니다. 원본 기본 설정의 null 값은 그대로이며 자동 선택하지 않습니다.
- 세션을 여는 호출자는 DI scope, 하나의 공통 취소 토큰, 활성 비동기 작업의 종료를 소유해야 합니다. 중지 시 토큰을 취소하고 작업 종료를 기다린 뒤 scope/앱 서비스를 해제합니다. Task 03에서 게임 실행과 연결할 때도 이 조건을 유지해야 합니다.
- `DetectAsync`, `InitializeAsync`, `DecodePngAsync`, ADB 프로세스 API는 작업 스레드를 사용합니다. 동기 `Detect`는 작업 스레드 전용이며 STA/UI에서 호출하면 거절합니다. UI에는 `Mat`를 노출하지 않습니다.
- `ImageFrame`은 관리 메모리 스냅샷을 소유합니다. 외부 네이티브 포인터·풀 반환 책임이 없으며, 소비자는 검사 구간 뒤 참조를 놓습니다. 엔진은 마지막 프레임 하나와 그 결과만 캐시합니다.
- 최초 로딩한 템플릿 Mat는 repository, 마지막 BGR Mat는 engine이 소유하고 `Dispose`에서 해제합니다. 디코딩·ROI·크기 조정·점수 Mat 및 OS 프로세스/스트림/취소 소스는 호출 안에서 해제합니다. 검사와 Dispose는 네이티브 버퍼가 겹쳐 해제되지 않도록 잠금으로 보호합니다.
- 기본 프로필은 ADB 명령 10초, CLI 회수 2초, 이미지 작업/최초 로딩 10초, 출력 스트림당 32MiB, 이미지 16M 픽셀 제한입니다. `AdbOptions`/`VisionOptions`에서 관리합니다.
- OpenCV 실행 중 native 호출은 강제로 중단하지 않습니다. 취소·제한 시간은 호출 전후에 협력적으로 검사하므로 긴 native 호출 동안 반응이 늦을 수 있습니다. 이 지연은 실측해야 합니다. 서버 재시작이나 강제 게임 종료로 해결하지 않습니다.

## 진단

기본 .NET `System.Diagnostics.Metrics`의 `NyangShinobi.Runtime` meter를 제공합니다. 구독자가 없으면 수집 스레드·파일·스크린샷을 만들지 않습니다. `MeterListener` 등으로 구독할 수 있습니다.

| instrument | 단위 / 범위 |
|---|---|
| `capture.duration` | ms, ADB 장치 확인·PNG 전송·디코딩, 캡처 게이트 대기 제외 |
| `vision.detect.duration` | ms, 비동기 인식 호출 전체 대기·변환·초기 로딩·검사·캐시 조회 |
| `vision.match.duration` | ms, 캐시 미스 시 템플릿 조회/초기 로딩 및 ROI 매칭 |

이 instrument의 기록과 값의 전달은 테스트했습니다. 측정 환경/분포/반복 횟수 없는 단발 단위 테스트 시간을 게임 성능 수치로 보고하지 않습니다.

## 생성·수정 파일

| 프로젝트/위치 | 파일 |
|---|---|
| Contracts 추가 | `IImageFrameDecoder.cs`, `RuntimeDiagnostics.cs` |
| Contracts 확장 | `IGameClock.cs`의 취소 가능한 지연, `IVisionEngine.cs`의 비동기 인식 (기존 구현을 위한 기본 메서드 포함) |
| Domain 확장 | `ScreenEvidence.cs`의 선택적 `DetectionRequest.TemplateId` |
| Application 추가 | `FrameScheduler.cs`, `InputSequencer.cs` |
| Application 수정 | `SettingsValidation.cs`의 PNG 방식 제한 |
| Device.Adb 추가 | `AdbOptions.cs`, `IAdbCommandRunner.cs`, `AdbProcessRunner.cs`, `AdbDeviceClient.cs` |
| Device.Adb 구현 | `AdbScreenCapture.cs`, `AdbInputController.cs` |
| Vision.OpenCvSharp 추가 | `VisionOptions.cs`, `PngFrameDecoder.cs`, `TemplateRepository.cs`, `RoiMatcher.cs` |
| Vision.OpenCvSharp 구현 | `OpenCvVisionEngine.cs`, 프로젝트의 OpenCvSharp5.Windows 참조 |
| App 수정 | `DependencyInjection.cs`, 출력에 manifest/catalog/assets를 복사하는 `.csproj`, 상태 안내 문구의 `MainViewModel.cs`/`MainWindow.xaml` |
| Application.Tests 추가 | `FrameSchedulerTests.cs` (수동 시계/가짜 캡처·입력·인식 포함) |
| Integration.Tests 추가 | `AdbTests.cs`, `AdbProcessRunnerTests.cs`, `VisionTests.cs` |
| 기존 테스트 조정 | `CompositionTests.cs`의 미구현 예외 기대를 잘못된 serial/설정 거절로 대체, scoped DI 확인; `WindowLifecycleTests.cs`에 STA 동기 인식 거절 확인 추가 |
| 문서/설정 | 루트 README, 시작/아키텍처 안내, 미해결 항목, 관련 프로젝트 README와 이 문서. 중앙 패키지 버전은 기존 고정값 유지 |

## NuGet와 아키텍처

추가 활성화한 직접 참조는 **OpenCvSharp5.Windows 5.0.0.20261109**이며, Task 01에서 고정한 버전을 유지합니다. [공식 NuGet 패키지](https://www.nuget.org/packages/OpenCvSharp5.Windows/5.0.0.20261109)의 OpenCvSharp5·GdipExtensions·WpfExtensions·runtime.win은 같은 버전으로 전이 복원됩니다. 자체 C++ DLL을 작성하지 않았습니다.

기존 직접 패키지는 Microsoft.Extensions.DependencyInjection/Abstractions **10.0.14**, Microsoft.NET.Test.Sdk **18.11.0**, xunit.v3 **3.2.2**, xunit.runner.visualstudio **3.1.5**로 유지했습니다. 추가 테스트/시계/로깅 패키지는 없습니다.

본체 7개·테스트 4개와 프로젝트 참조 방향은 `data/architecture.v0.1.json` 그대로입니다. Device는 Vision 구현 대신 Contracts의 PNG 디코더를 주입받습니다. Domain/Contracts/Application에는 WPF/OpenCV/ADB 구현 참조가 없습니다. `IGameFlow`와 Mission/Mirror/Neko/DoubleReward/DateChange/Events의 기존 경계·게임 워크플로는 변경하지 않았습니다.

## 검증 결과

2026-11-11 (한국 시간), Windows x64, 로컬 .NET SDK **10.0.404**로 다음을 실제 실행했습니다.

```powershell
.tools/dotnet/dotnet.exe restore NyangShinobi.slnx --configfile NuGet.Config -m:1
.tools/dotnet/dotnet.exe build NyangShinobi.slnx -c Release --no-restore -m:1
.tools/dotnet/dotnet.exe test NyangShinobi.slnx -c Release --no-build --no-restore -m:1 --logger trx --results-directory TestResults/Task02
python tools/validate_spec.py
```

| 검증 | 결과 |
|---|---|
| restore | 모든 프로젝트 복원 성공, exit 0 |
| Release build | **경고 0 / 오류 0**, exit 0 |
| Domain.Tests | 기존 8 통과 |
| Application.Tests | 기존 16 + 신규 10 = **26 통과** |
| Features.Tests | 기존 2 통과 |
| Integration.Tests | 기존 4 + 신규 27 = **31 통과** |
| 전체 | **기존 30 + 신규 37 = 67 통과 / 0 실패 / 0 건너뜀**, exit 0 |
| 명세/에셋 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |
| 아키텍처/보존 | 본체 7개·테스트 4개 및 참조 방향 일치, assets/reference/data/Features 변경 없음 |
| 로컬 문서 링크 | 55개 경로 정상 |

원본 에셋 최초 로딩 79개, PNG 버퍼 소유·헤더/해상도 제한·취소, ROI의 위치/점수/스케일/불확실 구간, 같은 프레임 결과 캐시, 잘못된 에셋/모호한 선택 거절, Dispose 후 사용 거절, 축소 후 단색 템플릿 오검출 방지를 확인했습니다. 가짜 ADB로 0/여러/오프라인/중복 장치 선택 방지, 실제 해상도 좌표, 입력 전/중 취소와 nonzero/비정상 응답을 검증했습니다.

수동 시계 테스트는 실제 3초를 기다리지 않고 2999+1ms 및 입력 완료 후 499+1ms 경계를 검증합니다. 대기 중 입력 선점, 동시 요청 공유, 중복/다른 화면·프레임 검증 실패, 취소 후 추가 입력/캡처 차단도 통과했습니다. 별도의 가짜 CLI 프로세스로 stdout/stderr·출력 제한·타임아웃·취소 후 소유 프로세스 회수를 확인했습니다.

TRX는 Git에서 제외되는 `TestResults/Task02/`에 있습니다. 기존 `VSTEST_ARTIFACTS_DIRECTORY` 미지정 안내는 개인 임시 폴더를 사용한다는 메시지이며 테스트 실패가 아닙니다. 실제 ADB/앱플레이어/게임을 실행하지 않았습니다.

## 실제 앱플레이어에서 남은 검증 / Task 03 전 조건

1. MuMu/BlueStacks별 사용자가 준비한 ADB 경로·버전, 실제 serial과 `device` 상태, `exec-out screencap -p` 지원과 타임아웃 반응을 확인합니다. 현재 테스트는 가짜 ADB 및 가짜 CLI만 사용했습니다.
2. 홈/임무 진입의 실제 캡처 fixture를 확보하고 정답 화면·오검출·불확실 사례에 대한 ROI/일치율·해상도 스케일 프로필을 검증해야 해당 입력을 활성화할 수 있습니다. 일반 템플릿 한 장의 일치가 게임 결과 확정은 아닙니다.
3. 회전/해상도 변경 후 새 캡처로 입력 좌표를 검증합니다. 전체 화면 참조 3개는 별도 검증된 템플릿 crop 프로필이 필요하며 일반 매칭에서 차단된 상태입니다.
4. 캡처/분석 지연의 평균·최악값, native 취소 반응, 연속 실행 메모리·CPU·게임 FPS·정확도를 측정해야 합니다. RAW는 이 비교 전 기본 방식으로 바꾸지 않습니다.
5. Task 03 구현 시 명시적 serial, 활성 장치 scope, 공통 취소 토큰과 종료 대기를 게임 흐름에 연결해야 합니다. 현재 UI는 자동 장치 탐색·캡처·게임 입력을 시작하지 않습니다.

이 항목은 [U-01~U-16 미해결 사항](07_OPEN_QUESTIONS.md) 및 후속 Task의 사전 조건입니다. 실제 게임 성능·정확도·클릭 반영을 검증 완료라고 주장하지 않습니다. 게임/ADB/OS 재시작, 워치독, 자동 복구, 스트리밍, RAW 캡처, 임무/거울 진행은 구현하지 않았습니다.
