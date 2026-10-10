# 요정왕국 에셋 크기 불일치 (임시 메모)

- 작성: 2026-10-10
- 상태: **원인 확인만 완료, 수정 전.** 수정이 끝나면 이 문서는 지우고 결과를 `docs/05-development-setup.md`의 요정왕국 에셋 절에 반영한다.
- 범위: `FairyKingdomArtworkSetup`으로 연결한 초안 그림(`Assets/Art/Drafts/FairyKingdom/`)의 게임 내 표시 크기.

## 원인

두 가지가 겹쳐 있다.

1. **가로 폭만 맞춘다.** `FairyKingdomArtworkCatalog.Fit(id, width)`는 그림의 보이는 영역(`visibleRect`, 알파 96 이상)을 가로 폭에 맞추고 세로는 비율대로 따라간다. 같은 1×1 칸 안에서도 그림 비율에 따라 높이가 달라진다.
2. **기준 폭이 예전 임시 스프라이트의 전체 폭이다.** `FairyKingdomArtworkSetup.ApplyHierarchy`가 `localWidth`를 교체 전 스프라이트의 `bounds.size.x`로 잡는다. 적은 투명 여백까지 포함한 폭(1.254)에 보이는 영역을 꽉 채우므로 판정보다 크게 그려진다.

## 현재 크기

월드 단위. 그림 크기는 `localWidth × 표시 Transform scale`과 `visibleRect` 비율로 계산한 값이며, 실제 Play 화면으로 확인한 값은 아니다.

| 대상 | 프리팹 | 판정 크기 | 현재 그림 크기 (가로×세로) | 비고 |
|---|---|---|---|---|
| 파괴 장애물 | `DestructibleObstacle` | 1×1 | 가로 1.0 고정, 세로 0.75~1.11 | 아래 표 참고 |
| 구덩이 | `RoomPit` | 1×1 | 1.0×0.61 | 칸의 위아래가 빔. `Hole` 렌더러는 꺼져 있음 |
| 비밀 구덩이 | `SecretPit` | 지름 0.9 | 0.9×0.64 | |
| 상자 (닫힘) | `TreasureChest` | 0.8×0.8 | 0.8×0.60~0.66 | 일반 0.64, 황금 0.66, 다이아 0.60 |
| 상자 (열림) | `TreasureChest` | 0.8×0.8 | 0.8×0.68~0.73 | 일반 0.73, 황금 0.73, 다이아 0.68 |
| 폭탄 픽업·설치 폭탄 | `BombPickup`, `PlacedBomb` | 지름 0.5 | 0.5×0.51 | |
| 열쇠 픽업 | `KeyPickup` | 지름 0.4 | 0.4×0.43 | |
| 부스러기 쫄따구 | `BuseureogiCrumbMinion` | 지름 0.72 | 0.90×0.96 | 판정보다 약 25% 큼 |
| 쥬비 | `JyubiEnemy` | — | 0.63×0.56 | 기존 그림은 약 0.53×0.58 |
| 기둥 | `room-large-central-pillar` 등 | 방마다 다름 | 가로 1 기준, 세로 1.36배 | 부모 scale이 비균등이면 왜곡 여부 확인 필요 |

### 파괴 장애물 종류별 세로 높이 (가로 1.0 기준)

| 종류 | 세로 |
|---|---|
| `obstacle-eshur-bread-box` | 0.75 |
| `obstacle-erpin-snack-box` | 0.78 |
| `obstacle-ricotta-food-box` | 0.78 |
| `obstacle-marie-bomb-box` | 0.79 |
| `obstacle-mayo-collection-box` | 0.80 |
| `obstacle-rock` | 0.83 |
| `obstacle-mayo-key-bundle` | 0.84 |
| `obstacle-gold-rock` | 0.90 |
| `obstacle-shady-random-box` | 0.90 |
| `obstacle-tree` | 1.05 |
| `obstacle-sist-vault` | 1.07 |
| `obstacle-explosive-box` | 1.11 |

세로가 1을 넘는 세 종류는 칸 밖으로 넘친다.

## 수정 제안 (미확정)

맞춤 방식을 바꾸면 게임 내 모든 연결 그림의 크기가 달라지므로 기준을 먼저 정한다.

- **장애물·상자·픽업**: 긴 변을 칸에 맞춘다 (칸 밖으로 넘치지 않음).
- **구덩이**: 칸을 꽉 채운다.
- **적**: 판정 지름 기준 배율로 맞춘다. 예전 스프라이트의 여백 포함 폭을 기준으로 쓰지 않는다.

### 정해야 할 것

- 나무처럼 칸보다 높게 그려지는 것이 의도인 종류가 있는지 (있다면 바닥 기준선 정렬로 처리).
- 구덩이를 칸에 꽉 채울 때 비율 왜곡을 허용할지, 그림을 다시 받을지.
- 적 그림을 판정 대비 몇 배로 그릴지 (기존 요정 적의 그림/판정 비율을 기준으로 삼을지).
- 상자 열림 그림이 닫힘 그림과 같은 바닥 기준선·폭을 유지해야 하는지 (현재는 바닥 기준선만 맞춤).

### 손댈 곳

- `game/Assets/Scripts/Frontend/FairyKingdomArtworkCatalog.cs` — `Fit`의 맞춤 방식.
- `game/Assets/Scripts/Frontend/FairyKingdomArtworkView.cs` — `localWidth` 한 값만 갖는 구조. 높이 또는 맞춤 방식 필드가 필요할 수 있다.
- `game/Assets/Editor/FairyKingdomArtworkSetup.cs` — `ApplyHierarchy`의 `width` 산출.
- `game/Assets/Editor/FairyKingdomArtworkVerification.cs` — 현재는 가로 폭 일치만 검사한다. 판정 대비 그림 크기 범위를 불변조건으로 추가한다.
- `game/Assets/Editor/FairyKingdomArtworkPlayVerification.cs` — 물줄기 크기 검사에 영향이 없는지 확인.

## 같은 날 처리한 것 (참고)

돌진형(`ChargingEnemy`)과 원거리·저격형(`RangedEnemy`, `QuickRangedFairy`, `HighBloodSugarFairy`)은 초안 그림 연결을 빼고 기존 Idle·걷기·공격 에셋으로 되돌렸다. `FairyKingdomArtworkVerification.Verify` 배치 통과(종료 코드 0). Play 화면 확인은 남아 있다.
