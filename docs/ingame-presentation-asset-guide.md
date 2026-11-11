# 인게임 연출 에셋 적용 가이드

PNG와 Unity Sprite 임포트 설정은 `game/Assets/Art/UI/InGamePresentation/`에 있다. UI의 `Image` 색은 흰색으로 두어 PNG 색이 그대로 보이게 한다. 배너와 스탯 아이콘은 원본 종횡비를 유지한다.

## 첫 입장 안내

- `map-intro-banner.png` (1536 × 320)는 양옆에 짙은 갈색 외곽선의 흰 깃털 날개가 달린 제목판이다. `Week13Hud4CSetup`에서 구성하는 `GameFloorNameView`의 안내용 `CanvasGroup` 아래에 배경 Image로 놓고, 기존 TMP 안내 문구를 중앙에 그린다. 글꼴은 기존 ONE Mobile POP TMP를 사용한다. 제목은 날개 안쪽의 중앙 70%에 맞춘다.
- 첫 진입 및 층 변경은 현재 `GameFloorNameView`의 알림 시점을 사용한다. 같은 층에서 방만 이동할 때는 반복 표시하지 않는다. 배너와 문구를 한 그룹으로 묶고 `Time.unscaledDeltaTime` 기준으로 0.25초 페이드인 → 1.0초 유지 → 0.25초 페이드아웃을 시작값으로 둔다. 배너의 Raycast Target은 끈다.

## 보상 선택 제단

- `reward-stone-altar.png` (512 × 416)를 보상 상호작용 오브젝트의 고정 시각 요소로 배치한다. 스프라이트 피벗은 하단 중앙이다. 보이는 돌탑 높이는 플레이어 스프라이트 높이의 약 75~85%로 시작한다.
- `reward-floating-gift.png` (384 × 384)를 별도 자식 SpriteRenderer에 붙인다. 제단 윗면 중앙 위에 놓고, 폭은 제단 너비의 약 50%로 시작한다. 선물 자식의 로컬 Y만 약 ±0.05 월드 유닛, 1.6초 주기로 움직인다. 가장 낮은 순간에도 선물 바닥이 윗면에서 살짝 떠 보이도록 기준 위치를 잡는다.
- 제단과 선물은 같은 정렬 그룹에 두고 선물의 그리기 순서를 제단보다 높인다. 기존 보상 상호작용 루트가 위치, 트리거 및 선택 UI 열기를 계속 맡는다. 선물 자식에는 콜라이더를 추가하지 않는다.

## 교주의 권능 HUD

| PNG | 연결 위치와 사용법 |
| --- | --- |
| `power-empty-slot.png` (216 × 216) | 현재 슬롯의 108 × 108 UI 영역에 얇은 테두리 배경으로 배치한다. 별도 Image를 두면 `UserArtwork.Apply`가 교체하는 권능 아이콘과 겹치지 않게 빈 상태에서만 켤 수 있다. |
| `power-gauge-track.png` (40 × 216) | 현재 20 × 108 세로 게이지의 바깥 Image에 연결한다. 기존 안쪽 마스크와 칸 레이아웃을 활용한다. |
| `power-gauge-fill.png` (32 × 64) | 충전된 칸에 배치하고 흰색 틴트를 적용한다. 기존 하늘색 `#73D1FF`를 사용하며 완충 칸도 같은 색을 유지한다. 미충전 칸은 기존 회색을 사용한다. |

`Week27Power1Setup`의 크기와 `GamePowerHudView`의 가변 칸 수 및 아래에서 위로 채우는 순서를 기준으로 한다. 이미지 연결 시 기존 틴트가 충전 PNG에 중복 적용되지 않는지 확인한다.

## 상세 스탯

현재 인게임 HUD의 순서는 `A` 공격력, `AS` 공격속도, `M` 이동속도, `C` 치명타 확률이다. `Week13Setting1CSetup`의 행과 `GameDetailedStatsHudView`의 값 순서를 맞춘다.

| 표시 | 적용 에셋 |
| --- | --- |
| 공격력 | 기존 공격 아이콘 |
| 공격속도 | `stat-attack-speed.png` (128 × 128), 활 |
| 이동속도 | `stat-move-speed.png` (128 × 128), 신발 |
| 치명타 확률 | 기존 치명타 표시 |
| 치명타 피해 | `stat-critical-damage.png` (128 × 128), 치명타 표시의 오른쪽 아래 절반가량을 칼로 겹친 아이콘 |

치명타 피해는 현재 인게임의 네 줄과 별도 값이다. 해당 행을 추가할 때는 일시정지 상세 스탯과 동일하게 `CriticalDamageMultiplier * 100`을 표시한다. 예를 들어 기본 1.5배는 150%다.

## ESC 일시정지 창

[확정 시안](art-references/pause-menu-approved.png)을 배치 기준으로 사용한다. 시안의 아티팩트와 숫자는 배치용 예시다.

| 구성 | 적용 에셋과 배치 |
| --- | --- |
| 창 프레임 | `Assets/Art/UI/Menu/UI_Menu_Panel_Main_9s.png`를 Sliced로 사용한다. 창 외곽은 투명하게 두고 게임 화면 위에 배치한다. |
| 제목 | `pause-title-plaque.png` (1024 × 208)를 상단 중앙에 원본 비율로 배치하고 `일시정지`를 TMP로 얹는다. |
| 현재 시드 | `pause-seed-plate.png` (768 × 112)를 제목 아래에 놓고 중앙에 `현재 시드`와 값을 배치한다. `RunProgress.HasRunSeed`가 참일 때 `RunProgress.RunSeed`를 표시하고, 초기화 전에는 `—`를 표시한다. |
| 본문 | 왼쪽 약 30%를 상세 스탯, 오른쪽 나머지를 찾은 아티팩트에 사용한다. 두 영역의 소제목 바탕은 기존 `UI_Menu_SectionRibbon_9s.png`를 사용한다. |
| 찾은 아티팩트 | 기존 `GamePauseArtifactView`의 스크롤 목록을 사용한다. 각 행에 기존 아이콘, 이름, 보유 수량, 효과 설명을 배치하고 행 바탕은 `UI_Menu_RoundedFill_9s.png`에 연한 복숭아색을 적용한다. |
| 하단 버튼 | 왼쪽부터 `설정`, `홈으로 나가기`, `게임 재개`, `처음부터 다시하기`를 놓는다. `게임 재개`는 기존 `UI_Menu_Button_Primary_9s.png`, 나머지는 `UI_Menu_Button_Secondary_9s.png`를 기존 버튼 구성과 함께 사용한다. |

상세 스탯은 행마다 아이콘과 오른쪽 정렬 숫자만 표시한다. 화면에 항목명을 넣지 않고, 툴팁이 필요하면 기존 입력 방식에 맞춰 별도로 연결한다. 순서와 값은 `GamePauseArtifactView.RefreshStats`를 기준으로 한다.

| 아이콘 | 값 |
| --- | --- |
| 기존 공격 아이콘 | `AttackDamage` |
| `stat-attack-speed.png` | `AttackSpeed` |
| `stat-move-speed.png` | `MoveSpeed` |
| 기존 치명타 표시 | `CriticalChance` 백분율 |
| `stat-critical-damage.png` | `CriticalDamageMultiplier` 백분율 |
| `stat-projectile.png` (128 × 128) | `ProjectileCount` |
| `stat-pierce.png` (128 × 128) | `PierceCount` |

연결 후 ESC 창을 열어 실제 Run 시드가 맞는지 확인한다. 아티팩트가 없을 때와 목록이 스크롤될 때, 네 버튼을 키보드로 순회할 때도 화면을 확인한다. 재개·나가기·재시작은 기존 동작과 확인창을 연결하고, 설정은 기존 설정 UI에 연결한다. 모든 문구에는 기존 ONE Mobile POP TMP를 사용한다.

## 연결 후 화면 확인

- 첫 입장과 다음 층에서 날개, 제목, 배경이 함께 나타났다 사라지는지 확인한다. 긴 맵 이름도 날개와 겹치지 않아야 한다.
- 보상 위치에서 선물만 움직이는지 확인하고, 기존 상호작용으로 선택 화면을 연다.
- 권능의 빈 슬롯, 장착 직후, 일부 충전, 완충 상태를 순서대로 확인한다. 완충 시 기존 사용 키 표시와 게이지 색을 확인한다.
- 상세 스탯을 켜고 활과 신발이 실제 HUD 크기에서 구분되는지 확인한다. 치명타 피해를 표시하는 화면에서는 치명타 확률과 피해 값이 서로 다른 행에 있어야 한다.

## 적용 상태 (2026-11-11)

- 구성: `Trickal Fan Game/Menu/Setup In-game Presentation`(`InGamePresentationSetup`). 다시 실행해도 같은 오브젝트를 고친다.
  HUD-3C·7B·7C·7D와 UX-0을 다시 실행하면 일시정지 창에 이 모양을 다시 입히고, UX-1은 권능 HUD를 같은 그림으로 다시 만든다.
- 검증: `Trickal Fan Game/Menu/Verify In-game Presentation`. 배치는 `InGamePresentationVerification.SetupAndVerifyBatch`(Setup
  두 번, 이전 일시정지 Setup 재실행, 캡처)와 `VerifyWithRegressionsBatch`. 검토용 캡처는 `output/menu-review/ingame-*.png`.
- 이 문서와 다르게 정한 것:
  - 일시정지 창은 [16번 UI 규격](./16-ui-spec.md)의 팝업 상한(1120×760) 안에 들도록 1120×720으로 두고 시안 비율을 따랐다.
  - 시드 초기화 전 표시는 `-`다. ONE Mobile POP에 `—` 글리프가 없다.
  - 창 바깥은 투명하게 두되(어둡게 덮지 않음) 클릭은 계속 막는다.
  - 선물이 보상 표시를 맡는다. 예전 하늘색 상호작용 표시는 지웠고, 보상을 받으면 선물이 사라지고 제단만 남는다. 제단은 플레이어
    몸통 아래(정렬 순서 1)에 그린다.
  - 인게임 상세 스탯 HUD는 기존 네 줄을 유지하고, 아이콘을 각 숫자 줄 높이에 맞췄다(줄 간격을 넓힘).
  - 치명타 확률 아이콘은 실행 중 `CriticalBurstSprites.Get(0)`으로 바뀐다. 편집 화면에서는 메뉴 별 아이콘이 자리를 채운다.
  - 상하 움직임은 `Time.time` 기준이라 일시정지 중에는 멈춘다.
  - 첫 입장 배너(2026-11-11 사용자 요청): 768×160으로 조금 줄여 화면 중앙 위 230에 둔다. 0.5초 동안 안개가 걷히듯
    흐릿한 상태에서 선명해지고, 1.0초 유지 후 0.5초 동안 다시 흐려지며 사라진다(총 2.0초). 그림은 `BannerMist` 셰이더로
    흐리게 하고 글자는 TMP 부드러움 값으로 번지게 한다. 이름과 ESC 창의 `일시정지`는 판 몸통 가운데에 오도록 조금 내렸다.
    첫 Run에서 배너가 안 보이던 문제: 장면을 처음 불러온 직후 한 프레임이 1초 넘게 걸려 표시 시간이 그려지기 전에 다 지나갔다.
    한 프레임에 세는 시간을 최대 1/30초로 묶었다(`GameFloorNameView.MaxFrameStep`).
- Play 확인: 위 "연결 후 화면 확인" 네 가지와 ESC 창, 안개 배너(첫 Run 포함). 사용자 Play 확인 완료(2026-11-11).

## 제작 출처

배너, 제단, 선물, 활, 신발은 내장 ImageGen을 사용했다. 프롬프트는 투명 배경의 단일 2D 요정 RPG 스프라이트를 공통으로 지정하고, 각각 양끝에 둥근 흰 깃과 짙은 갈색 외곽선의 날개가 달린 글자 없는 자주색 제목판(첨부 이미지를 날개 모양의 레퍼런스로 사용), 타원형 연보라 돌과 이끼로 된 빈 제단, 금색 리본을 단 산호색 선물 상자, 자주색 외곽선이 있는 나무 활과 화살, 크림색 커프가 달린 민트색 신발로 지정했다. 권능 슬롯과 게이지는 지정 크기와 색에 맞춰 그렸다. 치명타 피해 아이콘은 `CriticalBurstSprites`의 첫 번째 모양과 기존 `UI_Menu_Icon_Sword.png`를 합성했다.

ESC 제목판, 시드 표시판, 투사체 및 관통 아이콘은 확정 시안을 참조해 내장 ImageGen으로 제작했다. 프롬프트는 각각 글자 없는 베이지 날개 제목판, 양끝에 작은 별 장식이 있는 빈 시드 표시판, 짧은 꼬리의 하늘색 투사체, 잔상이 있는 금색 관통 화살을 투명 스프라이트로 지정했다.
