# 산사모 순수 — 걷기와 꽃 발사 v1

2026-10-19. 구현·단독 자동 검증 완료, 사용자 실제 Play 미술 검수 대기.

## 구성과 기준

- 사용처: `QuickRangedFairy` / 산사모-순수. 활발의 짧은 뿌리 다리 걷기를 참고하되 순수의 얼굴·데이지·병아리 가방·꽃 외형을 유지한다.
- 몸은 실제 이동 거리 / 보폭 1.05로 네 프레임을 반복한다. 공격 이벤트는 걷기 시계를 초기화하지 않는다. 정지·벽 접촉·행동 억제 때 원본 Idle로 돌아간다.
- 꽃은 같은 그림의 윗부분을 격자 메시로 표시하여 발사 방향 반대로 짧게 휜다. 몸통·발은 변형하지 않는다. 발사 이벤트에서 시작하고 0.24초에 원위치로 복원한다.
- 꽃 위치는 원본 가시 영역의 `(0.42, 0.91)`이다. 좌우 반전 시 X만 반전한다. 캐릭터 루트·콜라이더·피해량·탄속·연사 간격·연사 후 휴식은 유지한다.
- 실제 투사체 출발점을 몸 중심에서 꽃으로 옮겼으므로 예측 조준도 꽃 기준으로 계산한다. 몸→꽃, 꽃→표적의 장애물을 검사하고 가려졌으면 발사·탄수 소비 없이 재시도한다. 탄의 경로는 이 출발점 변경의 영향을 받는다.
- 기존 탄 외형을 유지한다. 별도 발사 섬광·입자·공격 전신 시트는 추가하지 않았다.

## 에셋과 생성 이력

- 도구: 내장 `image_gen`, `transparent_background: true`.
- 외형 참조: `game/Assets/Art/Drafts/FairyKingdom/Enemies/sansamo-pure.png`.
- 보행 참고: `game/Assets/Art/Enemies/FairyKingdom/Walking/Sansamo_Walk_0.png`.
- 생성 원본: `exec-6fb599c4-93b9-436c-9e68-4c926263f59a.png`.
- 저장: `game/Assets/Art/Enemies/FairyKingdom/Walking/SansamoPure_Walk.png`.
- 실제 출력: 1254×1254, 2×2, 627×627 셀, 행 우선 네 프레임. 알파 PNG. 원본은 그대로 보존했다.
- 임포트: Default Texture, Bilinear, 무압축, mipmap 없음, NPOT 축소 없음, Max Size 2048.
- Setup은 네 셀의 가시 영역 합집합을 계산한다. 공통 PPU·피벗을 원본 Idle 높이에 맞춰 모든 프레임에 적용하며 프레임별 늘리기를 하지 않는다.

최종 프롬프트:

```text
Create a production four frame WALK cycle atlas for the EXACT character in reference 1 (pure Sansamo: yellow round ginseng with white blank oval eyes, small happy mouth, two white daisies, chick handbag, leaves and tall curved stalk with purple flower). Reference 2 is ONLY the short root-feet walking pose style, DO NOT copy its face, red headband or belt. Identity preserve reference 1. 2x2 equal square cells, row major order. Four distinct alternating short root foot poses: left forward planted/right back, left support/right passing lifted, right forward planted/left back, right support/left passing lifted. Face torso handbag leaves stalk and purple flower same exact size and location in ALL frames, feet baseline consistent. Entire character within central 80 percent of each cell generous transparent margins, nothing clipped. Original clean thick brown outline bright shaded illustration. No text/grid/shadow/effects. True transparent background. Flower neutral and stable for independent firing overlay later. 2048x2048 square atlas.
```

## 구현과 검증

- `SansamoPureMotion`: 별도 걷기·꽃 반동 시계, 원본 복원, 피격 색·정렬 전달, 비활성화/파괴 정리.
- `MobileRangedEnemyController`: 꽃 발사점 및 장애물 확인. 다른 원거리 적 컨트롤러는 수정하지 않았다.
- `SansamoPureMotionSetup`: 단일 프리팹과 해당 텍스처만 구성.
- `SansamoPureMotionPreview`: 격리 Preview Scene에서 동일 런타임 메시를 재생. 걷기/반동 독립 토글, 정지·시간 이동·속도·방향·좌우 반전, 종료/Play 전환/리로드 정리. AI·피해 로직은 실행하지 않는다. 투사체 궤적과 실제 게임 배경은 Play에서 확인한다.
- Unity 6000.3.22f1 컴파일 성공. `SansamoPureMotionVerification.Verify` 최종 종료 코드 0, `Sansamo pure motion verification passed` 확인. 로그: `game/Logs/SansamoPureMotion-Final.log`.
- 검사: 네 프레임/반복/Idle, 꽃 반동 중 몸·발 보존, 반동 복원, 좌우 발사점, 실제 투사체 출발점, 발사 중 이동 시계, 벽에 막힌 발 정지, 꽃 장애물 차단/탄수 보존, 콜라이더 보존, 비활성화 정리.
- 첫 실행은 기존 Unity 인스턴스 때문에 검증 전에 종료됐다. 이후 해당 프로세스 종료 확인 후 배치 실행했다. 검증용 Edit Mode 호출 경고와 활성화 순서를 수정한 뒤 위 최종 검증을 통과했다.
- 렌더: `game/Logs/SansamoPureMotion/frame-00.png`~`frame-23.png`. 왼쪽 정지+발사, 가운데 걷기, 오른쪽 걷기+발사/좌우 반전. 크기와 꽃 반동을 정지 렌더로 확인했다. 실제 Play 수용을 대신하지 않는다.

## 수동 검수

1. Edit Mode → `Trickal Fan Game/Artwork/Preview Sansamo Pure Motion` → `Open / reset Scene preview`. 걷기와 꽃 반동을 독립 토글하고 시간 슬라이더·발사 방향·좌우를 비교한다.
2. Game Scene Play → F1 → `— Room —` → `Spawn pure Sansamo (walk + flower shots)`.
3. 이동하면서 꽃이 반동하고 탄이 꽃에서 출발하는지, 연사 후 쉬는 동안 발이 멈추는지, 피격·사망·방 전환에서 잔상이 남지 않는지 확인한다.
4. 상하·대각선·벽 근처에서 꽃과 발사점의 연결감, Idle↔걷기 크기/발 높이, 얼굴·가방의 원화 일관성을 확인한다.

미술 승인: 대기. 이번 프로토타입으로 전체 Motion-0을 완료 처리하지 않는다.
