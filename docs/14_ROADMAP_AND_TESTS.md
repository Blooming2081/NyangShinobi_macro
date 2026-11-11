# Codex 구현 로드맵 · 회귀 테스트 · CI 품질 게이트

## 명세의 권위 순서

1. 사용자 명시 요구(v0.1, C#, 복구 제외, v2.0.6 게임 순서, 날짜 팝업+이벤트 확장)
2. `AGENTS.md`, 이 최종 문서 세트
3. `data/workflows.v0.1.json`, `data/screen_catalog.v0.1.json`, `data/modules.v0.1.json`
4. `reference/legacy_v206_methods/`의 해당 함수 (읽기 전용 참고)
5. 이전 v3 임시 명세 및 macro_opt는 **계약 자료가 아님**

## PR/작업 단위 (순서대로)

| 순서 | 작업 파일 | 산출물·완료 기준 |
|---|---|---|
| 01 | `tasks/01_foundation.md` | solution 7 프로젝트+테스트, WPF 실행/중지, DI 빌드 |
| 02 | `tasks/02_capture_vision.md` | ADB 캡처(PNG), ROI/템플릿 인식, 가짜 프레임 테스트 |
| 03 | `tasks/03_mission_navigation.md` | 임무·등급·프리셋·관문 진입/전환 |
| 04 | `tasks/04_mission_battle.md` | 인법·룰렛·성공/실패·복귀 |
| 05 | `tasks/05_mirror.md` | 속성·캐릭터·거울 카드/다음층/실패 |
| 06 | `tasks/06_advanced_gameplay.md` | 비기·특수 타이밍·거울 임의반복, 테스트 전제 |
| 07 | `tasks/07_neko_and_reward.md` | 네코 별도 모드, 성공 후 광고 2배 |
| 08 | `tasks/08_date_change.md` | 04:00/날짜 팝업/일일 팝업/홈 재진입 |
| 09 | `tasks/09_event_extension.md` | JSON 이벤트 확장 + C# 핸들러 DI, 우선순위·중복 방지 |
| 10 | `tasks/10_quality_and_ci.md` | 아키텍처 참조 방향 검사, 테스트, 릴리스 기본 CI |

각 작업은 변경한 모듈의 자동 테스트만 추가하되 모든 기존 테스트를 재실행한다.
**복구/감시/재시작/자동 reconnect 기능 추가는 별도 범위 승인 없이는 금지.**

## 신규 핵심 테스트

| ID | 시나리오 | 기대 결과 |
|---|---|---|
| V01-D01 | 날짜 변경 팝업 존재 | D01~D02 우선 처리, 기존 게임 터치 없음 |
| V01-D02 | 03:59 → 04:00 경계 | 올바른 게임 날짜/요일 변경 |
| V01-D03 | 요일 자동 OFF | 거울 속성/캐릭터 설정 유지 |
| V01-D04 | 일일 후속 팝업 | 지정 템플릿 일치 시에만 닫음 |
| V01-D05 | 날짜 후 이벤트가 홈을 가림 | 먼저 이벤트를 종료하고 홈 2프레임 확인 뒤 이전 모드/설정으로 재진입 |
| V01-D06 | 60초 홈 미확인 | `NeedsUserAction`, 재시작·무작위 클릭 없음 |
| V01-E01 | 미등록/비활성 이벤트 | 실행되지 않고 정상 게임 흐름 유지 |
| V01-E02 | `AfterDateChange` 이벤트 | 날짜 확인 후 홈 안정화 이전 이벤트 실행, 완료 뒤 임무/거울 재개 |
| V01-E03 | 이벤트 중복 노출 | 같은 팝업에 입력 1회만 |
| V01-E04 | 이벤트와 임무 화면 동시 검출 | 허용 모드/phase/우선순위대로 한 동작만 |
| V01-E05 | 미확인 신규 이벤트 | 기존 화면 좌표 재사용 금지, 사용자 조치 요청 |
| V01-E06 | 템플릿 없는 이벤트 JSON | 스키마/에셋 검증 오류, 비활성 상태 유지 |
| V01-E07 | 복합 C# 이벤트 추가/삭제 | MissionFlow/MirrorFlow 수정 없이 DI 등록·해제로 교체 가능 |
| V01-E08 | JSON 금지 액션 | shell/임의 좌표/무한 반복 명령은 로드 단계 거절 |
| V01-E09 | 이벤트 도중 사용자 중지 | 새 ADB 입력 0건 |
| V01-ARCH01 | 의존성 경계 검사 | Domain/Contracts/Application에 WPF/OpenCV/adb 구현 참조 없음 |
| V01-ARCH02 | 단일 프레임 공유 | 날짜·이벤트·모드가 프레임을 재촬영하지 않음 |
| V01-ARCH03 | 기존 기능 회귀 | M/R/N/A 테스트 변경 없이 계속 통과 |

## CI 설계

- Windows GitHub Actions runner에서 .NET 10 SDK 설치 → restore → build -c Release → test.
- `python tools/validate_spec.py`로 명세 JSON, 에셋 SHA, 모듈/이벤트 교차 참조 검사.
- 종속성 버전은 `Directory.Packages.props`에 고정, 일관된 patch 업그레이드 PR로만 변경.
- 이미지 fixture를 사용한 오검출 회귀 테스트는 fixture 확보 후 확장; 실제 MuMu/BlueStacks 엔드투엔드 테스트는 수동 별도 항목.
- 공개 Git 저장소라면 원본 게임 리소스 배포 권리를 검토하고, 민감한 ADB 설정·스크린샷 및 개인 데이터가 실수로 커밋되지 않도록 한다.

## 완료 정의(DoD)

- 사용자 설정으로 각 기능 ON/OFF 가능, 비활성 모듈은 런타임에서 검사하지 않음.
- 새 날짜/이벤트 기능이 기존 임무/거울/광고/네코 기능을 변경하지 않음.
- 입력 명령은 항상 상태·근거·예상 다음 상태와 연결되어 검증 가능.
- C# 인터페이스와 실제 종속성 방향은 `data/architecture.v0.1.json`을 만족.
- `NeedsUserAction`에서 추가 입력 0건. 각 상태에 취소 동작 적용.
- 실기 검증하지 않은 요소는 테스트 성공을 주장하지 않으며 이슈와 함께 명시.
