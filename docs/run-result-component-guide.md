# Run 결과 UI 컴포넌트 적용 가이드

화면에서 배치할 부모 Transform을 각 생성 메서드에 전달한다. 글꼴이 필요한 컴포넌트는
`Week13FrontendSetup.FontPath`의 에셋을 사용한다. 각 컴포넌트의 루트 RectTransform은
부모 중앙을 기준으로 생성되며, 화면에서 루트의 앵커·위치·스케일을 지정한다.

| 컴포넌트 | 생성 | 표시 데이터 |
| --- | --- | --- |
| 승리 배지 | `RunResultBadgeView.CreateVictory(parent, portrait)` | 캐릭터 Sprite, 초록색 아우라의 `VICTORY` |
| 패배 배지 | `RunResultBadgeView.CreateDefeat(parent, portrait)` | 패배 초상화 Sprite, 갈색 원과 붉은 아우라의 `DEFEAT` |
| RUN 기록·경험치 | `RunResultStageView.CreateRecord(parent, font)` | `SetRecord(request)`, `SetExperience(level, current, required)`, `SetExperienceSource(text, opacity)` |
| 레벨업 | `RunResultStageView.CreateLevelUp(parent, font)` | `SetLevelUp(startLevel, endLevel)` |
| 스킬 포인트 | `RunResultStageView.CreateSkillPoints(parent, font)` | `SetSkillPoints(gained, balance)` |
| 획득 아티팩트 | `RunResultStageView.CreateArtifacts(parent, font)` | `SetRewards(payload)` |
| 최종 결과 | `RunResultStageView.CreateSummary(parent, font)` | `SetSummary(payload, progress)` |
| 결과 버튼 | `RunResultButton.Create(parent, font, label, primary)` | 반환된 Button의 `onClick`에 화면 동작 연결 |

- 에르핀 패배 초상화는 `UserArtwork.Load("run-result-erpin-defeat")`로 가져온다. 배지는 이름 없이
  표시된다. 원형 마스크가 캐릭터 Sprite를 자르며, `SetPortrait(sprite)`로 그림을 교체할 수 있다.
- 승패 글자는 각각의 PNG 아틀라스에서 알파벳마다 독립 Sprite와 Image로 구성된다. 자식
  `Letter 1 V`처럼 글자별로 구분되며, 등장 시 시간차로 튀어오른 뒤 작게 흔들린다. 글자 뒤의
  아우라와 파티클은 `Time.unscaledDeltaTime`으로 움직여 결과 화면이 정지 상태여도 재생된다.
  `Outcome Wordmark`의 RectTransform에서 글자 전체를, `Outcome Effects`에서 아우라와
  파티클을 따로 배치한다. 같은 배지를 재사용할 때는 `SetOutcome(cleared)`로 연출을 다시 시작한다.
- `SetCrestLayout(position, scale)`은 배지 루트 기준으로 테두리와 원형 마스크를 함께 움직인다.
  `SetPortraitLayout(offset, scale)`은 마스크 중앙을 기준으로 초상화만 조절한다. 크기는
  각각 1이 기본이며, 테두리 크기를 바꿔도 초상화 크기는 유지된다.
- 배지에 말풍선을 표시하려면 `SetDialogue(font, text)`를 호출한다. 빈 문구는 말풍선을 숨긴다.
  미리보기에서 복사한 `Dialogue Bubble` 보정값은 `SetDialogue` 호출 뒤 적용한다.
- 단계 컴포넌트는 비활성 상태로 생성된다. 데이터를 넣고 표시할 때 `view.gameObject.SetActive(true)`를 호출한다.
- 경험치 문구는 `SetExperienceSource(text, opacity)`로 투명도를 0 → 1 → 0으로 바꾼다. 아티팩트는
  등급별 수량과 합계를 전달하고, 게이지는 같은 항목의 진행에 맞춰 `SetExperience`로 갱신한다.
  레벨업 컴포넌트는 레벨이 올랐을 때 사용한다.
- 레벨업의 `LevelUpCrown`, `LevelUpWordmark`, `LevelUpLeftLeaf`, `LevelUpRightLeaf`는 각각 독립 Image다. 뒤쪽
  `LevelUpLight`는 Unity UI에서 별도로 그리며, 레벨업 단계가 보이는 동안 약 3.3초 주기로 옅은 중심광 위에 광선이 은은하게 선명해졌다가 흐려진다. 광선은 끝으로 갈수록 투명해지고 양옆 아래로도 짧게 퍼진다. 왕관과 문구는 독립적으로 표시된다.
- 최종 결과와 버튼은 각각 원하는 부모에 만든다. 기존 Button을 사용할 때는 `RunResultButton.Apply(button, primary)`로
  스타일을 적용한다. `FrontendStartButton`의 흰 테두리가 제거되며 포커스는 버튼 면 색으로 표시된다.
- 배경을 사용할 경우 `UserArtwork.Load("run-result-background")`로 Sprite를 가져와 화면 배경 Image에 지정한다.

Unity Play에서 승패별 원과 아우라 색, 글자별 순차 등장, 파티클, 원형 마스크를 확인한다. 버튼에 마우스를
올리거나 키보드로 포커스를 이동했을 때 흰 사각 테두리 없이 면 색이 바뀌는지도 확인한다.

## 현재 게임에 적용된 배치

`FrontendRunResultView`는 1920 × 1080 기준의 ‘결과창 시안’ 프리셋을 적용한다. 승리·패배 초상화와
말풍선, 각 단계, 최종 결과 버튼은 프리셋의 값을 사용한다. 아티팩트 수량의 보정은 슬롯을 채운
뒤 첫 두 슬롯에 적용한다. 레벨업 단계는 3.3초 맥동 한 주기를 표시한다.

## 미리보기에서 복사한 배치값

결과 단계와 그 안의 요소는 각각 부모 기준 위치와 배율을 가진다. 미리보기의 Unity C# 출력에서
`Place`는 공통 화면 부모 아래의 배치를 지정하고, `OffsetLayout`은 기본 컴포넌트 배치에서 바뀐
요소에 보정값을 더한다. 보정값은 컴포넌트를 생성하고 데이터를 채운 다음 한 번 적용한다.
아티팩트 아이콘과 수량은 해당 목록의 표시 순서로 적용되며, 없는 슬롯의 보정은 건너뛴다.
