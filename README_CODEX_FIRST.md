# 냥냥시노비 매크로 v0.1 — Codex 입력 패키지

**버전: v0.1 (신규 개발 / 이전 v3 초안 대체)**  
**원칙: 기능 흐름의 기준은 `NyangShinobiMacro_v2.0.6`이며, `macro_opt`는 이번 범위의 기준이 아니다.**

## 사용 방법
1. 이 ZIP을 **새 Git 저장소의 루트**에 풀고 Codex에게 `AGENTS.md`부터 읽도록 지시한다.
2. **최종 아키텍처 계약** `docs/10_ARCHITECTURE.md` → `docs/11_EVENT_EXTENSION.md` → `docs/12_DATE_CHANGE.md` → `docs/13_CONTRACTS_AND_SETTINGS.md` → `docs/14_ROADMAP_AND_TESTS.md`부터 읽는다.
3. 이후 게임 기능 흐름 `docs/01_SCOPE.md` → `docs/02_FLOW_MISSION.md` → `docs/03_FLOW_MIRROR.md` → `docs/04_SCREEN_ASSETS.md` → `docs/06_TESTS.md`를 읽는다.
4. 두 추가 기능은 `docs/08_NEKO_NINJA.md`, `docs/09_DOUBLE_REWARD.md`, `tasks/07_neko_and_reward.md`를 참고한다.
5. `data/*.json`은 기계 판독용 흐름·에셋 목록·기본 설정이다. 사람이 쓴 문서와 충돌하면 원본 v2.0.6 소스/명시된 결정 사항으로 검증하고 임의 결정하지 않는다.
6. `assets/`에는 v0.1에 필요한 원본 이미지와 검증용 참조 이미지가 **파일명·바이트 변경 없이** 들어 있다.
7. `reference/legacy_v206_methods/`에는 **비실행 읽기 전용** 원본 함수 발췌만 담았다. 복구·감시 코드가 일부 함수 본문에 섞여 있더라도 **이식 금지**.
8. `tasks/`의 작은 작업 단위로 차례대로 구현한다. 기능 완성 기준은 회귀 테스트 + 실제 화면 확인이다.

## 최종 기준
- Windows x64, **C# + WPF + .NET 10 LTS + OpenCvSharp5 + ADB**, 처음부터 C++ DLL 없음.
- `src`의 7개 프로젝트로 경계를 구분하고, 게임 기능을 `Features/Mission|Mirror|NekoNinja|DoubleReward|DateChange|Events` 하위로 독립 관리한다.
- **날짜 변경 팝업 정상 처리와 이후 신규 이벤트 확장**은 포함. 날짜 변경은 게임/ADB 재시작과 다름.
- `data/architecture.v0.1.json`, `data/modules.v0.1.json`, `data/events/` 참조.

## 지금 구현할 것
- 일반 임무: 홈→등급→캐릭터 프리셋→(선택)다음 관문→입장→인법/룰렛/비기→성공·실패→다음 판.
- 음양의 거울: 홈→시련→훈련→속성→캐릭터→입장→카드/비기→다음 층 또는 실패 후 재입장.
- 2.0.6의 **분기 순서와 인식 우선순위 유지**. 불명확한 판정만 명시적 `NeedsUserAction`으로 정지.
- 별도 **네코 닌자 뽑기** 모드(10회 뽑기 → 결과 계속하기 반복)와 임무 성공 후 **선택적 광고 2배 보상**(일반 확인보다 선행).
- 단순 WPF 실행 화면, ADB/캡처, OpenCvSharp, 이미지·설정 로딩. 기능 구현에 직접 필요한 최소 로그만.

## 이번 버전에 구현하지 않을 것
자동 복구/60초 정체 복구/워치독/재시작/Windows 재부팅/ADB 자동 재연결·강제 재시작/메모리 감시/학습데이터 자동 수집/로그 다운로드·통계 대시보드/스킬 순서 GUI 편집기.

> **주의**: 화면 클릭 직후의 전환 검증·제한적 재확인은 '복구'가 아니라 정상 게임 동작의 필수 확인이므로 **제외하지 않는다**. 앱플레이어는 사용자가 이미 실행하고 ADB가 연결된 상태에서 시작하는 것을 v0.1 전제 조건으로 둔다.

### Codex에 처음 전달할 프롬프트

> 이 저장소는 냥냥시노비 매크로 v0.1 신규 개발 프로젝트다. `AGENTS.md`와 `README_CODEX_FIRST.md`, `docs/01_SCOPE.md`를 먼저 읽어라. 기준 동작은 v2.0.6의 **게임 진행 순서**이나 자동 복구·정체 감시·재시작 등 비게임 운영 코드는 금지한다. `data/workflows.v0.1.json`과 `data/screen_catalog.v0.1.json`을 계약 자료로 사용하고, `reference/legacy_v206_methods/`는 읽기 전용의 이전 구현 참고로만 사용하라. 먼저 `docs/10_ARCHITECTURE.md`와 `docs/12_DATE_CHANGE.md`를 읽고 `tasks/01_foundation.md`만 작업하고, 구현 전 변경 파일/검증 방법을 제시하라. 화면 인식 임계값은 검증 없이 확정하지 말고 불명확한 흐름은 이슈로 남겨라.

> **중요**: `docs/05_IMPLEMENTATION.md`에 보이는 4개 프로젝트 최소 구조는 과거 초안이다. 최종 권위는 `docs/10_ARCHITECTURE.md`와 `data/architecture.v0.1.json`에 있다. 날짜 변경은 포함되며 자동 복구/재시작은 제외된다.
