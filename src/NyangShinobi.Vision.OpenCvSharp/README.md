# NyangShinobi.Vision.OpenCvSharp

OpenCvSharp5 이미지 검사/ROI/Mat 캐시; OpenCV 타입은 이 프로젝트 밖에 노출 금지.

Task 02: PNG→관리 BGRA 프레임 변환, manifest 크기/SHA-256 검증, 최초 템플릿 Mat 캐시, 정규화 ROI·해상도별 크기 조정·점수/중심 반환과 한 프레임 결과 캐시를 구현했습니다. 네이티브 Mat는 이 경계에서 Dispose합니다. 비동기 API는 작업 스레드를 사용하고 STA의 동기 Detect는 거절합니다. 타임아웃/취소는 native 호출 전후의 협력적 검사입니다. 전체 화면 참조와 모호한 템플릿 선택은 거절합니다. 실제 게임 정확도·성능은 미검증입니다.
