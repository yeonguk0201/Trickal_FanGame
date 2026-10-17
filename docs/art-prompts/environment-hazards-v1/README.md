# 환경 함정 에셋 v1

2026-10-14 원화 제작. 2026-10-17 사용자 요청으로 아래 5종을 런타임에 연결했다. 독가스는 두 번째 생성본을 사용한다.
내장 image_gen 사용. 기존 FairyKingdom 에셋을 편집 참조로 사용했으며 원본은 보존했다.

## 현재 적용 상태 (2026-10-17)

- 후속: 불 함정은 [v2 단계별 불꽃·안전한 잔해](../environment-hazards-v2/README.md)로 확장했다. 아래는 최초 적용 이력이며, 기존 적용본의 사용자 수동 확인은 완료되었다. 나무 길이 문제는 수정 원화만 제작된 상태다.

- 사용 파일: `game/Assets/Resources/EnvironmentHazards/`의 PNG 5개와 프레임 영역 JSON 5개. 이 폴더의 원본 PNG를 변경 없이 복사했다.
- `prepare_assets.py`는 Pillow로 alpha>=16 영역과 2px 여백을 측정해 정수 좌표로 프레임을 나눈다. 이미지 재생성·재샘플링 없이 JSON만 생성하며 기존 `.meta` GUID를 보존한다.
- `EnvironmentHazardArtwork`가 프레임 Sprite를 캐시한다. 시트별 공통 PPU(최대 프레임 폭/표시 폭)를 사용하고, 나무·불·가시·버섯은 원래 그림의 바닥 기준점을 유지한다. 가스는 중심 기준이다.
- 필터 Bilinear, Wrap Clamp, 압축 없음, Max Size 2048, mipmap 없음, alpha transparency 사용. 4열×2행을 왼쪽→오른쪽, 위→아래로 읽는다.
- 불타는 나무: 발화 상태에서 8프레임·10fps 반복. 기존 작은 불꽃 덧붙이기를 제거하고 기존 두 칸 표시 크기와 수관 정렬(15)을 유지한다.
- 주기 가시: 내림·경고 상태에서 `spikes-retracted`, 올림 상태에서 기존 원화. 내려간 바닥은 불투명하게 유지하고 경고는 기존 붉은색·alpha 0.65를 유지한다. 상시 바닥 가시는 변경하지 않았다.
- 독버섯: 첫 타격 뒤 팽창·균열 이미지, 두 번째 타격 파괴와 독가스 생성은 기존 규칙.
- 불 함정: 8프레임·10fps 반복. 피격에 따른 회색 착색 대신 원화 색을 유지하며 세 번째 타격 또는 기존 물줄기 소화 시 제거된다.
- 독가스: **두 번째 생성본** 8프레임·6fps 반복. 기존 폭 3, alpha 0.7, 정렬 11, 지속 3초와 마지막 0.5초 감쇠를 유지한다.
- 씬·프리팹 재생성 없이 기존 ArtworkView와 RoomTrap, PoisonGasCloud 경로에 연결했다. Backend/Web/API/안정 ID 변경 없음.

### 검증

- Unity 6000.3.22f1 배치 `TrickalFanGame.Editor.EnvironmentHazardArtworkVerification.Verify` 통과, 종료 코드 0.
- 로그: `tmp/environment-hazards-verification.log`. 24개 서로 다른 프레임, 루프·캐시, PPU·바닥 정렬, 가시 상태, 버섯 첫 타격, 나무 두 번째 타격 발화, 함정 파괴, 가스 감쇠·종료 검사.
- `git diff --check` 통과. 실제 Play 미술 검수는 대기이며 완료 승인으로 처리하지 않았다.
- 한계: AI 원본의 나무 수관·꽃·줄기 및 불 함정 돌에는 프레임별 미세 형태 차이가 남아 있다. 기준점 정렬이 원화 내부 형태 차이까지 없애지는 않는다.

### 수동 확인

1. Game Scene Play → F1 → `— Trap —` → `Place PeriodicSpikes`, `Place Fire`, `Place PoisonMushroom`.
2. 주기 가시는 내려간 바닥→붉은 경고→올라온 가시로 반복하고, B 위상 선택 시 반 주기 어긋나는지 확인한다.
3. 불은 바닥 돌의 위치를 유지하며 반복하고 파괴 시 사라지는지 확인한다.
4. 버섯을 한 번 때려 팽창 이미지를 확인하고, 다시 때려 두 번째 버전 독가스가 일렁이다 3초 뒤 사라지는지 확인한다.
5. 나무가 있는 방에서 F1 → `— Obstacle —` → `Hold a burn artifact (trees catch fire)`를 켜고 나무를 두 번 때린다. 불타는 나무 반복, 수관 가림, 네 번째 타격 제거를 확인한다.
6. 메뉴 `Trickal Fan Game/Artwork/Verify Environment Hazard Frames`로 자동 검사를 다시 실행할 수 있다.

## 파일

|파일|크기(px)|구성|
|---|---|---|
|tree-burning-sheet.png|1254×1254|불타는 나무 8프레임, 4열×2행|
|spikes-retracted.png|1376×1143|내려간 가시 정지 이미지|
|fire-trap-sheet.png|1774×887|불 함정 8프레임, 4열×2행|
|mushroom-hit-preburst.png|1312×1199|피격 후 팽창·균열, 폭발 전 정지 이미지|
|poison-gas-sheet.png|1774×887|독가스 8프레임, 4열×2행|

## 최초 제작 때의 확인 범위 (2026-10-14)

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

