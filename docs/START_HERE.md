# 냥냥시노비 v0.1 — 개발·Codex 시작 안내

기존 루트의 `START_HERE.md`와 `README_CODEX_FIRST.md`를 통합한 안내입니다. 명령과 백틱 안의 저장소 경로는 **저장소 루트 기준**이며, Markdown 링크는 이 문서의 위치를 기준으로 연결합니다.

현재 **Task 01 골격 + Task 02 캡처·입력·인식 기반**이 구현되었습니다. 본체 7개 프로젝트와 테스트 4개를 유지합니다. ADB PNG 캡처, 명시적 serial 입력, OpenCvSharp ROI 매칭과 공통 프레임 정책은 API로 제공하며, UI 시작 버튼은 여전히 게임 입력 없는 골격 세션만 제어합니다. 빌드·실행 방법은 [루트 README](../README.md), 구현과 검증 내역은 [Task 01 결과](TASK01_FOUNDATION.md)와 [Task 02 결과](TASK02_CAPTURE_VISION.md)를 참조하세요. 실제 게임 진행과 실기 성능·정확도 검증은 아직 수행하지 않았습니다.

WPF **개발자 진단** 탭에 입력 없는 수동 장치 조회·PNG 캡처·에셋/ROI 일치율 측정·명시적 PNG 저장을 추가했습니다. [진단 안내](TASK02_DIAGNOSTICS.md)를 따라 실제 장치에서 검증할 수 있습니다. 임계값을 바꾸거나 Task 03을 실행하지 않습니다.

Windows x64 자체 포함 ZIP 생성과 새 PC 실행은 [포터블 배포 안내](PORTABLE_DISTRIBUTION.md)를 따릅니다. SDK는 개발 PC에만 필요하며 배포 폴더에 .NET 런타임·WPF·OpenCV·에셋·기본 JSON을 포함합니다.

## 먼저 읽을 순서

1. [AGENTS.md](../AGENTS.md)
2. [최종 아키텍처](10_ARCHITECTURE.md) / [기계 판독 아키텍처](../data/architecture.v0.1.json)
3. [범위](01_SCOPE.md) / [일반 임무](02_FLOW_MISSION.md) / [거울](03_FLOW_MIRROR.md)
4. [네코 닌자](08_NEKO_NINJA.md) / [광고 2배 보상](09_DOUBLE_REWARD.md) / [날짜 변경](12_DATE_CHANGE.md)
5. [이벤트 확장](11_EVENT_EXTENSION.md) / [계약과 설정](13_CONTRACTS_AND_SETTINGS.md)
6. [화면 에셋](04_SCREEN_ASSETS.md) / [화면 카탈로그](../data/screen_catalog.v0.1.json) / [워크플로](../data/workflows.v0.1.json) / [모듈](../data/modules.v0.1.json)
7. [수락 테스트](06_TESTS.md) / [로드맵](14_ROADMAP_AND_TESTS.md) / [설계 결정](15_ARCHITECTURE_DECISIONS.md) / [이벤트 체크리스트](16_EVENT_IMPLEMENTATION_CHECKLIST.md)
8. 사용자가 지정한 [tasks/](../tasks/)의 작업만 진행합니다. Task 02 완료만으로 Task 03을 자동으로 시작하지 않습니다.

[docs/05_IMPLEMENTATION.md](05_IMPLEMENTATION.md)의 4개 프로젝트 구조는 과거 초안입니다. 최종 권위는 `docs/10_ARCHITECTURE.md`와 `data/architecture.v0.1.json`입니다. 이전 `macro_opt`나 v3 초안은 기능 기준이 아닙니다.

## 보존할 설계와 범위

- Windows x64 · C# · WPF · .NET 10 LTS · OpenCvSharp5 · ADB를 사용합니다. 병목 입증 전 별도 C++ DLL을 추가하지 않습니다.
- 본체는 7개 프로젝트로 구분하며, 기능은 `src/NyangShinobi.Features/{Mission,Mirror,NekoNinja,DoubleReward,DateChange,Events}`에서 독립 관리합니다.
- 게임 진행의 기준은 v2.0.6의 임무/거울 전환 순서·분기·인식 우선순위입니다. 불명확한 판정은 `NeedsUserAction`으로 멈춥니다.
- 전체 v0.1 설계에는 별도 네코 10회 뽑기 모드, 성공 확정 후 일반 확인보다 먼저 처리하는 선택적 광고 2배 보상, 날짜 변경/일일 팝업/홈 재진입, 신규 이벤트 확장을 포함합니다. 이 기능들이 Task 01에서 구현되었다는 뜻은 아닙니다.
- 네코는 사용자가 명시적으로 선택하고 지원 화면을 열어둔 상태에서만 수행하며 재화를 소비합니다. 광고 전환 뒤에는 기존 확인 좌표를 재사용하지 않습니다.
- 화면 클릭 직후 전환 검증·제한적 재확인은 정상 게임 진행입니다. 기본 3초 검사, 입력 완료 후 0.5초 첫 검증, 상태별 한시적 관찰 예외를 따릅니다. 검증 실패만으로 재터치하지 않습니다.
- 같은 프레임에서 근거를 확보합니다. `mission_clear` 단독 성공 판정과 `image6.PNG` 단독 거울 완료 판정은 금지합니다.
- 정체 복구, 워치독, 모니터링 백그라운드 스레드, 앱플레이어/게임/ADB/OS 재시작 및 자동 재연결은 제외합니다. 메모리 감시, 학습데이터 자동 수집, 로그 다운로드·통계 대시보드, 스킬 순서 GUI 편집기도 전체 v0.1 범위 밖입니다.
- 향후 실제 게임 실행은 사용자가 앱플레이어를 실행하고 ADB를 연결한 상태를 전제로 합니다. 새 앱플레이어 실행/종료와 공유 ADB `kill-server`는 금지합니다.

## 자료와 도구의 역할

- [data/](../data/)는 기계 판독용 흐름·에셋·모듈·기본 설정입니다. 문서와 충돌하면 명시된 결정과 원본 v2.0.6 근거를 확인하며 임의로 결정하지 않습니다.
- [assets/](../assets/)의 원본 파일명과 바이트를 보존합니다.
- [reference/legacy_v206_methods/](../reference/legacy_v206_methods/)는 비실행·읽기 전용 원본 함수 발췌입니다. 복사/이식하지 않고 입출력·분기·검증 조건을 새 C# 단위로 재구현합니다. 섞여 있는 복구·감시 코드는 이식하지 않습니다.
- 단순 이벤트는 검증된 JSON + PNG, 복합 이벤트는 C# handler + DI 등록으로 확장합니다. [예시 이벤트](../data/events/example_limited_time_popup.disabled.json)는 이미지가 없는 미검증 자료이며 비활성을 유지합니다.
- [tools/validate_spec.py](../tools/validate_spec.py)는 Git으로 관리하는 명세·에셋 검증 도구입니다. 게임 매크로 구현이 아닙니다.
- `.tools/`는 Git에서 제외되는 로컬 SDK·CLI·NuGet 캐시입니다. `tools/`와 별개입니다. `TestResults/`, 각 프로젝트 `bin/`·`obj/`도 생성물입니다.
- [변경 이력](changelog/)은 설계 자료 작성 당시의 기록입니다. 현재 구현 상태는 루트 README와 Task 01 결과를 확인합니다.

## Codex 작업 요청 기준

`AGENTS.md`와 이 안내를 먼저 읽고, **사용자가 이번에 지정한 범위만** 작업합니다. 기존 설계·참조 방향·에셋을 보존하고 변경 파일과 검증 방법을 확인합니다. 작업 후 restore/build/test와 명세 검증을 실제 실행하고 결과를 보고합니다. 실기 검증 없이 게임 동작을 확인했다고 주장하지 않습니다.

최초 Task 01 요청은 [tasks/00_first_codex_message.txt](../tasks/00_first_codex_message.txt)에 남겨 두었습니다. 후속 작업에서 이전 프롬프트를 새 기능 구현 허가로 해석하지 않습니다. 범위 변경은 [설계 결정](15_ARCHITECTURE_DECISIONS.md)에, 불명확한 사항은 [미해결 항목](07_OPEN_QUESTIONS.md)에 기록합니다.
