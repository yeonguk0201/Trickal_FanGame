# 에르핀 노란 에너지 구체 (2026-10-05)

내장 imagegen으로 기본공격·저학년이 공유하는 원형 노란 에너지 구체를 생성했다.
생성 ID: `1650c667-2d0f-4752-9635-a7390fb29845`.

## 생성 프롬프트

One isolated circular yellow energy orb projectile for Erpin in a cute painterly fantasy mobile game. Perfect ROUND spherical silhouette, centered white warm luminous core, bright lemon yellow body with soft golden rim, smooth readable radial energy shading and a few subtle curved internal light strokes. Clean crisp small-game-sprite readability. A tight modest yellow glow halo hugging the sphere only. Symmetric in every direction, NO teardrop, NO directional tail, NO comet trail, NO spikes, no character, no staff, no face, no stars or detached sparks, no cast ground shadow, no text. Single orb centered with generous transparent padding, true transparent alpha. This will be reused as a smaller basic attack and a larger low-grade skill orb; one unified yellow round design.

## 에셋과 판정

투명 여백을 정리하고 최대 480px로 균등 축소해 512×512 캔버스 중앙에 등록했다.
같은 픽셀 그림을 두 Sprite로 저장해 각 공격의 표시 크기를 독립적으로 설정한다.

| 구분 | 파일 (`Assets/Art/Characters/Projectiles/`) | PPU | 루트 배율 | 원형 판정 반경 |
|---|---|---|---|---|
| 기본공격 | `Erpin_Basic_Orb.png` | 480 | 0.4 | 0.20 |
| 저학년 | `Erpin_LowGrade_Orb.png` | 240 | 0.28 | 0.14 (유지) |

기본공격 루트 배율은 0.5→0.4로 20% 감소했고 히트박스 반경도 0.25→0.20으로 감소했다.
기본공격 그림의 최대 지름은 약 0.4 world units, 저학년은 약 0.56으로 40% 크다.
저학년 그림의 발광 테두리는 접촉 판정이 아니며 폭발 반경 1.25, 유도와 SP·연사 규칙은 유지한다.
속도·사거리·피해와 적/보스 투사체 크기는 유지한다. 분열탄은 축소된 기본공격 프리팹을 그대로 상속한다.
기존 두 프리팹의 GUID와 Scene 참조를 보존하고 Sprite·흰색 tint·공통 배율만 구성한다.

## 검증

`ErpinProjectileArtworkSetup.SetupAndVerifyBatch`를 실행해 두 번 구성한 뒤 Sprite·색상·표시 크기 비율·
실제 원형 Collider 크기·저학년 폭발 반경을 검사했다. 공통 투사체 판정, 저학년, 아이템 투사체 효과와
사거리 회귀를 통과했다. `game/Logs/erpin-projectile-artwork.log`, 종료 코드 0.
새 Run에서 방향키 기본공격과 Space 저학년으로 노란 구체의 크기 차이와 좁은 틈의 통과를 확인한다.
