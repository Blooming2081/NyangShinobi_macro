# NyangShinobi.Integration.Tests

App의 DI 구성, UI 명령의 시작/중지, 실제 WPF XAML 바인딩과 창 닫기, UI 세션의 장치 호출 0건을 검증합니다. Task 02에서는 가짜 ADB의 명시적 장치 선택·PNG/입력 인자, 합성 PNG의 실제 OpenCV 디코딩·ROI·캐시·무결성·Dispose, 타이밍 진단도 검증합니다. 프로세스 경계 테스트는 실제 ADB 대신 숨김 PowerShell CLI를 사용해 바이너리 stdout/stderr, 출력 제한, 취소·타임아웃·소유 프로세스 회수를 확인합니다. 실제 게임/ADB 서버/앱플레이어는 실행하지 않습니다.

개발자 진단 테스트는 장치 자동 선택 금지, 입력 없는 캡처·에셋 측정, 명시적 저장의 원본 픽셀 보존, 대상 변경 시 이전 결과 제거, 중복/취소/창 닫기와 일반 실행 동시 동작 금지, 임계값 없는 점수 측정, 실제 WPF 진단 컨트롤 바인딩을 확인합니다.

`TimeoutAndCancellationReleaseOnlyOwnedProcess`는 가짜 CLI가 비동기 Named Pipe로 실제 PID를 전달해야 준비 완료로 판단합니다. PID 파일 폴링이나 고정 시간 뒤 취소하지 않습니다. 준비 대기는 최대 15초이며, 생성 전부터 제한 시간이 흐르는 runner 특성에 맞춰 이 테스트의 전체 명령 제한은 18초입니다. 제품의 ADB 기본 제한 시간 및 3초/0.5초 검사 정책은 변경하지 않습니다. 정상 시작과 3초의 의도적 준비 지연을 각각 타임아웃·취소로 검증하며, 별도로 생성한 동일 실행 파일의 비교용 프로세스는 살아 있어야 합니다. 실패·테스트 취소 경로에서도 이 테스트가 생성한 프로세스와 파이프만 정리합니다.

진단은 프로세스 시작 실패(Win32 오류 코드), 준비 전 종료(exit code/stderr), 준비 대기 제한시간 초과(파이프 연결 여부/runner 상태)를 구분합니다. xUnit 출력에는 실행 경로와 인자, OS/.NET/아키텍처, 준비 소요 시간, 대상·비교용 PID와 종료 여부가 기록됩니다. 기존 GitHub Actions의 `verification-*` Artifact에 포함되는 TRX의 `StdOut`에서 확인할 수 있습니다. 실제 ADB/서버/앱플레이어는 사용하지 않습니다.

저장소 루트에서 Release 빌드 후 프로세스 테스트를 반복 실행할 수 있습니다.

```powershell
dotnet build NyangShinobi.slnx -c Release -m:1
1..5 | ForEach-Object {
    dotnet test tests/NyangShinobi.Integration.Tests -c Release --no-build --no-restore -m:1 `
        --filter FullyQualifiedName~AdbProcessRunnerTests `
        --logger "trx;LogFileName=adb-runner-$_.trx" --results-directory artifacts/adb-runner-repeat
    if ($LASTEXITCODE -ne 0) { throw "프로세스 테스트 반복 $_ 실패" }
}
```
