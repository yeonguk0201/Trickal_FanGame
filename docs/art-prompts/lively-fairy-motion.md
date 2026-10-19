# 저혈당 요정-활발 걷기·공격 모션

2026-10-19. 원화 기준은 `Art/Drafts/FairyKingdom/Enemies/low-blood-sugar-fairy.png`의 빵을 든 요정이다.

- 내장 image_gen, 투명 배경, 원화 참조로 4열×2행 아틀라스를 생성했다. 생성 원본: `exec-b847e664-7fab-4ead-9fef-5da02a23e631.png`.
- 저장: `game/Assets/Art/Enemies/FairyKingdom/Walking/LivelyFairy_Motion.png`. 위 행은 걷기 네 포즈, 아래 행은 중립·웅크린 준비·빵을 들어 올리는 발사·회복이다.
- 실제 출력은 1774×887 알파 PNG다. 요청 해상도와 다르므로 가로/4·세로/2로 셀을 나누며 원본 이미지를 리사이즈하지 않았다.
- 프롬프트: 원본 얼굴·갈색 머리·크림 리본·빵·노란 소매·분홍 앞치마·흰 날개와 짙은 갈색 외곽선을 유지한다. 동일 크기와 바닥 기준의 4×2 셀, 교차하는 보행과 준비/발사/회복 포즈, 텍스트·격자·발사체·이펙트 없는 투명 배경을 요청했다.
- `LivelyFairyMotion`은 실제 이동 거리 1.05당 네 프레임을 반복한다. 정지/막힘/행동 억제에는 원본 Idle을 표시한다. 기존 0.4초 예고에 준비 포즈, 실제 `VolleyFired` 이후 0.16초에 발사 포즈, 남은 회복 시간에 회복 포즈를 표시한다.
- 원본 Idle 높이와 중립 셀의 가시 영역으로 공통 PPU·피벗을 정한다. 프레임별 확대는 하지 않는다. 기존 렌더러를 사용하여 좌우 반전·피격 색·정렬을 유지한다. 루트/충돌 영역/피해량/탄속/발사 위치/공격 주기는 변경하지 않는다.
- 2026-10-20 사용자 요청으로 저격형 고혈당 요정의 `HighBloodSugarFairy` 그림자 프로필을 공유한다. 폭 배율 1.05, 두께 0.28, 위치 보정 (0.07, -0.15), 불투명도 0.6이며 실제 크기는 각 적의 충돌 영역 기준이다. Setup 재실행 시에도 연결을 유지한다.
- `LivelyFairyMotionSetup.SetupAndVerifyBatch`는 이 텍스처와 `LivelyLowBloodSugarFairy` 프리팹만 구성하고 걷기·준비·발사·회복·정지·억제 포즈와 물리 영역 보존을 검사한다.

## 수동 검수

1. 재구성: Edit Mode → `Trickal Fan Game > Artwork > Setup Lively Fairy Motion`.
2. 단독 검사: `Trickal Fan Game > Artwork > Verify Lively Fairy Motion` → `Lively fairy motion verification passed.`
3. Game Scene Play → F1 → `— Room —` → `Spawn lively fairy (4-way shots)`.
4. 이동 중 발 교차, 정지 예고의 웅크림, 4방향 탄 발사와 빵 들기의 동기, 회복 후 보행을 확인한다. 좌우 이동·벽·피격·사망에서도 원화 크기와 발 기준 및 잔상을 확인한다.

실제 Play 미술 검수는 미확인이다. 생성 시트의 포즈 확인과 자동 검증은 사용자 수용을 대신하지 않는다.

## 자동 검증

Unity 6000.3.22f1에서 `TrickalFanGame.Editor.LivelyFairyMotionSetup.SetupAndVerifyBatch` 최종 실행 종료 코드 0 및 `Lively fairy motion verification passed.`를 확인했다. 로그: `game/Logs/LivelyFairyMotion.log`. 첫 실행의 정사각 셀 가정은 생성 크기와 맞지 않아 실패했으며, 실제 크기를 사용하는 수정 후 재실행했다. 컴파일·프리팹 저장·위 단독 검사를 통과했다.
