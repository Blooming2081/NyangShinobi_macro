# Task 09 — 확장 가능한 게임 이벤트 프레임워크

참조: `docs/11_EVENT_EXTENSION.md`, `docs/13_CONTRACTS_AND_SETTINGS.md`, `data/events/event.v0.1.schema.json`.

구현: `IGameEventHandler`, `IEventRegistry`, `EventDispatcher`, phase/priority/modeScope 검사, 하나의 공유 프레임을 사용한 Probe, 예외·취소·타임아웃·fingerprint 중복 방지.
단순 이벤트: 스키마 검증, `tap_detected`/`wait_for_screen`만 허용, 템플릿 없으면 fail closed, `*.disabled.json` 실행 금지.
복합 이벤트: 별도 폴더/클래스 추가와 DI 등록으로 확장. 런타임 DLL 로더 금지.

테스트 V01-E01~E09, V01-ARCH02, V01-ARCH03. Mission/Mirror 코드는 수정하지 않는 것을 수용 조건으로 삼는다.
