# 바위 피격 균열 프레임

2026-10-14. 내장 imagegen 도구로 기존 원화를 편집했다. 일반 바위, 황금 바위, 가시 바위 함정에 각각 가벼운 균열 → 깊은 균열 → 파괴 직전의 세 프레임을 사용한다. 무피격 상태는 기존 원화, 네 번째 타격의 파괴·비활성화·드롭은 기존 규칙을 유지한다.

## 에셋과 연결

- `game/Assets/Resources/RockDamage/`의 PNG는 투명 배경의 가로 3칸 원본 시트다. PNG를 재가공하지 않고 Unity가 각 사각형을 Sprite로 사용한다.
- 같은 이름의 JSON은 원본 픽셀 크기와 각 칸의 알파 경계(Rect, Unity 좌하단 좌표)를 기록한다. 텍스처는 CPU 읽기를 사용하지 않는다.
- `RockDamageArtwork`가 Resources로 불러와 크기별 Sprite를 캐시한다. 원본의 표시 폭과 지면 기준점을 유지한다.
- `FairyKingdomArtworkView`가 피격 단계에 맞춰 표시하며 세 종류의 색상을 흰색으로 유지한다. 나무·상자·다른 함정의 기존 표현은 유지한다.
- 기존 45종 원화 Catalog와 프리팹 GUID를 변경하지 않는다.

## 검증

- 2026-10-14: Unity 6000.3.22f1 배치 `TrickalFanGame.Editor.RockDamageArtworkVerification.Verify` 통과, 종료 코드 0. 로그: `tmp/rock-damage-verification.log`. 컴파일 오류·검증 예외 없음.
- 실행 인수: `-batchmode -quit -nographics -projectPath <repository>/game -executeMethod TrickalFanGame.Editor.RockDamageArtworkVerification.Verify -logFile <repository>/tmp/rock-damage-verification.log`.
- 정적 검사: UTF-8 디코딩, 9개 프레임 사각형의 이미지 범위, 신규 Unity 에셋의 `.meta` 쌍, 이번 변경의 diff 공백 검사 통과.
- 실제 Play 미술 검수는 미실행. 자동 검증으로 화면 항목을 완료 처리하지 않았다.
- 자동 메뉴: Edit Mode → `Trickal Fan Game/Artwork/Verify Rock Damage Frames`.
- 성공 로그: `Rock damage artwork verification passed`. 3종 9프레임, 색상, 표시 폭·바닥 기준점, 충돌 크기, 4번째 타격 파괴를 검사한다.
- 실제 Play 미술 검수는 별도다. Game Scene에서 일반 바위를 1회씩 타격하고, `F1 → — Obstacle — → gold-rock`으로 황금 바위를 선택한다. 가시 바위는 `F1 → — Trap — → Place SpikedRock`으로 배치한다. 각 종류에서 1·2·3회 타격 후 균열 증가, 4회 파괴, 위치·크기 튐 여부와 가시 접촉 피해를 확인한다.

## 생성 프롬프트

### 일반 바위

```text
Use case: precise-object-edit. Create a production game sprite sheet from the attached rock edit target. Exactly THREE frames in ONE horizontal row of equal square cells, transparent background, no text. Each cell contains the SAME original rock at identical scale, center and ground baseline with generous padding. Preserve original silhouette, beige gray palette, lighting, thick dark brown outline and hand painted cartoon style. Left: light damage with a few NEW jagged thin dark branching cracks across the broad faces. Middle: moderate damage extending those same cracks with deeper branching fissures and tiny chipped edges. Right: severe damage with large dark connected fractures separating still-standing chunks, a few small chips inside footprint; remains same height and silhouette, NOT rubble. Cracks must clearly progress and read at small game scale, no overall tint darkening. No background, no shadows outside object, no extra objects. Output wide 3:1 sheet.
```

### 황금 바위

```text
Use case: precise-object-edit. Create a production game sprite sheet from the attached rock edit target. Exactly THREE frames in ONE horizontal row of equal square cells, transparent background, no text. Each cell contains the SAME original rock at identical scale, center and ground baseline with generous padding. Preserve original silhouette, beige gray palette, lighting, thick dark brown outline and hand painted cartoon style. Left: light damage with a few NEW jagged thin dark branching cracks across the broad faces. Middle: moderate damage extending those same cracks with deeper branching fissures and tiny chipped edges. Right: severe damage with large dark connected fractures separating still-standing chunks, a few small chips inside footprint; remains same height and silhouette, NOT rubble. Cracks must clearly progress and read at small game scale, no overall tint darkening. No background, no shadows outside object, no extra objects. Output wide 3:1 sheet.
This edit target is GOLD ORE ROCK. Preserve every bright yellow gold deposit in exactly the original locations and sizes. Damage fractures cross stone and some ore faces, keep yellow gold saturation and shine. Do not turn it into an ordinary rock.
```

### 가시 바위

```text
Use case: precise-object-edit. Create a production game sprite sheet from the attached rock edit target. Exactly THREE frames in ONE horizontal row of equal square cells, transparent background, no text. Each cell contains the SAME original rock at identical scale, center and ground baseline with generous padding. Preserve original silhouette, beige gray palette, lighting, thick dark brown outline and hand painted cartoon style. Left: light damage with a few NEW jagged thin dark branching cracks across the broad faces. Middle: moderate damage extending those same cracks with deeper branching fissures and tiny chipped edges. Right: severe damage with large dark connected fractures separating still-standing chunks, a few small chips inside footprint; remains same height and silhouette, NOT rubble. Cracks must clearly progress and read at small game scale, no overall tint darkening. No background, no shadows outside object, no extra objects. Output wide 3:1 sheet.
This edit target is SPIKED ROCK TRAP. Preserve original dark gray purple stone colors (not beige), and all SIX red-tipped spikes in original positions, same sizes, vividly red and dangerous throughout all three frames. New progressive fractures in broad central stone slab and spike bases, never remove spikes. Leave at least 4 percent transparent padding around each rock so none touches adjacent cell or sheet edge.
```

가시 바위는 원본 시트의 인접 프레임 간격이 좁아 아래 프롬프트로 여백을 보정했다. 최종 JSON은 바위 부분만 지정하며 시트 경계의 선은 표시 영역에 포함하지 않는다.

```text
Edit target: the attached three-stage spiked rock sprite sheet. Retain the art and progressive fractures, gray stone and SIX red tips. Correct ONLY layout: exactly 3 equally-sized square cells in one horizontal row. Each rock must occupy ONLY 65 percent of its cell width and 65 percent height, centered inside that cell with at least 17 percent TRANSPARENT EMPTY MARGIN on EVERY side. There must be very wide fully transparent vertical gutters between sprites. No touching or overlapping sprites and no cropping. Same scale and bottom alignment for all frames. This is for slicing into three independent animation frames so generous blank margins are mandatory. Fully transparent RGBA background. No text, no grid, no visible cell borders.
```
