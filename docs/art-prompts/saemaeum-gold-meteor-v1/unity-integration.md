# 새마음금고 메테오 Unity 연결

## 적용 파일

- `game/Assets/Resources/CombatEffects/saemaeum-gold-meteor-fall.png`: 일체형 v3, 2×2·4프레임.
- `game/Assets/Resources/CombatEffects/saemaeum-gold-meteor-impact.png`: 착지 폭발 v1, 2×2·4프레임.
- 두 PNG 모두 1254×1254, 셀 627×627. Bilinear·Clamp·무압축·Max Size 2048·NPOT 보존·밉맵 없음·알파 투명도 사용.
- 런타임 `SaemaeumGoldMeteorArtwork`가 Resources 텍스처에서 좌상 → 우상 → 좌하 → 우하 순서로 Sprite를 생성한다.

## 방향·크기·타이밍

- 낙하: 셀별 본체 중심을 피벗으로 잡고 +45° 회전해 꼬리가 위로 향한다. 실제 낙하는 기존 수직 경로·0.4초·높이 7을 사용한다.
- 본체 지름 약 150px를 월드 폭 1.1로 표시하도록 PPU 설정. 불꽃을 포함한 캔버스 전체 폭으로 크기를 계산하지 않는다.
- 낙하 4프레임은 장당 0.07초로 반복한다. 기존 그림자 경고·피해·넉백·장애물 파괴·효과음 시점은 유지한다.
- 착지: 기존 먼지 대신 `SaemaeumGoldMeteorImpact`가 섬광 0.04초 → 최대 팽창 0.08초 → 파편 0.10초 → 먼지 0.12초를 재생한다. 마지막 구간은 완전 투명까지 감쇠한다.
- 폭발은 회전하지 않고 각 셀의 지면 피벗을 사용한다. 최대 폭 약 2.4, 피해 범위와 독립적인 장식이다.
- 폭발에는 Collider나 피해 처리를 넣지 않는다. 메테오와 별도 오브젝트로 수명을 유지하며 보스 소유 오브젝트로 등록해 보스·방 정리 시 함께 제거한다.

## 수동 Play 검수

1. `Trickal Fan Game/Debug/Open Boss Test Room`으로 보스 테스트 공간을 열고 Play 후 새마음금고를 선택한다.
2. 2페이즈 황금 메테오에서 그림자 중심으로 본체가 떨어지고 꼬리가 위로 이어지는지 확인한다.
3. 착지 순간 낙하 그림이 사라지며 폭발이 같은 지면에 붙는지, 4프레임 사이 본체·바닥이 흔들리지 않는지 확인한다.
4. 중앙·벽 근처·동시 낙하에서 캐릭터 가림·밝기·크기를 확인하고, 일시정지 때 애니메이션도 멈추는지 확인한다.
5. 보스 사망·방 이동 뒤 남은 불꽃·폭발이 없는지 확인한다.

사용자는 일체형 원화 방향을 수용하고 연결을 요청했다. 실제 Play 미술 수용은 아직 확인하지 않았다.

## 자동 검증 결과

2026-11-20 Unity 6000.3.22f1 배치에서 TrickalFanGame.Editor.SaemaeumGoldMeteorArtworkVerification.Verify 통과. 로그: game/Logs/saemaeum-meteor-artwork-verification.log. 종료 코드 0과 성공 메시지를 확인했다. 순수 전용 Verify만 실행했으며 회귀 묶음은 실행하지 않았다.
