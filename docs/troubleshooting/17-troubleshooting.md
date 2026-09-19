# 트러블슈팅 기록

## 1. 목적

이 문서는 구현 중 실제 Editor·Build·런타임에서 발견한 문제와 해결 근거를 기록한다. 단순한 오류
메시지 모음이 아니라 증상, 조사 과정, 실패한 가설, 실제 원인, 수정 방법과 재발 방지 검증을 함께
남긴다.

새 항목은 다음 순서를 따른다.

1. 재현 환경과 관찰 가능한 증상
2. 정상으로 확인된 부분과 영향 범위
3. 조사한 가설과 반증 근거
4. 확인된 원인
5. 적용한 해결
6. 자동·수동 검증 결과
7. 다른 기능에도 적용할 재발 방지 기준

## 최근 기록

- [새마음금고 마지막 점프가 중간에 끊기는 문제](./saemaeum-vault-final-jump-cutoff.md)

---

## 2. Unity Build에서 TMP 두 번째 줄이 표시되지 않는 문제

### 2.1 발생 범위

- 기능: HUD-3B 아티팩트 획득 알림
- 환경: Unity 6000.3.22f1 Development Build
- 발생일: 2026-09-10
- 관련 화면: `Assets/Scenes/SampleScene.unity`의 `Artifact Acquisition Toast`

아티팩트를 획득하면 중앙 패널과 28px 아이템 이름은 정상 표시됐지만, 이름 아래에 배치한 20px
효과 설명은 표시되지 않았다. 패널과 이름은 잘리지 않았고 전투 입력과 시간도 정상 동작했다.

### 2.2 당시 구조

이름과 설명을 서로 다른 `TextMeshProUGUI` 오브젝트로 구성했다.

```text
Artifact Acquisition Toast
├── Artifact Name
└── Artifact Description
```

런타임에서는 `PlayerInventory.ItemAcquired` 이벤트를 받아 두 텍스트의 `text` 값을 각각 설정했다.
자동 검증은 다음 항목을 통과하고 있었다.

- Scene의 이름·설명 참조가 모두 연결됨
- 설명 문자열이 비어 있지 않고 줄바꿈을 포함하지 않음
- 이름 28px, 설명 20px와 비가로채기 설정이 직렬화됨
- 1.5초 표시, 연속 획득 큐와 최대 스택 거부 동작

하지만 이 검증은 설명 컴포넌트에 문자열이 들어갔는지만 확인했으며, Player Build에서 실제 렌더
메시가 만들어졌는지는 확인하지 않았다.

### 2.3 첫 가설과 실패한 수정

첫 가설은 동적으로 만드는 효과 설명의 한글 글리프가 TMP Build 아틀라스에 없다는 것이었다.
`ItemDefinition`의 모든 효과 설명을 미리 생성하여 `Frontend Noto Sans KR` 폰트에 추가하고,
검증기에서 `TMP_FontAsset.HasCharacters`를 확인하도록 보강했다.

글리프 검사는 통과했지만 다음 Development Build에서도 이름만 표시됐다. 따라서 글리프 누락은
재발 방지상 보완할 문제였지만, 이번 미표시 증상의 충분한 원인은 아니었다.

### 2.4 확인된 원인

별도 `Artifact Description` TMP 렌더러 경로가 Build에서 최종적으로 가시 텍스트를 내지 못했다.
정확한 TMP 내부 조건 하나를 별도 최소 프로젝트로 분리하지는 않았지만, 작은 고정 높이와
`Ellipsis` overflow를 함께 사용해 문자열과 글리프가 정상이어도 줄 전체가 컬링될 수 있는 구조였다.

핵심 판단 근거는 다음과 같다.

- 같은 CanvasGroup과 패널의 이름 TMP는 Build에서 정상 렌더링됐다.
- 설명 참조, 문자열과 필요한 글리프는 자동 검사에서 모두 존재했다.
- 설명 영역 확대와 글리프 추가 후에도 별도 설명 TMP는 Build에서 표시되지 않았다.
- 이름과 설명을 하나의 충분히 큰 TMP 메시로 합친 뒤에는 실제 2줄 메시 자동 검증과 Development
  Build 수동 확인이 모두 통과했다.

### 2.5 해결

글리프와 머티리얼 경로가 이미 정상임이 확인된 이름 TMP 하나에서 이름과 설명을 함께 렌더링하도록
구조를 바꿨다. 기존 `Artifact Description` 오브젝트는 Setup 재실행 시 제거한다.

```text
Artifact Acquisition Toast
└── Artifact Name  ← 이름과 설명을 함께 렌더링
```

런타임 메시지는 다음 형식을 사용한다.

```text
<b>{아이템 이름}</b>
<size=20><color=#C2D6EB>{효과 설명}</color></size>
```

- 기본 이름 크기: 28px
- 효과 설명 크기: 20px
- 설명 색상: `#C2D6EB`
- 패널 크기: 720×120
- 메시지 영역: 680×104
- 줄바꿈: 이름과 설명 사이의 명시적 한 번만 허용
- overflow: `Overflow`

효과 설명은 새 데이터 계약을 추가하지 않고 기존 `ItemEffectEntry`에서 생성한다. 따라서 Backend,
Database, API DTO, Web과 안정 `itemId`에는 영향이 없다.

### 2.6 검증 결과

`Week13Hud3BVerification.SetupAndVerifyBatch`에서 다음을 확인했다.

- 파싱된 TMP 텍스트에 아이템 이름과 효과 설명이 모두 존재한다.
- 하나의 TMP 메시가 정확히 두 줄을 생성한다.
- 생성된 글자 수가 이름 길이보다 커 설명 글자도 메시 생성에 포함된다.
- `preferredHeight`가 104px 메시지 영역을 넘지 않는다.
- 모든 유효 아티팩트 설명 글리프가 Build용 TMP 폰트에 존재한다.
- Setup 두 번 실행 후 별도 설명 오브젝트가 남거나 중복 생성되지 않는다.
- Scene GUID와 계층 수가 유지되고 HUD-3A·HUD-2·Flow-4 회귀가 통과한다.
- Unity 배치 실행이 종료 코드 0으로 완료된다.

수정 후 Development Build에서 아이템 이름 아래에 더 작은 효과 설명이 표시되고 문구가 잘리지
않음을 사용자가 확인했다.

### 2.7 재발 방지 기준

- 런타임 TMP 검증은 `text` 값과 `HasCharacters`만으로 완료하지 않는다.
- 중요한 동적 텍스트는 `ForceMeshUpdate` 후 파싱 결과, `textInfo.characterCount`, 줄 수와
  `preferredHeight`를 함께 검사한다.
- 이름과 설명처럼 항상 동시에 나타나고 사라지는 짧은 텍스트는 한 TMP의 rich text로 구성하는
  방식을 우선 검토한다.
- 작은 고정 높이에 `Ellipsis`를 사용할 때는 한글 폰트의 실제 line height를 기준으로 여유를 두고,
  Player Build에서 줄 전체 컬링 여부를 확인한다.
- 자동 검증이 통과해도 동적 텍스트의 최종 완료에는 실제 Development Build 화면 확인을 포함한다.

### 2.8 관련 파일

- 런타임 알림: `game/Assets/Scripts/Frontend/GameArtifactAcquisitionToastView.cs`
- 효과 설명 생성: `game/Assets/Scripts/Frontend/ArtifactEffectDescription.cs`
- Scene 구성: `game/Assets/Editor/Week13Hud3BSetup.cs`
- 자동 검증: `game/Assets/Editor/Week13Hud3BVerification.cs`
- 상세 검증 기록: `docs/verification/week13-frontend/README.md`

---

## 3. HUD-4C 검증 예외와 Build 층 진입 알림 미표시

### 3.1 발생 범위

- 기능: HUD-4C 층 이름과 층 진입 알림
- 환경: Unity 6000.3.22f1 Editor 및 Development Build
- 발생일: 2026-09-10

Editor에서 HUD-4C Setup을 실행한 뒤 층 이름은 보였지만, 검증 메뉴에서
`InvalidOperationException: Sequence contains more than one element`가 발생했다. 앞서 실행한 Build에서는
층 진입 알림이 보이지 않았다.

### 3.2 조사와 원인

검증 예외의 Editor 로그는 `Week13Hud4CVerification.VerifyRuntimeState`의 테스트 복제본 생성 직후를
가리켰다. 검증기가 Scene의 단일 `GameFloorNameView`를 복제한 뒤 다시 `Single()`로 검색하여 원본과
복제본 두 개를 동시에 찾은 것이 원인이었다. 이는 HUD 런타임 로직이나 Scene 중복 생성 오류가 아니다.

Build 파일과 실행 로그의 시각도 비교했다. 확인한 Player 실행 로그는 HUD-4C Setup으로
`SampleScene.unity`가 저장되기 전에 생성되어, 해당 Build에는 `Floor Entry Announcement`가 포함되지
않았다. 현재 Scene에는 단일 HUD-4C 컴포넌트와 텍스트·CanvasGroup 참조가 정상 직렬화되어 있다.

HUD-3B의 과거 문제와 달리 HUD-4C는 하나의 충분한 높이를 가진 한 줄 TMP 렌더러를 사용한다. 따라서
이번 증상은 별도 TMP 줄 컬링과 관련이 없다.

### 3.3 적용한 해결

- 검증 시작 시 원본 `GameFloorNameView`를 한 번만 보관하고, 테스트 복제 후 다시 `Single()`을 호출하지 않는다.
- 알림 TMP는 `ForceMeshUpdate` 후 파싱 문자열, 글자 수, 한 줄 여부와 선호 높이를 검사한다.
- 초기화 순서가 달라도 `Start`에서 현재 층을 재확인하여 첫 층 알림을 한 번 보장한다.
- HUD-4C Setup으로 Scene을 저장한 뒤 새 Development Build를 만들어 확인한다.

### 3.4 재발 방지 기준

- Scene 단일성 검사는 테스트 복제본을 만들기 전에 수행하고 원본 참조를 보관한다.
- Editor에서 새 Setup을 적용한 뒤에는 Scene 저장 시각보다 나중에 생성된 Build로 확인한다.
- 동적 TMP 알림은 문자열 대입뿐 아니라 실제 메시 생성과 영역 내 높이를 자동 검증한다.

### 3.5 검증 결과

검증기 수정 후 스크립트 컴파일이 오류 없이 통과했다. 사용자가 HUD-4C Setup이 저장된 새
Development Build에서 미니맵 층 이름과 화면 중앙보다 위에 표시되는 층 진입 알림을 확인했으며,
기존 Build의 미표시 문제가 재현되지 않았다. (사용자 확인, 2026-09-10)

---

## 4. Wide 방 카메라 추적 중 캐릭터가 떨려 보이는 문제

### 4.1 증상과 원인

- 기능: Room-5 Wide `24 × 9` 방의 가로 카메라 추적
- 환경: Unity 6000.3.22f1 Play Mode
- 발생일: 2026-09-15

Small·Basic 방과 방 전환은 정상이었지만, Wide 방에서 카메라가 플레이어를 따라 움직일 때 캐릭터가
짧게 끊기며 떨려 보였다. 플레이어는 `FixedUpdate`에서 `Rigidbody2D` 속도로 이동하고 카메라는
`LateUpdate`에서 매 렌더 프레임 위치를 읽는데, 플레이어 Rigidbody의 보간이 꺼져 있어 물리 프레임
사이의 계단식 위치가 카메라 이동 중 눈에 띈 것이 원인이었다.

### 4.2 해결과 검증

Room-5 Setup에서 플레이어 `Rigidbody2D.interpolation`을 `Interpolate`로 설정하고 자동 검증에서
해당 설정을 필수 조건으로 확인한다. 수정 후 Room-0~5 Unity 배치 검증이 종료 코드 0으로 통과했으며,
사용자가 Small·Basic·Wide 방을 다시 플레이해 Wide 카메라 이동과 캐릭터 표시가 자연스러워졌음을
확인했다.

### 4.3 재발 방지 기준

- `FixedUpdate`로 이동하는 물리 객체를 렌더 프레임 카메라가 추적할 때 Rigidbody 보간을 확인한다.
- 카메라 추적 문제는 이동 속도를 먼저 바꾸지 말고 물리·렌더 업데이트 주기와 보간 설정을 점검한다.
- 고정 카메라 방뿐 아니라 실제로 카메라가 움직이는 대형 방에서 수동 체감 검증을 수행한다.

## 5. 가로문 전환 직후 화면 가장자리에 남는 플레이어 사각형

Wide/Large에서 Basic/Small/Tall로 가로문을 통과하면 넘어온 쪽 화면 가장자리에 흰 사각형이 잠깐
나타났다. 불투명 카메라 배경과 URP 히스토리 초기화로 해결되지 않았다. GIF와 실제 Play Mode의
첫 전환 프레임을 비교하니 `Rigidbody2D.position=(14.15,0)`인데 보간된 표시 Transform은
`(10.95,0,0)`에 남아 있었다. 새 카메라의 왼쪽 경계 약 `10.67` 안에 이전 플레이어가 들어온 것이다.

`RoomGraphController.MovePlayer`에서 보간을 잠시 끄고 물리·표시 위치를 동시에 이동한 후 원래
보간 모드를 복원했다. 효과 없는 URP 히스토리 초기화는 제거했다. 동일 Play Mode 테스트가 수정 전
첫 전환에서 3.2유닛 불일치로 실패하고, 수정 후 24개 좌우 왕복 전환의 첫 5프레임 검사를 통과했다.
평상시 보간은 유지하므로 Wide 카메라 추적 떨림 대응과 양립한다. 물리 객체의 순간이동은 Edit Mode의
물리 좌표 검사만으로 검증하지 않고 실제 Play Mode에서 렌더 직전 Transform도 함께 검사한다. 이후
사용자가 동일한 Wide/Large 가로문 왕복 경로에서 잔상이 사라졌음을 Game View로 최종 확인했다.
