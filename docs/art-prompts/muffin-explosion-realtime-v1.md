# 폭발 머핀 실시간 폭발 v1

Motion-0 조각. 원화 없이 셰이더·쿼드·월드 파티클로 만든 작은 폭발이다. 폭발 3종은 서로 다른 에셋을 쓴다는
사용자 결정(2026-10-13)에 따라 폭발 머핀만 `circular-explosion` 시트에서 분리했다.

## 목적과 사용처

- 발생 이벤트: `PlayerBasicAttackExplosion.Explode`. 기본 공격 8회 적중마다 맞은 적 위치에서 1회.
- 전달 정보: 타격 순간과 실제 피해 반경(1.5).
- 기준점: 맞은 적의 transform 위치. 피해 반경·피해 시점·대상 판정은 바꾸지 않았다.
- 결정(사용자, 2026-10-16): 색은 일반적인 폭발(흰 노랑 중심 → 주황 → 붉은 가장자리), 재생 시간은 기존 0.4초 유지.

## 제작 구성

| 레이어 | 제작 방식 | 크기 규칙 | 타이밍(초) | 정렬 |
|---|---|---|---|---|
| 중심 섬광 | 쿼드 + 방사형 그라디언트 | 반경 × 0.62 | 0 ~ 0.07 | 원본 +5 |
| 불덩이 | 쿼드 + 노이즈 외곽, 중심부터 찢어지며 비는 구멍 | 최대 반경 × 0.5 | 0 ~ 0.26, 구멍 0.06부터 | +3 |
| 충격파 링 | 쿼드 + 링 셰이더, 노이즈 침식 | 바깥 가장자리가 정확히 피해 반경에서 멈춤 | 0.015 ~ 0.3 | +2 |
| 불꽃 조각 | Particle System 14개, World, Stretch | 반경 × 0.92 안에서 멈춤 | 0에 방출, 수명 0.17 ~ 0.3 | +4 |
| 연기 | Particle System 6개, World | 크기 0.42 ~ 0.64 × (반경/1.5) | 0.07에 방출, 0.4 전에 소멸 | +1 |

- 정렬 원본은 이전과 같은 플레이어 SpriteRenderer다.
- 셰이더: `game/Assets/Resources/CombatEffects/MuffinExplosion.shader` (`_Mode` 0~4로 레이어 구분, 알파 블렌드라 연속 발생해도 화면이 하얗게 차지 않음).
- 런타임: `game/Assets/Scripts/Combat/MuffinExplosionEffect.cs`. 시드는 정적 카운터와 해시를 쓰며 `UnityEngine.Random`을 소비하지 않는다.
- 방 이동(소유자 4 이상 순간 이동)·소유자 비활성 때 즉시 제거한다. `CombatSpriteEffect`와 같은 규칙이다.
- `circular-explosion.png`와 GUID는 그대로다. 쫄따구 사망 폭발·에르핀 저학년 유도탄이 계속 쓴다.
- Scene 뷰 미리보기: `Trickal Fan Game/Effects/Preview Muffin Explosion`. 런타임과 같은 쿼드·셰이더·방출 규칙을 쓰고,
  시간을 옮길 때 같은 시드로 입자를 1/60초 단위로 다시 시뮬레이션한다. 레이어별 표시, 기준 적, 기존 시트 A/B, 피해 반경 조절을 지원한다.
  배경 합성과 플레이어 캐릭터 동시 표시는 지원하지 않는다.

## 검수와 검증

- 대표 형태: Edit Mode 정지 렌더 `output/art-review/muffin-explosion/unity-frames.png`(0.01 ~ 0.38초 9프레임, 아래 행은 기존 시트). Claude가 렌더를 보고 불덩이 색(너무 흰 문제)·불꽃 조각 크기를 조정했다. 조정 뒤 사용자가 "지금 느낌이 딱 좋다"며 수용(2026-10-16). 연기 포함 상태 그대로.
- 자동 검증(2026-10-16): `MuffinExplosionVerification.VerifyAndExportBatch` 종료 코드 0. 셰이더 컴파일, 레이어 정렬,
  링이 피해 반경을 넘지 않고 도달함, 불덩이 크기 상한과 0.2초까지 중심 비움, 불꽃 조각 반경 안 유지, 0.4초 뒤 잔여 없음,
  같은 시드 재현과 `UnityEngine.Random` 미소비, 기존 시트 GUID 보존.
- 커밋 전 회귀(2026-10-16): `MuffinExplosionVerification.VerifyWithRegressionsBatch` 종료 코드 0 (새 효과·검수된 전투 이펙트·Artifact-2 피해 규칙, `output/muffin-explosion-precommit.log`).
- 실제 Play: 벽 근처·여러 적·연속 발생의 개별 조건은 기록된 확인 근거 없음. `F1 → — Artifact — → Show artifact drops → Drop 폭발 머핀` 획득 후 기본 공격 8회 적중.
  중앙·벽 근처·여러 적·연속 발생에서 적 가독성과 화면 가림, 연기를 남길지 판단한다.

## 최종 적용 상태

- v1, `PlayerBasicAttackExplosion`에 연결. 사용자 수용(2026-10-16).
