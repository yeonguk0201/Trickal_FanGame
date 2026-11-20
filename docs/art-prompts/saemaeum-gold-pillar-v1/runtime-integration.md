# 황금 기둥 런타임 적용

2026-11-20. 사용자 원화·시트 수용 후 기존 금바위 대신 적용 요청.

## 구성과 규격

- `game/Assets/Resources/CombatEffects/saemaeum-gold-pillar.png`: 정상 상태. 원본 1254×1254, PPU 863, 피벗 (619.5/1254, 13/1254).
- `game/Assets/Resources/CombatEffects/saemaeum-gold-pillar-break.png`: 1254×1254, 2열×2행, 셀 627×627. 위쪽부터 행 우선. 모든 셀 PPU 398, 피벗 (317/627, 38/627).
- 두 이미지의 실제 불투명 폭을 기준으로 정상·균열 상태의 표시 폭을 약 1 월드 단위로 맞췄다. 지면은 루트 Y -0.2. 파괴 셀은 개별 크롭·확대하지 않아 낮게 무너지는 형태를 유지한다.
- Bilinear, Clamp, mipmap 없음, 무압축, Max Size 2048, NPOT 축소 없음, 알파 투명도 사용. 런타임 `Sprite.Create`로 셀 분할.
- 첫 타격 후 셀 1을 유지하고 두 번째 타격에는 셀 2→3→4를 각 0.1초 표시한다. 마지막 셀은 0.1초 동안 알파 0까지 감쇠한 뒤 제거한다.
- `SaemaeumGoldPillar`는 전용 표시를 쓰며 일반 방 금바위의 `FairyKingdomArtworkView`를 붙이지 않는다. 기존 논리 kind ID `gold-rock`과 소환물 ID는 보존했다. 일반 방 금바위는 변경하지 않았다.
- 보스 프리팹·런타임 기본값·`BossPattern0Setup`을 2타로 맞췄다. 기존 보스 검증기의 5타 기대값도 2타로 갱신했다.
- 파괴 콜백에서 충돌 없는 독립 연출을 생성한다. 장애물은 기존 로직대로 즉시 비활성화·제거한다. 파편은 방 아래에 두고 보스 소유 목록에도 등록해 정리된다. 보스 걷기·착지·메테오로 파괴되어도 같은 콜백을 사용한다.
- 솟아오름은 기존 0.18초 동작을 유지한다. 프레임은 승인된 합성 원화의 기둥과 흙을 함께 사용한다.

## 검증

- Unity 6000.3.22f1, `TrickalFanGame.Editor.SaemaeumGoldPillarArtworkVerification.Verify` 배치 실행.
- 로그: `game/Logs/saemaeum-gold-pillar-artwork.log`.
- 컴파일 성공, `Saemaeum gold pillar artwork verification passed: intact, cracked, three breakup cells, two-hit removal.`, `Batchmode quit successfully invoked` 확인.
- 원화 로드·셀 크기·공통 피벗과 배율·첫 타격 균열·2타 비활성화·재타격 거부·파괴 3셀 순서·알파 0을 자동 확인했다.
- 실제 Play의 크기·배경과 캐릭터 가림·알파 경계·연속 재생·보스 접촉 파괴·방 이동 정리는 미확인. 자동 검증을 미술 검수로 처리하지 않는다.
- 수동 경로: Game Scene Play → F1 → `— Boss room —` → `Boss F2`. 새마음금고 기둥을 기다려 1타 균열, 2타 파괴 및 즉시 통과, 보스 착지 시 파괴, 방 이동 시 잔해 정리를 확인한다.
- 별도 Scene 뷰 실시간 미리보기는 추가하지 않았다. 실제 재생 확인은 Play에서 한다.
