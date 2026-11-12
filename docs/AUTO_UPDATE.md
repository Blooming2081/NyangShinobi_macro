# GitHub Releases 자동 업데이트

기존 7개 본체·4개 테스트 프로젝트, Windows x64 자체 포함 ZIP, 게임·인식·PNG/RAW 벤치마크 및 3초/500ms 정책을 유지합니다. 새 프로젝트·업데이트 프레임워크·NuGet 패키지는 없습니다.

## 사용 방법

1. 창이 표시된 후 Releases를 비동기로 **한 번** 확인합니다. 실패해도 앱 사용은 계속 가능합니다. 주기적인 확인은 없습니다.
2. **업데이트** 탭에서 현재/최신 버전과 Release 내용을 보고 **업데이트** 또는 **나중에**를 선택합니다. 이후에는 **업데이트 확인**으로 수동 조회합니다.
3. 채널은 **Stable**과 **Pre-release (테스트)** 두 개입니다. Pre-release (테스트)는 RC와 정식을 모두 확인하고, Stable은 GitHub Pre-release 표시와 SemVer prerelease가 없는 정식만 확인합니다. 기본값은 Pre-release (테스트)이며 `%LocalAppData%/NyangShinobi/updates.json`에 저장합니다. 기존 내부 값 `Development=0`/`Stable=1`을 유지해 설정 호환성을 보존하며 세 번째 채널은 추가하지 않습니다. 게임 설정은 수정하지 않습니다.
4. 매크로와 진단을 먼저 중지합니다. 업데이트 버튼의 종료·설치·재실행 안내에 동의해야 다운로드합니다. 다운로드 중 취소할 수 있습니다. 다운로드 중 매크로를 새로 시작했다면 설치로 넘어가지 않습니다.
5. 세션/ADB 완료를 확인하고 UI 시작·진단을 잠근 뒤 도우미 준비를 확인합니다. 원본 앱 종료 후 도우미가 교체하고 새 앱의 시작 완료 응답을 확인합니다. 관리자 승격이나 ADB/게임/앱플레이어 재시작은 하지 않습니다.

## 버전과 배포

SemVer 2.0.0 기준으로 `0.1.0-rc.2 < 0.1.0-rc.10 < 0.1.0`입니다. build metadata(`+...`)는 비교 우선순위에 영향을 주지 않습니다. 날짜나 GitHub Latest 표시 대신 버전으로 선택합니다.

배포 스크립트는 `app/version.json`에 실제 SemVer·앱 ID·플랫폼·소스 커밋·태그를, `app/install-manifest.json`에 최상위 진입점/설명서와 `app/` 전체 관리 파일의 경로·크기·SHA-256을 기록합니다. 두 메타데이터는 schema 2이며 관리 경로는 ZIP 내부 최상위 배포 폴더 기준입니다. UI의 현재 버전은 앱 옆의 `version.json` 기준입니다. Actions 개발 빌드는 `0.1.0-dev.<run>.g<commit>`이며, 숫자로만 된 SHA가 SemVer를 깨지 않도록 `g`를 붙입니다. 기존 ZIP SHA-256 첨부도 유지합니다.

**구형 평면 구조(schema 1) ↔ 새 app/ 구조(schema 2)의 자동 전환은 허용하지 않습니다.** 새 업데이트 코드는 다운로드 검증 후 도우미 인계 전에 구조 차이를 확인하고 수동 설치를 안내하며, 설치 도우미도 파일 교체/백업 전에 다시 거부합니다. 구형 업데이트 코드는 ZIP 루트에 기존 `install-manifest.json`이 없으므로 새 패키지를 설치 가능 상태로 승인하지 못합니다. 구형 코드의 오류 문구는 수동 설치 안내가 아닐 수 있으므로 Release 노트에도 전환 안내를 제공합니다. ZIP을 **새 폴더**에 풀고 실행하세요. 기존 `%LocalAppData%/NyangShinobi` 설정/채널은 유지하며 구형 폴더의 개인 스크린샷/추가 파일은 별도로 보관합니다. 새 구조끼리의 자동 설치·백업·롤백은 지원합니다.

Actions는 `v0.1.0-rc.3`처럼 prerelease 식별자가 있는 태그를 Pre-release로, `v0.1.0`처럼 없는 태그를 Stable Release로 게시합니다. draft에 파일을 올린 뒤 공개하는 흐름과 권한·검증 조건은 유지합니다. 일반 Push의 Actions Artifact는 업데이트 소스가 아닙니다. **메타데이터 없는 과거 ZIP은 자동 설치하지 않습니다.** 새 포맷 포터블을 수동 설치한 이후부터 자동 설치를 사용합니다. SDK 개발 빌드는 조회만 가능하고 설치는 자체 포함 포터블에서만 지원합니다.

## 다운로드·ZIP·개인 파일 보호

공식 `Blooming2081/NyangShinobi_macro`의 공개 Releases(최대 500개)에서 선택한 태그의 `NyangShinobi-<tag>-win-x64.zip` 및 정확한 `.sha256`만 받습니다. HTTPS와 공식 GitHub/asset 호스트를 리다이렉트마다 검사하며 토큰을 배포하지 않습니다.

다운로드는 최대 512 MiB, ZIP은 최대 항목 10,000개/개별 파일 256 MiB/확장 합계 1 GiB입니다. API·수신 크기 및 SHA-256이 맞아야 합니다. ZIP의 최상위 폴더, 경로 이탈·절대 경로·Windows 예약 이름/대체 스트림·링크/재분석 지점·대소문자 중복을 검사합니다. 설치 목록과 ZIP 파일이 일치하고 내부 파일 해시·실제 버전/태그도 맞아야 합니다.

작업 폴더는 배포의 `app/.updates/<GUID>/`이며 설치 목록에 `.updates`를 넣지 못합니다. 관리 목록 밖의 파일을 삭제하지 않고 개인 파일과 새 파일이 충돌해도 덮어쓰지 않습니다. `%LocalAppData%/NyangShinobi/settings.v0.1.json`과 개인 스크린샷/로그는 보존합니다. 기본 설정은 배포 파일로 갱신하지만 사용자 설정의 우선순위는 유지합니다.

## 도우미·백업·롤백

기존 EXE의 `--update-helper` 모드를 별도 프로세스로 실행합니다. 관리 목록의 `app/` 직속 EXE/DLL/JSON만 `app/.updates/<GUID>/runner/`로 복사하므로 원본 파일을 잠그지 않습니다. 에셋·게임 세션은 로드하지 않습니다. 설치 대상 경계는 바깥 배포 폴더이고 실행/재실행 경로는 그 안의 `app/NyangShinobi.App.exe`입니다. 원본 PID·시작 시각·실행 경로를 확인하고 최대 2분 기다리며 부모나 다른 프로세스를 강제 종료하지 않습니다.

같은 설치 폴더의 GUI는 단일 인스턴스로 제한합니다. 도우미가 설치 잠금을 유지하는 동안 일반 GUI의 새 시작을 차단하고, 검증한 계획의 재실행만 허용합니다. 도우미 인계 실패 시 원본 앱은 유지하고 그 호출이 만든 도우미만 회수합니다.

ZIP에는 추가 런타임이 필요 없어 코드·JSON 증가분만 늘어납니다. 설치 중에는 도우미 런타임 복사본, ZIP·추출본 및 백업을 위해 배포 크기의 수 배에 해당하는 디스크 공간이 필요합니다. 쓰기 권한이 없으면 중단하고 UAC 승격하지 않습니다.

기존 관리 파일을 `backup/`에 복사한 뒤 관리 파일만 원자적 이동으로 교체합니다. 제거된 관리 파일도 백업합니다. 교체나 재실행에 실패하면 기존 파일을 복원하고 새로 추가한 파일만 제거합니다. 복원이 성공하면 기존 앱을 실행합니다. 새 앱이 종료되지 않으면 잠긴 파일에 롤백하지 않고 백업을 보존합니다.

완료 후에도 `app/.updates/<GUID>/` 안의 백업과 `journal.json`/`result.json`/`runner/helper-error.txt`는 자동 삭제하지 않습니다. 앱 종료 후 사용자가 완료된 작업 폴더를 정리할 수 있습니다. 백업 역시 바깥 배포 폴더 기준 경로이므로 `backup/app/` 및 백업된 최상위 진입점/설명서를 구분해 복원합니다. 전원 차단·OS 강제 종료 중의 자동 복구는 구현하지 않았습니다. 중단된 설치나 롤백 실패 시 앱을 종료하고 백업으로 수동 복원해야 할 수 있습니다.

## 검증용 CLI

`app/NyangShinobi.App.exe --verify-update-source`는 실제 공개 Release 조회·다운로드·해시·메타데이터를 검사하고 `app/.updates/<GUID>/`에 보고서를 남깁니다. **설치는 하지 않습니다.** 자동 테스트는 가짜 HTTP를 쓰며 이 모드만 실서버를 확인합니다.

`--verify-update-restart <plan.json>`은 격리한 포터블 복사본의 설치·재실행 프로토콜 검증용입니다. 동봉 런타임/템플릿/PNG/ROI 검사 후 응답을 남기고 창 없이 종료합니다. 일반 설치는 정상 창을 재실행합니다.

공식 참고: [SemVer](https://semver.org/spec/v2.0.0.html), [Releases 목록](https://docs.github.com/en/rest/releases/releases#list-releases), [asset 다운로드](https://docs.github.com/en/rest/releases/assets#download-a-release-asset). 실제 검증 범위는 [검증 기록](PORTABLE_VERIFICATION.md)에 구분합니다.

최신 테스트 수와 실제 실행 결과는 [배포 검증 기록](PORTABLE_VERIFICATION.md)을 확인하세요. 신규 포맷 Release 게시 후 실제 사용자 GUI에서 설치하는 전체 과정은 아직 검증하지 않았습니다. CI는 ZIP 업로드 전에 `tools/Test-UpdateInstall.ps1`로 격리 설치 검사를 실행하도록 구성했습니다.
