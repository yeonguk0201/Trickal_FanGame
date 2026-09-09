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
