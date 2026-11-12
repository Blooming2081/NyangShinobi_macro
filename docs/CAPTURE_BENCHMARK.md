# 개발자 진단 — RAW vs PNG 캡처 벤치마크

실제 MuMu/BlueStacks에서 사용자가 PNG 유지 또는 RAW 전환 여부를 판단하기 위한 수동 측정 도구입니다. **게임의 기본 PNG 캡처는 유지**하며 RAW를 게임 캡처 인터페이스에 등록하지 않습니다. 새 프로젝트·외부 패키지·C++ DLL은 없습니다.

## 사용법

1. 사용자가 앱플레이어와 게임 및 ADB 연결을 준비하고 일반 매크로 실행을 중지합니다.
2. **개발자 진단**에서 기존 ADB 경로를 확인하고 **장치 조회** 후 원하는 serial을 직접 선택합니다. 하나여도 자동 선택하지 않습니다.
3. 아래 결과 영역의 **RAW vs PNG** 탭에서 **RAW vs PNG 벤치마크** 버튼을 누릅니다. 이전 스크린샷·인식·벤치마크 결과를 비우고 시작합니다.
4. 방식별 예열 1회 후 기본 10회씩 측정합니다. 측정 1회차는 PNG→RAW, 2회차는 RAW→PNG이며 이후도 교대합니다. 예열은 통계에서 제외됩니다.
5. 사용한 경로·serial, 방식별 지원/변환 확인·오류·완료 표본 수·해상도, 통계와 회차별 기록을 확인합니다. 실패 또는 취소로 10개 미만인 방식은 완전한 비교로 해석하지 않습니다.
6. **진단 취소**를 누르면 진행 중 명령을 취소하고 회수한 뒤 완료된 표본만 표시합니다. 창을 닫으면 종료를 기다리고 보관 프레임도 해제합니다.

측정 중 경로/장치 변경, 다른 진단 작업 및 일반 매크로 시작을 차단합니다. 화면과 결과는 메모리에서만 보관하며 자동 저장하지 않습니다. 방식별 마지막 프레임 한 장만 유지하고 회차별 픽셀을 누적하거나 매번 미리보기로 복사하지 않습니다. 대상 변경·장치 재조회·다음 스크린샷/벤치마크·창 닫기로 결과가 지워집니다.

## 명령과 측정 범위

장치 목록을 한 번 재확인한 다음 아래 두 명령만 순차 실행합니다. 장치 확인 비용은 측정에서 제외합니다. 터치·스와이프·자동 연결·게임 실행·서버/앱플레이어 재시작을 수행하지 않습니다.

```text
adb -s <직접 선택한 serial> exec-out screencap -p
adb -s <직접 선택한 serial> exec-out screencap
```

| 항목 | 범위 |
|---|---|
| ADB 수신 ms | runner 호출 직전부터 stdout 수신·stderr 배출 및 CLI 종료까지. 프로세스 생성과 worker 스케줄링 포함 |
| 전송 byte | 헤더를 포함한 stdout 바이트 수. PNG 압축 데이터 또는 RAW 전체 응답 |
| 변환/프레임 ms | 기존 PNG 디코더 또는 RAW 검증/채널 변환 호출부터 관리 BGRA32 `ImageFrame` 생성 완료까지. 디코더 worker 대기 및 버퍼 소유 복사 포함 |
| 전체 ms | ADB 호출 직전부터 `ImageFrame` 생성까지. 미리보기·인식·화면 렌더링은 제외 |

각 항목의 평균·중앙값·최솟값·최댓값·P95를 제공합니다. P95는 정렬 후 `ceil(0.95×n)`번째 값인 nearest-rank 방식입니다. 10개 표본에서는 최댓값과 같습니다. 경과 시간은 단조 증가하는 `Stopwatch`로 측정합니다.

PNG/RAW 화면은 서로 다른 시점입니다. 해상도가 달라져도 기록에 표시하며, 픽셀 완전 일치를 요구하지 않습니다. 해상도가 변한 결과를 같은 조건의 속도 비교로 해석하지 마세요. 같은 앱플레이어·해상도·게임 장면에서 여러 번 직접 측정해 부하와 변동 폭을 비교해야 합니다. 이 도구는 기본 캡처 방식을 자동 전환하거나 임계값을 변경하지 않습니다.

## RAW 지원 범위

Little-endian AOSP screencap 출력만 지원합니다.

- 12바이트 헤더: width / height / pixelFormat 각 uint32.
- 16바이트 헤더: 위 항목 + 색공간 코드(uint32, 0=unknown / 1=sRGB / 2=Display P3).
- pixelFormat 1=RGBA_8888, 2=RGBX_8888, 5=BGRA_8888. RGBX의 alpha는 255로 만듭니다. 다른 형식(RGB565, FP16, 10bit, YUV 등)은 미지원으로 표시합니다.
- 입력 해상도와 픽셀 제한, 안전한 정수 계산, `width×4` packed stride 및 `header + width×height×4`의 정확한 전체 길이를 확인합니다. 음수에 해당하는 값·0·오버플로·잘림·추가 데이터·독자 패딩은 거부합니다. 16바이트 헤더의 모르는 색공간 코드도 거부합니다.

AOSP는 GPU 내부 stride를 전송 헤더에 싣지 않고 각 행의 실제 width만 씁니다. 수신 행 패딩/stride를 추측하지 않습니다. 12/16바이트는 정확한 길이로 구분하며 그 외 레이아웃은 지원하지 않습니다. 색공간 코드는 기록만 하고 별도의 색역/ICC 변환은 하지 않습니다. BGRA32 생성 확인이 실제 장치의 색상·회전·인식 정확도 검증을 의미하지는 않습니다.

공식 참조: [AOSP 현재 screencap](https://android.googlesource.com/platform/frameworks/base/+/refs/heads/main/cmds/screencap/screencap.cpp), [AOSP Android 7.1.2 screencap](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/android-7.1.2_r39/cmds/screencap/screencap.cpp), [Android 픽셀 형식](https://android.googlesource.com/platform/system/core/+/refs/heads/main/libsystem/include/system/graphics-base-v1.0.h).

## 오류·자원·호환성

한 방식이 잘못된 헤더·미지원 형식·ADB 오류·제한시간 초과로 실패하면 그 방식을 더 요청하지 않고 오류와 완료 표본 수를 남깁니다. 정상인 다른 방식은 계속 측정할 수 있습니다. 실패 표본과 예열은 통계에 넣지 않습니다. 취소는 추가 캡처를 중단하며 완료된 표본만 남깁니다.

기존 `AdbProcessRunner`의 명령 제한·출력 크기 제한·취소·소유 CLI 회수를 그대로 사용합니다. 기본 ADB 명령 제한은 10초, 수신 한도는 32 MiB이며 큰 RAW 응답은 한도 오류가 될 수 있습니다. 디코딩/변환도 기존 `VisionOptions`의 10초 제한과 픽셀 한도를 적용합니다. RAW 변환은 행마다 취소를 확인합니다. 기존 네이티브 PNG 디코더는 실행 중인 native 호출을 강제 종료하지 않고 반환 후 취소를 확인합니다.

개발자 진단의 gate가 일반 진단 PNG 캡처와 벤치마크의 동시 수신을 막습니다. 무거운 작업은 worker에서 수행하며 UI에는 작은 진행 메시지와 최종 결과만 전달합니다. `IScreenCapture`, `AdbScreenCapture`, `FrameScheduler`, `InputSequencer`, 게임의 3초/500ms 정책은 그대로입니다.

기존 [Windows x64 포터블 배포](PORTABLE_DISTRIBUTION.md)에 포함되는 App 코드/XAML과 이미 동봉한 OpenCV/ADB만 사용하므로 배포 파일 구성·워크플로 조건을 바꿀 필요가 없습니다. 실제 MuMu/BlueStacks 측정과 GitHub Actions 실행은 로컬 가짜 ADB 검증과 구분합니다.

로컬 Release 전체 **121개 테스트(신규 37개 포함)** 및 포터블 EXE/ZIP 자가 검사 결과와 생성·수정 파일 목록은 [검증 기록](PORTABLE_VERIFICATION.md)에 있습니다. 이 결과는 실제 MuMu/BlueStacks의 캡처 성능·정확도 검증이 아닙니다.
