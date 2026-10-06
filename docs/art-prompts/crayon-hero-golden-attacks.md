# 크레용사용 각성 공격 모션

사용자가 제공한 [각성 원본](./crayon-awakened-reference.png)의 흰금색 보석·흰 다이아·노란 망토·금색 장갑·빛 날개를 유지한다. 내장 `image_gen`으로 생성하고 게임 Prefab에 연결했다.

## 결과와 연결

게임 출력 폴더: `game/Assets/Art/Bosses/FairyKingdom/Movement`.
`CrayonHero_Golden_{Swing,Dash,Slam,SlamCharge}_0..3.png` 총 16 PNG를 보존한다.
1024×1024, 중앙 pivot, 400 PPU이며, 기존 GUID와 일반 모드 모션은 유지했다.

[공격 미리보기](./crayon-golden-attacks-preview.png): 위에서부터 일반 베기·하향 대시·상향 대시·내려찍기.
[금빛 차징 3단계](./crayon-golden-charge-preview.png): 짧고 얇은 금빛 → 높은 금빛 → 큰 황금빛과 밝은 중심.

| 런타임 인덱스 | 에셋 |
|---|---|
| 0–3 | Golden_Swing_0..3, 칼 올리기/유지/하향 베기/후속 자세 |
| 4–7 | Golden_Dash_0..3, 하향 준비/하향 대시/상향 준비/상향 대시 |
| 8 | Golden_SlamCharge_0, 중립 준비 |
| 9 | Golden_SlamCharge_1, 첫 금빛 충전 |
| 10–11 | Golden_Slam_2..3, 내려찍기/회복 |
| 12–13 | Golden_SlamCharge_2..3, 두 번째/세 번째 충전 |

14프레임을 `CrayonHeroBoss.prefab`의 `goldenAttackFrames`에 연결했고 `BossMovementAnimationSetup` 재실행 시에도 유지한다. `Golden_Slam_0..1`은 생성 원본 보존용이며 실제 충전에는 전용 SlamCharge 시트를 사용한다. 공격 후 원본 각성 대기 자세로 돌아온다. 2페이즈 걷기는 현재 원본 본체를 유지한다.

## 차징 시점

색상은 전 단계 금빛 계열이며 붉은 불꽃으로 바꾸지 않는다. 칼 위로 아우라 높이와 폭이 커지고 밀도가 진해진다. 날개는 같은 밝기를 유지한다. 최종 PNG에서 충전 1·2·3의 가장 높은 불투명 픽셀은 각각 위쪽에서 163·105·33px에 있어 증가를 확인했다.

기존 실제 충돌 시각 `chargeImpactAt`과 `ChargeBeat`를 그대로 사용한다. 2페이즈의 예고 `1.30 × 0.78 + 1.35 = 2.364초`와 위치 고정 후 `0.2초`를 합친 `2.564초`를 세 구간으로 나눈다. 각 약 0.8547초 구간에 9 → 12 → 13번 아우라를 표시하고, 실제 피해가 해결되는 시점에 10번 내려찍기로 바꾼다. 마지막 아우라는 피해 직전까지 유지하며, 별도 시각용 타이머로 공격 속도를 바꾸지 않는다.

## 생성 프롬프트

모든 입력은 각성 원본이며 내장 이미지 생성 도구를 사용했다. 전용 투명 여백을 만들고 연결된 포즈 픽셀만 분리해 이웃 프레임 조각을 제거했다.

### Swing

```text
Use case: identity-preserve. Asset type: transparent 2x2 game animation sprite atlas, FOUR separate poses reading order top-left, top-right, bottom-left, bottom-right. Reference is EXACT awakened knight identity: white/ivory GOLD crown crystal, white diamond forehead jewel, YELLOW scarf and YELLOW cape, golden gloves and bracers, white-gold chest armor with small navy underlayers, brown boots/belt, white leaf-engraved sword with golden hilt/white diamond in screen LEFT hand, brown-gold shield with ivory wing pattern on screen RIGHT, TWO pale ivory light wings behind shoulders. Keep same face and chibi proportions, faceted painted shading/thick brown outlines. NO blue crown, red jewel or red cape. True transparent alpha. No smoky background glow, backdrop, floor, lettering or detached particles. All wings/blades/effects remain connected to the character silhouette. Very wide transparent gutters and 15% outer margin in EVERY cell, uncropped entire sword tips/cape/boots/wings, equal character head size and boot baseline across all poses. Effects must not approach neighboring cells. Four poses of a simple VERTICAL overhead DOWNWARD sword slash: 0 raises sword overhead, 1 holds sword vertically up for short windup, 2 sword swings straight DOWN with a gold-ivory curved vertical trail, 3 crouched downward follow-through sword near ground with SMALL attached gold impact. Wings stay behind body, no spin or diagonal attack.
```

### Dash

```text
Use case: identity-preserve. Asset type: transparent 2x2 game animation sprite atlas, FOUR separate poses reading order top-left, top-right, bottom-left, bottom-right. Reference is EXACT awakened knight identity: white/ivory GOLD crown crystal, white diamond forehead jewel, YELLOW scarf and YELLOW cape, golden gloves and bracers, white-gold chest armor with small navy underlayers, brown boots/belt, white leaf-engraved sword with golden hilt/white diamond in screen LEFT hand, brown-gold shield with ivory wing pattern on screen RIGHT, TWO pale ivory light wings behind shoulders. Keep same face and chibi proportions, faceted painted shading/thick brown outlines. NO blue crown, red jewel or red cape. True transparent alpha. No smoky background glow, backdrop, floor, lettering or detached particles. All wings/blades/effects remain connected to the character silhouette. Very wide transparent gutters and 15% outer margin in EVERY cell, uncropped entire sword tips/cape/boots/wings, equal character head size and boot baseline across all poses. Effects must not approach neighboring cells. Four poses of ALTERNATING DIAGONAL dash attacks: 0 prepare sword raised at upper-left, body leaning forward; 1 slash diagonally from upper-left to lower-right with attached gold-white diagonal trail; 2 prepare opposite slash with sword low left; 3 slash diagonally from lower-left to upper-right with attached gold-white diagonal trail, extended sword hand crossing toward high right while shield remains screen right. Left/right diagonal cuts distinct; do NOT mirror the character or exchange hands. Cape trails backward and wings swept back for speed.
```

### Slam

```text
Use case: identity-preserve. Asset type: transparent 2x2 game animation sprite atlas, FOUR separate poses reading order top-left, top-right, bottom-left, bottom-right. Reference is EXACT awakened knight identity: white/ivory GOLD crown crystal, white diamond forehead jewel, YELLOW scarf and YELLOW cape, golden gloves and bracers, white-gold chest armor with small navy underlayers, brown boots/belt, white leaf-engraved sword with golden hilt/white diamond in screen LEFT hand, brown-gold shield with ivory wing pattern on screen RIGHT, TWO pale ivory light wings behind shoulders. Keep same face and chibi proportions, faceted painted shading/thick brown outlines. NO blue crown, red jewel or red cape. True transparent alpha. No smoky background glow, backdrop, floor, lettering or detached particles. All wings/blades/effects remain connected to the character silhouette. Very wide transparent gutters and 15% outer margin in EVERY cell, uncropped entire sword tips/cape/boots/wings, equal character head size and boot baseline across all poses. Effects must not approach neighboring cells. Four poses of sword ground slam: 0 raised sword overhead neutral; 1 overhead sword at maximum gold charge; 2 deep forward crouch and sword driven DOWN INTO ground in front-left, gold-white attached burst centered at blade tip; 3 low follow-through recovering with sword still low and glow reduced. Preserve exact awakened white crown/yellow cape/ivory wings, no blue/red.
```

### SlamCharge

```text
Use case: identity-preserve. Asset type: transparent 2x2 game animation sprite atlas, FOUR separate poses reading order top-left, top-right, bottom-left, bottom-right. Reference is EXACT awakened knight identity: white/ivory GOLD crown crystal, white diamond forehead jewel, YELLOW scarf and YELLOW cape, golden gloves and bracers, white-gold chest armor with small navy underlayers, brown boots/belt, white leaf-engraved sword with golden hilt/white diamond in screen LEFT hand, brown-gold shield with ivory wing pattern on screen RIGHT, TWO pale ivory light wings behind shoulders. Keep same face and chibi proportions, faceted painted shading/thick brown outlines. NO blue crown, red jewel or red cape. True transparent alpha. No smoky background glow, backdrop, floor, lettering or detached particles. All wings/blades/effects remain connected to the character silhouette. Very wide transparent gutters and 15% outer margin in EVERY cell, uncropped entire sword tips/cape/boots/wings, equal character head size and boot baseline across all poses. Effects must not approach neighboring cells. Four frames with IDENTICAL body/boot/helmet placement and sword held VERTICALLY UP over head. All awakened golden design. 0 white sword NO charge aura. 1 FIRST CHARGE a short thin pale yellow-gold aura sheath hugging blade. 2 SECOND CHARGE a clearly taller and wider saturated golden aura extending above blade and thickening along both sides. 3 THIRD CHARGE a MUCH taller denser rich amber-GOLD aura with bright ivory core extending significantly above blade. All stages remain in GOLD/YELLOW/AMBER palette, NEVER orange-red fire. Charge increase must be obvious by size, height and density, NOT just blinking or sparkles. Keep sword silhouette and leaf engraving visible. Aura attached to blade; wing shape and brightness unchanged across charge frames so sword charge is distinguishable.
```

대시 시트의 마지막 포즈에서 좌우가 뒤집힌 출력이 발생해, 원본만 참조한 단일 상향 베기로 교체했다. `crayon-golden-dash-rising.png`가 최종 3번 포즈의 생성 원본이다. 프롬프트는 동일한 앞왼쪽 방향·화면 오른쪽 방패·왼쪽 검 팔을 유지하면서, 왼쪽 검 팔을 위로 들어 칼끝을 왼쪽 위로 향하게 하고 금빛 궤적이 왼쪽 아래에서 오른쪽 위로 휘어 오르도록 지정했다. 최초/편집된 시트의 마지막 포즈는 최종 게임 출력에 사용하지 않는다.

차징은 아우라 꼭대기가 잘리지 않도록 모든 포즈를 셀 안에서 균일하게 줄이고 최소 15% 투명 여백을 확보하는 추가 편집을 했다. 단계를 무아우라/짧고 얇은 연금빛/높고 넓은 금빛/가장 높은 호박금빛과 흰 중심으로 유지하고, 붉은 불꽃·날개 밝기 변경·배경 안개·떨어진 입자를 제거하도록 요청했다.

## 재현

`scripts/split-crayon-attacks.ps1`은 `Golden_*` 그룹에서 네 포즈별 원본 왕관 보석 등록 좌표를 받는다. 투명 여백을 재배치할 때 이 좌표도 함께 옮기고, 일반 모드 보석 너비와 부츠 바닥에 맞춰 출력한다. 후속 자세는 눌린 원근 때문에 과도하게 작아지지 않도록 등록 너비를 보정했다.

```powershell
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-golden-swing-sheet.png -Attack Golden_Swing -PaddedSourcePath docs/art-prompts/crayon-golden-swing-padded-sheet.png -IvoryCrystals '388,181,87,81;1098,187,85,82;395,612,89,83;1058,668,96,92'
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-golden-dash-sheet.png -Attack Golden_Dash -PaddedSourcePath docs/art-prompts/crayon-golden-dash-padded-sheet.png -IvoryCrystals '305,122,104,90;1182,120,101,102;322,640,110,90;1077,605,88,110'
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-golden-dash-rising.png -Attack Golden_Dash -SingleFrame 3 -IvoryCrystals '707,229,243,150'
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-golden-slam-sheet.png -Attack Golden_Slam -PaddedSourcePath docs/art-prompts/crayon-golden-slam-padded-sheet.png -IvoryCrystals '407,156,122,90;1096,170,122,80;383,670,105,100;1134,679,110,110'
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-golden-charge-sheet.png -Attack Golden_SlamCharge -PaddedSourcePath docs/art-prompts/crayon-golden-charge-padded-sheet.png -IvoryCrystals '310,249,80,62;966,249,80,62;310,884,80,62;966,884,80,62'
./scripts/verify-crayon-awakening.ps1 -GoldenAttacks
```

## 검증

각성/전환 5 PNG와 공격 16 PNG, 총 21개 이미지에서 캔버스·8px 이상 여백·별도 포즈 조각 없음·GUID·Prefab 연결·PPU/pivot 검사와 금빛 충전 3단계 높이 증가를 통과했다.

Unity 검증 메뉴: `Trickal Fan Game > Artwork > Verify Crayon Hero Golden Attacks`.
전체 모션 회귀 메뉴: `Trickal Fan Game > Artwork > Verify Crayon Hero Attack Animations`.
배치 메서드: `TrickalFanGame.Editor.CrayonHeroAttackAnimationVerification.Verify`.
로그: `game/Logs/CrayonHeroGoldenAttackVerification.log`.
일반/각성 피해 시점·교차 대시·3단계 실제 충전 시점·변신/대기 복원·기존 보스 이동 회귀의 성공 로그를 확인했다. 실제 Play Mode 화면 확인은 별도다.


2026-10-06 차징 조정: 예고를 1.05초에서 1.30초로 늘렸다. 방향 고정 후 0.2초는 유지하여 전체 차징은 일반 1.25 → 1.50초, 각성 1.019 → 1.214초로 약 20% 길어진다. 아우라 세 단계는 실제 피해 시점까지의 시간을 자동으로 삼등분한다.

2026-10-06 내려찍기 차징 시간을 조정했다. Prefab과 Week15Boss3Setup의 예고를 1.05 → 1.30초로 맞추었고 방향 고정 후 0.2초는 유지했다. 총 차징은 일반 1.50초, 각성 1.214초이며 아우라 3단계는 실제 피해 시각까지 자동으로 분배된다. 검증기는 Prefab의 실제 패턴 시간을 읽어 검사한다. Unity 6000.3.22f1의 CrayonHeroAttackAnimationVerification.Verify 배치가 종료 코드 0으로 통과했다(로그: game/Logs/CrayonHeroChargeDurationVerification.log). 실제 화면 확인은 별도다.

2026-10-06 추가 조정: 내려찍기 전체 차징에 일반·각성 모두 1.35초씩 추가했다. 일반은 1.50 → 2.85초, 각성은 1.214 → 2.564초이다. BossPatternDefinition.additionalTelegraphDuration은 페이즈 배속을 적용한 예고에 고정 시간으로 더하며, 해당 Prefab과 Setup의 내려찍기만 1.35로 설정했다. 방향 고정 0.2초와 아우라 3단계/피해 동기화는 유지한다. Unity CrayonHeroAttackAnimationVerification.Verify가 종료 코드 0으로 통과했다(로그: game/Logs/CrayonHeroChargeExtensionVerification.log). 실제 화면 확인은 별도다.
