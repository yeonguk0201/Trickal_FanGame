# 황금 기둥 피격·파괴 4프레임

작성일: 2026-11-20. 내장 image_gen으로 기존 원화를 참조하여 생성.

- 파일: `gold-pillar-break-sheet.png`.
- 실제 해상도: 1254 × 1254 px, 셀당 627 × 627 px. 32비트 RGBA이며 모서리와 시트 중앙 빈 공간의 알파 0을 확인했다.
- 배치: 2열 × 2행. 왼쪽 위 → 오른쪽 위 → 왼쪽 아래 → 오른쪽 아래 순서.
- 기준 원화: `gold-pillar-concept.png`. 사용자가 원화 형태를 수용한 후 2회 타격 파괴에 맞춘 4프레임을 요청했다.
- 1프레임: 첫 타격 뒤 금이 간 채 서 있는 상태. 두 번째 타격까지 유지한다.
- 2프레임: 두 번째 타격으로 큰 황금 조각이 분리되며 파괴 시작.
- 3프레임: 금 조각과 흙·돌이 바닥으로 무너져 낮은 잔해가 된다.
- 4프레임: 작은 파편과 옅은 먼지만 남는다. 재생 종료 시 남은 표시를 제거하거나 알파를 0으로 감쇠한다.
- 원화와 별도의 새 파일로 저장했다. 셀 전체 크기를 유지하여 분할하고 잔해만 자동 크롭해 확대하지 않는다.
- 생성 결과에서 각 단계의 형태 변화와 후반 프레임 상부의 빈 공간을 확인했다. 프레임별 정확한 피벗 정렬·알파 경계와 실제 크기·연속 재생은 적용 시 확인한다.
- 사용자 시트 수용 후 Unity 적용 요청(2026-11-20). [런타임 적용 기록](./runtime-integration.md)에 최종 임포트 규격·재생 시간과 전용 검증 통과 결과를 기록했다. 실제 Play 검수는 대기 상태다.

## 생성 프롬프트

```text
Create a transparent 2D game sprite animation sheet using the provided gold pillar image as the EXACT design reference. Preserve its yellow gold beveled rectangular prism, flat top, narrow amber right side, dark brown outlines, cel shading, and jagged brown earth and gray stones at its base. No redesign.
Output exactly FOUR equally sized square cells in a 2 by 2 grid, reading order top-left, top-right, bottom-left, bottom-right. Suggested canvas 2048x2048, each cell 1024x1024. No visible grid lines, no labels or numbers. True transparent background. Each cell has the SAME camera, original pillar scale, horizontal center and ground baseline at 90% of cell height. Entire original standing pillar fits with padding. Never zoom into later debris. No fragments crossing cell boundaries.
These are FOUR DIFFERENT sequential states, NOT four intact pillars:
FRAME 1 top-left: AFTER FIRST HIT, still standing at full original height and outline. Clearly visible dark branching jagged cracks running through the upper, middle and lower gold front and right side. A few small edge chips, but the pillar remains solid and upright and its earthy base remains. This is a persistent damaged state, not a destruction explosion.
FRAME 2 top-right: SECOND HIT DESTROYS IT. The entire rectangular pillar fractures into roughly 7 to 10 large angular gold chunks with clean flat metallic fracture faces. Pieces separate outward slightly along the cracks, leaving clear transparent gaps. Original pillar shape is now broken; top sections tip sideways, lower sections begin falling. Moderate compact breakup, no fire, no large glow flash, no intact pillar behind chunks. Earth base breaks apart too.
FRAME 3 bottom-left: COLLAPSE. All gold chunks have fallen to the lowest third of the cell, rotated and spread into a low scattered rubble heap, smaller gold splinters and a few small fading dust wisps. The upper two thirds of this cell are TRANSPARENT EMPTY SPACE. Absolutely no standing pillar, no tall silhouette, no refilling with another pillar. Keep the same chunk scale and ground anchor as earlier.
FRAME 4 bottom-right: ALMOST GONE. Only 3 to 5 tiny fading gold chips, a few tiny earth crumbs and very faint translucent low dust near the same ground baseline; over 95% of the cell is transparent empty space. No solid rubble pile, no persistent earth ring, no intact pillar. Final remnants can fade to zero in engine.
Maintain hand drawn cartoon style, consistent lighting and perspective, crisp shapes, metallic yellow chunks distinct from brown earth. No ground plane, baked shadows, text, watermark, scenery or characters.
```
