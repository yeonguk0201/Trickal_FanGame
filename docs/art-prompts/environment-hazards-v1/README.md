# 환경 함정 에셋 v1

2026-10-14. 사용자 요청에 따라 에셋만 제작. Unity 임포트·Scene·Prefab·코드 연결 없음.
내장 image_gen 사용. 기존 FairyKingdom 에셋을 편집 참조로 사용했으며 원본은 보존했다.

## 파일

|파일|크기(px)|구성|
|---|---|---|
|tree-burning-sheet.png|1254×1254|불타는 나무 8프레임, 4열×2행|
|spikes-retracted.png|1376×1143|내려간 가시 정지 이미지|
|fire-trap-sheet.png|1774×887|불 함정 8프레임, 4열×2행|
|mushroom-hit-preburst.png|1312×1199|피격 후 팽창·균열, 폭발 전 정지 이미지|
|poison-gas-sheet.png|1774×887|독가스 8프레임, 4열×2행|

## 확인 범위

- 5개 PNG 모두 32bit ARGB 및 모서리 alpha=0 확인.
- 생성 출력에서 외형과 8프레임 배치를 육안 확인. 독가스 첫 시트는 경계 흔적·여백 부족으로 제외하고 재생성했다.
- 순서: 왼쪽→오른쪽, 위 행→아래 행.
- 생성 시트 원본으로, 크기가 열·행 수로 정수 분할되지 않는다. 적용 전 개별 프레임 분할과 기준점 정렬 필요.
- 나무 시트는 좌우 여백이 좁으며 프레임 간 수관·꽃·줄기 형태에 미세 차이가 있다. 고정 본체와 불꽃 분리는 후속 적용 검토 사항.
- 연속 재생의 매끄러움, 루프 연결, 게임 크기·배경에서의 가독성은 미검수. 사용자 미술 검수 대기.
- Unity 배치 생략: 원화 PNG와 제작 기록만 추가. PPU·피벗·필터·압축은 미설정.

## 최종 생성 프롬프트

### 불타는 나무

```text
Edit target: existing flowering tree game sprite. Produce a transparent PNG animation sprite sheet of this SAME tree burning, exactly 8 frames in 4 columns and 2 rows, reading left to right then next row. Equal rectangular cells, generous transparent gutters, NO grid or labels. Preserve tree trunk, roots, foliage and flowers, painterly cartoon shading and dark brown outlines. Tree is fixed perfectly at identical scale and root anchor in every cell. Add bright orange yellow stylized flames licking upward over trunk and among leaves; foliage remains recognizable. Eight successive subtle hand-drawn flame shapes, rising tongues and sparse embers, loop end close to beginning. Constant burning intensity, not destruction progression; no collapse or camera motion. Entire tree and flame tips contained within each cell. Actual transparent background, no ground/environment or checkerboard. Aim 2048x2048 sheet. Asset-only review draft.
```

### 내려간 가시

```text
Edit target: attached periodic spike trap sprite. Create ONE retracted/inactive state sprite. Preserve exactly the same stone rectangular base, three dark rectangular sockets, gold trim, moss placement, cracks, front medallions, perspective, outline and cartoon shading. Lower all THREE silver spikes completely into their sockets, no projecting spikes above the stone top surface. Only small flush recessed metallic tips may be visible deep inside the dark holes. Keep original base proportions and position and transparent space above where spikes were. No added items, no text, no background, actual transparent PNG. Single object not sheet.
```

### 피격 독버섯

```text
Edit target: attached poison mushroom sprite. ONE sprite showing this exact mushroom just after being struck, swelling and about to burst but still intact. Preserve purple cap with lime green round spots, cream squat body, dark eye opening with lime eyes, little feet, thick dark brown contour, polished cute 2D cartoon game shading and same camera. Cap and body bulge moderately with tense asymmetry, small stress fissures with lime light showing in two places on cap, tiny beads of poison at those fissures, eyes squeezed with strain. Clearly pre-burst, not exploded, no flying chunks, no large gas cloud, no weapon, no impact star, no text or motion lines. Same planted foot anchor, entire silhouette visible with transparent margin. Single sprite on actual transparent PNG background.
```

### 불 함정

```text
Edit target: attached cartoon fire ground trap. Create exactly EIGHT consecutive looping animation frames on ONE transparent sprite sheet, 4 columns x 2 rows, equal square cells, 2048x1024 desired. Same stylized orange/yellow/cream fire, dark warm outline and dark rock base as reference. Lock rock base design, position and scale identically across all eight cells, base bottom at 88% of each cell, centered horizontally. Animate ONLY the flames: successive upward flowing curls, alternating left/right tongues, inner flame shapes rising and a few changing embers, smooth continuous burning loop from 8 back to 1, not eight identical copies and not ignition/extinction. Constant overall intensity. Entire flame tips and rocks inside central 78% of each cell with clear transparent gutters on every side. No text, labels, grid, checkerboard, shadows outside cells or scene background. Actual alpha transparency.
```

### 독가스 (수정본)

```text
Using the attached original poison gas only as design reference, REDRAW a clean animation sheet with 8 poison clouds, precisely 4 columns by 2 rows. Transparent PNG. CRITICAL: each cloud must be SMALL, only 65 percent of its cell WIDTH and 45 percent of cell HEIGHT, centered within its equal square cell. WIDE EMPTY TRANSPARENT GUTTERS, at least 60 pixels all around each cloud. No pixels or streaks whatsoever on cell boundaries; NO GRID LINES. Original lime and lavender outlined cartoon ground mist, broad oval with curled puffs and translucent center. Eight successive billowing shapes with subtly evolving curls and drifting wisps, stable footprint center, loop. Fully separate clouds, no touching, no clipping. No background, labels, panels or scene. Target 2048x1024 total, 512x512 cells.
```

