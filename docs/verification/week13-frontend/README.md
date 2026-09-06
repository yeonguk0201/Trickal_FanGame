# 13주차 Frontend 타이틀 검증 — 2026-09-07

## 구현 범위

`Assets/Scenes/FrontendScene.unity`를 첫 번째 활성 Build Scene으로 추가했다. 기존 전투 Scene인 `SampleScene.unity`의 이름·내용·GUID는 보존했다. Frontend에는 배경 전용 Camera, Overlay Canvas, EventSystem과 타이틀 UI만 구성한다. `GAME START`는 이번 단계에서 준비 중 안내를 표시하며 Scene 전환·Run 생성·Backend 호출을 하지 않는다.

Canvas Scaler는 1920×1080, Scale With Screen Size, Match Width Or Height 0.5다. 중앙 기준 프레임 안에 좌우 64·상하 54의 안전 여백을 적용한다. 로고는 72px TMP, 버튼은 280×64와 24px TMP이며 한글·영문은 Noto Sans KR을 사용한다. 비16:9 창에서도 16:9 프레임을 중앙에 유지한다. 최소 창 크기 제한과 해상도 설정 화면은 후속 설정 범위다.

## 자동 검증 결과

Unity 6000.3.22f1에서 기존 Editor를 유지하고 `game/Temp/Week13Verification`의 독립 복사본으로 실행했다. 최종 구성 검증 로그는 해당 경로의 `verify.log`, Play Mode 로그는 `play.log`다. 생성된 Scene·메타·폰트를 원본에 복사했으며 검증 코드와 원본 코드의 일치를 확인했다.

| 검증 | 결과와 근거 |
|---|---|
| Unity 컴파일 | 최종 코드 컴파일 성공, `error CS` 없음 |
| Setup 재실행 | 동일 프로세스에서 두 번 실행, Transform 수 동일, Frontend GUID 유지 |
| Game Scene 보존 | Setup 전후 UTF-8 원문 일치 |
| 시작 Scene | 첫 활성 Build Scene이 Frontend, Frontend·Game 각각 한 번만 포함 |
| 전투 격리 | 모든 비활성 자식까지 검사하여 전투·Run·Network·Meta 컴포넌트와 월드 Renderer·Collider2D·Rigidbody2D 부재 확인 |
| UI 구성 | Canvas·EventSystem·Input System UI module·버튼 단일성, 직렬화 참조·TMP 글리프·Scaler·안전 여백 통과 |
| 레이아웃 | 1280×720, 1920×1080, 2560×1440, 1600×1200, 2560×1080에서 RectTransform 경계·TMP 필요 크기·안전 영역 통과 |
| 실패 경로 | 버튼 참조 누락, 중복 버튼, 전투 Collider 유입, 시작 Scene 역순을 각각 명시적으로 거부 |
| Play Mode | 초기 포커스, 클릭·Submit 이벤트, 반복 요청 후 안내 표시와 Scene·오브젝트 수 유지 통과 |

최종 구성 배치 로그:

```text
Week 13 Frontend setup complete. Frontend is build index 0; SampleScene remains the Game Scene.
Week 13 Frontend setup complete. Frontend is build index 0; SampleScene remains the Game Scene.
Week 13 layout passed: 1280 x 720; TMP bounds and safe area.
Week 13 layout passed: 1920 x 1080; TMP bounds and safe area.
Week 13 layout passed: 2560 x 1440; TMP bounds and safe area.
Week 13 layout passed: 1600 x 1200; TMP bounds and safe area.
Week 13 layout passed: 2560 x 1080; TMP bounds and safe area.
Week 13 Frontend verification passed: scene isolation, build entry, references, scaler, safe area and title layout.
Week 13 batch verification passed: setup twice, stable GUID, unchanged Game Scene, invalid-state rejection.
Exiting without the bug reporter. Application will terminate with return code 0
```

Play Mode 검증 로그:

```text
Week 13 Play Mode verification passed: initial focus, click/submit, repeated start, combat isolation.
```

Play Mode 배치에서는 Unity Editor 검색 인덱서의 `UnityEditor.Search.SearchDatabase.EnumerateAll`에서 `ArgumentOutOfRangeException`이 별도로 발생했다. 타이틀 검증은 이후 위 성공 로그까지 실행됐다. 따라서 Console 전체 오류 0개를 달성했다고 주장하지 않는다. 첫 복사본 import의 2D Tooling 긴 경로 오류와 최초 샌드박스 Package Manager IPC 차단도 있었으며, 최종 구성 배치는 종료 코드 0으로 완료됐다.

## 렌더링 미리보기

실제 타이틀 UI를 별도 미리보기 Scene의 Camera로 렌더링했다. 두 이미지에서 로고·한글 부제·버튼의 잘림이나 겹침은 관찰되지 않았다. PNG는 배치·폰트 확인용이며 실제 Overlay Canvas의 입력·Game View 또는 EXE 실행 증거는 아니다.

- [1280×720 미리보기](./title-1280x720.png)
- [1920×1080 미리보기](./title-1920x1080.png)

## 수동 확인 (2026-09-07 완료)

- [x] `Assets/Scenes/FrontendScene.unity`를 열고 Game View의 Fixed Resolution을 1280×720, 1920×1080으로 각각 설정하여 Play한다. 로고·부제·버튼이 잘리지 않고 중앙에 유지되어야 한다.
- [x] 첫 버튼 포커스의 3px 테두리와 2px 바깥 여백, 마우스 호버의 2px 테두리, Tab 포커스 복귀, Enter/Space 실행을 확인한다. 반복 입력 후에도 준비 중 안내만 표시되고 Room·Player·Enemy·RunSession이 생기지 않아야 한다.
- [x] Build Profiles에서 첫 활성 Scene이 Frontend인지 확인한다.
- [ ] (선택) EXE를 빌드·실행하여 타이틀이 가장 먼저 표시되는지 확인한다.

Setup·검증·PNG 출력 메뉴와 배치 명령은 [개발 환경 설정 §17](../../05-development-setup.md)에 기록했다. 원본 Editor의 현재 Scene과 저장하지 않은 변경은 자동으로 전환하거나 덮어쓰지 않았다.

## 영향 범위

- Runtime: `FrontendLayout`, `FrontendTitleView`, `FrontendStartButton`.
- Editor: `Week13FrontendSetup`, `Week13FrontendVerification` 및 같은 파일의 Play Mode 배치 진입점.
- Assets: Frontend Scene, Noto Sans KR OTF·OFL·TMP 에셋, TMP Essential Resources, Build Scene 순서.
- Backend·Web·Prisma·DTO·seed·기존 영문 ID는 변경하지 않았다. 닉네임 등록·프로필 저장·홈 화면은 구현하지 않았다.
