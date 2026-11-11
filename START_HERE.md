# 냥냥시노비 매크로 v0.1 — Codex 전달용 최종 자료

> **이 ZIP은 구현 자료/에셋/명세서이며 실행 가능한 완성 앱은 아니다.**

## 최종 설계 확정

Windows x64 · C# · .NET 10 LTS · WPF · OpenCvSharp5(네이티브 OpenCV 래퍼) · ADB. 처음부터 C++ DLL/영상 스트리밍을 도입하지 않는다.

**게임 기능**: v2.0.6 흐름의 임무/등급/프리셋/인법/룰렛/비기/결과, 거울(속성/캐릭터/카드/다음층/실패), 별도 네코 닌자 10회 뽑기 모드, 임무 성공 광고 2배 보상, 날짜 변경 팝업/일일 팝업/홈 복귀 및 요일 설정, 신규 이벤트 추가 구조.

**제외**: 60초 정체 감지·복구, 워치독, 앱플레이어·게임·ADB·OS 재시작/자동 재연결, 무의미한 백그라운드 감시와 자동 스크린샷 수집.

## Codex에게 반드시 읽힐 순서

1. `AGENTS.md`
2. `docs/10_ARCHITECTURE.md` / `data/architecture.v0.1.json`
3. `docs/01_SCOPE.md` / `docs/02_FLOW_MISSION.md` / `docs/03_FLOW_MIRROR.md`
4. `docs/08_NEKO_NINJA.md` / `docs/09_DOUBLE_REWARD.md` / `docs/12_DATE_CHANGE.md`
5. `docs/11_EVENT_EXTENSION.md` / `docs/13_CONTRACTS_AND_SETTINGS.md`
6. `docs/04_SCREEN_ASSETS.md` / `data/screen_catalog.v0.1.json` / `data/workflows.v0.1.json` / `data/modules.v0.1.json`
7. `docs/14_ROADMAP_AND_TESTS.md` / `docs/15_ARCHITECTURE_DECISIONS.md` / `docs/16_EVENT_IMPLEMENTATION_CHECKLIST.md`
8. `tasks/01_foundation.md`부터 순서대로 구현, 기존 `reference/`는 참고만

## 실제 src/와 tests/에 대해

프로젝트별 폴더와 책임 README는 실제로 만들어 두었다. **실행 가능한 .csproj·C# 앱 코드는 아직 생성되지 않았다.** Codex가 Task 01에서 실제 솔루션과 구현 골격을 만들도록 의도적으로 분리했다.

## 개발 시작 명령

`python tools/validate_spec.py` — 명세 JSON/에셋/모듈 구조 점검 (Python 매크로 구현이 아님; 개발용 검증 도구)

`.NET SDK`는 자료 작성 컨테이너에 설치되어 있지 않으므로 .NET 컴파일/실기 테스트는 수행하지 않았다. Codex가 Windows 개발 환경에서 실제 `dotnet build`, `dotnet test`를 실행해야 한다.

## 중요한 세부 규칙

- 이벤트는 `data/events/example_limited_time_popup.disabled.json`과 `assets/events/`를 예시로 추가한다. **예시 이미지는 없음·실행 금지**.
- 날짜 변경 팝업을 포함해야 하지만 이것은 **자동 복구**가 아니라 정상 게임 UI 처리다.
- 기본 3초 폴링, 탭 완료 후 0.5초 검증. 룰렛/비기 예외는 상태별 제한 캡처.
- `mission_clear` 단독 성공 판정 금지. `image6.PNG` 단독 거울 완료 판정 금지.
- v0.1 범위 변경은 `docs/15_ARCHITECTURE_DECISIONS.md`에 남긴다.
