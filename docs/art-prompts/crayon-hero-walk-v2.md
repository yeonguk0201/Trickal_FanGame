# 용사용 걷기 모션 v2 (2026-10-08)

- 도구: 내장 `image_gen.imagegen`, 투명 배경.
- 참조: `game/Assets/Art/Bosses/FairyKingdom/Boss_크레용사용.png`.
- 결과: `crayon-hero-walk-v2-sheet.png`, 4열 × 2행, 행 우선 8프레임.
- 미리보기: `crayon-hero-walk-v2-preview.html` (기본 10fps, 재생/정지/프레임 이동/속도 조절).
- 적용 원본: 사용자가 프레임 간격을 수정하고 미리보기 승인한 `crayon-hero-walk-v3-spaced-sheet.png` (2000×887).
- 적용: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Walk_0..7.png`, 512×512 RGBA, 중앙 pivot, 400 PPU. 정수 픽셀 경계로 분리한 셀을 (6,34)에 원본 픽셀 그대로 복사하여 투명 여백만 더했다. 비율 변경·리샘플링 없음.
- 재현: 저장소 루트에서 `./scripts/split-crayon-walk.ps1`. 기존 0..3의 meta/GUID를 보존하고 새 4..7 meta를 함께 추가했다. 프리팹 걷기 참조와 Setup·검증기의 프레임 수를 8로 변경했다.
- 재생: 기존 이동 거리/보폭 1.6 기준 주기를 유지한다. 미리보기의 고정 10fps와 실제 이동 속도에 따른 프레임 속도는 다르다. 공격 우선순위와 황금 각성 본체 표시는 기존 규칙을 유지한다.
- 자동 검증: Unity 6000.3.22f1의 `TrickalFanGame.Editor.BossMovementAnimationVerification.Verify`, 종료 코드 0 및 `Boss movement verification passed` 확인. 8프레임 연결·크기·pivot·PPU·원래 비율·반복·Idle 복원·이동 방향·물리 보존 검사 통과. 로그: `game/Logs/CrayonWalkV3Verification.log`.
- 추가 공격 회귀 배치: `TrickalFanGame.Editor.CrayonHeroAttackAnimationVerification.Verify`는 검증 로그 없이 시작 단계에서 종료 코드 1. 공격 회귀 통과로 기록하지 않는다. 로그: `game/Logs/CrayonWalkV3AttackVerification.log`.
- 정적 확인: 8장 모두 투명 테두리 검사 통과, 변경한 코드·프리팹 diff 공백 검사 통과.
- 실제 Play 확인은 남아 있다. `Trickal Fan Game > Debug > Open Boss Test Room` → Play → `Boss 3 - Crayon Hero`를 새로 소환해 이동 때 망토·다리·칼·방패가 반복되고 멈출 때 원화로 돌아오며 공격 중에는 공격 그림이 우선하는지 확인한다. 열린 Editor에서 `Trickal Fan Game > Artwork > Verify Crayon Hero Attack Animations`를 실행하면 추가 회귀를 확인할 수 있다.

## 생성 프롬프트

```text
Use case: precise-object-edit. Make an animation sprite sheet based on the referenced EXACT character: the chibi Crayon Hero knight with blue helmet, golden wing ornaments and red gem, white square face, blue tunic, red neck scarf and trailing RED CAPE, brown boots, silver sword on image-left with gold guard and red gem, brown-and-gold shield on image-right with white wings and silver diamond. Preserve this character's identity, proportions, colors, facial expression, equipment design, thick dark brown outlines and painted game sprite rendering.

Deliver a transparent-background 4-column by 2-row sprite sheet containing EXACTLY EIGHT frames of ONE seamless WALK-IN-PLACE CYCLE, ordered left-to-right top row then left-to-right bottom row. Every cell equal size. No captions, numbers, borders, shadows, ground or effects. Each whole character INCLUDING sword tip, helmet wings and cape must fit within its own cell with 12 percent transparent padding all around, no overlapping neighboring cells. Same scale, orientation (three-quarter facing image-left), body registration and baseline in all frames. Head size never changes. Camera stationary.

Animation: clearly articulated alternating steps, NOT eight static copies and NOT running. Frame 1 left boot forward/right boot back contact; 2 weight down and knees bend; 3 passing with rear foot lifted and crossing under hips; 4 weight rises and opposite foot swings forward; 5 right boot forward/left back opposite contact; 6 down; 7 opposite passing; 8 rise, continuing naturally to 1. Preserve short chibi legs but make alternate foot contacts readable. Small body bob only, stable head. Sword-holding arm swings gently in opposition to legs; sword tilts a few degrees, always held securely with intact hilt and blade. Shield arm gives small counter-swing and tilt, shield remains facing viewer with unchanged emblem. Do NOT attack or wave sword.

MOST IMPORTANT: red cape has a visibly DIFFERENT flowing silhouette and folds in EVERY frame: wave passes from shoulders to free tail with delayed secondary motion, alternately lifts/curls outward and relaxes lower, then lifts again across the cycle. Cloth is attached consistently behind neck, never becomes a second limb; cape stays behind body and shield. Natural graceful loop, not random shape changes. Scarf tips subtle secondary flutter. Preserve exact design with no added ornaments and no duplicated limbs. Actual transparent alpha background.
```

