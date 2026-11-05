# 자원 상자 피격 파손 프레임

2026-11-05. 사용자가 제공한 파손 원화를 자원 장애물 8종에 연결했다. 바위 균열 프레임([작업 기록](rock-damage-frames.md))과 같은 방식이다. 무피격 상태는 기존 원화를 쓰고, 마지막 타격의 파괴·드롭·폭발·쥬비 방출 규칙은 바꾸지 않는다.

| 종류 | 타격 수 | 프레임 |
| --- | --- | --- |
| 마리의 폭탄상자, 마요의 열쇠꾸러미, 에르핀의 간식상자, 에슈르의 빵상자, 리코타의 음식상자, 셰이디의 랜덤박스, 마요의 수집품 상자 | 4 | 1·2·3회 타격 후 가벼운 파손 → 깊은 파손 → 파괴 직전 |
| 폭발 상자 | 2 | 1회 타격 후 1장 |

시스트의 금고는 폭탄·열쇠로만 열려 타격 단계가 없으므로 대상이 아니다.

## 에셋과 연결

- `game/Assets/Resources/RockDamage/obstacle-<종류>.png`는 받은 PNG 원본 그대로다(가로 3칸, 폭발 상자는 1칸).
- 같은 이름의 JSON에 `frames`(각 칸의 표시 사각형)와 `bodies`(그 칸에서 원본 무피격 원화가 놓인 사각형)를 Unity 좌하단 좌표로 기록한다.
  - 칸 경계는 공칭 1/3 지점 ±60px 중 알파가 가장 낮은 열이다. 셰이디 랜덤박스의 보라 연기가 칸 경계에서 잘려 보이지 않게 한다(경계 최대 알파 10~14).
  - `frames`는 알파 8 이상의 경계라 파편과 연기를 포함한다.
  - `bodies`는 원본 원화의 알파 96 이상 실루엣(Catalog의 `visibleRect`와 같은 기준)을 배율·위치 탐색으로 각 프레임에 맞춘 결과다. IoU 0.82~0.96.
- `RockDamageArtwork`는 `bodies`가 있으면 상자 본체가 원본과 같은 폭·중심에 오도록 Sprite의 pivot·PPU를 정한다. 그래서 바닥·옆으로 흩어진 파편 때문에 상자가 작아지거나 뜨지 않는다. `bodies`가 없는 바위 시트는 기존 계산을 유지한다.
- 상자에도 바위처럼 타격 시 회색 어둡게 하기를 끄고 흰색을 유지한다. 충돌·타격 수·프리팹·Catalog GUID는 변경하지 않는다.

## 검증

- 2026-11-05: Unity 6000.3.22f1 배치 `TrickalFanGame.Editor.RockDamageArtworkVerification.VerifyBoxes` 통과, 종료 코드 0. 로그: `tmp/box-damage-verification.log`. 8종 22프레임, 흰색, 원본 대비 크기·중심·바닥 위치, 충돌·타격 수, 조기 파괴 없음을 검사한다.
- 같은 날 공용 코드 변경 회귀로 `RockDamageArtworkVerification.Verify`(바위 3종 9프레임) 통과, 종료 코드 0.
- 자동 메뉴: Edit Mode → `Trickal Fan Game/Artwork/Verify Box Damage Frames`.
- 2026-11-05: 실제 Play 미술 검수를 사용자 확인으로 수용했다. 맵을 돌며 상자를 타격해 확인했다. 원본 대비 상자 크기가 살짝 다르지만 거슬리지 않는 수준이라 그대로 둔다.
- 재검수 방법: Game Scene에서 상자를 1·2·3회 타격(폭발 상자는 1회)하며 파손 진행, 위치·크기 튐, 마지막 타격의 파괴 결과를 본다. `F1 → — Obstacle — → Make <종류>` 버튼은 현재 방에 특수 장애물 자리(`RoomObstacleVariantSlot`)가 있을 때만 나온다. 없으면 `No unbroken obstacle slot in this room.`만 표시된다.
