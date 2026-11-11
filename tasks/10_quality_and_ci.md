# Task 10 — 품질 및 CI 게이트

- `Directory.Build.props` / `Directory.Packages.props`로 빌드·패키지 버전 중앙 관리.
- Windows .NET 10 CI에서 restore, build, test, spec validation 실행.
- `data/architecture.v0.1.json` 의존성 방향 검사.
- v2.0.6 동작 순서 기반 회귀 테스트와 날짜/이벤트 테스트 추가.
- 실행 없는 dry-run/fake ADB 회귀 fixture를 CI에서 사용.
- 실제 MuMu/BlueStacks 검증은 분리된 수동 체크리스트로 관리.

필수: 빌드/테스트에 실패하면 기능 완료 선언 금지, 미확인 처리 `NeedsUserAction`.
