# 냥냥시노비 v0.1 — Windows x64 포터블 실행 안내

## 새 Windows PC에서 실행

1. Windows 10/11 x64의 일반 데스크톱 환경에서 ZIP을 **폴더 전체로 압축 해제**합니다. ZIP 안에서 바로 실행하거나 EXE만 다른 곳에 복사하지 않습니다.
2. 폴더의 `NyangShinobi.App.exe`를 실행합니다. .NET 10 런타임이 함께 포함되어 있으므로 사용자 PC에 **.NET SDK나 .NET Runtime을 따로 설치할 필요가 없습니다**.
3. 사용자가 MuMu/BlueStacks를 실행하고 ADB 사용 및 연결을 준비합니다. **개발자 진단** 탭의 기본 `adb.exe`는 앱 실행 파일 옆의 동봉 ADB를 사용합니다. 별도 ADB 설치나 PATH 설정은 필요 없습니다. 장치 조회 후 serial을 직접 선택하며 자동 선택하지 않습니다. 앱플레이어와 게임은 ZIP에 포함하지 않습니다.
4. 메모리 스크린샷을 캡처하고 에셋/ROI를 선택해 일치율을 측정합니다. PNG는 저장 버튼으로 경로를 명시했을 때만 저장합니다. 진단은 터치·스와이프를 보내지 않습니다.

캡처 방식 비교는 같은 진단 화면 아래 **RAW vs PNG** 탭의 **RAW vs PNG 벤치마크**를 누릅니다. 직접 선택한 ADB/serial로 예열 후 각 10회를 교대로 측정합니다. 결과는 메모리만 사용하며 기본 PNG 방식은 바뀌지 않습니다. **진단 취소**로 중단할 수 있고 측정 중 일반 실행·다른 진단은 차단됩니다.

새 포터블은 실제 버전 `version.json`과 관리 파일 목록 `install-manifest.json`을 포함합니다. **업데이트** 탭에서 Stable / Pre-release (테스트) 두 채널을 확인하고, 매크로/진단 중지 및 동의 후 백업·설치·재실행합니다. 메타데이터 없는 과거 ZIP은 자동 설치하지 않습니다. [업데이트/복원 안내](https://github.com/Blooming2081/NyangShinobi_macro/blob/HEAD/docs/AUTO_UPDATE.md)를 확인하세요.

시작/중지는 현재 게임 입력 없는 골격 세션입니다. Task 03의 임무 진행은 구현되지 않았습니다. 기존 사용자 설정 파일이 있으면 `%LocalAppData%/NyangShinobi/settings.v0.1.json`을 우선 읽고, 없으면 폴더의 `data/settings.default.v0.1.json`을 사용합니다. 포터블은 설치 프로그램이 없는 폴더 배포이며, 이 기존 설정 위치는 변경하지 않았습니다.

## 포함 파일과 경로

- `NyangShinobi.App.exe`, 앱/프로젝트 DLL, `.deps.json`, `.runtimeconfig.json`.
- .NET 10 Windows x64의 `hostfxr.dll`, `hostpolicy.dll`, `coreclr.dll`, 프레임워크/WPF DLL 등 자체 포함 런타임.
- OpenCvSharp5 5.0.0.20261109의 관리 DLL, `OpenCvSharpExtern.dll`, `opencv_videoio_ffmpeg500_64.dll`.
- `assets/`의 원본 이미지와 `data/`의 기본 설정·manifest·화면 카탈로그.
- 공식 Android Platform-Tools 37.0.1의 `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`. Google의 Windows ADB는 x86 바이너리로 Windows x64의 WOW64에서 실행되며, WPF/OpenCV 본체는 x64입니다.
- `licenses/android-platform-tools/`의 원본 NOTICE, source.properties 및 배포 출처·SHA-256 메타데이터.
- 이 안내의 사본 `PORTABLE_README.md`.

앱과 진단은 작업 디렉터리 대신 EXE가 있는 `AppContext.BaseDirectory`를 기준으로 파일을 찾습니다. 바로 가기의 시작 위치가 다른 곳이어도 동작해야 합니다. `assets/`, `data/`, DLL을 이동하거나 이름을 바꾸지 않습니다. WPF/DI/네이티브 라이브러리와 외부 에셋의 경계를 보존하기 위해 단일 파일·trimming은 사용하지 않습니다.

기본 ADB 이름은 Windows의 [실행 파일 검색 순서](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw)에 따라 앱 옆의 파일을 찾습니다. ADB 및 동반 DLL도 원래 이름과 위치를 유지합니다. 앱플레이어가 기본 ADB 서버에 노출되지 않으면 사용자가 제품 안내에 따라 연결한 후 조회해야 합니다. 자동 연결·복구나 ADB 서버 재시작은 수행하지 않습니다. USB 연결에 필요한 장치별 드라이버는 사용자 환경에서 준비합니다.

ADB는 공식 ZIP에서 필요한 파일만 원본 그대로 추출합니다. 버전·URL·다운로드 및 개별 파일 SHA-256은 `tools/dependencies/platform-tools.windows.json`에 고정했습니다. 해시는 해당 공식 ZIP을 내려받아 계산한 값이며 불일치 시 배포가 중단됩니다. 버전 갱신 시 원본과 고지를 검토하고 메타데이터도 갱신해야 합니다. 공식 안내: [Platform-Tools](https://developer.android.com/tools/releases/platform-tools), [ADB 소스와 라이선스](https://android.googlesource.com/platform/packages/modules/adb/).

## VC++ 및 Windows 구성 요소

배포 스크립트는 포함한 OpenCV 및 ADB 네이티브 파일의 PE **일반 및 지연 import**를 검사합니다. 현재 고정 버전에는 별도 설치가 필요한 `MSVCP`/`VCRUNTIME`/`CONCRT` 의존성이 없습니다. VC++ 재배포 패키지를 필수 설치 항목으로 요구하지 않습니다. 향후 패키지가 외부 VC++ 런타임을 요구하도록 바뀌면 누락 의존성 검사에서 배포를 중단합니다. .NET WPF용 `vcruntime140_cor3.dll`은 자체 포함 게시에 포함됩니다.

- .NET 런타임 자체의 native 의존 파일은 자체 포함 게시가 함께 배포합니다. Windows 시스템 DLL까지 번들하는 방식은 아닙니다.
- 현재 OpenCV full runtime은 `MF.dll`, `MFPlat.dll`, `MFReadWrite.dll` 등 Windows Media Foundation도 import합니다. N/KN 에디션이나 미디어 기능이 제거된 환경에서는 해당 Windows 기능을 준비해야 합니다. 일반 Windows 데스크톱 이외 환경의 구성 요소 검증은 별도입니다.
- 깨끗한 새 Windows PC에서 VC++가 설치되지 않은 상태의 실제 실행은 별도로 확인해야 합니다. 현재 PC에서 게시 폴더의 로컬 .NET 런타임·네이티브 로딩·PNG 디코딩·ROI 매칭을 검사한 결과와 구분합니다.

공식 설명: [.NET 자체 포함 배포](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [OpenCvSharp Windows 설치](https://shimat.github.io/opencvsharp/articles/getting-started/installation.html), [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist).

## 개발 PC에서 ZIP 만들기

저장소 루트에서 Windows PowerShell 5.1 이상으로 실행합니다. 개발 PC에는 `global.json`에 맞는 .NET 10 SDK가 필요합니다. 스크립트는 저장소의 `.tools/dotnet/dotnet.exe`가 있으면 우선 사용하고, 없으면 PATH의 dotnet을 사용합니다. 사용자 배포에는 SDK를 넣지 않습니다.

최초 실행은 공식 Platform-Tools ZIP 다운로드를 위해 인터넷이 필요합니다. 이후 검증된 `.tools/` 캐시를 재사용합니다. [GitHub 자동 배포 안내](https://github.com/Blooming2081/NyangShinobi_macro/blob/HEAD/docs/GITHUB_DISTRIBUTION.md)에 Push Artifact 및 태그 Stable/Pre-release 사용법을 정리했습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Portable.ps1
# SDK를 직접 지정할 경우:
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Portable.ps1 -DotnetPath 'C:\Program Files\dotnet\dotnet.exe'
# 태그명과 같은 파일명으로 로컬 검증:
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Portable.ps1 -ReleaseTag v0.1.0
```

스크립트는 순서대로 다음을 수행합니다.

1. 전체 solution restore → Release build → 전체 test. 실패하면 게시/압축을 중단합니다.
2. `PortableWinX64.pubxml`로 `win-x64`, `.NET 10`, `SelfContained=true` 게시.
3. SHA-256으로 검증한 ADB 추가, 필수 실행/WPF/OpenCV/ADB 파일, 기본 JSON 동일성, 포함 에셋 SHA-256 및 native import 검사.
4. 게시 EXE를 `--verify-portable`로 실행해 로컬 .NET 런타임 사용, 79개 템플릿, 기본 설정, PNG 디코딩과 합성 프레임 ROI 매칭 검사. 동봉 `adb version`도 오프라인 실행합니다. ADB 서버·실제 장치·게임·사용자 설정은 사용하지 않습니다.
5. ZIP 생성 → 별도 경로에 압축 해제 → 이동한 EXE/ADB로 같은 검사 → ZIP SHA-256 기록.

각 실행은 새 시각 폴더를 사용하며 이전 배포를 재귀 삭제하거나 덮어쓰지 않습니다. 출력은 Git에서 제외하는 다음 위치입니다.

```text
artifacts/portable/<yyyyMMdd-HHmmss-fff>/
├── NyangShinobi-v0.1.0-win-x64/       # 배포 폴더
├── NyangShinobi-v0.1.0-win-x64.zip    # 전달할 ZIP
├── NyangShinobi-v0.1.0-win-x64.zip.sha256
├── portable-check.json
├── zip-check.json
├── adb-check.txt / zip-adb-check.txt
├── native-dependencies.json
├── tests/                            # 전체 테스트 TRX
└── zip-verification/                 # ZIP에서 다시 푼 검증 사본
```

실제 앱플레이어의 ADB 연결·캡처 시간·인식 정확도와 깨끗한 Windows VM 검증은 이 자가 검사에 포함되지 않습니다. `--verify-portable`의 높은 점수는 알려진 합성 프레임의 배포 검증 결과이며 게임 임계값을 변경하지 않습니다.
