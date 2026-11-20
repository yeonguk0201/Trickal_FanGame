# 황금 기둥 손상·파괴 6프레임

2026-11-20. 기존 3타 파괴를 유지하면서 손상부터 소멸까지 6프레임으로 확장했다.

## 최종 파일과 구성

- 원화: `gold-pillar-six-frame-sheet.png`.
- Unity 사용 파일: `game/Assets/Resources/CombatEffects/saemaeum-gold-pillar-six-frame.png`.
- 내장 image_gen으로 기존 정상 원화와 4프레임 시트를 참조해 생성했다. 후광 제거 편집·재생성 후보도 만들었으나 첫 후보가 기존 그림체와 가장 가까워 선택했다. 알파를 직접 읽어 후광처럼 보인 넓은 빈 영역의 알파 0을 확인했다. 원본 알파는 보존했다.
- 1536×1024 RGBA, 3열×2행, 셀 512×512, 왼쪽 위부터 행 우선.
- 1: 작은 균열(첫 타격 뒤 유지).
- 2: 큰 균열(두 번째 타격 뒤 유지).
- 3: 조각 분리(세 번째 타격으로 파괴 시작).
- 4: 중간 낙하(추가한 붕괴 단계).
- 5: 바닥 잔해.
- 6: 작은 파편·먼지만 남음.
- 정상 상태는 기존 `saemaeum-gold-pillar.png`를 유지한다. 예전 4셀 시트와 별도 경미한 손상 PNG는 보존하되 런타임은 새 시트를 사용한다.

## 정렬·타이밍

- 모든 셀 PPU 290. 셀을 개별 크롭하거나 잔해만 확대하지 않는다.
- 생성 셀의 미세한 위치 차이를 피벗으로 보정한다. 셀 왼쪽 아래 기준 피벗 픽셀: (266,48), (254.5,48), (247,48), (264,39), (251.5,38), (255.5,42). 지면 기준점은 기존 루트 Y -0.2다.
- 3타 뒤 셀 3→4→5→6을 각 0.12초 재생한다(파괴 구간 0.48초). 이후 기존 유지 0.35초·페이드 0.45초를 보존한다. 총 1.28초 뒤 연출을 제거한다.
- 기존 전용 그림자와 그 복사본의 동반 페이드를 보존했다. 3타 시 충돌 즉시 제거도 유지한다.
- 임포트: Bilinear, Clamp, mipmap 없음, 무압축, Max Size 2048, NPOT 축소 없음, 알파 투명도 사용. 기존 리소스를 덮어쓰지 않고 새 PNG와 meta를 추가했다.

## 확인

- 전용 자동 검증: `TrickalFanGame.Editor.SaemaeumGoldPillarArtworkVerification.Verify`.
- Unity 6000.3.22f1 배치 통과·종료 코드 0. `game/Logs/saemaeum-gold-pillar-six-frame.log`에서 `six cells, two damage states, four breakup cells, hold and fade, three-hit removal.` 성공 메시지를 확인했다. 셀 순서·공통 배율·1/2타 상태·3타 제거·4개 파괴 셀 재생·유지·완전 감쇠를 검사했다.
- 실제 Play 미술 검수는 대기. Game Scene Play → F1 → `— Boss room —` → `Boss F2`에서 1타·2타 손상, 3타 붕괴의 중간 낙하, 바닥 정렬, 잔해·그림자 페이드와 방 이동 정리를 확인한다.

## 선택한 이미지의 생성 프롬프트

```text
Create a SIX FRAME transparent sprite sheet for this exact gold pillar game prop. Reference image 1 is the original intact pillar design. Reference image 2 is an earlier four-cell damage/destruction sheet showing style and destruction. Preserve original rectangular gold prism design, narrow dark amber right side, beveled top, golden cel shading, brown outlines, jagged brown soil and gray stones.
OUTPUT exactly 3 columns by 2 rows, SIX equally sized square cells, suggested canvas 3072x2048 with 1024x1024 cells. Row-major playback order: top-left, top-middle, top-right, bottom-left, bottom-middle, bottom-right. No borders, numbers, text or grid lines. Genuine transparent alpha background.
All cells have exactly the SAME camera, scale, x center and ground baseline at 90% cell height, with full upright pillar fitting each cell with transparent padding. The pillar width about 42% cell width and height about 75% cell height, ground rubble a little wider. Keep the entire prop and every fragment inside its own cell. NEVER enlarge rubble to fill later cells; later cells must have large empty transparent upper regions.
Frame 1 top-left: MINOR damage after first hit. Pillar remains fully upright and solid. Only 3 short fine cracks near gold edges, a tiny chipped corner, large center face stays smooth. Same earth base.
Frame 2 top-middle: HEAVY damage after second hit, still fully upright and solid with original full silhouette. Thick dark branching zigzag cracks cross the front and right side, a few small edge chips. No major gaps or separated pieces yet.
Frame 3 top-right: Third hit, FRACTURE. Pillar splits along those cracks into 7 to 10 large gold chunks, clear narrow transparent gaps between pieces, still roughly forming original standing silhouette but outer pieces beginning to tip outward. No intact pillar behind.
Frame 4 bottom-left: MID-COLLAPSE, an additional transition before ground impact. Gold chunks have fallen downward, visibly rotated and spread, occupying only the LOWER HALF of the cell. Several large pieces are still airborne at knee-height above ground, no standing pillar. Clearly different from frame 3 and frame 5. Upper half entirely transparent.
Frame 5 bottom-middle: GROUND IMPACT. All chunks have settled into a compact LOW heap of gold rubble and broken earth in the lowest quarter of cell, a few small dust wisps. Upper three quarters entirely transparent.
Frame 6 bottom-right: DISAPPEARING. Only a few tiny gold chips and small pale translucent dust wisps near the ground baseline, more than 95 percent of cell empty transparent. No large gold chunks and no persistent earth ring.
Maintain consistent chunky 2D cartoon game art. No explosions, glowing flashes, floor planes, baked shadows, scenery, characters, logos, labels, numbers or watermark.
```
