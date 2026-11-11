# Task 02 — ADB 단발 캡처 · 화면 인식

목표: `IScreenCapture` + `IInputController`, `IVisionEngine`, 에셋 manifest 검사, 공통 프레임 캐시.
수용: fake ADB로 3초 대기/터치 후 0.5초 검증 타이머 테스트; 화면 동일 프레임 검사; ROI 검출 결과와 점수 반환.
주의: PNG 기준 구현; RAW는 벤치마크만. 게임 실행/복구/kill-server 금지.
