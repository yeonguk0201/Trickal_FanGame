# 13주차 Frontend Flow 검증 — 2026-09-08

## 구현 범위

`Assets/Scenes/FrontendScene.unity`를 첫 번째 활성 Build Scene으로 유지하고 기존 전투 Scene인
`SampleScene.unity`의 이름·내용·GUID를 보존했다. Frontend에는 타이틀, 닉네임 등록과 홈 화면을
구성한다. 새 사용자는 타이틀의 `GAME START`에서 닉네임 등록으로, 저장 프로필은 홈으로 이동한다.
등록 성공도 같은 홈 전환 경로를 사용한다.

홈에는 게임 시작·스킬 강화·설정·나가기 버튼과 전용 임시 배경이 있다. 앞의 세 버튼은 후속 조각이
교체할 목적지 화면 골격과 뒤로가기에 연결하고, 나가기는 `Application.Quit`을 요청한다. 이 단계에서는
Game Scene을 로드하거나 Run을 생성하지 않는다.

Flow-3에서는 게임 시작 목적지를 실제 캐릭터 선택 화면으로 교체했다. `Assets/Characters`의 유효한
정의를 ID 순서로 직렬화하고 런타임에 카드 수만큼 UI를 만든다. 현재는 에르핀 1명이지만 개수는
코드에 고정하지 않는다. 선택 전 확인은 비활성이며, 선택 확정은 `characterId`를 한 번만 전달한다.
Game Scene 로드와 Run 생성은 Flow-4 전까지 수행하지 않는다.

Canvas Scaler는 1920×1080, Scale With Screen Size, Match Width Or Height 0.5다. 중앙 기준 프레임 안에
좌우 64·상하 54의 안전 여백을 적용한다. 타이틀 버튼은 280×64, 홈 버튼은 360×64이며 24px TMP와
Noto Sans KR을 사용한다. 비16:9 창에서도 16:9 프레임을 중앙에 유지한다. 최소 창 크기 제한과
해상도 설정 화면은 후속 설정 범위다.

## 자동 검증 결과

Unity 6000.3.22f1에서 기존 Editor를 유지하고 시스템 임시 폴더의 독립 복사본으로 실행했다.
최종 구성과 Play Mode 검증은 각각 종료 코드 0으로 끝났으며, 생성된 Scene·폰트와 홈 미리보기를
원본에 반영했다.

| 검증 | 결과와 근거 |
|---|---|
| Unity 컴파일 | 최종 코드 컴파일 성공, `error CS` 없음 |
| Setup 재실행 | 동일 프로세스에서 두 번 실행, Transform 수 동일, Frontend GUID 유지 |
| Game Scene 보존 | Setup 전후 UTF-8 원문 일치 |
| 시작 Scene | 첫 활성 Build Scene이 Frontend, Frontend·Game 각각 한 번만 포함 |
| 전투 격리 | 모든 비활성 자식까지 검사하여 프로필 등록용 `ApiClient` 외 전투·Run·Meta 컴포넌트와 월드 Renderer·Collider2D·Rigidbody2D 부재 확인 |
| UI 구성 | 루트 Canvas·EventSystem·Input System UI module 단일성, 일곱 버튼과 화면 직렬화 참조·TMP 글리프·Scaler·안전 여백 통과 |
| 레이아웃 | 1280×720, 1920×1080, 2560×1440, 1600×1200, 2560×1080에서 RectTransform 경계·TMP 필요 크기·안전 영역 통과 |
| 실패 경로 | 버튼 참조 누락, 중복 버튼, 전투 Collider 유입, 시작 Scene 역순을 각각 명시적으로 거부 |
| Flow-0 Play Mode | 초기 포커스, 클릭·Submit 이벤트, 반복 요청 후 Scene·오브젝트 수 유지 통과 |
| Flow-2 구성 | 홈 전용 배경, 네 버튼, 세 목적지·뒤로가기 참조와 명시적 키보드 순환 통과 |
| Flow-2 Play Mode | 타이틀 선행, 새 사용자 닉네임 경로, 저장 프로필 홈 진입, 세 목적지·뒤로가기, 나가기 요청과 전투 격리 통과 |
| Flow-3 데이터·구성 | 유효 캐릭터 정의, 고유 안정 ID, 300×420 카드 템플릿, 동적 목록과 다섯 화면 크기 경계 통과 |
| Flow-3 Play Mode | 에르핀 1명 목록, 미선택 확인 차단, 선택 표시·포커스, 단일 확정 ID, 뒤로가기·재진입 초기화와 Run 미생성 통과 |
| Flow-4 구성 | Frontend 전환기와 Game Bootstrap 단일성, 두 Scene GUID, 기존 Game 참조와 전체 Frontend 회귀 통과 |
| Flow-4 Play Mode | 로컬 사용자·에르핀 전달, Game Scene 단일 이동, UUID `clientRunId`, 새 seed·첫 방·빈 인벤토리와 종료 전 Run API 미호출 통과 |
| HUD-1 구성 | 전용 Overlay Canvas, 1920×1080 Scaler, 좌상단 500×128 영역, 비활성 40×40 SP 템플릿과 입력 비가로채기 통과 |
| HUD-1 상태 경계 | 피해·회복 HP 갱신, SP 획득·소비, 최대 SP 3→4 슬롯 재구성, 저학년 스킬 SP 0/1 사용 가능 상태 통과 |
| HUD-1 회귀 | Setup 두 번 실행 후 계층 수·Game Scene GUID 유지와 Flow-4 구성 회귀 통과 |

최종 구성 배치 로그:

```text
Week 13 Frontend setup complete. Title, profile and home flow are isolated from the Game Scene.
Week 13 Frontend setup complete. Title, profile and home flow are isolated from the Game Scene.
Week 13 layout passed: 1280 x 720; TMP bounds and safe area.
Week 13 layout passed: 1920 x 1080; TMP bounds and safe area.
Week 13 layout passed: 2560 x 1440; TMP bounds and safe area.
Week 13 layout passed: 1600 x 1200; TMP bounds and safe area.
Week 13 layout passed: 2560 x 1080; TMP bounds and safe area.
Week 13 Frontend verification passed: scene isolation, build entry, references, scaler, safe area and screen layouts.
Week 13 batch verification passed: setup twice, stable GUID, unchanged Game Scene, invalid-state rejection.
Exiting without the bug reporter. Application will terminate with return code 0
```

Play Mode 검증 로그:

```text
Week 13 Flow 2 Play Mode verification passed: title-first flow, nickname route, saved-profile Home, four actions, back focus and combat isolation.
```

Play Mode 배치에서는 Unity Editor 검색 인덱서의 `UnityEditor.Search.SearchDatabase.EnumerateAll`에서 `ArgumentOutOfRangeException`이 별도로 발생했다. 타이틀 검증은 이후 위 성공 로그까지 실행됐다. 따라서 Console 전체 오류 0개를 달성했다고 주장하지 않는다. 첫 복사본 import의 2D Tooling 긴 경로 오류와 최초 샌드박스 Package Manager IPC 차단도 있었으며, 최종 구성 배치는 종료 코드 0으로 완료됐다.

## 렌더링 미리보기

실제 타이틀 UI를 별도 미리보기 Scene의 Camera로 렌더링했다. 두 이미지에서 로고·한글 부제·버튼의 잘림이나 겹침은 관찰되지 않았다. PNG는 배치·폰트 확인용이며 실제 Overlay Canvas의 입력·Game View 또는 EXE 실행 증거는 아니다.

- [1280×720 미리보기](./title-1280x720.png)
- [1920×1080 미리보기](./title-1920x1080.png)

Flow-2 홈 화면은 같은 UI를 1280×720과 1920×1080으로 렌더링했다. 전용 배경, 환영 문구와
네 버튼이 중앙 16:9 콘텐츠 영역 안에 유지된다.

- [1280×720 홈 미리보기](./home-1280x720.png)
- [1920×1080 홈 미리보기](./home-1920x1080.png)

## 수동 확인 (2026-09-08 완료)

- [x] `Assets/Scenes/FrontendScene.unity`를 열고 Game View의 Fixed Resolution을 1280×720, 1920×1080으로 각각 설정하여 Play한다. 로고·부제·버튼이 잘리지 않고 중앙에 유지되어야 한다.
- [x] 첫 버튼 포커스의 3px 테두리와 2px 바깥 여백, 마우스 호버의 2px 테두리, Tab 포커스 복귀, Enter/Space 실행을 확인한다. 반복 입력 후에도 준비 중 안내만 표시되고 Room·Player·Enemy·RunSession이 생기지 않아야 한다.
- [x] Build Profiles에서 첫 활성 Scene이 Frontend인지 확인한다.
- [x] Development Build EXE를 실행하여 전투 화면 노출 없이 Frontend 타이틀과 `GAME START`가
  가장 먼저 표시되는 것을 확인한다. (사용자 확인, 2026-09-08)

위 자동·Game View·EXE 검증을 근거로 13주차 `Flow-0`을 완료 처리한다. 닉네임 등록 이후 홈
이동과 전체 Frontend 흐름은 `Flow-1` 이후 조각에서 별도로 검증한다.

## Flow-2 수동 확인 (2026-09-08 완료)

- [x] 저장 프로필 상태에서 타이틀의 `GAME START`를 누르면 홈이 표시되고 닉네임 환영 문구가 맞는지 확인했다.
- [x] 1280×720과 1920×1080 Game View에서 네 버튼의 마우스 호버, 위·아래 키 순환과 Enter/Space 실행을 확인했다.
- [x] 게임 시작·스킬 강화·설정이 각각 올바른 제목의 목적지 골격을 열고 `뒤로`가 홈 첫 버튼으로 포커스를 돌리는지 확인했다.
- [x] Development Build에서 전투 화면이 배경에 보이지 않고 `나가기`가 프로세스를 종료하는지 확인했다.

자동 검증과 사용자 수동 확인을 근거로 `Flow-2`를 완료 처리한다. (사용자 확인, 2026-09-08)

## Flow-3 자동 확인 (2026-09-09 완료)

- [x] 홈의 게임 시작에서 에르핀 1명이어도 캐릭터 선택 화면을 거친다.
- [x] 캐릭터 정의 배열 길이를 UI 코드에 고정하지 않고 유효 데이터마다 카드를 생성한다.
- [x] 미선택 확인은 이벤트를 만들지 않고, 에르핀 선택 후 확인은 `erpin`을 정확히 한 번 전달한다.
- [x] 확인 전후로 Scene은 하나이며 `RunSession`은 생성되지 않는다.
- [x] 뒤로가기와 재진입 시 선택·확인 상태를 초기화하고 홈 포커스를 복원한다.

화면의 최종 미감은 임시 도형 단계이므로 정식 UI 에셋 교체 범위에 포함한다. 기능 완료 근거는
구성·레이아웃·Play Mode 자동 검증이며, Flow-4의 Game Scene 이동과 새 Run 초기화는 포함하지 않는다.

사용자가 Development Build에서 캐릭터 화면, 선택 전 확인 차단과 포커스를 확인했고 별도 이상이
없음을 확인했다. (사용자 확인, 2026-09-09)

## Flow-4 자동 확인 (2026-09-09 완료)

- [x] Frontend 확정 이벤트가 저장된 로컬 `userId`·닉네임과 `erpin`을 단일 전환 컨텍스트로 만든다.
- [x] Game Scene Bootstrap이 컨텍스트를 한 번만 소비하고 이전 전투 Scene 선택 UI를 비활성화한다.
- [x] 기존 진행 조회를 한 번 수행한 뒤 UUID `clientRunId`와 새 seed로 Run을 한 번 시작한다.
- [x] 같은 seed의 생성 그래프, 1층 1번 시작방과 빈 플레이어 인벤토리를 확인한다.
- [x] 중복 Run 시작을 거부하고 종료 전 `POST /runs` 호출 수가 0인지 확인한다.
- [x] Flow-4 Setup 두 번 후 두 Scene GUID와 구성 요소 단일성, Flow-0~3 Play Mode 회귀를 확인한다.

Flow-4는 기존 Backend·DTO·Database·Web 계약을 바꾸지 않는다. Backend Run 레코드는 기존 계약대로
사망 또는 최종 클리어 결과를 저장하는 Flow-5 경로에서만 생성한다.

## HUD-1 자동·Build 확인 (2026-09-09 완료)

- [x] 실제 플레이어 `Health`, `PlayerSP`, `PlayerSkill`이 하나의 `GameHudView`에 연결된다.
- [x] HP 피해·회복이 수치와 게이지를 즉시 갱신하고 변화 방향별 피드백을 활성화한다.
- [x] SP 획득·소비가 활성 슬롯 수에 반영되고 최대 SP 증가 시 같은 템플릿으로 슬롯을 다시 구성한다.
- [x] 저학년 스킬 사용 가능 표시는 실제 `PlayerSkill.CanCast` 조건 및 SP 0/1 상태와 일치한다.
- [x] HUD 그래픽과 비활성 GraphicRaycaster가 전투 입력을 가로채지 않는다.
- [x] HUD-1 Setup 두 번, Game Scene GUID·Transform 수 유지와 Flow-4 회귀 검증이 통과한다.

Unity 6000.3.22f1의 독립 검증 복사본에서 최종 배치 검증이 종료 코드 0으로 완료됐다. 원본
프로젝트를 직접 연 Unity 인스턴스와의 잠금 충돌을 피하기 위한 복사본이며, 생성된 Game Scene
HUD 구성만 원본에 반영했다. Backend·Database·API DTO·Web 계약 변경은 없다.

사용자가 직접 빌드하여 현재·최대 HP와 SP 표시, 피해·회복, 최대 HP 변경 및 관련 상태 갱신이
정상임을 확인했다. 별도 문제는 발견되지 않았다. (사용자 확인, 2026-09-09)

정식 아이콘·애니메이션 교체는 후속 UI 에셋 범위다.

## Profile-1~3 검증 (2026-09-08 완료)

- Backend 사용자 Repository·Service·DTO 관련 테스트 45개와 전체 테스트 125개가 통과했다.
- Backend 빌드가 통과했다.
- 신규 User와 `erpin` 초기 진행의 트랜잭션 생성, 동일 요청 멱등 응답, 프로필 ID 충돌과 닉네임
  충돌, DB Unique 경쟁 충돌 후 결과 분류를 검사했다.
- Unity Play Mode에서 첫 등록 요청을 네트워크 실패로 만든 뒤 같은 `clientProfileId`로 재시도해
  성공하고, `userId`·닉네임 저장, 홈 진입과 저장 프로필 재진입을 검사했다.
- 사용자가 Unity와 EXE 재실행 후 기존 `clientProfileId`·`userId`·닉네임 유지, 신규 User 미생성,
  타이틀에서 홈 진입을 확인했다.

개발용 `RESET LOCAL PROFILE`은 `clientProfileId`를 유지한다. 기존 닉네임을 지운 뒤 다른 대소문자의
닉네임을 등록하면 대소문자 중복이 아니라 프로필 멱등성 충돌이 발생하는 것이 정상이다.

Setup·검증·PNG 출력 메뉴와 배치 명령은 [개발 환경 설정 §17](../../05-development-setup.md)에 기록했다. 원본 Editor의 현재 Scene과 저장하지 않은 변경은 자동으로 전환하거나 덮어쓰지 않았다.

## 영향 범위

- Runtime: `FrontendLayout`, `FrontendTitleView`, `FrontendHomeView`, `FrontendCharacterSelectionView`, `FrontendCharacterCardView`, `FrontendRunLauncher`, `RunLaunchContext`, `GameRunBootstrap`, `RunSession`, `PlayerProgressClient`, `FrontendStartButton`, `GameHudView`, `Health`, `PlayerSkill`.
- Editor: `Week13FrontendSetup`, `Week13FrontendVerification`, `Week13Flow4Setup`, `Week13Flow4Verification`, `Week13Hud1Setup`, `Week13Hud1Verification` 및 Play Mode 배치 진입점.
- Assets: Frontend Scene, Game Scene Bootstrap, Noto Sans KR OTF·OFL·TMP 에셋, TMP Essential Resources, Build Scene 순서.
- Backend 사용자 등록의 동시 Unique 충돌 복구와 관련 테스트를 보강했다. Prisma 스키마, Web,
  Unity DTO, seed와 기존 영문 ID는 변경하지 않았다. Game Scene 이동·Run 초기화, 스킬 강화의 실제
  데이터 기능은 후속 조각으로 남겼다.
