# NyangShinobi.Application

모드 실행, 프레임 스케줄링, 입력 직렬화, 이벤트 우선순위, 취소.

Task 01의 설정/골격 세션을 유지하며 Task 02에서 scoped `FrameScheduler`와 `InputSequencer`를 추가했습니다. 동시 관측의 동일 프레임 공유, 3초 캡처, 입력 완료 후 500ms 검증, 실패/중지 시 입력 차단을 제공합니다. 이벤트 실행과 게임 흐름 연결은 후속 Task 범위입니다.
