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
| Flow-3 데이터·구성 | 유효 캐릭터 정의, 고유 안정 ID, 세로형 320×520 카드 템플릿, 동적 목록과 다섯 화면 크기 경계 통과 |
| Flow-3 Play Mode | 에르핀 1명 목록, 미선택 확인 차단, 선택 표시·포커스, 단일 확정 ID, 뒤로가기·재진입 초기화와 Run 미생성 통과 |
| Flow-4 구성 | Frontend 전환기와 Game Bootstrap 단일성, 두 Scene GUID, 기존 Game 참조와 전체 Frontend 회귀 통과 |
| Flow-4 Play Mode | 로컬 사용자·에르핀 전달, Game Scene 단일 이동, UUID `clientRunId`, 새 seed·첫 방·빈 인벤토리와 종료 전 Run API 미호출 통과 |
| Skill-1 조회·표시 | 스킬 강화용 캐릭터 선택, 로딩·빈 상태·조회 실패, 레벨·경험치·포인트·두 스킬 레벨 표시 통과 |
| Skill-2 강화·오류 | 목표 레벨 요청, 요청 중 중복 차단, 성공 스냅샷 즉시 갱신, 포인트 부족·최대·충돌·네트워크 오류 분류 통과 |
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

채움 Sprite 보정 후 플레이어가 피해를 받을 때 HP 게이지가 실제 현재 HP 비율에 맞춰 줄어드는 것을
Unity 실행 화면에서 다시 확인했다. (사용자 재확인, 2026-09-11)

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

## HUD-4C 스크립트·Build 확인 (2026-09-10 완료)

- [x] 미니맵의 작은 머리글에 현재 층을 `요정의 숲 · N층` 형식으로 상시 표시한다.
- [x] 첫 층과 다른 층에 진입할 때 같은 층 이름을 화면 중앙보다 180px 위에 표시한다.
- [x] 층 진입 알림은 unscaled 시간 기준 약 1.5초 뒤 숨겨지고 전투 입력을 가로채지 않는다.
- [x] 같은 층의 다른 방으로 이동할 때 알림을 다시 시작하거나 방 이름·번호를 표시하지 않는다.
- [x] 현재 숫자 층 이름을 유지하면서 추후 필요한 층만 고유 이름으로 대체할 수 있다.
- [x] Game Scene에 `Floor Entry Announcement`와 단일 `GameFloorNameView`가 저장되고 실제 `RunProgress`를 참조한다.

런타임과 Editor 검증 스크립트를 포함한 `Assembly-CSharp`·`Assembly-CSharp-Editor` 컴파일이 오류 없이
통과했다. 최초 검증 메뉴의 `Sequence contains more than one element`는 테스트 복제본을 만든 뒤
Scene 단일 검색을 반복한 검증기 문제였으며, 원본 참조를 복제 전에 보관하도록 수정했다. 자세한
원인과 재발 방지 기준은 [트러블슈팅 §3](../../17-troubleshooting.md#3-hud-4c-검증-예외와-build-층-진입-알림-미표시)에 기록했다.

사용자가 HUD-4C Setup 이후 새로 만든 Development Build에서 미니맵 층 이름과 층 진입 알림이
정상 표시되며 위치와 지속 시간이 의도에 맞는 것을 확인했다. 별도 방 이름·번호는 표시되지 않았다.
(사용자 확인, 2026-09-10)

## HUD-5 자동·수동 확인 (2026-09-10 완료)

- [x] 하단 중앙 720×64 안전 영역에 현재 방의 활성 보스 이름, 현재·최대 HP와 페이즈를 표시한다.
- [x] 실제 `Health` 피해 이벤트가 HP 수치와 가로 게이지를 즉시 갱신한다.
- [x] `BossController`의 실제 현재·전체 페이즈가 바뀌면 표시도 즉시 갱신한다.
- [x] 보스가 활성화되기 전, 처치된 뒤와 일반 방으로 복귀한 뒤에는 HUD를 숨기고 이전 보스를 구독 해제한다.
- [x] 모든 그래픽이 전투 입력을 가로채지 않으며 Setup 두 번 뒤 계층 수와 Game Scene GUID를 유지한다.
- [x] HUD-4C와 Flow-4 구성·참조 회귀 검증을 통과한다.

Unity 6000.3.22f1에서 `Week13Hud5Verification.SetupAndVerifyBatch`를 실행해 종료 코드 0과
`Week 13 HUD-5 verification passed`, `Week 13 HUD-5 batch verification passed`를 확인했다. 현재
보스 전투에는 패턴 페이즈 전환이 없으므로 임의 규칙을 추가하지 않고 실제 상태인 `페이즈 1 / 1`을
표시한다. 이후 선정된 보스 구현이 `BossController`의 표시 이름과 페이즈 상태를 갱신하면 같은 HUD가
이를 소비한다. Unity 런타임 상태만 확장했으며 Backend·Database·API DTO·Web 계약과 안정 ID에는
영향이 없다.

이후 실제 플레이 재확인에서 현재·최대 HP 수치는 바뀌지만 게이지가 줄지 않는 시각 회귀가 발견됐다.
원인은 `Filled Image`에 Sprite가 없어 Unity가 `fillAmount`를 렌더링에 적용하지 않은 것이었다. HUD-1,
HUD-5, HUD-6B Setup이 공통 Unity 내장 임시 Sprite를 지정하도록 보정했고, 검증기는 Sprite가 없는
채움 이미지를 실패 처리한다. 보정 후 보스가 피해를 받을 때 하단 HP 게이지가 실제 현재 HP 비율에
맞춰 줄어드는 것을 Unity 실행 화면에서 확인했다. (사용자 재확인, 2026-09-11)

## HUD-6·HUD-6B 자동·수동 확인 (2026-09-11 완료)

- [x] 런타임에 생성된 일반 적과 보스의 실제 피해 결과만 월드 TMP 숫자로 표시한다.
- [x] 플레이어 피해·회복은 월드 숫자를 만들지 않고 기존 고정 HP 바의 변화 피드백만 사용한다.
- [x] 숫자는 영향받은 대상보다 위에서 시작해 0.8초 동안 0.65 월드 단위 상승하며 사라진다.
- [x] 동시에 발생한 숫자는 좌우 시작 위치를 번갈아 분산해 겹침을 줄인다.
- [x] 수명이 끝난 텍스트는 비활성 풀로 반환되고 다음 피해 때 새 인스턴스 없이 재사용된다.
- [x] 일반 적의 첫 피격은 실제 현재·최대 HP에 연결된 월드 체력바를 하나만 생성한다.
- [x] 일반 적 체력바는 풀 체력과 사망 시 숨고 재피격 시 기존 인스턴스를 다시 표시한다.
- [x] 보스에는 머리 위 체력바를 만들지 않고 기존 하단 전용 HUD만 유지한다.
- [x] Setup 두 번 뒤 계층 수와 Game Scene GUID를 유지하고 HUD-5·HUD-4C·Flow-4 회귀를 통과한다.

Unity 6000.3.22f1에서 `Week13Hud6Verification.SetupAndVerifyBatch`를 실행해 종료 코드 0과
`Week 13 HUD-6 verification passed`, `Week 13 HUD-6 batch verification passed`를 확인했다. 첫
실행에서 발견한 TMP 공통 타입의 정렬 순서 API 오류는 실제 월드 렌더러에 정렬 순서를 지정하도록
수정한 뒤 재검증했다. 사용자 확인에 따라 플레이어 월드 숫자를 제거하고 일반 적 체력바 경계를
추가한 검증도 통과했다. Unity 런타임 이벤트와 Game Scene 구성만 확장했으며 Backend·Database·API
DTO·Web 계약과 안정 ID에는 영향이 없다. 정식 서체 크기와 색상에 대한 실제 플레이 화면 확인은
후속 통합 Build 검증에서 조정할 수 있다.

사용자가 Unity 실행 화면에서 플레이어에게는 월드 숫자가 표시되지 않고 적에게만 피해 숫자가
표시되며, 일반 적 머리 위 체력바가 나타나는 것을 확인했다. 이후 실제 플레이 재확인에서 플레이어,
보스, 일반 적 모두 수치는 갱신되지만 Sprite가 없는 `Filled Image`는 채움 감소를 그리지 않는 문제를
발견했다. 세 Setup에 같은 Unity 내장 임시 Sprite를 지정하고 검증기에 Sprite 필수 조건을 추가했다.
따라서 정식 에셋 교체 시 상태 연결 코드는 유지한 채 이 Sprite와 프레임·색상·가독성만 교체하면 된다.
보정 후 플레이어, 일반 적, 보스가 피해를 받을 때 각 HP 게이지가 실제 현재 HP 비율에 맞춰 정상적으로
줄어드는 것을 Unity 실행 화면에서 확인했다. (사용자 재확인, 2026-09-11)

## HUD-7B 자동 확인 (2026-09-11 완료)

- [x] 일시정지 메뉴가 실제 `PlayerStats`의 공격력·공격속도·이동속도·치명타율·치명타 피해·투사체·관통을 표시한다.
- [x] 평상시 HUD `GraphicRaycaster`는 꺼져 있고 일시정지 중에만 켜진다.
- [x] 메뉴가 열리면 `게임으로 돌아가기`에 첫 포커스를 두고, 포커스가 패널 밖으로 나가면 다시 내부로 제한한다.
- [x] 메뉴를 닫으면 열기 전 선택, 이전 timeScale과 평상시 입력 상태를 복원한다.
- [x] 다른 취소 불가 화면이 이미 timeScale을 0으로 만든 상태에서는 일시정지를 중첩하지 않는다.
- [x] 1120×760 중앙 팝업 안에 300×560 상세 스탯과 700×520 아티팩트 목록을 분리해 배치한다.
- [x] Setup 두 번 뒤 계층 수와 Game Scene GUID를 유지하고 HUD-3C·Flow-4 회귀를 통과한다.

Unity 6000.3.22f1에서 `Week13Hud7BVerification.SetupAndVerifyBatch`를 실행해 종료 코드 0과
`Week 13 HUD-7B verification passed`, `Week 13 HUD-7B batch verification passed`를 확인했다.
최초 제한 실행은 Unity Package Manager와 Licensing Client가 작업 폴더 밖의 상태에 접근하지 못해
중단됐으며, 허용된 배치 실행에서 전체 Library 재생성과 스크립트 컴파일 후 정상 통과했다. 이 조각은
Unity Scene과 런타임 UI만 변경하므로 Backend·Database·API DTO·Web과 안정 ID에는 영향이 없다.
Setting-1C의 상세 스탯 상시 HUD 토글과 HUD-7C의 홈 나가기 확인은 후속 범위로 유지한다.

## Flow-5 Run 결과·순차 연출 자동 확인 (2026-09-11 완료)

- [x] 사망과 클리어가 같은 Run 종료 경로에서 요청을 한 번 고정하고 Frontend 전체 화면 결과로 이동한다.
- [x] 불투명한 1920×1080 결과 배경이 이전 전투, HUD와 일시정지 팝업을 완전히 가린다.
- [x] 왼쪽 캐릭터 반응·말풍선은 유지하고 오른쪽 단일 영역에서 Run 기록, 경험치, 레벨 변화,
  스킬 포인트, 아티팩트와 최종 요약을 순서대로 교체한다.
- [x] 경험치는 Backend가 확정한 시작·최종 진행과 Lv.1~19 경계를 사용하고, 첫 구간 뒤 속도를 높이며
  경계마다 게이지 펄스와 짧은 효과음을 낸다.
- [x] 여러 레벨 상승도 `LEVEL UP!`과 시작·최종 레벨을 한 번만 표시하고 레벨 상승이 없으면 생략한다.
- [x] 저장 실패 중에는 성장 결과를 확정 표시하지 않고, 재시도 시 같은 요청 객체와 `clientRunId`를 사용한다.
- [x] 첫 확인은 진행 중 경험치 구간을 즉시 끝내고 다음 확인은 남은 연출을 건너뛰되 최종 데이터는 유지한다.
- [x] 다시 도전은 캐릭터 선택으로, 홈으로는 홈으로 복귀하고 시간·포커스·입력 상태를 복원한다.
- [x] 복귀 후 Frontend에는 `RunSession`, `RunProgress`, `PlayerInventory`가 없어 이전 Run 상태가 남지 않는다.
- [x] Setup 두 번 뒤 Scene GUID·계층 단일성을 유지하고 Flow-4 회귀가 통과한다.

Unity 6000.3.22f1에서 `Week13Flow5Verification.SetupAndVerifyBatch`와
`Week13Flow5PlayVerification.RunBatch`가 각각 종료 코드 0으로 통과했다. Play Mode 검증은 실제
플레이어 사망으로 Run을 끝내고 첫 저장을 의도적으로 실패시킨 뒤 동일 요청 재시도, Lv.1→Lv.3
확정 결과, 연출 단축, 캐릭터 선택과 홈 복귀를 차례로 확인했다. Unity Scene과 런타임 표시 계층만
확장했고 Backend·Database·API DTO·Web 계약과 안정 ID는 변경하지 않았다. 캐릭터 반응 영역은
정식 일러스트·애니메이션 교체 전 텍스트 자리표시자다.

사용자가 실제 실행 화면에서 Flow-5 결과 저장과 순차 연출, 결과 화면의 조작과 복귀가 정상
동작하며 레벨 경계 효과음도 재생되는 것을 확인했다. 별도 기능 이상은 발견되지 않았다.
(사용자 수동 확인, 2026-09-11)

## Setting-1A 음량·Scene 오디오 자동 확인 (2026-09-13 완료)

- [x] 홈·전투 BGM과 UI 클릭음이 두 Scene의 공용 오디오 컨트롤러에 연결된다.
- [x] BGM은 Streaming, 짧은 클릭음은 PCM·Decompress On Load 임포트 설정을 사용한다.
- [x] 전체 음량과 BGM·SFX 채널 음량의 곱이 슬라이더 변경 즉시 실제 Source에 적용된다.
- [x] 활성 타이틀 버튼이 공용 클릭음에 연결되고 한 번 클릭할 때 한 번 호출된다.
- [x] `Frontend → Game → Frontend` 전환에서 컨트롤러 인스턴스를 유지하며 홈·전투 BGM을 교체한다.
- [x] `LocalSettings.Load()` 뒤에도 저장한 세 음량과 실제 출력 음량을 유지한다.
- [x] Setup을 두 번 실행해도 Scene별 컨트롤러 1개·AudioSource 2개와 기존 Scene GUID를 유지한다.

Unity 6000.3.22f1에서 `Week13Setting1AVerification.SetupAndVerifyBatch`와
`Week13Setting1APlayVerification.RunBatch`가 모두 종료 코드 0으로 통과했다. 적용 음원의 출처와
교체용 안정 경로는 `docs/audio-sources.md`에 기록했다. Unity 런타임·Scene·오디오 에셋만 변경했으며
Backend·Database·API DTO·Web 계약과 안정 ID에는 영향이 없다.

사용자가 실제 실행 화면에서 홈·전투 BGM 전환과 UI 클릭 효과음을 청감 확인하고, 세 음량
슬라이더의 즉시 적용과 재실행 후 저장값 유지가 정상 동작함을 확인했다. 별도 이상은 발견되지
않았다. (사용자 수동 확인, 2026-09-13)

## Setting-1B 화면 모드·해상도 자동 확인 (2026-09-14 완료)

- [x] 설정 UI는 1280×720·1920×1080·2560×1440만 제공하고 목록 밖 크기를 저장 단계에서 거부한다.
- [x] 1280×720 창 모드 선택이 즉시 로컬 상태에 반영되고 `LocalSettings.Load()` 뒤에도 유지된다.
- [x] 지원하는 세 16:9 크기는 전체 뷰포트를 사용하며, 4:3과 울트라와이드 입력은 중앙 16:9 영역과 상하·좌우 여백으로 계산된다.
- [x] Frontend·Game 카메라와 두 UI 기준 프레임이 Scene 전환 뒤에도 같은 중앙 16:9 규칙을 사용한다.
- [x] 빌드는 1920×1080 테두리 없는 전체 화면을 기본값으로 하며 창 크기 조절을 허용하지 않는다.
- [x] Setup을 두 번 실행해도 Scene별 비율 제어기와 UI 프레임 제어기가 하나이고 Scene GUID를 유지한다.
- [x] 펼친 드롭다운의 세 옵션이 40px 행과 20px 라벨로 마스크 안에 표시된다.

Unity 6000.3.22f1에서 `Week13Setting1BVerification.SetupAndVerifyBatch`와
`Week13Setting1BPlayVerification.RunBatch`가 모두 종료 코드 0으로 통과했다. 첫 검증은 지원 목록,
저장·재로드, 비율 계산, Scene 단일성과 GUID를 검사했고, Play Mode 검증은 실제 설정 UI로
1280×720 창 모드를 고른 뒤 Game Scene까지 전환해 저장값과 중앙 HUD 구성을 확인했다. Unity
런타임·Scene·Player Settings만 변경했으며 Backend·Database·API DTO·Web 계약과 안정 ID에는
영향이 없다.

사용자가 실제 실행 화면에서 전체 화면·창 모드·세 해상도 적용이 정상임을 확인했다. 최초에는
드롭다운의 스트레치 폭이 중복 적용되어 옵션 라벨이 마스크 밖에 놓였고 빈 목록처럼 보였지만,
가로 추가 폭 제거와 TMP 목록 `CanvasGroup` 보정 후 Play Mode에서 세 옵션 라벨의 실제 생성을
검증했다. 이 보정으로 Setting-1B 수동 검증도 완료했다. (사용자 수동 확인, 2026-09-14)

## Setting-1C 상세 스탯 HUD 자동 확인 (2026-09-14 완료)

- [x] 설정 화면에 기본 꺼짐인 `상세 스탯 표시` 토글이 있고 선택을 로컬에 저장한다.
- [x] 저장한 선택은 `LocalSettings.Load()` 뒤에도 유지되며 현재 HUD 표시에도 즉시 반영된다.
- [x] 공격력·공격속도·이동속도·치명타율이 실제 `PlayerStats` 값과 일치한다.
- [x] 240×216 HUD가 좌측 안전 영역에 놓이고 중앙 전투 영역을 침범하지 않는다.
- [x] 배경 8%·텍스트 68%·임시 표식 55% 불투명도를 사용하며 Raycast를 차단하지 않는다.
- [x] Setup을 두 번 실행해도 Scene GUID와 구성 단일성을 유지하고 HUD-1 회귀가 통과한다.

Unity 6000.3.22f1에서 `Week13Setting1CVerification.SetupAndVerifyBatch`가 종료 코드 0으로
통과했다. 정식 아이콘 전에는 `A`·`AS`·`M`·`C` 색상 표식을 사용한다. Unity Scene과 런타임
설정·표시 계층만 변경했으며 Backend·Database·API DTO·Web 계약과 안정 ID에는 영향이 없다.
실제 전투 화면에서는 `설정 → HUD 설정 → 상세 스탯 표시`를 켜고 왼쪽 수치의 가독성·방해도를
확인한다.

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

## Skill-1~2 검증 (2026-09-14 완료)

- Unity 6000.3.22f1에서 `Week13SkillUpgradeVerification.SetupAndVerifyBatch` 종료 코드 0.
- Frontend Setup 2회 실행 뒤 Scene GUID 유지와 전체 Frontend 레이아웃·격리 회귀 통과.
- `320 × 520` 세로 카드의 데이터 기반 에르핀 선택과 강화 화면 진입 통과.
- 서버 진행 로딩·빈 상태·실패, 성공 표시와 재시도 상태 통과.
- 강화 요청 중 재입력 1회 차단, 목표 레벨과 성공 응답의 포인트·레벨 즉시 반영 통과.
- 포인트 부족·최대 레벨·진행 충돌·네트워크 실패의 전용 안내 통과.
- Backend 사용자 Controller·Service·Repository Jest 3 suites, 41 tests 통과.

Setup·검증·PNG 출력 메뉴와 배치 명령은 [개발 환경 설정 §17](../../05-development-setup.md)에 기록했다. 원본 Editor의 현재 Scene과 저장하지 않은 변경은 자동으로 전환하거나 덮어쓰지 않았다.

## 영향 범위

- Runtime: `DisplayAspectController`, `FrontendLayout`, `FrontendTitleView`, `FrontendHomeView`, `FrontendCharacterSelectionView`, `FrontendCharacterCardView`, `FrontendSkillUpgradeView`, `FrontendRunLauncher`, `FrontendRunResultView`, `ApiClient`, `ISkillProgressApiClient`, `SkillUpgradeDto`, `RunLaunchContext`, `RunResultContext`, `GameRunBootstrap`, `GameRunResultTransition`, `RunSession`, `PlayerProgressClient`, `FrontendStartButton`, `GameAudioController`, `LocalSettings`, `GameHudView`, `GameDetailedStatsHudView`, `GameArtifactHudView`, `ArtifactHudSlotView`, `GameArtifactAcquisitionToastView`, `ArtifactPauseListEntryView`, `GamePauseArtifactView`, `ArtifactEffectDescription`, `GameMinimapView`, `MinimapRoomMarkerView`, `GameFloorNameView`, `GameBossHudView`, `WorldCombatNumberPool`, `EnemyWorldHealthBarView`, `RoomRunState`, `Health`, `PlayerStats`, `PlayerSkill`, `PlayerUltimate`, `PlayerInventory`.
- Editor: `Week13FrontendSetup`, `Week13FrontendVerification`, `Week13SkillUpgradeVerification`, `Week13Flow4Setup`, `Week13Flow4Verification`, `Week13Flow5Setup`, `Week13Flow5Verification`, `Week13Setting1ASetup`, `Week13Setting1AVerification`, `Week13Setting1BSetup`, `Week13Setting1BVerification`, `Week13Setting1CSetup`, `Week13Setting1CVerification`, `Week13Hud1Setup`, `Week13Hud1Verification`, `Week13Hud2Setup`, `Week13Hud2Verification`, `Week13Hud3ASetup`, `Week13Hud3AVerification`, `Week13Hud3BSetup`, `Week13Hud3BVerification`, `Week13Hud3CSetup`, `Week13Hud3CVerification`, `Week13Hud4ASetup`, `Week13Hud4AVerification`, `Week13Hud4BSetup`, `Week13Hud4BVerification`, `Week13Hud4CSetup`, `Week13Hud4CVerification`, `Week13Hud5Setup`, `Week13Hud5Verification`, `Week13Hud6Setup`, `Week13Hud6Verification`, `Week13Hud7BSetup`, `Week13Hud7BVerification` 및 Play Mode 배치 진입점.
- Assets: Frontend Scene, Game Scene Bootstrap, Noto Sans KR OTF·OFL·TMP 에셋, TMP Essential Resources, Build Scene 순서.
- Backend 사용자 등록의 동시 Unique 충돌 복구와 관련 테스트를 보강했다. Prisma 스키마, Web,
  Unity DTO, seed와 기존 영문 ID는 변경하지 않았다. Game Scene 이동·Run 초기화, 스킬 강화의 실제
  데이터 기능은 후속 조각으로 남겼다.
