# NyangShinobi.App

WPF MVVM/UI 설정/실행 제어 및 DI 구성 루트.

Task 01: WPF 시작/중지 UI와 DI 구성 루트를 구현했습니다. UI는 골격 세션만 실행하며 게임 입력은 없습니다.

Task 02: ADB/디코더/인식의 구현 DI와 장치 세션용 scoped 프레임·입력 정책을 등록했습니다. 배포 출력에 manifest/catalog/assets를 복사합니다. UI 시작 버튼은 이 API를 자동으로 실행하지 않습니다.

후속 개발자 진단은 `Diagnostics/`에 분리했습니다. 수동 ADB 경로·장치 선택, 메모리 PNG 캡처·미리보기, 에셋/ROI 점수 측정과 명시적 PNG 저장을 제공합니다. 입력 컨트롤러를 진단에 연결하지 않으며 일반 실행과 동시에 동작하지 않습니다.

`Properties/PublishProfiles/PortableWinX64.pubxml`은 .NET 10 자체 포함 폴더 배포용입니다. `--verify-portable`은 동봉 런타임·기본 설정·에셋·PNG/ROI를 검사하고 종료하며 실제 ADB/게임을 사용하지 않습니다.
