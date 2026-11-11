# Task 08 — 게임 날짜 변경 정상 처리

먼저 `docs/12_DATE_CHANGE.md`, `docs/11_EVENT_EXTENSION.md`, `data/workflows.v0.1.json`의 `globalInterrupts`를 읽는다.

범위: 날짜 변경 팝업, 확인 후 500ms 검증, 04:00 기준 요일 갱신, 알려진 후속 팝업 닫기, 홈 2회 확인, 이전 임무/거울 홈 재진입.
네코 모드: 날짜 변경 후 자동 뽑기 재개 금지, 수동 복귀.
제외: 재실행, ADB 재연결, 정체 복구, 미확인 화면 중앙 탭.

테스트 V01-D01~D06 + 기존 임무·거울 회귀. 이벤트 핸들러 확장 지점 `AfterDateChange`를 발생시키되 아직 신규 이벤트 자체는 구현하지 않는다.
