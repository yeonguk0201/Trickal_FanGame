# 쫄따구 사망 폭발 분홍 폭발 v1

Motion-0 조각. 쫄따구 사망 폭발(`EnemyDeathBlast`)이 임시로 빌려 쓰던 폭발 머핀의 금빛 `circular-explosion` 시트를 뺐다.
그 자리에 기존 분홍 원화(`effect-crumb-death`)를 둘러싸는 분홍 실시간 효과를 더했다. 사용자 요청(2026-10-18):
"쫄따구 터지는 에셋이 분홍색 계열이니 거기에 효과를 추가하는 거라 분홍 계열로 터지는 효과".
폭발 3종은 서로 다른 에셋을 쓴다는 결정(2026-10-13)도 이것으로 충족한다.

## 목적과 사용처

- 발생 이벤트: `EnemyDeathBlast.Explode`. 예고 0.6초 뒤 터지는 순간 1회.
- 전달 정보: 터지는 순간과 실제 피해 반경(1.2). 플레이어에게 위험한 폭발이지만 원화와 같은 분홍으로 통일했다.
- 피해 반경·시점·대상, 예고(그림자 색 보간·몸 붉어짐), 분홍 원화의 크기(몸 폭 × 1.2)와 0.24초 표시는 바꾸지 않았다.

## 제작 구성

색은 원화에서 땄다: 사탕 분홍, 진분홍 그늘, 밤색 외곽선, 크림색 하이라이트, 황갈색 케이크 부스러기.

| 레이어 | 제작 방식 | 크기 규칙 | 타이밍(초) | 정렬(몸 기준) |
|---|---|---|---|---|
| 연기 | Particle System 6개, World, 연분홍 | 크기 0.36 ~ 0.56 × (반경/1.2) | 0.06에 방출, 0.6 전에 소멸 | +0 |
| 충격파 링 | 쿼드 + 링 모드, 노이즈 침식, 흰분홍 → 진분홍 | 바깥 가장자리 반경 0.35 → 1.0, 피해 반경에서 멈춤 | 0.02 ~ 0.36 | +1 |
| 분홍 구름 | 쿼드 + 거품 8개 합집합, 셀 음영·밤색 외곽선, 중심부터 찢어지며 비는 구멍 | 최대 반경 × 0.56 | 0 ~ 0.34, 구멍 0.08부터 | +2 |
| (분홍 원화) | 기존 `FairyKingdomSpriteEffect` | 몸 폭 × 1.2 | 0 ~ 0.24 | +3 |
| 부스러기 | Particle System 8개, 사각 덩어리 셰이더, 회전·낙하 | 반경 × 0.95 안 | 0에 방출, 수명 0.38 ~ 0.58, 줄어들며 소멸 | +4 |
| 젤리 방울 | Particle System 12개, 광택 구슬(밤색 테두리·흰 하이라이트), 짧은 Stretch, 낙하 | 반경 × 0.95 안 | 0에 방출, 수명 0.32 ~ 0.52 | +5 |
| 중심 섬광 | 쿼드 + 분홍빛 흰 방사형 | 반경 × 0.5 | 0 ~ 0.06 | +6 |

- 셰이더: `game/Assets/Resources/CombatEffects/CrumbDeathBlast.shader`(`_Mode` 0~5). 폭발 머핀·폭탄이 쓰는 `MuffinExplosion.shader`는 건드리지 않았다.
- 런타임: `game/Assets/Scripts/Combat/CrumbDeathBlastEffect.cs`. 블라스트와 별개 오브젝트로 0.6초 재생하고, 블라스트의 방이 비활성화되면 즉시 제거한다.
  정적 카운터·해시 시드로 `UnityEngine.Random`을 소비하지 않는다.
- `circular-explosion.png`와 GUID는 그대로다. 에르핀 저학년 유도탄이 계속 쓴다.
- Scene 뷰 미리보기: `Trickal Fan Game/Effects/Preview Crumb Death Blast`. 분홍 원화(게임과 같은 0.24초·페이드)와 레이어별 표시, 피해 반경, 원화 폭, 시간 조절. 배경 합성은 지원하지 않는다.

## 검수와 검증

- 대표 형태: Edit Mode 정지 렌더 `output/art-review/crumb-death-blast/unity-frames.png`(0.01 ~ 0.55초 9프레임, 위: 원화 포함, 아래: 새 레이어만).
  Claude가 렌더를 보고 두 번 조정했다(방울·부스러기가 구름 안에 갇힘, 구름이 납작한 과자처럼 보임, 링이 밝은 바닥에서 약함, 연기가 회색 → 방울이 꽃잎처럼 겹쳐 보임).
  사용자가 렌더와 실제 Play를 보고 "좋다, 맘에 들어"라며 수용(2026-10-18).
- 자동 검증(2026-10-18): `CrumbDeathBlastVerification.VerifyAndExportBatch` 종료 코드 0. 셰이더, 원화 기준 정렬, 링이 피해 반경을 넘지 않고 도달,
  구름 크기 상한과 0.24초 전후 중심 비움, 방울·부스러기 반경 안 유지와 원화 뒤까지 남음, 0.6초 뒤 잔여 없음, 같은 시드 재현과 `UnityEngine.Random` 미소비, 기존 시트 보존.
- 커밋 전 회귀(2026-10-18): `CrumbDeathBlastVerification.VerifyWithRegressionsBatch` 종료 코드 0 (새 효과, 요정왕국 원화 연결, Enemy-7 블라스트 규칙, `output/crumb-death-blast-precommit.log`).
- 실제 Play(2026-10-18): 사용자 수동 확인 완료. 확인 경로는 `F1 → — Room — → Spawn crumb minion (explodes on death)`으로 쫄따구를 소환해 처치하고,
  벽 근처·여러 마리 연쇄 같은 개별 조건별 기록은 없다.

## 최종 적용 상태

- v1, `EnemyDeathBlast.Explode`에 연결. 사용자 수용(2026-10-18).
