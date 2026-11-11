# Task 01 — C# 프로젝트 골격

목표: Windows WPF 앱 + `docs/10_ARCHITECTURE.md`의 Domain/Contracts/Application/Features/Vision.OpenCvSharp/Device.Adb/App 7개 프로젝트와 테스트 프로젝트 생성. `v0.1` 명시.
수용: 기본 빌드, 실행/정지 UI, 기본 설정 파싱, 테스트 러너 동작.
범위 밖: 게임 화면 인식 구현, 자동 복구, 네이티브 DLL.
Codex 프롬프트: `AGENTS.md`를 준수하고 Task 01만 구현해줘. UI에서 Core는 직접 OpenCvSharp에 의존하지 않게 해줘. 빌드 및 테스트 결과를 보고해줘.

추가 수용: `data/architecture.v0.1.json` 참조 방향 준수, C# `net10.0`/`net10.0-windows` 타깃, DI 구성, `IGameFlow`/`IGameEventHandler` 껍데기 계약, 이벤트 없이도 빌드 가능.
