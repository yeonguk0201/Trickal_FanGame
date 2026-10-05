# 크레용사용 공격 모션 (2026-10-06)

내장 `image_gen.imagegen`으로 포즈 간격을 넓히고 칼에 세 단계 누적 아우라를 만들었다. 기존 에셋·프리팹 GUID는 보존했다.

최종 에셋: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_{Swing,Dash,Slam,SlamCharge}_0..3.png` 16장, 각 1024×1024, 중앙 pivot, 400 PPU. 프리팹은 14장을 사용한다. 준비·차징은 SlamCharge를, 내려찍기 타격·후속은 Slam을 사용한다.

## 포즈 분리

종전에는 본체 사각 영역을 통째로 복사해 그 안의 이웃 포즈 조각까지 가져왔다. 새 코드는 전체 시트에서 포즈별 연결된 픽셀만 추출한다. 네 포즈를 2048×2048 시트의 1024px 셀에 배치하며 각 셀 최소 112px 투명 여백과 중앙 224px 간격을 확보한다. 이후에도 해당 포즈의 픽셀만 복사한다. 셀 경계를 먼저 자르지 않아 큰 아우라도 보존된다.

투구 크기·중심과 발 접지선으로 등록하며, 512px 원본에 256px 여백을 더해 큰 검과 아우라를 포함한다. 출력이 8px 투명 테두리를 침범하면 분리가 실패한다.

- [일반 공격 시트](./crayon-swing-padded-sheet.png)
- [대시 시트](./crayon-dash-padded-sheet.png)
- [내려찍기 시트](./crayon-slam-padded-sheet.png)
- [누적 아우라 시트](./crayon-charge-aura-padded-sheet.png)

## 동작과 타이밍

일반 공격은 머리 위 준비 후 실제 swingResolvesAt에 내려 베고 후속 포즈를 표시한다. 대시는 실제 대시마다 반대 대각선을 번갈아 사용하며 재조준 중 다음 베기 준비 그림을 표시한다.

내려찍기는 작은 노란 아우라 → 더 높은 주황 아우라·금색 칼 → 큰 붉은 아우라·주홍색 칼로 누적된다. 각 아우라는 다음 단계까지 유지되며 마지막 단계는 타격 직전까지 남는다. 예고 시작부터 실제 slashResolvesAt까지 세 구간으로 나누고, Active 진입 때 실제 확정된 타격 시각을 반영한다. 1페이즈는 예고 2.65초+방향 고정 0.2초, 2페이즈는 예고 2.364초+고정 0.2초를 따른다. 피해 해결 틱에 타격 그림으로 전환한다.

공격 표시가 걷기보다 우선한다. 페이즈 전환·사망·비활성화·다음 패턴에서 초기화한다. 기존 피해·범위·이동·연속 대시당 한 번의 피해 규칙을 유지한다.

## 구성과 검증

프리팹: `Assets/Prefabs/CrayonHeroBoss.prefab`.
재구성: `Trickal Fan Game > Artwork > Setup Boss Movement Animations`.
검증: `Trickal Fan Game > Artwork > Verify Crayon Hero Attack Animations`.
배치: `TrickalFanGame.Editor.CrayonHeroAttackAnimationVerification.Verify`.

분리 재현(PowerShell 7): `scripts/split-crayon-attacks.ps1 -SourcePath <생성 원본> -PaddedSourcePath <여백 확장 시트> -Attack Swing|Dash|Slam|SlamCharge`. 이미 확장한 시트는 PaddedSourcePath 없이 분리한다.

2026-10-06 수정 후 별도 System.Drawing 검사에서 최종 16장 모두 1024px 크기·8px 투명 테두리·연결 영역 검사를 통과했다. 모든 이미지의 두 번째 연결 영역 크기는 0으로 이웃 조각이 없었다. UTF-8·프리팹 참조·diff 공백을 정적으로 검사한다. Unity 검증기에 이미지 분리와 각 차징 단계의 시작·유지·마지막 단계에서 타격 전환 검사를 추가했다.

이번 Unity 배치는 열린 프로젝트 잠금 상태에서 검증 진입 전 종료 코드 1로 끝났다(`game/Logs/CrayonHeroAuraVerification.log`). 이전 반짝임 버전의 대상·이동 검증 종료 코드 0을 새 아우라 버전의 자동 통과로 간주하지 않는다. 이전 넓은 보스 회귀는 기존 Boss-2 Game Scene 조립기·프리팹 참조 검사에서 실패했다.

열린 Editor에서 검증 메뉴를 실행한다. 기대 로그: `Crayon Hero attack animation verification passed: overhead swing, alternating dash, three timed charges, impact damage and cancellation.`
화면 확인: `Trickal Fan Game > Debug > Open Boss Test Room` → Play → `Boss 3 - Crayon Hero`. 공격에 다른 포즈 조각이 없어야 한다. 노랑·주황·빨강 아우라가 차례로 커지고 마지막 아우라가 타격 직전까지 남아야 한다. 세 충전 중에는 피해가 없고 내려찍는 순간 피해를 받아야 한다. 2페이즈에서도 세 단계가 유지되며 빨라져야 한다.

## 간격 확장 프롬프트

### swing

Use case: precise-object-edit. Re-layout this exact 2x2 four-pose game sprite sheet with MUCH MORE EMPTY TRANSPARENT SPACE. Preserve all four original character poses, weapons, outfits, facing, diagonals, art and body proportions. Each full pose including blade, cape, slash arc and impact effect must fit COMPLETELY inside central 65 percent of its own equal square cell. Leave at least 16 percent totally transparent margin on EVERY side of EVERY cell; wide clear transparent gutters across both central grid axes, no pixels anywhere near those dividers. All four same torso/head size and same feet baseline in each cell, stable registration, full uncropped weapons and effects. Do not draw grid lines or labels. True alpha transparency, no shadows or checkerboard. Layout only; never allow any pose or effect to enter another cell. For swing retain the exact four motions from reference.

### dash

Use case: precise-object-edit. Re-layout this exact 2x2 four-pose game sprite sheet with MUCH MORE EMPTY TRANSPARENT SPACE. Preserve all four original character poses, weapons, outfits, facing, diagonals, art and body proportions. Each full pose including blade, cape, slash arc and impact effect must fit COMPLETELY inside central 65 percent of its own equal square cell. Leave at least 16 percent totally transparent margin on EVERY side of EVERY cell; wide clear transparent gutters across both central grid axes, no pixels anywhere near those dividers. All four same torso/head size and same feet baseline in each cell, stable registration, full uncropped weapons and effects. Do not draw grid lines or labels. True alpha transparency, no shadows or checkerboard. Layout only; never allow any pose or effect to enter another cell. For dash retain the exact four motions from reference.

### slam

Use case: precise-object-edit. Re-layout this exact 2x2 four-pose game sprite sheet with MUCH MORE EMPTY TRANSPARENT SPACE. Preserve all four original character poses, weapons, outfits, facing, diagonals, art and body proportions. Each full pose including blade, cape, slash arc and impact effect must fit COMPLETELY inside central 65 percent of its own equal square cell. Leave at least 16 percent totally transparent margin on EVERY side of EVERY cell; wide clear transparent gutters across both central grid axes, no pixels anywhere near those dividers. All four same torso/head size and same feet baseline in each cell, stable registration, full uncropped weapons and effects. Do not draw grid lines or labels. True alpha transparency, no shadows or checkerboard. Layout only; never allow any pose or effect to enter another cell. For slam retain the exact four motions from reference.

## 누적 아우라 프롬프트

Use case: identity-preserve. Production game sprite sheet of exact crayon knight boss in reference, ONLY raised sword charge poses. Four equal square cells in 2x2 transparent atlas. Top-left: same raised sword stance with ordinary silver blade, NO aura. Top-right: identical stance, first charge, SMALL thin translucent pale yellow flame aura hugging silver blade. Bottom-left: identical stance, second charge, visibly TALLER and WIDER dense orange flame aura up to one quarter of blade length beyond sword tip, blade colored orange-gold. Bottom-right: identical stance, third charge, MUCH TALLER, wider saturated red-orange flame aura with bright core extending half a blade length above sword tip, blade saturated deep orange-red. Charge intensity and size must be unmistakably increasing from weak yellow to orange to strong red. SAME exact body pose, hands, sword angle, helmet/face/shield/cape/boots position, body scale and baseline in ALL FOUR. Preserve original knight identity blue crystal gold wing helmet red jewel, red cape blue tunic brown boots, sword on screen LEFT held above head and gold wing shield on RIGHT, faces left-front 3/4, no mirrors. Clean thick dark outline painterly shading. ALL parts including the tallest aura must fit inside central 65 percent of each cell. At least 16 percent EMPTY transparent margin at EACH cell edge; WIDE transparent center gutters. Full figure including all effects uncropped, transparent alpha, no text/grid/labels/shadow/background. Charge aura is attached ONLY to blade, do not glow entire character. No small sparkle as substitute for substantial flame aura.

원래 시트와 편집 후 생성 원본(`*-spaced-sheet.png`, `crayon-charge-aura-sheet.png`)은 비교·재현용이다. 최종 분리 입력은 `*-padded-sheet.png`다.
