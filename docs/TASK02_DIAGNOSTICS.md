# Task 02 후속 — 개발자용 캡처·인식 진단

Task 03을 구현하기 전 실제 MuMu/BlueStacks에서 수동으로 확인할 수 있는 WPF 도구입니다. 기존 7개 본체·4개 테스트 프로젝트와 Task 02 API를 재사용합니다. 프로젝트·NuGet 패키지를 추가하지 않았습니다.

SDK/Runtime 없는 Windows x64 사용자 PC에서는 [포터블 배포 안내](PORTABLE_DISTRIBUTION.md)에 따라 ZIP 전체를 풀어 실행할 수 있습니다.

## 사용 순서

1. 사용자가 앱플레이어와 게임을 열고, 해당 환경의 ADB 연결을 준비합니다. 도구는 앱플레이어를 띄우거나 자동 연결하지 않습니다.
2. [README](../README.md)의 방법으로 앱을 실행하고 **개발자 진단** 탭을 엽니다. 일반 실행 중이면 먼저 중지합니다.
3. `ADB 경로`에 사용 중인 `adb.exe` 또는 BlueStacks의 `HD-Adb.exe` 등 실제 실행 파일을 입력하거나 **찾기…**로 선택합니다. 기본값은 기존 `AdbOptions.ExecutablePath`입니다. 경로를 바꿔도 자동으로 실행하지 않습니다.
4. **장치 조회**를 누른 뒤 목록에서 serial과 상태를 확인하고 **직접 선택**합니다. 장치가 하나여도 자동 선택하지 않습니다. offline/unauthorized는 캡처할 수 없습니다.
5. **스크린샷**을 눌러 메모리에 한 장을 캡처합니다. 미리보기, serial, 수신 시각, frameId, 실제 해상도, 캡처/ADB·디코딩·전체 시간을 표시합니다. 파일은 생성하지 않습니다.
6. **에셋 목록**을 누른 뒤 현재 캡처 화면에 해당하는 에셋을 직접 선택합니다. manifest 검증을 통과한 79개 개별 템플릿을 제공합니다. 기존 전체 화면 참조는 일반 매칭 대상에서 제외한 상태입니다.
7. 필요하면 정규화 ROI의 왼쪽·위·오른쪽·아래(0~1)를 입력하고 **인식 테스트**를 누릅니다. 처음에는 화면 전체 `0,0,1,1`입니다. 선택한 메모리 프레임을 재사용하며 재캡처하지 않습니다.
8. 일치율(퍼센트/소수), 정규화 중심, 인식 시간과 frameId를 확인합니다. 이 기능은 점수 측정만 수행하며 성공/실패 판정이나 설정 저장을 하지 않습니다.
9. 파일이 필요한 경우에만 **PNG 저장…**을 누르고 파일 대화상자에서 저장 경로를 확인합니다. 취소하면 저장하지 않습니다. 기존 파일은 대화상자의 덮어쓰기 확인을 거칩니다.

장치·ADB 경로가 바뀌거나 장치 목록을 다시 조회하면 이전 캡처·결과를 지웁니다. 에셋·ROI를 바꾸면 이전 측정 결과만 지우고 동일 캡처를 유지합니다. 각 작업은 한 번에 하나만 실행하며, **진단 취소**와 창 닫기는 취소를 요청하고 진행 중 작업의 종료를 기다립니다. 취소한 작업을 자동 재시도하지 않습니다.

## 시간·임계값의 의미

| 표시 | 측정 범위 |
|---|---|
| 캡처/ADB | 전체에서 디코딩 시간을 뺀 값. 장치 준비 확인·PNG 전송·호출 부대 시간 포함 |
| 캡처 전체 | 장치 준비 확인 + PNG 전송 + 디코딩. 미리보기 변환 시간은 제외 |
| 디코딩 | Task 02 PNG 디코더의 작업 스레드 대기 + 디코딩 + 관리 프레임 생성. 캡처 전체에 포함 |
| 인식 | 비동기 엔진 호출 전체. 프레임 변환·템플릿 조회·매칭 또는 캐시 조회가 포함됨 |

에셋 목록은 최초 템플릿 로딩을 수행하므로 이후 인식 시간에는 이미 로딩한 템플릿의 캐시가 반영됩니다. 같은 프레임/요청을 반복 검사하면 기존 결과 캐시를 사용할 수 있습니다. 단발 표시값으로 평균·최악 성능이나 정확도를 확정하지 않습니다.

기존 게임 임계값과 `VisionOptions.UncertaintyMargin`, `data/settings.default.v0.1.json`은 수정하지 않았습니다. `DetectionRequest.MinimumScore=null`인 **진단용 측정 요청**을 추가했으며, 이때 엔진은 점수가 높아도 `Detected`를 반환하지 않습니다. 따라서 기존 입력 검증의 근거로 사용할 수 없습니다. 양수 임계값을 지정한 기존 호출의 판정 동작은 유지됩니다.

## 분리·메모리·입력 제한

- 구현은 `src/NyangShinobi.App/Diagnostics/`에 모았습니다. `CaptureDiagnostics`는 기존 `AdbDeviceClient`, `AdbScreenCapture`, PNG 디코더, 템플릿 저장소와 `IVisionEngine`을 재사용합니다. 입력 컨트롤러·InputSequencer·게임 Flow를 의존성으로 받지 않습니다.
- 진단 ADB 명령은 장치 목록 조회와 선택 serial의 `exec-out screencap -p`뿐입니다. 터치·스와이프·게임 진행·ADB/게임 재시작 명령은 없습니다. 일반 실행과 진단 작업은 UI에서 동시에 시작할 수 없습니다.
- 수동 단발 캡처는 기존 `IScreenCapture` 경로를 사용합니다. `FrameScheduler`/`InputSequencer`의 3초/0.5초 정책은 변경하지 않았습니다. 반복 캡처·워치독·자동 복구·RAW·scrcpy·자체 C++ DLL은 추가하지 않았습니다.
- 메모리 프레임을 진단 VM이 보관하고 인식에 같은 객체를 전달합니다. 미리보기는 작업 스레드에서 한 번 생성해 `Freeze`한 WPF 이미지입니다. 렌더링·에셋/ROI 변경 때 프레임을 다시 만들지 않습니다. WPF 표시용 픽셀 저장소 복사는 한 번 필요합니다.
- PNG 디코딩에는 기존 압축 버퍼의 span을, OpenCV BGR 변환에는 `ImageFrame` 소유 배열을 직접 전달해 불필요한 전체 관리 배열 복사를 줄였습니다. Task 02의 네이티브 Mat 수명과 마지막 프레임 캐시는 유지합니다.
- PNG 인코딩·디스크 쓰기와 ADB/이미지 처리는 작업 스레드에서 수행합니다. UI에는 `Mat`를 노출하지 않습니다. native 호출/PNG 인코딩은 실행 중 강제 중단하지 않으며 종료 시 완료를 기다립니다.
- 사용자 요청 전에는 이미지나 진단 결과를 파일로 기록하지 않습니다. PNG 저장은 마지막 미리보기의 원본 픽셀 해상도를 사용하며, 화면에 축소 표시된 크기로 저장하지 않습니다.

## 생성·수정 파일

| 위치 | 내용 |
|---|---|
| `App/Diagnostics/CaptureDiagnostics.cs` | 기존 ADB/디코더/인식 API 조합, 개별 처리 시간 측정 |
| `App/Diagnostics/DiagnosticImages.cs` | 비동기 미리보기·명시적 PNG 저장 |
| `App/Diagnostics/DiagnosticsViewModel.cs` | 수동 선택, 단일 작업, 취소, 결과/프레임 수명 |
| `App/Diagnostics/DiagnosticsView.xaml(.cs)` | 개발자 진단 UI와 파일 대화상자 |
| `App/DependencyInjection.cs`, `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml` | DI, 탭, 일반 실행과 진단의 동시 실행 차단·종료 대기 |
| `Domain/ScreenEvidence.cs`, `Vision.OpenCvSharp/RoiMatcher.cs` | 임계값 없는 진단 점수 요청 지원 |
| `Vision.OpenCvSharp/TemplateRepository.cs` | 기존 템플릿 메타데이터 목록 제공 |
| `Vision.OpenCvSharp/PngFrameDecoder.cs`, `OpenCvVisionEngine.cs` | 기존 메모리 버퍼 직접 사용 |
| `Integration.Tests/DiagnosticsTests.cs`, `VisionTests.cs`, `WindowLifecycleTests.cs` | 수동 선택·입력 금지·저장/픽셀·취소·점수 전용·WPF 바인딩 검증 |
| README와 관련 안내 | 사용 방법과 실기 미검증 상태 갱신 |

## 검증 및 실기 미검증 항목

2026-11-11 (한국 시간), Windows x64, 로컬 .NET SDK **10.0.404**로 restore/build/test를 실제 실행했습니다.

| 검증 | 결과 |
|---|---|
| restore | 성공, 모든 프로젝트 최신 상태 (exit 0) |
| Release build | 경고 0 / 오류 0 (exit 0) |
| 전체 테스트 | **기존 67 + 신규 11 = 78 통과 / 0 실패 / 0 건너뜀** (exit 0) |
| 프로젝트별 | Domain 8, Application 26, Features 2, Integration 42 |
| 명세/에셋 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |
| 아키텍처/링크 | 7개 본체·4개 테스트와 참조 방향 유지, 로컬 Markdown 링크 59개 정상 |
| 보존 | 원본 assets/reference/data/Features 변경 없음. 기존 3초/0.5초 타이머 테스트 통과 |

장치 1개/여러 개/오프라인에서 자동 선택이 없는지, 명시적 선택 후에만 캡처하는지, 메모리 프레임을 인식에 재사용하는지, 저장 요청 전에는 파일이 없는지 확인했습니다. 명시적 저장 PNG를 다시 디코딩해 원본 미리보기와 픽셀이 같은지도 검증했습니다. 대상 변경·재조회 실패·잘못된 ROI에서 과거 결과를 지우는지, 중복 작업·취소·종료 및 일반 실행과 진단의 동시 동작을 차단하는지도 통과했습니다.

실제 WPF XAML/Dispatcher에서 새 진단 버튼·장치 콤보박스 바인딩과 수동 선택 상태를 검사했으며, 테스트에서는 창을 표시하거나 실제 ADB를 실행하지 않았습니다. 가짜 ADB는 `devices -l`과 선택 serial의 PNG 캡처 외 명령을 거절하도록 구성했습니다. 새 임계값 없는 요청이 높은 점수에서도 `Uncertain`이며 기존 양수 임계값의 판정에 영향을 주지 않는 테스트도 통과했습니다.

TRX 파일은 Git에서 제외하는 `TestResults/Diagnostics/`에 있습니다. 기존 `VSTEST_ARTIFACTS_DIRECTORY` 미지정 안내는 개인 임시 폴더 사용 메시지이며 실패가 아닙니다. 외부 패키지 버전과 프로젝트 수를 바꾸지 않았습니다.

실제 MuMu/BlueStacks에서는 다음을 사용자가 확인해야 합니다.

- 선택한 ADB 실행 파일과 실제 serial의 호환성·권한·offline/unauthorized 처리.
- 현재 화면 PNG의 실제 해상도·색상·회전, 미리보기와 명시적 PNG 저장 결과.
- 실제 장면의 정상/오검출/불확실 ROI와 일치율, 해상도 스케일에 따른 변화.
- 실제 캡처/디코딩/인식 지연의 분포, 취소 응답, 연속 수동 작업의 UI 반응·메모리.

도구 추가와 자동 테스트 통과를 실기 검증 완료로 보고하지 않습니다. Task 03은 구현하지 않았습니다.
