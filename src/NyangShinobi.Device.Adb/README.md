# NyangShinobi.Device.Adb

adb.exe CLI를 통한 PNG 단발 캡처와 기본 tap/swipe. RAW·지속형 shell은 실측 전 구현하지 않습니다.

Task 02: 명시한 serial의 준비된 장치만 사용합니다. stdout/stderr 바이너리 처리·출력 제한·취소·타임아웃·소유 CLI 회수를 구현했습니다. 입력은 같은 장치의 캡처 해상도가 필요하며 전송 결과는 Pending입니다. PNG 디코딩은 Contracts 포트로 주입받아 Vision 구현을 직접 참조하지 않습니다. 공유 ADB 서버나 게임/앱플레이어를 종료하지 않습니다.
