# 화면/에셋 카탈로그 규칙

## 원본 경로
- v2.0.6 `assets/images_nav`: 홈/등급/룰렛/관문/팝업/**날짜 변경 및 후속 팝업/2배 보상 아이콘/네코 닌자 뽑기·결과 참고화면**.
- v2.0.6 `assets/images`: 거울 결과/선택/재입장.
- v2.0.6 `assets/skill_priority`: 3개 등급의 인법 후보(총 46장).
- v2.0.6 `assets/secret_skill`: 비기 관련 3장.
- **이 ZIP**의 `assets/` 아래에는 현재 범위에서 사용할/검증할 파일만 원본 바이트 그대로 복사했다. 제외된 파일은 `data/screen_catalog.v0.1.json`의 상태 `excluded`로 관리.

## 검출 정책
- `neko_ninja_summon.png`/`neko_ninja_result.png`는 **전체 화면 참고 이미지**이며 각각 3개/2개 ROI를 교차 대조한다. `mission_double_reward.png`는 성공 화면 하단 작은 재생 아이콘이다.
- 게임 화면 하나가 단일 이미지 파일 하나와 같지는 않다. 어떤 파일은 버튼, 어떤 파일은 결과 제목, 어떤 파일은 전체 화면 참고 이미지다.
- 임무 성공은 `mission_success_title` + `mission_clear`(같은 프레임)로 판단. 추가 `mission_double_reward`가 보이면 승인된 설정에서만 먼저 탭하고, 광고 후 옛 확인 좌표를 재사용하지 않는다. `mission_failed`가 보이면 먼저 실패 처리.
- 거울 `image5`는 카드 선택의 확인 버튼. 거울 `image9`는 실패 확인. 임무 `mission_clear`는 **성공/실패 공통 확인**. 구분 없이 같은 템플릿으로 처리 금지.
- 거울 `image6`는 **'임무' 글자 이미지**라서 소스상 완료 검출과 실 이미지 의미가 불일치. 실제 완료 증거를 추가하기 전까지 단독 클릭 금지.
- `*_reference.png`는 버튼 템플릿으로 전체 화면 검색하면 안 된다. 참조의 정해진 ROI/픽셀 특징을 사용하거나 fixture로만 활용.
- 인법 46장의 파일명 숫자 접두사는 선택 우선순위이며, 뒤쪽 이름의 `card/icon` 패턴은 서로 다른 판별 처리의 근거.
- 앱플레이어/해상도/스케일에 따른 일치율 임계값은 기존 소스의 값이 **후보**일 뿐 v0.1 검증 완료값이 아니다.

## 자세한 구조화 파일
- `data/screen_catalog.v0.1.json`: 자산별 역할·상태·인식 컨텍스트·주의점.
- `data/asset_manifest.v0.1.json`: 원본 경로·SHA256·파일 크기. `excluded` 항목은 본 ZIP에 복사되지 않음.
- `data/workflows.v0.1.json`: 화면 전이 stepId / 증거 / 동작.
- 카드·룰렛은 같은 프레임을 재사용한다. 모듈별로 중복 캡처하지 않는다.

## 이벤트 전용 에셋

- 신규 이벤트 PNG는 `assets/events/<event_id>/`에 격리하고 JSON 정의는 `data/events/`에 둔다.
- `date_changed.png`, `date_change_followup_close.png`는 v0.1의 필수 이미지다. 해당 에셋 SHA는 manifest에 원본 그대로 기록.
- 단순 이벤트 예시 `example_limited_time_popup.disabled.json`은 이미지가 존재하지 않고 미활성인 **템플릿**이다.
