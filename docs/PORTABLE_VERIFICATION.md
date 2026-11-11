# Windows x64 포터블 배포 검증 기록

2026-11-11 (한국 시간), Windows x64, .NET SDK 10.0.404로 `tools/Publish-Portable.ps1`를 실제 실행했습니다. 실행 방법은 [포터블 배포 안내](PORTABLE_DISTRIBUTION.md)를 참조하세요.

## 생성 결과

```text
artifacts/portable/20261111-230302-058/
├── NyangShinobi-v0.1.0-win-x64/
├── NyangShinobi-v0.1.0-win-x64.zip
├── NyangShinobi-v0.1.0-win-x64.zip.sha256
├── portable-check.json
├── zip-check.json
├── native-dependencies.json
├── wpf-ui-check.json
├── tests/
└── zip-verification/
```

ZIP은 **114,526,458 bytes (약 109.2 MiB)**이며 SHA-256은 다음과 같습니다.

```text
B8E9E9E1D7F6FC2CD3289A579C22EB5200C1CE31DACE75A8AD2556E801CAC905
```

이 경로들은 생성물로 Git에서 제외합니다. 이후 스크립트 실행은 새로운 시각 폴더를 만들며, 위 경로는 이번 실행 기록입니다.

## 실제 실행 결과

| 검증 | 결과 |
|---|---|
| solution restore | 성공 |
| 전체 Release build | 경고 0 / 오류 0 |
| 전체 test | **79 통과 / 0 실패 / 0 건너뜀** (Domain 8, Application 26, Features 2, Integration 43) |
| self-contained publish | Windows x64, .NET 10.0.15 Core + WindowsDesktop 포함, single-file/trimming 비활성 |
| 게시 EXE 자가 검사 | exit 0, `LocalRuntime=true`, `X64`, 템플릿 79, 합성 ROI 점수 1.0 |
| ZIP 재압축 해제 EXE | 별도 폴더에서 같은 검사 exit 0, 같은 결과 |
| 일반 WPF EXE | 게시 EXE를 임시 폴더를 작업 디렉터리로 숨김 실행, 정상 창 생성·진단 탭 접근·79개 에셋 로딩 확인 후 종료 |
| WPF의 네이티브 모듈 | `coreclr.dll`과 `OpenCvSharpExtern.dll`이 실제 게시 폴더에서 로드됨 |
| 진단 장치 선택 | 자동 선택 없음, 장치 미선택 상태에서 캡처 비활성 |
| 명세·에셋 검증 | PASS: 86 catalog assets, 7 project specs, 6 modules, 1 event definitions |

기존 78개 테스트에, 전역 런타임을 쓰는 테스트 호스트를 자체 포함 실행으로 잘못 보고하지 않는 검증 1개를 추가했습니다. 배포 자가 검사는 사용자 설정/ADB/게임을 사용하지 않고 동봉 기본 설정·에셋·디코더·진단 인식 API를 사용합니다. 합성 ROI 점수는 게임 성능/정확도 지표가 아닙니다.

## VC++와 OS 의존성

고정 OpenCvSharp5 5.0.0.20261109의 `OpenCvSharpExtern.dll`과 `opencv_videoio_ffmpeg500_64.dll`에 대해 x64 PE 일반/지연 import를 검사했습니다. 외부 `MSVCP`/`VCRUNTIME`/`CONCRT` 의존성은 없었습니다. .NET WPF용 `vcruntime140_cor3.dll`은 게시 폴더에 포함됨을 확인했습니다. 현재 배포의 별도 VC++ 재배포 패키지 설치를 필수로 지정하지 않았습니다.

OpenCV full runtime의 Media Foundation 등 Windows 시스템 DLL은 OS 의존성으로 남습니다. N/KN 또는 미디어 기능이 빠진 환경은 배포 안내의 조건을 확인해야 합니다.

## 변경 파일과 범위

- `tools/Publish-Portable.ps1`: 전체 빌드/테스트, 자체 포함 게시, 필수 파일·JSON·에셋 해시·native import 확인, 게시/ZIP EXE 검사, ZIP/SHA 생성.
- `src/NyangShinobi.App/Properties/PublishProfiles/PortableWinX64.pubxml`: 자체 포함 x64 폴더 게시 설정.
- `src/NyangShinobi.App/NyangShinobi.App.csproj`: 기존 에셋/JSON의 게시 복사 정책 명시.
- `src/NyangShinobi.App/Diagnostics/PortableValidation.cs`, `App.xaml.cs`: `--verify-portable` 자가 검사 경로. 일반 시작/진단 동작은 유지.
- `tests/NyangShinobi.Integration.Tests/PortableValidationTests.cs`: 프레임워크 의존 테스트 호스트의 잘못된 자체 포함 보고 방지.
- `.gitignore`, README, 시작/진단 안내 및 이 배포 문서들: 출력 제외·배포 사용 방법·검증 범위 기록.

7개 본체·4개 테스트 프로젝트, 기존 참조 방향, 이미지 원본과 게임 워크플로는 유지했습니다. Task 03은 구현하지 않았습니다. 깨끗한 새 Windows VM 및 실제 MuMu/BlueStacks의 ADB 연결·캡처 성능·인식 정확도는 이번 검증에 포함하지 않았습니다.
