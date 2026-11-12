# GitHub Actions 자동 포터블 배포

`.github/workflows/portable.yml`은 Windows x64 호스팅 러너에서 기존 `tools/Publish-Portable.ps1`을 실행합니다. 7개 본체·4개 테스트 프로젝트와 게임 코드는 변경하지 않습니다. 별도 PAT/Secrets 없이 작업별 `GITHUB_TOKEN`을 사용합니다.

## 트리거와 결과

| 트리거 | 결과 | 파일명 예시 |
|---|---|---|
| 모든 브랜치 Push / 수동 실행 | 개발용 ZIP + SHA-256을 Actions Artifact로 제공 | `NyangShinobi-v0.1.0-dev.12.1234abcd-win-x64.zip` |
| `v*` 태그 Push | 검증된 ZIP을 Artifact와 GitHub **Pre-release**로 제공 | `NyangShinobi-v0.1.0-win-x64.zip` |

개발 빌드는 실행번호와 소스 커밋으로 구분합니다. 태그는 `v0.1`, `v0.1.0`, `v0.1.0-rc.1` 형태를 허용하며 파일명에 그대로 보존합니다. 숫자 버전은 `Directory.Build.props`의 `Version`과 일치해야 합니다(`v0.1`은 `0.1.0`으로 비교). 이후 버전 출시 전에는 프로젝트 버전도 갱신해야 합니다. 현재 태그 배포는 모두 Pre-release이며 Latest로 표시하지 않습니다.

명세·에셋 → 전체 restore → Release build → 전체 test → 자체 포함 게시/ADB 번들 → 게시 및 ZIP 재압축 해제 EXE 자가 검사/오프라인 ADB 버전 검사 순서입니다. 실패하면 배포 ZIP 업로드와 Release 공개가 진행되지 않습니다. 테스트·검사 보고서는 별도 `verification-*` Artifact에 남기며 초기 실패 시 보고서가 없을 수 있습니다. Artifact 보관 기간은 14일입니다.

## GitHub에 Push

저장소 루트에서 변경 사항을 검토한 뒤 필요한 파일만 커밋합니다. 다음은 처음 배포 기능을 올리는 예시입니다.

```powershell
git status --short
git add .github/workflows/portable.yml tools/Publish-Portable.ps1 tools/Copy-PortableAdb.ps1 tools/dependencies/platform-tools.windows.json README.md docs/PORTABLE_DISTRIBUTION.md docs/GITHUB_DISTRIBUTION.md docs/PORTABLE_VERIFICATION.md
git commit -m "Add validated Windows portable CI and prereleases"
git push origin HEAD
```

ZIP/ADB 바이너리, SDK 캐시, `artifacts/`는 커밋하지 않습니다. Settings → Actions → General에서 Actions를 허용해야 합니다. 조직 정책이 제한하면 공식 `actions/*`와 Release 작업의 `contents: write`를 허용해야 합니다. 전체 워크플로 권한을 쓰기로 바꿀 필요는 없습니다.

## Actions 결과와 개발 ZIP

1. [Actions](https://github.com/Blooming2081/NyangShinobi_macro/actions) → **Windows portable** → 해당 커밋 실행을 엽니다. 다른 원격 저장소를 사용하면 그 저장소의 Actions로 이동합니다.
2. **Validate and package Windows x64**에서 명세 검사 및 `Restore, Release build, all tests, publish and verify ZIP` 로그의 성공을 확인합니다.
3. 요약의 Artifacts에서 `NyangShinobi-v...-win-x64`를 받습니다. 웹에서 받는 바깥 ZIP에는 실제 포터블 ZIP과 `.sha256`이 있습니다. **안쪽 포터블 ZIP도 압축 해제**한 뒤 EXE를 실행합니다. Artifact 다운로드는 GitHub 로그인이 필요할 수 있습니다.
4. `verification-*`에는 4개 프로젝트의 TRX, 네이티브 의존성 및 EXE/ADB 검사 보고서가 있습니다.

Run workflow로 현재 브랜치를 수동 검증할 수도 있습니다. 수동 실행은 Release를 만들지 않습니다.

## 태그 Pre-release 게시

배포할 커밋을 Push하고 Actions 성공을 확인한 뒤, 아직 사용하지 않은 태그를 해당 커밋에 만듭니다.

```powershell
git tag -a v0.1.0 -m "NyangShinobi v0.1.0 pre-release"
git push origin v0.1.0
```

태그의 build 성공 후에만 **Publish pre-release**가 실행됩니다. 현재 실행의 Artifact만 다운로드해 SHA-256을 확인하고 draft에 ZIP/체크섬을 올린 뒤 Pre-release로 공개합니다. build는 `contents: read`, release만 `contents: write`입니다. 외부 Actions는 전체 커밋 SHA에 고정했습니다.

업로드 실패로 draft가 남으면 같은 태그 실행을 재시도하여 draft의 파일을 교체하고 공개할 수 있습니다. 이미 공개된 Release는 덮어쓰지 않고 실패합니다. 수정 배포는 새 소스 버전과 태그를 사용하며 공개 태그를 강제로 이동하지 않습니다.

## Releases 다운로드

1. [Releases](https://github.com/Blooming2081/NyangShinobi_macro/releases)에서 원하는 태그의 **Pre-release**를 엽니다.
2. Assets에서 `NyangShinobi-v<버전>-win-x64.zip`과 `.zip.sha256`을 받습니다. 자동 생성된 Source code ZIP은 실행용이 아닙니다.
3. 필요하면 다음으로 무결성을 확인합니다.

```powershell
$zip = 'NyangShinobi-v0.1.0-win-x64.zip'
$expected = (Get-Content "$zip.sha256" -Raw).Trim()
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) { throw 'ZIP SHA256 mismatch' }
```

4. ZIP 전체를 풀고 `NyangShinobi.App.exe`를 실행합니다. .NET/WPF/OpenCV/ADB/이미지/기본 설정을 동봉하므로 .NET/ADB 별도 설치 없이 실행할 수 있습니다. 앱플레이어는 사용자가 실행·연결하고 serial을 선택합니다. Windows 구성 요소 등 실행 조건은 [포터블 안내](PORTABLE_DISTRIBUTION.md)를 따릅니다.

## 검증 범위

로컬 검증과 GitHub 호스팅 실행은 별개입니다. 실제 Push 후 GitHub 실행 상태 및 Release 첨부 파일을 확인해야 합니다. 이번 로컬 결과와 GitHub 실행 여부는 [배포 검증 기록](PORTABLE_VERIFICATION.md)에 구분해 기록합니다. 실제 MuMu/BlueStacks 및 깨끗한 새 Windows PC 검증은 별도이며 Task 03은 포함하지 않습니다.

공식 참고: [워크플로 권한](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions), [gh release create](https://cli.github.com/manual/gh_release_create).
