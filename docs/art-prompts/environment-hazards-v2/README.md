# 환경 함정 에셋 v2 — 불 함정 적용, 나무 검수본

## 불 함정 적용 (2026-10-17)

- 사용자 승인: 타격별 불꽃·소멸을 연결하고, 소멸 시트의 마지막 식은 돌 프레임을 안전한 바닥 장식으로 유지한다.
- `game/Assets/Resources/EnvironmentHazards/`에 불 관련 PNG/JSON 3쌍을 추가했다. 재생성은 `python docs/art-prompts/environment-hazards-v1/prepare_assets.py --fire-v2`.
- 무피격은 기존 큰 불, 첫 타격은 중간 불, 두 번째는 잔불을 10fps 반복한다. 마지막 타격은 8프레임·10fps 소멸 후 마지막 프레임을 유지한다.
- 원래 RoomTrap·DestructibleObstacle 루트는 기존 파괴 경로에서 즉시 비활성화된다. 별도 `ExtinguishedFireArtwork`만 같은 방 부모 아래 남으며 Collider2D·피해 컴포넌트가 없다. 정렬 -8로 캐릭터 아래에 그린다.
- 물줄기 소화·폭탄 파괴도 동일하게 잔해를 남긴다. 기존 RoomRunState의 파괴 기록으로 방 재생성 시 소멸을 재생하지 않고 식은 돌을 복원한다. 같은 함정의 재바인딩·반복 소화로 잔해를 중복 생성하지 않는다.
- 기존 Item/Character/Run ID 및 저장/API 계약 변경 없음. 수정 나무 시트는 이번 불 함정 적용에 포함하지 않았다.
- Unity 6000.3.22f1: `EnvironmentHazardArtworkVerification.Verify` 및 내부 호출하는 `Week25Trap0Verification.Verify` 통과. 종료 코드 0, 로그 `tmp/fire-remnant-verification.log`.
- 48프레임 로딩, 불 타격 단계, 바닥 기준점, 소멸 종료 프레임, 피해 거절, 충돌체 없는 잔해, 재바인딩 멱등성, 방 활성화/복원/제거, 물·폭탄 소화 검사. 최초 검사에서 Edit Mode의 미실행 OnDestroy 전제를 발견하여 실제 방 부모 제거 경로로 검사를 수정했다.
- 새 불 단계와 잔해의 실제 Play 미술 확인 완료(사용자, 2026-10-18). 아래 기록은 최초 에셋 제작 당시의 이력이다.

### 수동 확인

1. Game Scene Play → F1 → `— Trap —` → `Place Fire`.
2. 세 번 때려 큰 불→중간 불→잔불→소멸→식은 돌을 확인한다.
3. 소멸 시작 직후부터 걸어서 통과해 피해·이동 막힘이 없는지 확인한다. 돌은 캐릭터 아래에 보여야 한다.
4. 방을 나갔다 돌아와 식은 돌이 유지되는지 확인한다. 다른 불을 물줄기·폭탄으로 꺼도 같은 결과여야 한다.

## 사용자 피드백과 범위

- 기존 적용본 수동 확인 완료를 사용자에게 전달받음. 불타는 나무가 기존 나무보다 짧아 보여 재제작 요청. 불 함정은 타격에 따라 약해지는 단계별 프레임 요청.
- 이번 요청은 **에셋 제작만**. Unity 에셋·코드·Scene·Prefab은 이번 턴에서 변경하지 않았다. 기존 v1 및 적용 상태를 유지한다.
- 내장 image_gen 도구로 생성/편집. 원래 나무·불 함정 원화를 참조했다.

## 결과

|파일|구성|목적|
|---|---|---|
|tree-burning-tall-sheet.png|1254×1254, 4열×2행, 8프레임|긴 줄기와 원본 수관 비율을 보강한 불타는 나무|
|fire-hit-1-medium-sheet.png|1774×887, 4열×2행, 8프레임|첫 타격 후 중간 불, 반복용|
|fire-hit-2-low-sheet.png|1774×887, 4열×2행, 8프레임|두 번째 타격 후 잔불, 반복용|
|fire-hit-3-extinguish-sheet.png|1774×887, 4열×2행, 8프레임|마지막 타격 후 불꽃→숯불→식은 돌, 일회 재생용|

순서: 좌→우, 위 행→아래 행. 무피격 큰 불은 기존 v1을 비교 기준으로 유지한다.

## 제작·확인

- 나무 최초 v2 후보는 거의 불이 없는 시작 프레임이 섞여 제외. 최종 시트는 모든 프레임에서 타도록 보정했다.
- 나무는 노출된 긴 줄기를 유지하고 불꽃이 좌우로 크게 퍼져 전체를 넓히지 않게 요청했다. 원본 높이/폭 약 2.01을 기준으로 했다.
- 불 단계 2는 단계 1 시트를, 소멸은 단계 2 시트를 편집하여 돌과 구도 변화를 줄였다.
- 4개 이미지 모두 RGBA 및 alpha 0~255 확인. 생성 출력에서 프레임 수·긴 줄기·불 단계 구분·소멸 끝 상태를 육안 확인.
- 픽셀 단위 원본 일치·프레임 간 완전 고정은 보장하지 않는다. 나무와 돌의 미세한 생성 차이, 루프 연결은 최종 미술 검수 대상이다.
- 원본 시트 크기가 행·열 수로 정수 분할되지 않으므로 추후 적용 시 정수 프레임 경계·바닥 기준점 정렬이 필요하다.
- 추후 나무 크기를 맞출 때 **불꽃까지 포함한 경계 폭으로 나무 본체를 축소하지 않는다**. 원래 나무의 뿌리·꼭대기 위치를 함께 비교해야 한다.
- 불은 단계마다 동일 바닥 돌 폭과 기준점을 사용해야 한다. 프레임 전체 높이를 같게 늘리면 약해진 불이 다시 커져 보인다.
- 소멸 시트 마지막은 식은 돌이 남는 미술 원본이며, 게임에서 잔해를 유지할지 제거할지는 이번 작업에서 바꾸지 않았다.
- Unity 배치/Play 실행 없음. 새 v2 에셋 사용자 검수 대기.

## 프롬프트 기록

### 나무 최초 재제작

```text
Edit target: supplied original tall flowering tree game sprite. Make a corrected burning-tree animation sheet, exactly 8 frames, 4 columns by 2 rows. CRITICAL shape preservation: copy the ORIGINAL tree with its long exposed trunk, roots, branch junctions and flower canopy at unchanged proportions in every frame. Original tree visible silhouette is 872 wide by 1752 high (height/width 2.01). The base-to-canopy exposed trunk must remain long, roughly lower half of tree height. Do NOT shorten trunk, lower canopy, enlarge canopy, make squat bush, widen silhouette or zoom. Each equal cell has the same tree ground point and identical tree scale. Flames are narrow orange/yellow tongues hugging INSIDE existing trunk and foliage silhouette; no large lateral flames increasing sprite bounding width. Keep the uppermost green leaf at same height, roots same baseline; mostly the tree itself changes neither width nor height on burning. Bright thin fire curls on bark and within foliage with eight progressively changing flame shapes, loop, no extra ember outside silhouette. Preserve all flowers, root design, original brown outline and painterly 2D game style. Transparent PNG sheet, no labels/grid/background, generous consistent transparent margins, all roots visible. Target square sheet 2048x2048, each tall 512x1024 cell has tree about 400 wide and 804 high. Review draft asset only.
```

### 나무 반복 상태 보정 (최종)

```text
Edit target attached burning tree sprite sheet. Keep the same 8 tall trees, exactly same 4x2 layout, tall exposed trunk lengths, root anchors, canopy positions, proportions and tree scale. Correct ONLY the flame animation: ALL EIGHT frames must show continuous MEDIUM burning on trunk and canopy, never an unlit tree or ignition/fade sequence. Frame 1 must have plainly visible orange yellow flames wrapping the long trunk and several flame tongues inside foliage, matching middle frames' intensity. Frame 8 must transition smoothly back to frame 1, same flame amount. Eight successive different curls rather than intensity ramp. Keep flames narrow, confined within original tree outline, no wide side embers, no fire tips above top leaf. Tall original body height to canopy width ratio 2.01, do not shrink length or thicken canopy. Preserve dark brown outlines, flowers, roots, transparent background. No labels or grid.
```

### 첫 타격 중간 불

```text
Edit target original fire trap. Produce 8 frame loop for DAMAGE STAGE 1: after first of three hits, medium weakened fire. Exactly 4 columns x 2 rows equally sized cells on transparent PNG. Maintain the reference dark oval stone ring base, individual rock positions, perspective, brown contour, style and footprint at SAME size for every frame. Flames now only 55 percent of original height, moderately narrow orange yellow tongues, less white-hot core, much more of stones visible. All 8 cells depict SAME medium intensity with successive flickering shapes; not a progression from large to small. Ground bottom identical at 86 percent of each cell, stone ring width 68 percent of cell width, centered. Preserve large transparent empty space ABOVE small flames so later full/medium/low sheets can share same cell bounds. Flat cartoon game art, crisp outlines, no smoke column, no new objects, no labels/grid, no background. Target 2048x1024, 512 square cells. Do not zoom small fire to fill the frame.
```

### 두 번째 타격 잔불

```text
Edit target is the attached medium fire animation sheet. Produce matching DAMAGE STAGE 2 after second of three hits: very small dying flames and embers, exactly eight frames at same 4 columns x 2 rows cell positions as input sheet. LOCK the stone bases: same size, same positions, same shapes and perspective, same ground baseline. Change ONLY fire intensity/shape. Replace medium fire with a few short low orange tongues just above stone rim, height about one quarter of the current medium flames (about 10-15% of original full fire). Most interior shows dark embers and red orange cracks, very little pale yellow core. Each frame is a successive gentle flicker at the SAME low intensity, suitable for loop, no regrowth to medium flame. Preserve empty top space, never enlarge or zoom the low fire. No smoke column, no additional props, labels, grid or background. Actual transparent PNG, preserve exact sheet layout and dimensions.
```

### 마지막 소멸

```text
Edit target attached low-fire trap sheet. Produce eight-frame ONE-SHOT extinguishing animation, 4 columns x 2 rows same layout and exact stone base scale/positions as input. Reading left to right then next row: frame1 low small orange flames; frame2 smaller flames; frame3 tiny tongues hugging stones; frame4 only a couple tiny orange tips; frame5 no flames just glowing coals; frame6 coals dim red with one thin wisp; frame7 nearly dark coals thin fading wisp; frame8 cold dark stones and black charcoal with NO glow, NO flame, NO smoke. This is a progressive extinguishing sequence NOT a looping flicker. Keep the same opaque oval stone base in all frames, do not scale or move rocks. Air above mostly EMPTY TRANSPARENCY, any wisp small and translucent gray, no tall smoke columns. Exact same cartoon game illustration style and dark outlines. Transparent PNG, no background, grid, text, labels or extra objects. Same 4x2 sheet dimensions.
```

