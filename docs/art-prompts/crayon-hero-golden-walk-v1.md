# 각성 용사용 걷기 모션 초안 (2026-10-11)

미리보기 등록 보정: 아래 줄 5~8번의 짙은 윤곽 접지선은 셀 내부 y=342~346, 위 줄 1~4번은 y=360~365였다. 아래 줄만 원본 기준 19px 아래로 표시해 줄 사이 높이 차를 보정했다. 포즈별 작은 상하 움직임·이미지 비율·원본 PNG는 유지한다. Unity 프레임 분리에도 같은 줄 오프셋을 반영했다.

## 최종 재생성 프롬프트

```text
Create a NEW clean production-ready sprite atlas using these two references. Reference 1 is the exact AWAKENED ivory-and-gold chibi knight identity. Reference 2 gives only an eight-step WALK cycle idea (ordinary colors must never appear). Genuine transparent background, clean crisp painterly brown outlines, NO colored edge speckles or aura.

LAYOUT IS CRITICAL: 4 columns x 2 rows of equal SQUARE cells. Each COMPLETE character including wide wings and cape must be SMALL inside the cell, maximum 60 percent cell width and maximum 60 percent cell height. Thus leave 20 percent transparent empty padding on EVERY side of EVERY cell. LARGE blank gutters separating the eight characters; no wings or sword touches another cell. Same character size and body center in all eight cells. Draw eight deliberately small full-body characters inside a spacious atlas, NOT eight characters filling the sheet.

The character ALWAYS has ivory white-gold helmet crystal and ivory forehead diamond, GOLD gloves, ivory/gold tunic, yellow scarf and flowing YELLOW cape, brown boots, ivory white sword in image-left hand, gold/brown shield with ivory wings and central ivory diamond in image-right hand, TWO small pale ivory light wings behind shoulders. Preserve exact first-reference style and proportions. Same three-quarter facing left in all eight frames. No blue gems, no red clothing, no brown gloves. Never mirror.

ONE seamless natural walk-in-place cycle row-major order: 1 left boot forward contact, 2 bend knees settle weight, 3 passing lifted rear foot, 4 rise opposite boot forward, 5 opposite contact, 6 down, 7 opposite passing, 8 rise continuing into1. Readable alternating leg movement with small steady head bob, no lunging or running. Sword and shield sway a little with arms, no attack. Each frame yellow cape has a distinct soft flowing wave silhouette: tail raised, curled, relaxes, then rises again; remains attached behind neck. Light wings only gently flex, never flap for flight. Facial design and weapon shapes remain consistent. No effects, no floating particles, no shadows, no text, no frames or labels.
```

## 확인 상태

최종 시트에서 8개 포즈와 노란 망토·흰금색 장비·빛 날개를 확인했다. 투명 배경과 높이 보정 미리보기를 사용자가 승인하여 게임에 연결했다. 날개·망토 주변의 생성 잔점은 승인 시트 그대로 남아 있다.

## Unity 적용

- 에셋: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Golden_Walk_0..7.png`, 각 1024×1024 RGBA, 중앙 pivot, 400 PPU. 기존 각성 원화·공격 에셋 GUID는 유지하고 새 프레임 meta를 함께 추가했다.
- 재현: 저장소 루트에서 `./scripts/split-crayon-golden-walk.ps1`. 각 443.5px 셀을 가로·세로 동일 1.6배로 배치한다. 기본 위치 (157.2,152), 아래 줄은 19×1.6px 아래로 보정한다. 8장 모두 투명 테두리 검사를 통과했다.
- `BossMovementAnimator.goldenWalkFrames`와 프리팹·Setup을 연결했다. 실제 이동 거리에 따른 기존 주기를 사용한다. 각성 중 정지는 기존 각성 본체로 복원하고, 변신과 공격 표시를 우선한다.
- 단독 검증: `TrickalFanGame.Editor.CrayonHeroAttackAnimationVerification.VerifyAwakening`. 8프레임 순서·등록·반복·Idle 복원·공격 우선순위·일반 페이즈 초기화를 검사한다.
- 결과: Unity 6000.3.22f1 로그에서 `Crayon Hero awakening verification passed: timed snap transformation, eight golden walking poses, attack priority, awakened idle and phase reset.`와 종료 코드 0 기록을 확인했다. 로그: `game/Logs/CrayonGoldenWalkVerification.log`. 변경한 코드·프리팹·문서의 diff 공백 검사도 통과했다.
- 실제 Play 확인: `Trickal Fan Game > Debug > Open Boss Test Room` → Play → `Boss 3 - Crayon Hero` 새 소환 → HP 50% 이하. 이동·정지 및 4→5·8→1 연결을 확인한다. 실제 Play 화면 검수는 아직 미확인이다.


- 도구: 내장 `image_gen.imagegen`, 투명 배경.
- 디자인 참조: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Awakened.png`.
- 움직임 참조: 승인된 `crayon-hero-walk-v3-spaced-sheet.png`.
- 결과: `crayon-hero-golden-walk-v1-sheet.png`, 4열 × 2행, 행 우선 8프레임.
- 미리보기: `crayon-hero-golden-walk-v1-preview.html`. 기본 10fps, 재생/정지/프레임 이동/속도 조절. 셀의 원본 가로·세로 비율을 유지한다.
- 범위: 걷기 원화 제작·반복 재생 미리보기·Unity 연결. 실제 Play 검수는 아직 수행하지 않았다.

## 생성 프롬프트

```text
undefined
```

