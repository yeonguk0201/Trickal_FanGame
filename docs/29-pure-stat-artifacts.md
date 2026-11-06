# 순수 스탯 아티팩트(Pool-2)

> 작성일: 2026-11-06
> 계획: [28-difficulty-enemies-drops-plan.md](./28-difficulty-enemies-drops-plan.md) §4.3(Pool-2), §4.1(Pool-0), §4.2(Pool-1)
> 관련 계약: [23-item-classification-contract.md](./23-item-classification-contract.md) §2.3·§2.4, [11-artifact-reference.md](./11-artifact-reference.md)(원작 카드)
> 원문: `docs/references/trickcal-revive-artifacts-namuwiki-source.txt`
> 상태: **1순위 11종·2순위 7종 구현, 자동 검증과 사용자 Play 확인 완료(2026-11-06). 피격 무효 확률 3종 보류.**

## 1. 요약

- 보스·상점 풀의 순수 스탯 아이템이 6종뿐이라 트릭컬 원작 카드에서 18종을 더했다. 순수 스탯 묶음은 **6종 → 24종**이다.
- 모두 보스·상점 풀(Pool-0 `PureStat`)에 속하고 **스택 제한이 없다**(Pool-1, `maxStacks` 0).
- 15종은 기존 효과 타입만 쓴다. 3종(티아라·향로·파랑새)은 작은 새 효과 87~89를 쓴다.
- 원작 수치를 그대로 옮기지 않는다. 게임의 기존 아이템 값(§3)에 맞춰 따로 정했다.

## 2. 원작 → 게임 변환 규칙

### 2.1 카드 분류

원작 102개 헤더 중 본문이 있는 84종을 분류했다(글로벌 전용 17종과 마도 공학 배터리는 본문 없음).

| 분류 | 기준 | 처리 |
| --- | --- | --- |
| A. 순수 스탯 | 효과 줄이 "특별한 기능이 존재하지는 않지만 기본에 충실하다." | 스탯만 옮긴다 |
| B. 스탯 + 단순 상시 보정 | 조건 없이 항상 켜지는 % 보정만 붙음 | 게임 스탯으로 옮길 수 있으면 넣는다 |
| C. 고유 기능 | 발동 조건·타이밍·상태이상 | 이 문서 범위 밖. 이미 게임에 있거나 별도 설계 |

### 2.2 스탯 대응

| 원작 스탯 | 게임 효과 | 비고 |
| --- | --- | --- |
| 물리·마법 공격력 | `AttackDamagePercent`(6) | 게임은 공격력 하나. 같은 값이 겹치면 "마법 → 스킬 피해"(`SkillDamagePercent`, 7)로 바꾼다 |
| 치명타 | `CriticalChance`(8) | %p |
| 치명 피해 | `CriticalDamage`(57) | %p, 배율에 더함 |
| 공격 속도 | `AttackSpeedPercent`(9) | |
| 이동 속도 | `MoveSpeedPercent`(19) / `MoveSpeedPenaltyPercent`(20) | |
| HP | `MaxHealthFlat`(11) | 반 칸 단위 정수 |
| 일반 공격 피해 | `BasicAttackDamagePercent`(46) / `BasicAttackDamagePenaltyPercent`(89) | |
| 고학년 스킬 피해 | `UltimateDamagePercent`(88) | |
| 받는 보호막 효과 | `ShieldGainBonus`(87) | %가 아니라 반 칸 고정값 |
| 물리·마법 방어력, 치명 저항류 | 없음 | 체력으로 바꾸거나 제외(§6) |
| 받는 피해 감소 % | 없음 | 피해가 반 칸 단위로 올림되어 효과가 0. 피격 무효 확률로 대체 예정(§5) |
| 치유량·HP 회복량 %, SP 회복량 % | 없음 | 회복·SP가 칸 단위라 % 증가가 묻힌다(§6) |
| 아군 버프(같은 열) | 본인 버프로 축소 | 만년필만 적용 |

## 3. 게임 기준점

- 체력: 반 칸 단위. 기본 10(5칸), 체력+방어막 상한 15칸.
- 치명: 기본 확률 5%, 기본 배율 1.5배. 치명 피해 +30%p = 배율 +0.3.
- 등급 가중치: Common 60 / Uncommon 25 / Rare 12 / Epic 3.
- 기존 순수 스탯 1개당 값: 급조한 목검 공격력 +5%(C), 녹슨 송곳 치명 +3%p(C), 낡은 화살 공속 +10%(U),
  돈까스 모양 머리핀 HP +1칸(U), 탐욕의 반지 치피 +30%p(U, 섞인 스탯), 30KG 케틀벨 스킬 +25%(R), 장난감 망원경 공격력 +15%(E).
- 게임 값은 원작보다 공격력은 약 절반, 치명 확률은 약 1/3, 치명 피해는 오히려 크게 잡아 왔다.

## 4. 구현 목록(18종)

모든 아이템: `ItemKind.Artifact`, 활성, `maxStacks` 0, 보스·상점 풀. 원화는 `Assets/Resources/UserArtwork/<ID>.png`(사용자 제공).

### 4.1 1순위 — 기존 효과만

| 이름 | ID | 원작 | 등급 | 효과(1개당) | 근거 |
| --- | --- | --- | --- | --- | --- |
| 수건 머리띠 | `artifact-towel-headband` | 일반, HP | Common | 최대 HP +반 칸 | 머리핀(U, +1칸)의 절반 |
| 고목나무 비수 | `artifact-old-tree-dagger` | 일반, 치명·치피 | Common | 치명타 확률 +2%p · 치명타 피해 +10%p | 송곳(치명 3%p)을 둘로 나눔 |
| 덧댄 지팡이 | `artifact-patched-staff` | 일반, 마공 | Common | 스킬 피해 +8% | 목검과 겹치지 않게 "마법 → 스킬" |
| 안개 비수 | `artifact-mist-dagger` | 고급, 치피 | Uncommon | 치명타 피해 +25%p | 단일 스탯 U |
| 흑요석 수리검 | `artifact-obsidian-shuriken` | 고급, 치피 + 평타 피해 | Uncommon | 치명타 피해 +10%p · 기본 공격 피해 +8% | 원작 구성 그대로 |
| 여우 단검 | `artifact-fox-dagger` | 고급, 공속·치명 + 피해량 | Uncommon | 공격속도 +5% · 치명타 확률 +3%p · 공격력 +3% | 세 갈래, 각각 작게 |
| 전투 지침서 | `artifact-combat-manual` | 희귀, 치피 두 겹 | Rare | 치명타 피해 +40%p | 두 겹을 하나로. 탐욕의 반지보다 위 |
| 옥빛 마법서 | `artifact-jade-grimoire` | 희귀, 마공·치명 + 스킬 | Rare | 공격력 +5% · 치명타 확률 +3%p · 스킬 피해 +10% | 스킬 3종 중 확률 담당 |
| 날카로운 지팡이 | `artifact-sharp-staff` | 희귀, 물공·치피 + 스킬 | Rare | 공격력 +5% · 치명타 피해 +15%p · 스킬 피해 +10% | 스킬 3종 중 배율 담당 |
| 황금 왕관 | `artifact-golden-crown` | 희귀, 치명·치피 + 스킬 | Rare | 치명타 확률 +4%p · 치명타 피해 +15%p · 스킬 피해 +12% | 공격력 대신 스킬·치명이 큼 |
| 벨리타의 지팡이 | `artifact-belita-staff` | 전설, 마공·치피 + 피해량 | Epic | 공격력 +20% · 치명타 피해 +15%p | 원작 평가 "조건 없이 깡화력" |

### 4.2 2순위 — 방어계 전환·아군 버프 축소

| 이름 | ID | 원작 | 등급 | 효과(1개당) | 근거 |
| --- | --- | --- | --- | --- | --- |
| 중량 조끼 | `artifact-weighted-vest` | 일반, 치저·치피저 | Common | 최대 HP +1칸 · 이동속도 -5% | 방어계 → 체력. 이름대로 무거운 대가 |
| 요정 로브 | `artifact-fairy-robe` | 일반, HP·마방 | Common | 최대 HP +반 칸 · 이동속도 +3% | 머리띠와 겹치지 않게 이속 |
| 비늘 갑옷 | `artifact-scale-armor` | 희귀, 물방·마방 | Rare | 최대 HP +2칸 | 긴급 보호 벨트와 겹치지 않게 방어막·이속 제외(사용자 결정) |
| 제이드의 비싼 만년필 | `artifact-jade-fountain-pen` | 희귀, 같은 열 마법 피해 | Rare | 공격력 +5% · 공격속도 +5% · 스킬 피해 +10% | 아군 버프 → 본인 버프 |

### 4.3 2순위 — 새 효과 87~89

| 이름 | ID | 원작 | 등급 | 효과(1개당) |
| --- | --- | --- | --- | --- |
| 찬란한 티아라 | `artifact-shining-tiara` | 고급, HP + 받는 보호막 +28% | Uncommon | 최대 HP +반 칸 · 방어막을 얻을 때 반 칸 더 |
| 제사장의 향로 | `artifact-priest-censer` | 희귀, HP + 받는 보호막 +52% | Rare | 최대 HP +1칸 · 방어막을 얻을 때 반 칸 더 |
| 블랑셰의 유리 파랑새 | `artifact-blanchet-glass-bluebird` | 전설, 고학년 +63% · 평타 -60% | Epic | 고학년 스킬 피해 +100% · 기본 공격 피해 -30% |

- 파랑새: 원작 그대로면 2개에 평타 -120%가 되므로 1개당 -30%로 낮추고 합계 하한 -80%를 둔다. 새 타입 없이 "스킬 피해 + 이속
  페널티"로 만들면 30KG 케틀벨과 같은 카드가 되어 고학년 전용 타입을 만들었다.
- [12번 후보 문서](./12-artifact-spell-candidates.md)의 예전 적용안(안개 비수 "치피 5%, 치명 3%", 벨리타 "공격력 25%",
  파랑새 "고학년 +300%, 평타 -50%", 비늘 갑옷 "피해 감소 + 이속 감소")은 이 표로 대체한다.

### 4.4 등급 분포

| 등급 | 기존 순수 스탯 | 추가 | 합계 |
| --- | --- | --- | --- |
| Common | 2(목검, 송곳) | 5(머리띠, 고목나무 비수, 덧댄 지팡이, 중량 조끼, 요정 로브) | 7 |
| Uncommon | 2(낡은 화살, 머리핀) | 4(안개 비수, 흑요석 수리검, 여우 단검, 티아라) | 6 |
| Rare | 1(칸나의 대포) | 7(전투 지침서, 옥빛 마법서, 날카로운 지팡이, 황금 왕관, 비늘 갑옷, 만년필, 향로) | 8 |
| Epic | 1(실라의 바람살) | 2(벨리타의 지팡이, 파랑새) | 3 |

풀 크기: 보스 32, 상점 28(Pool-0 이전 보스 14, 상점 10).

## 5. 새 효과와 공용 규칙

### 5.1 효과 87~89(`ItemEffectType` 뒤에 추가)

| 번호 | 이름 | 동작 | 위치 |
| --- | --- | --- | --- |
| 87 | `ShieldGainBonus` | 방어막을 얻을 때마다 `magnitude`(반 칸 단위)만큼 더 얻는다. 체력+방어막 15칸 한도 안 | `Health.AddShield`(벨트·날먹보호막·갑옷축제 초대장 등 모든 획득 경로), 풍선 갑옷 획득 방어막(`PlayerInventory`). 값은 플레이어 `Health`에만 설정되고 적은 0 |
| 88 | `UltimateDamagePercent` | 고학년 스킬 피해 +`magnitude`. 스킬 피해 보너스에 더한다(`PlayerStats.UltimateDamageMultiplier`) | `PlayerUltimate.ApplyToTarget`. 감전 대상 스킬 보너스도 고학년 배율 기준으로 계산 |
| 89 | `BasicAttackDamagePenaltyPercent` | 기본 공격 피해 -`magnitude`. 합계는 `PlayerStats.MaximumBasicAttackDamagePenalty`(80%)에서 멈춘다 | `PlayerStats.BasicAttackRoomDamageMultiplier` |

세 효과 모두 양수 `magnitude`가 필요하다. Backend 카탈로그는 같은 이름 문자열을 쓴다.

### 5.2 스택과 상한(Pool-1 기준)

- 순수 스탯은 스택 제한이 없다. 대신 `PlayerStats`의 전체 상한·하한이 깨지는 스탯을 막는다(치명 100%, 공속 5배, 이속 0.1~2배,
  체력+방어막 15칸 등, [28번 §4.2](./28-difficulty-enemies-drops-plan.md)).
- 아이템의 **이득 효과가 모두** 전체 상한이면 3택1·상점·상자 후보에서 뺀다(`PlayerInventory.AreAllEffectsAtOverallCap`).
  - **페널티(20 이속 감소, 89 평타 감소)는 보지 않는다.** 페널티만 남은 아이템을 계속 주지 않기 위해서다.
  - **87은 체력이 15칸이면 상한**으로 본다. 체력만으로 15칸이면 방어막이 들어갈 자리가 없다.
- 후보에서 빠지는 시점: 머리띠 20개, 비늘 갑옷 5개, 중량 조끼 10개, 티아라·향로는 체력 15칸. 나머지는 상한 없는 효과(공격력·
  치명 피해·스킬·고학년)가 있거나 이속 상한이 늦게 와서 계속 나온다.

## 6. 보류·제외

| 카드 | 상태 | 이유 |
| --- | --- | --- |
| 로네의 건틀릿, 말랑말랑한 조끼, 리츠의 대지 뽀개기 | 보류 | 원작 "받는 피해 감소"가 반 칸 올림에 묻힌다. "N% 확률로 피격 1회 무효"(기대값 동일, 반 칸에도 효과)로 바꿀 안이 있으나 새 전투 흐름과 "막음" 표시가 필요해 품이 크다. 이 스탯이 생기면 다시 본다(제안: 건틀릿 U 무효 5%, 조끼 E HP +1칸·무효 10%, 리츠 E HP +1칸·공격력 +10%·무효 5%, 상한 50%) |
| 치유의 호롱불, 가죽 갑옷, 루드의 운동 교본, 엘다인 램프 | 제외 | 회복량 %가 반 칸 단위 회복에 묻히고 회복 출처가 적다. 회복 쪽을 손볼 때 다시 본다 |
| 캬롯의 사탕수수 | 제외 | SP는 칸 단위 줍기라 "SP 회복량 %"를 받을 자리가 없다 |
| 버터의 옐로카드 | 제외 | 강화 공격 개념이 없다 |
| 알루미늄 골무, 잎사귀 로브, 골판지 갑옷, 보자기 로브, 슬라임 쿠션, 오븐 장갑, 다이아몬드 팔찌 | 제외 | 방어계는 대표 3장(중량 조끼, 요정 로브, 비늘 갑옷)만 체력으로 전환. 나머지까지 바꾸면 같은 효과 카드만 늘어난다 |
| 보석 반지, 묘수, 행운의 주사위 등 C 분류 | 범위 밖 | 고유 기능이 있어 순수 스탯이 아니다 |

이미 게임에 있는 원작 카드(목검, 송곳, 낡은 화살, 머리핀, 바람살, 케틀벨 등)는 다시 만들지 않았다.

## 7. 데이터·계약

- Unity: `Assets/Items/<ID>.asset`, 생성 메뉴 `Trickal Fan Game > Week 28 > Setup Pool-2 Pure Stat Artifacts`
  (`Pool2PureStatArtifactSetup.Artifacts`가 단일 목록). 재실행해도 같은 에셋·GUID를 쓴다. Game Scene 보상 풀에 ID 순서로
  넣고 TMP 글꼴에 이름·설명 글자를 더한다.
- 풀 분류: `Pool0ItemPoolSetup.Sources`(`PureStat`), 무제한 목록: `Pool1StackLimitSetup.UnlimitedItemIds`. 두 목록이 같아야
  Pool-1 검증을 통과한다.
- Backend: `backend/src/contracts/item-catalog.ts`에 18종, `maxStacks` 0(= 제한 없음). Pool-1에 맞춰 기존 순수 스탯 6종도 0으로
  맞췄다. 테스트는 `maxStacks >= 0`을 허용한다. 로컬 DB는 `backend/`에서 `pnpm prisma:seed`로 반영한다. Prisma·DTO 형태 변경 없음.
- 설명 문구는 `ArtifactEffectDescription`이 효과에서 만든다(예: "방어막을 얻을 때 반 칸 더", "고학년 스킬 피해 +100%",
  "기본 공격 피해 -30%").

## 8. 검증

- 단독: `Trickal Fan Game > Week 28 > Setup and Verify Pool-2 Pure Stat Artifacts`
  (`Pool2PureStatArtifactVerification.SetupAndVerifyBatch`) — Setup 두 번·GUID 유지, 18종 계약·설명·글꼴·원화·풀·무제한 목록,
  보상 풀 1회, 2스택 스탯 적용, 상한 제외 시점, 87~89 번호·검증·방어막 경로·15칸 한도·파랑새 -80% 하한·적 미적용.
- 회귀: `Verify Pool-2 Pure Stat Artifacts With Regressions` — 위 + Pool-0, Pool-1 회귀 묶음, 사용자 원화, Artifact-2, Shop-0,
  HP-1, Phase G-2·G-4, 방어막 버블, Phase D 고학년 스킬, Spell-5, Power-1. **통과(2026-11-06).**
- Backend: `item-catalog.spec.ts`(18종 효과·순서·등급·`maxStacks` 0, Pool-1 6종), lint, `pnpm build` 통과.
- 수동 확인(Play): 아래 다섯 가지 모두 사용자 확인, 정상(2026-11-06).
  1. F1 `— Artifact —` → `Show artifact drops`에 18종과 `[BS]` 표시, `Drop`으로 획득 시 카드·일시정지 목록에 원화·설명.
  2. 보스 3택1·상점에 새 아이템이 섞여 나오는지, 등급 비중이 괜찮은지.
  3. 긴급 보호 벨트 + 티아라로 전투방 입장 시 방어막 1칸.
  4. 파랑새로 고학년 스킬이 세지고 평타가 약해지는지(3개 이상에서 평타가 너무 약하지 않은지).
  5. 체력 15칸 뒤 머리핀·머리띠·조끼·티아라가 보상·상점에서 빠지는지.
