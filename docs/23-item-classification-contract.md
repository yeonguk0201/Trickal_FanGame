# 아이템 분류·저장 계약 — Contract-0

> 작성일: 2026-10-02  
> 계획: [21-sixth-month-plan.md §4.1 Contract-0, §8 D0](./21-sixth-month-plan.md)  
> 관련: [06-database.md](./06-database.md), [07-api.md](./07-api.md), [11-artifact-reference.md](./11-artifact-reference.md)  
> 범위: 여섯째 달 신규 아이템의 분류값·안정 ID·획득 경로·Run 저장·골드 enum 방식

이 문서는 **계약 결정**이다. 문서 작성 시점에 코드·에셋·API는 바뀌지 않았다. 아래 규칙은 Gold-0,
Slot-0, Spell-0~4, Chest-2, Flight-0, Jjangsem-0·1이 구현할 때 따르는 기준이며, 각 조각의 검증으로
적용 여부를 확인한다.

## 1. 현재 상태(2026-10-02 확인)

| 위치 | 현재 계약 |
|---|---|
| Unity `ItemKind` | `Artifact = 0`, `Spell = 1` |
| Unity `ItemDefinition.IsItemIdValidForKind` | Artifact는 `item-`·`artifact-`, Spell은 `spell-` 접두사만 유효 |
| Unity `ItemRarity` | `Common = 0`, `Uncommon = 1`, `Rare = 2`, `Epic = 3` |
| Unity `ItemEffectType` | 0~27. 21~23은 기존 스펠 전용(`NextCombatRoomAttackDamagePercent`, `BossRoomAttackSpeedPercent`, `BossRoomMoveSpeedPercent`) |
| Unity 획득 풀 | `SampleScene`의 `RoomGraphAssembler.selectionRewardPool` 16종에 `spell-*` 3종 포함. 상점 아이템 상품도 같은 풀을 사용 |
| Unity `RunResourceType` | `Elif = 0`, `Key = 1`, `Bomb = 2`. 픽업 Prefab에 값으로 직렬화 |
| Backend `Item` | `id`·`name`·`description`·`rarity`·`isActive`·`maxStacks`·`effectData`. **분류(kind) 컬럼 없음** |
| Backend Run 저장 | `items[]`의 모든 ID가 존재하고 `isActive=true`여야 저장. 아니면 `ITEM_NOT_FOUND` |
| Backend 경험치 | `RunItem` 1건마다 등급별 경험치 COMMON 10 / UNCOMMON 20 / RARE 30 / EPIC 50 |
| Web | 전적 상세는 `name`, `itemId`, 등급 라벨을 표시. 아이템 통계는 `itemId` 기준 집계. 분류를 표시하지 않음 |
| Unity Run 저장 재시도 | 같은 세션 안의 `pendingRequest` 재전송만 있음. 오프라인 영구 대기열 없음 |

## 2. 분류 체계

### 2.1 `ItemKind` 값

기존 값의 숫자와 의미를 바꾸지 않고 뒤에 추가한다. 예약 번호는 다른 용도로 쓰지 않는다.

| 값 | 식별자 | 게임 표기 | ID 접두사 | 상태 |
|---|---|---|---|---|
| 0 | `Artifact` | 아티팩트 | `item-`, `artifact-` | 기존 유지 |
| 1 | `Spell` | (레거시) 스펠 | `spell-` | **레거시**. 신규 획득 풀에서 제외 |
| 2 | `SingleUseSpell` | 스펠 | `single-spell-` | Slot-0에서 추가(2026-10-02) |
| 3 | `JjangsemSpell` | 짱셈스펠 | `jjangsem-` | Slot-0에서 추가(2026-10-02, 첫 짱셈스펠과 함께 사용) |
| 4 | `Trinket` | 장신구 | `trinket-` | **예약**. 첫 장신구 구현(D7) 때 추가 |
| 5 | `Active` | 액티브 | `active-` | **예약**. 첫 교주의 권능 구현(D7) 때 추가 |

- **아티팩트와 패시브는 같은 분류다.** 아이작의 "패시브"에 해당하는 상시 효과 아이템을 이 게임에서는
  아티팩트라고 부른다. 계획 문서의 "패시브"·"공격 변화 패시브"는 `ItemKind.Artifact`이며 UI 표기도
  "아티팩트"를 유지한다. 별도 `Passive` 값을 만들지 않는다.
- 예약 값(4·5)은 실제 아이템이 생기기 전까지 enum에 넣지 않는다. 빈 분류가 검증·풀·UI에 노출되지 않게 한다.
- 접두사는 분류마다 겹치지 않는다. `single-spell-`은 `spell-`로 시작하지 않으므로 레거시 판정과 충돌하지 않는다.
  `IsItemIdValidForKind`에 새 분류의 접두사를 추가하고, 다른 분류의 접두사로 만든 ID는 무효로 처리한다.
- 황금상자 전용 같은 **획득 경로 제한은 분류값이 아니라 풀 소속으로 표현**한다. 시스트의 가짜 날개는
  `ItemKind.Artifact`(`artifact-` 접두사)이며, 전용 풀에만 들어가고 일반 선택 보상·상점 풀에는 들어가지 않는다.

### 2.2 레거시 `ItemKind.Spell` 처리

- `spell-catch-that-one`, `spell-final-sprint`, `spell-afterimage`의 ID·이름·설명·등급·효과(21~23 포함)를
  바꾸지 않는다. Unity 에셋과 GUID를 보존한다.
- **Unity**: Spell-1~3에서 `selectionRewardPool`(따라서 상점 아이템 상품)에서 제거한다. 제거 대상은
  `Editor/LegacySpellRetirement.cs` 목록으로 관리하며 Spell-1(2026-10-02)에서 `spell-catch-that-one`을, Spell-2(2026-10-02)에서 `spell-afterimage`를, Spell-3(2026-10-02)에서 `spell-final-sprint`를 제거했다. 레거시 스펠 3종이 모두 빠져 선택 보상 풀은 아티팩트만 담으며, Reward-3 Setup·검증도 "레거시 스펠 없음"을 요구하도록 바꿨다. 에셋의 `isActive`는
  바꾸지 않아 아이템 테스트 씬 로드아웃과 과거 효과 검증이 계속 동작하게 한다. 신규 풀 제외는
  `isActive`가 아니라 "획득 풀에 `ItemKind.Spell`이 없음"이라는 검증 불변조건으로 보장한다.
- **Backend**: 카탈로그의 `isActive=true`를 유지한다. `false`로 바꾸면 이전 빌드나 진행 중 재전송이
  `ITEM_NOT_FOUND`로 실패하고, 경험치 계산도 `RUN_ITEM_INACTIVE`로 거부된다.
- **Web**: 과거 전적의 `spell-*`는 기존처럼 이름·ID·등급으로 표시된다. 새 일회용 스펠과 표시 이름이
  같아도 ID가 함께 표시되므로 구분된다. 별도 화면 변경은 필요 없다.
- 레거시 효과 타입 21~23을 새 일회용 스펠에 재사용하지 않는다(§2.4).

### 2.3 신규 안정 ID

계획에 이름이 확정된 신규 아이템의 ID를 미리 정한다. 구현 순서와 무관하게 이 ID를 쓰고, 다른
의미로 재사용하지 않는다. 표에 없는 후보는 구현 조각에서 같은 규칙(소문자 kebab-case, 64자 이하)으로 정한다.

| 이름 | 분류 | ID | 조각 |
|---|---|---|---|
| 저놈 잡아라 | 스펠 | `single-spell-catch-that-one` | Spell-1 |
| 그건 내 잔상 | 스펠 | `single-spell-afterimage` | Spell-2 |
| 막판 스퍼트 | 스펠 | `single-spell-final-sprint` | Spell-3 |
| 아로마 테라피 | 스펠 | `single-spell-aroma-therapy` | Spell-0 |
| 명상의 시간 | 스펠 | `single-spell-meditation-time` | Spell-0 |
| 멤버십카드 | 스펠 | `single-spell-membership-card` | Spell-4 |
| 멜룬카드 | 짱셈스펠 | `jjangsem-melune-card` | Jjangsem-1 |
| 시스트의 가짜 날개 | 아티팩트(황금 전용) | `artifact-sist-fake-wings` | Flight-0 |

### 2.4 등급·효과·스택

- 신규 분류도 기존 4등급(`ItemRarity`)과 Backend 문자열 `COMMON`/`UNCOMMON`/`RARE`/`EPIC`을 쓴다.
  새 등급을 추가하지 않는다. 등급은 경험치·상점 가격·선택 가중치에 쓰인다.
- 새 효과는 `ItemEffectType` 뒤(28~)에 추가한다. 기존 숫자를 재배치하거나 다른 의미로 재사용하지 않는다.
  Spell-0(2026-10-02)에서 `RestoreAllSPWithOvercharge = 28`(`integerAmount` = 최대치 위 초과 칸 수)과
  `RegenerateSPHalvesOverTime = 29`(`integerAmount` = 틱당 SP 반 칸 수, `intervalSeconds`, `durationSeconds`)를
  추가했다. 두 효과는 슬롯 사용 시에만 `PlayerSingleUseEffects`가 실행한다.
  Spell-1(2026-10-02)에서 `CurrentRoomBasicAttackDamagePercent = 30`(`magnitude` = 사용한 방의 기본 공격 피해 증가율)을
  추가했다. 레거시 21(`NextCombatRoomAttackDamagePercent`)은 재사용하지 않고, `PlayerStats`의 별도 방 한정 피해
  출처로 합산해 레거시 스펠과 서로 덮어쓰지 않는다.
  Spell-2(2026-10-02)에서 `EscapeToFloorStartRoom = 31`(수치 필드 없음)을 추가했다. 레거시 `spell-afterimage`의
  `MoveSpeedPercent`·`AttackSpeedPercent`(19·9)는 그대로 두고 일회용 스펠은 이 효과만 쓴다.
  Spell-3(2026-10-02)에서 `CurrentBossRoomSpeedPercent = 32`(`magnitude` = 공격속도, `secondaryMagnitude` = 이동속도
  증가율)를 추가했다. 레거시 22·23은 재사용하지 않고 `PlayerStats`의 별도 방 한정 속도 출처로 합산한다.
  Range-0(2026-10-02)에서 `ProjectileSpeedPercent = 33`(`magnitude` = 기본 공격 탄속 증가율)과
  `ProjectileLifetimePercent = 34`(`magnitude` = 기본 공격 체공 시간 증가율)를 추가했다. 사거리 = 체공 시간 × 탄속이므로
  둘 다 사거리를 늘린다. 탄속(33)은 **계약만** 열어둔 상태다. 체공 시간(34)은 같은 날 `item-15` 장난감 망원경의
  세 번째 효과(`magnitude` 0.3, 사거리 약 5.3 → 약 6.9)로 추가했고 Backend 카탈로그 `effectData`·설명에도 같은 값을 기록했다.
  기존 두 효과(공격력 +15%, 2~6m 거리 비례 피해)와 ID·등급·스택은 그대로다.
- 일회용 아이템(`SingleUseSpell`, `JjangsemSpell`)은 `maxStacks = 1`이다. 보유 중에는 **효과가 적용되지
  않고**, 사용 성공 시에만 효과를 실행한다. `PlayerInventory`의 상시 효과 적용·아티팩트 HUD·일시정지
  아티팩트 목록에 들어가지 않는다.
- Backend 카탈로그의 `effectData`는 일회용 아이템도 비어 있지 않게 기록한다(카탈로그 테스트가 활성 항목의
  효과 1개 이상을 요구). Unity와 같은 효과 타입·수치를 쓴다.

## 3. 획득 경로별 허용 분류

| 획득 경로 | Artifact | Spell(레거시) | SingleUseSpell | JjangsemSpell | 비고 |
|---|---|---|---|---|---|
| 보물방·비밀방·보스방 선택 보상(`selectionRewardPool`) | 허용 | **금지** | 금지 | 금지 | 황금 전용 아티팩트도 금지 |
| 상점 아이템 상품 | 허용 | **금지** | D5에서 결정 | D5에서 결정 | 현재는 선택 보상 풀 공유 |
| 일반상자 | — | **금지** | 허용(낮은 확률, D2) | 금지 | 기본 소모품 중심 |
| 황금상자 특별 보상 | 전용 풀만 | **금지** | — | — | 장신구 구현 전에는 전용 아티팩트만 |
| 다이아몬드 상자 | — | **금지** | 허용 | 허용 | 짱셈스펠은 구현된 유효 ID만 |
| 개발 패널·테스트 씬 | 허용 | 허용 | 허용 | 허용 | 정상 Run 기록과 분리 |

검증기는 정상 Run의 모든 획득 풀에 `ItemKind.Spell`이 있으면 실패해야 하고, 각 풀에 허용되지 않은
분류나 `IsValid=false` 항목이 있으면 실패해야 한다.

## 4. Run 저장·기록 범위

### 4.1 획득 기록(사용자 결정: 획득 기록 + 경험치)

- 일회용 아이템도 **획득을 `RunItem`으로 기록**하고 등급별 경험치를 받는다. Prisma·DTO·API 변경은 없다.
- 기록 단위는 **획득 인스턴스**다. 바닥·상자에서 생긴 일회용 아이템마다 Run 안에서 유일한 인스턴스 ID를
  두고, 그 인스턴스가 처음 슬롯에 들어올 때 1번만 기록한다.
  - 슬롯 교체로 바닥에 배출된 아이템은 같은 인스턴스 ID를 유지한다. 다시 주워도 기록하지 않는다.
  - 사용·소비해도 기록을 지우지 않는다. 사용하지 않고 Run이 끝나도 기록은 남는다.
  - 서로 다른 인스턴스라면 같은 ID를 여러 번 획득해도 각각 기록한다(기존 아티팩트 스택 기록과 같음).
- `floor`는 처음 획득한 층, `acquiredAt`은 처음 획득한 시각이다. `order`는 아티팩트와 같은 순번 공간을 쓴다.
- 멜룬카드는 일회용 아이템 자체를 복제하지 않으므로(21번 §3.6) 복제를 통한 기록 증가는 없다.
- 기존 `clientRunId` 재전송 멱등성·경험치 중복 방지는 그대로 적용된다. 같은 요청 재전송은 새 기록을 만들지 않는다.

### 4.2 사용 기록

- 아이템 **사용 로그는 서버에 저장하지 않는다.** `RunItem`·`Run` 스키마를 확장하지 않는다.
- Balance-0·Play-2에 필요한 사용 횟수는 Unity 로컬 개발 기록(`DevelopmentPlaytestRecord` 계열)에만 남긴다.
  로깅 실패가 게임을 멈추지 않게 한다.
- 서버 사용 기록이 필요해지면 별도 계약 조각으로 Prisma·DTO·Web 영향을 함께 설계한다.

### 4.3 분류 정보의 서버 노출

- 이번 달에는 Backend `Item`에 분류 컬럼을 추가하지 않는다. 저장·경험치·전적 표시에 분류가 필요 없다.
- Web에서 분류 표시가 필요해지면 ID 접두사로 임시 판정하지 말고, Backend 카탈로그·Prisma `Item`에
  분류 필드를 추가하는 계약 조각을 따로 만든다.
- 새 아이템을 추가할 때는 Unity 에셋, Backend `ITEM_CATALOG`·카탈로그 테스트, seed 반영을 함께 한다.
  카탈로그에 없는 ID를 Unity가 보내면 Run 저장이 실패한다.

## 5. 콘텐츠 버전과 재현

- seed 재현은 **같은 `gameVersion`(Unity `Application.version`)과 같은 콘텐츠**에서만 보장한다.
  풀 구성·확률·드롭 표를 바꾸면 같은 seed라도 이전 버전과 결과가 달라질 수 있다.
- 과거 전적은 저장된 `itemId`·서버 카탈로그 이름·등급으로 표시하며 재현 가능성에 의존하지 않는다.
- 아이템을 더 이상 지급하지 않을 때도 카탈로그 항목을 삭제하지 않는다(`RunItem` → `Item`은 `onDelete: Restrict`).

## 6. 골드 enum 방식(사용자 결정: 이름만 변경)

- `RunResourceType.Elif = 0`을 **`Gold = 0`으로 식별자만 바꾼다.** 값 0의 의미 "Run 한정 화폐"는 유지하므로
  픽업 Prefab의 직렬화 값과 GUID가 바뀌지 않는다.
- 영구 엘리프 픽업이 필요해지면 다음 달에 새 값(`Elif = 3` 등)을 **뒤에 추가**한다. 값 0을 엘리프로 되돌리지 않는다.
- `RunResourceType`을 문자열로 저장하는 코드는 없다(2026-10-02 확인). 표시 문자열(HUD, 상점 `NotEnoughElif` 프롬프트,
  개발 패널, 아이템 테스트 씬, 검증기 로그)은 Gold-0에서 "골드"/`GOLD`로 함께 바꾼다.
- `ShopPurchaseResult.NotEnoughElif` 같은 보조 식별자는 Gold-0에서 `NotEnoughGold`로 바꾼다. 직렬화되지 않는 값이다.
- `Prefabs/ElifPickup.prefab`은 Editor Setup·검증기가 경로 문자열로 참조한다. Gold-0에서 파일명을 바꾸려면
  `AssetDatabase.MoveAsset`으로 GUID를 보존하고 모든 경로 상수를 함께 갱신한다. 위험 대비 이득이 작으면
  파일명은 유지하고 표시만 바꾼다. **Gold-0에서 파일명 유지로 결정했다.** 드롭 표의 드롭 ID `elif`도 데이터 키로 유지한다.
- Backend·Web·Prisma에는 엘리프/골드 계약이 없으므로 영향이 없다.

## 7. 사용 규칙 결정(D0)

- **막판 스퍼트**는 보스방에서만 사용할 수 있다. 보스방 밖에서는 사용 실패로 처리하고 **소비하지 않으며**
  슬롯에 그대로 남는다. 보스방에서 사용하면 그 방에서만 공속 +30%·이속 +5%, 방을 떠나면 해제한다.
- 사용 실패(조건 불충족·일시정지·사망 전환·Run 종료)는 아이템을 소비하지 않고 효과도 실행하지 않는다.
- D1(2026-10-02): 공용 사용 키는 Left Shift, 찬 슬롯은 닿으면 자동 교체하고 기존 아이템을 같은 인스턴스 ID로
  그 자리에 배출한다. 효과가 연결되지 않은 일회용 아이템은 사용할 수 없고 소비되지 않는다(`PlayerSingleUseEffects`).
  사용 횟수는 개발 기록(`DevelopmentPlaytestRecord`의 `item-uses`)에만 남는다.

## 8. 구현 조각별 체크

| 조각 | 이 계약에서 지켜야 할 것 |
|---|---|
| Gold-0 | §6. 값 0 유지, Prefab GUID 보존, 표시 문자열 전환, 상한 99·상점 구매 회귀 |
| Slot-0 | `SingleUseSpell`/`JjangsemSpell` 추가, 접두사 검증, 보유 중 효과 미적용, 인스턴스 ID 기반 1회 기록(§4.1), 카드 UI 분류 표기 |
| Spell-0~4 | §2.3 ID, Backend 카탈로그·테스트·seed 동시 반영, 레거시 `spell-*` 풀 제거·Backend 활성 유지 |
| Chest-1·2 | §3 경로별 허용 분류, 유효 ID만 생성 |
| Flight-0 | 가짜 날개는 `ItemKind.Artifact`, 전용 풀 소속으로만 획득 |
| Jjangsem-0·1 | `jjangsem-` 접두사, 일회용 아이템 비복제 |
| Verify-0 | 풀별 분류 불변조건과 Unity↔Backend 카탈로그 ID·등급 일치 검사 |
| Range-0 | 효과 33·34는 뒤에 추가, 33은 아이템 미사용·34는 `item-15`만 사용(검증기가 확인), 아이템 추가 시 Backend 카탈로그·seed 동시 반영 |
