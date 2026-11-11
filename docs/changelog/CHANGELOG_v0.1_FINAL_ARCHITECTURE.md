# v0.1 최종 Codex 자료 — 변경 내역

> 설계 자료 작성 당시의 역사적 기록입니다. 현재 구현 상태는 [루트 README](../../README.md)와 [Task 01 결과](../TASK01_FOUNDATION.md)를 참조하세요. 아래 명령과 백틱 경로는 저장소 루트 기준입니다.

기존 v0.1 Starter Updated 패키지를 기반으로 변경.

## 새 요구 반영

- C#·WPF·.NET 10 LTS·OpenCvSharp5·ADB 구조를 **최종 고정**, 처음부터 C++ DLL 없음.
- 솔루션 경계를 Domain/Contracts/Application/Features/Vision.OpenCvSharp/Device.Adb/App 7개 프로젝트로 명시.
- Mission/Mirror/NekoNinja/DoubleReward/DateChange/Events를 기능별 독립 폴더, 이벤트는 등록/설정으로 ON/OFF.
- 간단한 이벤트 JSON + 실제 이미지; 복잡한 이벤트 C# handler/DI 등록 확장, 동적 DLL 로더 없음.
- 날짜 변경 팝업 v0.1에 포함: 원본 이미지 `date_changed.png`, `date_change_followup_close.png` 추가, 원본 함수 3개 참조 수록.
- 흐름/카탈로그/manifest/기본 설정/모듈 정의/기계 판독 아키텍처 데이터 갱신.
- 재시작·정체 복구·ADB 복구는 계속 제외. 날짜 변경 자체는 게임 정상 UI 처리.
- 날짜/이벤트/아키텍처 테스트 및 Task 08~10 추가.

## 원본 로직 보존 주의

날짜 팝업의 홈 이동은 이전 코드의 `DateChangedRestart` 예외를 그대로 복제하지 않고 명시적 상태 전환으로 설계. 기능적 순서만 보존하고 위험한 미확인 화면 무작위 클릭은 제외했다.

## 검증

`python tools/validate_spec.py`로 에셋 무결성/JSON 참조 검사. .NET SDK가 없는 작업 환경이므로 C# 빌드/실기 자동화 검증은 Codex의 Windows 환경에서 수행해야 한다.
