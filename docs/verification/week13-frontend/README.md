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
| HUD-2 구성 | 우하단 300×128 영역, 72×72 스킬 아이콘 두 개, SPACE·Q 키와 입력 비가로채기 통과 |
| HUD-2 상태 경계 | 실제 저학년·고학년 사용 조건, 사용 중, 쿨타임 중간값·종료 경계와 방사형 표시 통과 |
| HUD-2 일시정지·회귀 | scaled game time 정지 중 쿨타임 유지, Setup 두 번, Scene GUID·계층 유지와 HUD-1·Flow-4 회귀 통과 |
| HUD-3A 구성 | 좌하단 288×112 영역, 48×48 슬롯, 8px 간격의 5열×2행과 입력 비가로채기 통과 |
| HUD-3A 상태 경계 | 빈 상태, 중복 획득 스택 2, 11종의 최초 획득 순서 10개와 `+1`, 재구성 상태 유지 통과 |
| HUD-3A 회귀 | Setup 두 번 실행 후 Scene GUID·계층 유지와 HUD-2·Flow-4 회귀 통과 |
| HUD-3B 구성 | 중앙 720×120 영역, 단일 TMP 메시의 28px 이름·20px 설명, 동적 설명 글리프와 입력 비가로채기 통과 |
| HUD-3B 상태 경계 | 실제 획득 즉시 표시, 1.49초 유지·1.5초 종료, 연속 획득 큐와 최대 스택 거부 무알림 통과 |
| HUD-3B 시간·회귀 | unscaled 표시 진행·기존 전투 시간 불변, Setup 두 번과 HUD-3A·HUD-2·Flow-4 회귀 통과 |
| HUD-3C 구성 | 중앙 1120×760 패널, 1024×600 세로 스크롤 목록과 전체 화면 입력 차단 통과 |
| HUD-3C 상태 경계 | 빈 상태, 12종 전체 획득 순서, 11번째 항목 2스택과 모든 이름·효과 설명 통과 |
| HUD-7A 일시정지·회귀 | timeScale·전투 입력 루프 정지와 상태 보존, 중첩 거부, Setup 두 번과 HUD-3B·HUD-3A·HUD-2·Flow-4 회귀 통과 |
| HUD-4A 구성 | 우상단 220×180 영역, 비색상 `P` 현재 표식, 생성 격자 기반 방·연결선과 입력 비가로채기 통과 |
| HUD-4A 생성 그래프 | 고정 seed의 실제 격자 좌표와 상·하·좌·우 상대 위치, 현재 방의 모든 직접 연결 수 일치 통과 |
| HUD-4A 전환·회귀 | 인접방 진입 후 현재 표식 이동, Setup 두 번, Scene GUID·계층 유지와 HUD-3C·HUD-7A·Flow-4 회귀 통과 |
| HUD-4B 누적 탐색 | 방문방과 그 인접방만 공개, 이동 후 이전 공개방 유지와 실제 격자 상대 위치 통과 |
| HUD-4B 상태 경계 | `?` 일반 미확인, `P` 현재, `V` 실제 진입 후 클리어, 탐색 범위의 `S/T/B` 특수 문 공개 통과 |
| HUD-4B 맞춤·회귀 | 공개 범위 증가 시 220×180 패널 자동 맞춤, Setup 두 번과 HUD-4A·HUD-3C·HUD-7A·Flow-4 회귀 통과 |
| HUD-4B 사용자 확인 | 보물방 진입 전 `T`만 표시되고 실제 진입 뒤부터 `V`가 표시되는 경계 통과 |

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

## HUD-2 자동 확인 (2026-09-09 완료)

- [x] 우하단 300×128 안전 영역에 저학년·고학년 72×72 임시 아이콘과 `SPACE`·`Q` 키를 표시한다.
- [x] 저학년은 `PlayerSkill.CanCast`, 고학년은 `PlayerUltimate.IsReadyAt`과 같은 조건으로 준비 상태를 표시한다.
- [x] 고학년 돌진 종료부터 시작한 실제 쿨타임을 남은 초와 반시계 방사형 오버레이로 표시한다.
- [x] 쿨타임 중간값과 종료 경계에서 숫자·채움·사용 가능 상태가 실제 스킬 값과 일치한다.
- [x] `Time.timeScale = 0`인 동안 `Time.time`과 HUD의 남은 쿨타임 표시가 함께 정지한다.
- [x] 모든 그래픽이 전투 입력을 가로채지 않고 Setup 재실행 후 중복 오브젝트를 만들지 않는다.
- [x] Game Scene GUID를 유지하고 HUD-1 구성과 Flow-4 참조 회귀 검증을 통과한다.

Unity 6000.3.22f1에서 `Week13Hud2Verification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
`Week 13 HUD-2 verification passed`와 `Week 13 HUD-2 batch verification passed`가 출력됐다.
Backend·Database·API DTO·Web 계약 변경은 없다. 정식 아이콘과 전체 화면의 시각 확인은 후속
UI 에셋 및 통합 Build 검증 범위다.

사용자가 Development Build에서 두 스킬의 사용 가능 상태와 고학년 쿨타임 표시가 정상 동작함을
확인했다. 별도 문제는 발견되지 않았다. (사용자 확인, 2026-09-09)

## HUD-3A 자동·Build 확인 (2026-09-09 완료)

- [x] 실제 플레이어 `PlayerInventory`를 좌하단 `GameArtifactHudView`에 연결한다.
- [x] 최초 획득 순서의 고유 아티팩트를 48×48 임시 아이콘으로 5개 × 2줄 표시한다.
- [x] 같은 아티팩트의 반복 획득은 새 슬롯 없이 기존 우하단 배지의 스택 수를 갱신한다.
- [x] 고유 아티팩트 11종에서 앞의 10종과 `+1`을 표시한다.
- [x] 재구성 후에도 순서·스택·초과 수가 유지되며 전체 획득 기록은 기존 중복 순서를 보존한다.
- [x] 모든 그래픽이 전투 입력을 가로채지 않고 Setup 재실행 후 중복 오브젝트를 만들지 않는다.
- [x] Game Scene GUID를 유지하고 HUD-2 구성과 Flow-4 참조 회귀 검증을 통과한다.

Unity 6000.3.22f1에서 `Week13Hud3AVerification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
종료 코드 0과 `Week 13 HUD-3A verification passed`, `Week 13 HUD-3A batch verification passed`를
확인했다. Backend·Database·API DTO·Web 계약과 기존 안정 `itemId`의 의미는 변경하지 않았다.
정식 아이콘, 획득 알림과 일시정지 전체 목록은 각각 후속 UI 에셋·HUD-3B·HUD-3C 범위다.

사용자가 Development Build에서 좌하단 아티팩트 아이콘 표시와 같은 아티팩트 반복 획득 시 스택
배지 갱신이 정상 동작함을 확인했다. 별도 문제는 발견되지 않았다. (사용자 확인, 2026-09-09)

## HUD-3B 자동·Build 확인 (2026-09-10 완료)

- [x] 실제 `PlayerInventory.ItemAcquired` 이벤트가 이름과 효과 계약 기반 한 줄 설명을 즉시 표시한다.
- [x] 모든 유효 아티팩트 효과 설명의 TMP 글리프가 Build용 폰트 아틀라스에 포함된다.
- [x] 첫 알림은 1.49초까지 유지되고 1.5초 경계에서 숨겨진다.
- [x] 빠르게 연속 획득하면 현재 알림을 덮어쓰지 않고 다음 알림을 큐에서 1.5초 동안 표시한다.
- [x] 최대 스택으로 거부된 획득은 알림을 만들지 않는다.
- [x] unscaled 시간으로 표시가 진행되며 기존 timeScale과 EventSystem은 바뀌지 않는다.
- [x] 단일 TMP 메시가 이름과 설명을 정확히 2줄로 생성하고 선호 높이가 중앙 720×120 패널 안에 들어간다.
- [x] 중앙 패널의 모든 Graphic과 CanvasGroup이 전투 입력을 가로채지 않는다.
- [x] Setup 두 번 뒤 Scene GUID·계층 수를 유지하고 HUD-3A·HUD-2·Flow-4 회귀를 통과한다.

Unity 6000.3.22f1에서 `Week13Hud3BVerification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
종료 코드 0과 `Week 13 HUD-3B verification passed`, `Week 13 HUD-3B batch verification passed`를
확인했다. 설명은 새 데이터 계약 없이 기존 `ItemEffectEntry`를 짧은 한국어 문구로 변환하므로
Backend·Database·API DTO·Web과 안정 `itemId`에는 영향이 없다. 정식 프레임·전환 애니메이션은
후속 UI 에셋 범위이며 전체 목록·스택·설명은 HUD-3C에서 구현한다.

두 차례 Development Build에서 이름 문구의 잘림은 없었지만 별도 TMP 오브젝트로 만든 효과 설명은
표시되지 않았다. 두 번째 확인으로 글리프 추가만으로 해결되지 않음을 확인했으며, 별도 설명
렌더러를 제거하고 빌드에서 렌더링되는 이름 TMP 하나에 28px 이름과 보조색 20px 설명을 2줄로
함께 생성하도록 변경했다. 보정 후 실제 TMP 메시의 두 줄·글자 수·선호 높이를 포함한 배치 검증이
종료 코드 0으로 통과했다. 수정된 Development Build에서 아티팩트 이름 아래 보조색 효과 설명이
정상 표시되고 문구가 잘리지 않음을 사용자가 확인했다. (사용자 확인, 2026-09-10)

## HUD-3C·HUD-7A 자동·Build 확인 (2026-09-10 완료)

- [x] `Esc` 일시정지 기반이 scaled game time과 플레이어 이동·공격·스킬 입력 루프를 함께 멈춘다.
- [x] 전투 컴포넌트를 끄지 않아 진행 중 행동 상태를 보존하고 복귀 시 이전 timeScale을 복원한다.
- [x] 다른 팝업이 이미 timeScale을 0으로 만든 상태에서는 중첩해 열리지 않는다.
- [x] 중앙 1120×760 패널의 세로 스크롤 목록에 전체 획득 순서를 표시한다.
- [x] 빈 상태와 12종 전체 목록, 11번째 항목의 2스택, 각 이름·효과 설명을 검사한다.
- [x] Setup 두 번 뒤 Scene GUID·계층 수를 유지하고 HUD-3B·HUD-3A·HUD-2·Flow-4 회귀를 통과한다.

Unity 6000.3.22f1에서 `Week13Hud3CVerification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
종료 코드 0과 `Week 13 HUD-3C verification passed`, `Week 13 HUD-3C batch verification passed`를
확인했다. 상세 스탯·포커스 제한과 홈 나가기 확인은 HUD-7B·7C에 남겼다. Backend·Database·API
DTO·Web 계약과 안정 `itemId`에는 영향이 없다.

사용자가 Development Build에서 일시정지·복귀, 전투 시간 정지, 플레이어 입력 루프 정지·복원,
아티팩트 이름을 포함한 전체 상세 목록 표시가 모두 정상임을 확인했다. 별도 표시 누락이나 진행
이상은 발견되지 않았다. (사용자 확인, 2026-09-10)

## HUD-4A 자동 확인 (2026-09-10 완료)

- [x] 우상단 220×180 안전 영역에서 실제 생성 격자 위의 현재 방을 `P` 표식과 밝은 외곽선으로 강조한다.
- [x] 현재 방의 모든 직접 연결을 실제 생성 그래프의 상·하·좌·우 방향대로 인접 방과 연결선으로 표시한다.
- [x] 고정 seed로 생성한 층의 실제 `GridPosition`과 표시 상대 좌표가 일치한다.
- [x] 인접 방 진입 이벤트 후 실제 격자 위치의 현재 방 표식과 연결 관계를 즉시 다시 구성한다.
- [x] HUD-4A 단계에서는 방문·클리어·특수방 정체와 층 이름을 섞지 않고 후속 범위로 분리했다.
- [x] 모든 그래픽이 전투 입력을 가로채지 않고 Setup 재실행 후 중복 오브젝트를 만들지 않는다.
- [x] Game Scene GUID를 유지하고 HUD-3C·HUD-7A 구성과 Flow-4 참조 회귀 검증을 통과한다.

Unity 6000.3.22f1에서 `Week13Hud4AVerification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
종료 코드 0과 `Week 13 HUD-4A verification passed`, `Week 13 HUD-4A batch verification passed`를
확인했다. 미니맵은 기존 `RunProgress.GeneratedGraph`와 `RoomChanged`를 소비하므로 Backend·Database·
API DTO·Web 계약과 안정 ID에는 영향이 없다. 방문·클리어·발견한 특수방 표식은 HUD-4B에서 추가했고,
층 이름과 진입 알림은 HUD-4C에 남겼다.

## HUD-4B 자동·수동 확인 (2026-09-10 완료)

- [x] 방문한 모든 방과 각 방문방에 직접 연결된 미방문 방을 현재 층 지도에 누적한다.
- [x] 현재 방이 이동해도 이전에 밝혀진 방과 연결선이 사라지지 않는다.
- [x] 일반 미방문 방은 `?`, 현재 방은 `P`, 실제 진입한 클리어 방은 별도 `V` 배지로 색 외의 상태를 제공한다.
- [x] 인접한 미방문 보물방·보스방은 특수 문과 일치하는 `T`·`B`로 표시한다.
- [x] 전투 없는 보물방의 선클리어 상태는 실제 방문과 분리하며, 진입 전에는 `T`만 표시하고 `V`를 숨긴다.
- [x] 방문한 시작방·보물방·보스방은 현재 방을 떠난 뒤 각각 `S`·`T`·`B` 임시 아이콘으로 유지한다.
- [x] 클리어·방문 상태 변경 알림으로 현재 미니맵을 즉시 갱신하고 중복 상태 기록은 추가 갱신을 만들지 않는다.
- [x] 공개 그래프의 격자 상대 위치를 보존하면서 220×180 미니맵 영역을 넘으면 자동 축소한다.
- [x] Setup 두 번 뒤 Scene GUID·계층 수를 유지하고 HUD-4A·HUD-3C·HUD-7A·Flow-4 회귀를 통과한다.

Unity 6000.3.22f1에서 `Week13Hud4BVerification.SetupAndVerifyBatch`를 실행했으며 컴파일 오류 없이
종료 코드 0과 `Week 13 HUD-4B verification passed`, `Week 13 HUD-4B batch verification passed`를
확인했다. 기존 `RoomRunState`에 멱등 변경 알림과 방문을 수반하지 않는 선클리어 상태를 추가하고
생성 그래프·안정 ID 계약은 유지했으므로 Backend·Database·API DTO·Web에는 영향이 없다. 수동 확인에서
발견된 보물방 진입 전 `T V` 표시는 `T`만 남도록 회귀 검증을 보강했다. 층 이름과 진입 알림은
HUD-4C에 남겼다.

사용자가 Unity 실행 화면에서 보물방 진입 전에는 `T`만 표시되고 실제 진입 후에만 `V`가 추가되는
것을 확인했다. 누적 지도와 특수 문 표식에도 별도 이상이 없었다. (사용자 확인, 2026-09-10)

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

- Runtime: `FrontendLayout`, `FrontendTitleView`, `FrontendHomeView`, `FrontendCharacterSelectionView`, `FrontendCharacterCardView`, `FrontendRunLauncher`, `RunLaunchContext`, `GameRunBootstrap`, `RunSession`, `PlayerProgressClient`, `FrontendStartButton`, `GameHudView`, `GameArtifactHudView`, `ArtifactHudSlotView`, `GameArtifactAcquisitionToastView`, `ArtifactPauseListEntryView`, `GamePauseArtifactView`, `ArtifactEffectDescription`, `GameMinimapView`, `MinimapRoomMarkerView`, `RoomRunState`, `Health`, `PlayerSkill`, `PlayerUltimate`, `PlayerInventory`.
- Editor: `Week13FrontendSetup`, `Week13FrontendVerification`, `Week13Flow4Setup`, `Week13Flow4Verification`, `Week13Hud1Setup`, `Week13Hud1Verification`, `Week13Hud2Setup`, `Week13Hud2Verification`, `Week13Hud3ASetup`, `Week13Hud3AVerification`, `Week13Hud3BSetup`, `Week13Hud3BVerification`, `Week13Hud3CSetup`, `Week13Hud3CVerification`, `Week13Hud4ASetup`, `Week13Hud4AVerification`, `Week13Hud4BSetup`, `Week13Hud4BVerification` 및 Play Mode 배치 진입점.
- Assets: Frontend Scene, Game Scene Bootstrap, Noto Sans KR OTF·OFL·TMP 에셋, TMP Essential Resources, Build Scene 순서.
- Backend 사용자 등록의 동시 Unique 충돌 복구와 관련 테스트를 보강했다. Prisma 스키마, Web,
  Unity DTO, seed와 기존 영문 ID는 변경하지 않았다. Game Scene 이동·Run 초기화, 스킬 강화의 실제
  데이터 기능은 후속 조각으로 남겼다.
