# 구덩이 모듈 타일 원화 v1

> 이 문서는 이전 버전 기록이다. 이음새·꺾임·큰 구덩이 내부를 개선한 현재 Unity 적용 버전은 [연결형 구덩이 v2](../pit-tiles-v2/README.md)다. v1의 사분면 Atlas는 현재 표시에 사용하지 않는다.

- 최초 요청: 에셋 제작과 1칸·ㄱ·ㄷ·ㅁ·ㄹ·ㅡ·ㅣ·ㅏ 배치 미리보기. 후속 요청으로 Unity 연결까지 적용했다.
- 제작 도구: built-in image_gen. 기존 terrain-pit.png는 화풍 참고로만 사용했다.
- 원화: [pit-tile-atlas-v1.png](./pit-tile-atlas-v1.png)
- 배치 컨셉: [pit-shapes-preview-v1.png](./pit-shapes-preview-v1.png)
- 기존 원화·에셋 GUID는 유지하고, RoomPit Prefab에 PitTileArtwork 표시 컴포넌트를 추가했다. SecretPit는 기존 표시를 유지한다.

## 구성

4열 × 4행, 왼쪽 위부터 읽는 순서:

| 행 | 1열 | 2열 | 3열 | 4열 |
|---|---|---|---|---|
| 1 | 내부 | 위 가장자리 | 오른쪽 가장자리 | 아래 가장자리 |
| 2 | 왼쪽 가장자리 | 바깥 NW | 바깥 NE | 바깥 SE |
| 3 | 바깥 SW | 안쪽 NW | 안쪽 NE | 안쪽 SE |
| 4 | 안쪽 SW | 가로 통로 | 세로 통로 | 단독 구덩이 |

밝은 돌과 연두색 풀 테두리, 짙은 청회색 내부를 사용한다. 연결된 구덩이 칸 사이에는 테두리를 두지 않는 방향이다. ㅁ의 중앙은 걸을 수 있는 땅이다.

## 최초 원화 검수 상태와 한계

- 에셋 원화와 배치 컨셉 검수용 초안이다. 사용자 미술 검수는 대기 중이다.
- 생성 원본에는 셀 사이 여백과 타일별 그림 크기 차이가 있다. 균등 자동 슬라이스 가능한 완성 아틀라스로 취급하지 않는다.
- 배치 미리보기는 image_gen이 같은 화풍으로 그린 연결 컨셉이며, 원본 타일을 픽셀 단위로 조립한 결과가 아니다.
- 실제 seamless 연결, 고정 셀 규격으로 정렬·분할, 안쪽 모서리의 대각선 이웃 처리, Unity 임포트·Play 검증은 미수행이다.
- 구현 전에는 고정 셀의 연결면과 모서리를 정규화하고 실제 타일 조립으로 이음새를 검증해야 한다.

## Unity 적용 결과 (2026-10-10)

- 최종 에셋: `game/Assets/Resources/PitTiles/Atlas.png`, 1024×1024 RGBA, 4×4·256px 셀. `scripts/export-pit-tiles.cjs`로 원화 여백 제거·연결면 크기 보정·내부 및 모서리 조각을 내보낸다. 새 원화는 만들지 않았다.
- Runtime: `PitTileArtwork`가 기존 BoxCollider2D 범위를 1단위 이하의 셀로 나누고, 128px 사분면 네 개씩 조립한다. 같은 Pits 부모의 활성 구덩이 범위에서 가로·세로·대각선 이웃을 확인한다. 큰 직사각형 구덩이와 여러 구덩이의 접합을 모두 처리한다.
- 기존 RoomPit Prefab에 연결했고, 기존 FairyKingdomArtworkView와 Setup도 타일 표시를 존중한다. 런타임 생성 그림은 저장하지 않으며 재생성 시 제거한다. 콜라이더·루트 크기·ID·물리 레이어·투사체 통과 규칙은 변경하지 않는다.
- 필터 Point, 무압축, mipmap 없음, 사분면 PPU 256·중심 피벗. 셀당 실제 표시 폭·높이는 해당 Collider 범위로 결정한다. 기존 표시 Material·sorting layer/order를 사용한다.
- [실제 Unity 렌더](./pit-unity-preview-v1.png), [동일 조각의 오프라인 조립](./pit-assembled-preview-v1.png). 최초 `pit-shapes-preview-v1.png`는 여전히 생성형 컨셉 이미지이며 실제 렌더와 구분한다.
- 자동 검증: Unity 6000.3.22f1, `TrickalFanGame.Editor.PitTileArtworkVerification.ApplyAndVerifyBatch`, 종료 코드 0. 로그 `output/pit-tile-unity-verified.log`, 성공 메시지 `Connected pit tile verification passed`. 8가지 모양, 대각선만 접한 경우 분리, ㅁ의 안쪽 모서리, 직사각형 내부 테두리 제거, 재생성 중복 방지, 콜라이더·루트 크기 보존을 확인했다. Runtime/Editor 별도 Roslyn 컴파일도 성공했다.
- Unity 미리보기 Scene: `Assets/Scenes/PitTileArtworkPreview.unity`. 기존 TMP 글꼴을 재사용했으며 모양 라벨에 필요한 글리프가 기존 동적 글꼴 아틀라스에 추가되었다.
- 수동 확인: Edit Mode에서 `Trickal Fan Game/Artwork/Open Connected Pit Tile Preview`로 8개 모양을 열고 Game 뷰에서 확인한다. 검증 메뉴는 `Trickal Fan Game/Artwork/Verify Connected Pit Tiles`. 실제 Game Scene에서는 F1 → `— Layout —` → `Show layouts` → `Force wide-pit-bridges`를 선택하여 연결 표시와 보행 차단·투사체 통과를 확인한다.
- 실제 Play 미술 검수는 대기 중이다. 내부 절벽 텍스처와 일부 안쪽 모서리에 그림 패턴 차이가 남아 있다. 자동 통과를 seamless 미술 승인으로 취급하지 않는다.

## 원화 프롬프트

```text
Create a production-oriented modular 2D pit terrain sprite atlas for a cute Korean fairy-kingdom dungeon game. Input image is STYLE REFERENCE ONLY: warm cream tan rocks, lime green grass tufts, tiny sparse daisies, dark brown cartoon outlines, painterly clean chunky shapes and deep charcoal-blue abyss. Redesign as orthographic TOP-DOWN square-grid compatible tiles, NOT elliptical standalone holes, NOT isometric diamonds. Genuine transparent background.

Output a precise 4 columns x 4 rows atlas, 2048x2048 canvas, sixteen 512x512 equally sized cells, NO gaps, NO labels or grid drawn. Every tile uses same scale and global lighting from upper left. Boundary rim width about 90 pixels. Pit interior uniform nearly black blue #121720 with subtle depth texture; all connecting pit sides must match seamlessly in color and texture. Walkable ground outside each rim is TRANSPARENT. Rim should fit inside the tile, never extend beyond its square cell.

Exact tile order from top-left, read left to right:
row 1: solid seamless dark abyss center; NORTH boundary (grass/stone rim across top, abyss below, transparent above rim); EAST boundary (rim on right, abyss left); SOUTH boundary (rim across bottom, abyss above).
row 2: WEST boundary (rim left, abyss right); convex OUTER corner NW (rim along top and left joining rounded at upper-left, abyss lower-right); convex OUTER corner NE (top and right); convex OUTER corner SE (bottom and right).
row 3: convex OUTER corner SW (bottom and left); concave INNER corner NW (tiny transparent walkable-ground notch touching upper-left corner, curved grass-rock rim around it, abyss occupies all other area); concave INNER corner NE (notch upper-right); concave INNER corner SE (notch lower-right).
row 4: concave INNER corner SW (notch lower-left); horizontal narrow pit channel (rim top and bottom, abyss between, open all the way at left/right cell edges); vertical narrow pit channel (rim left/right, abyss between, open at top/bottom cell edges); isolated ONE CELL square pit (rounded squarish rim on all four sides, deep dark opening in center).

Tiles occupy their precise square cells. Edges that connect to other tiles must reach the cell edge flush and align. No framing outlines around cells, no drop-shadow backdrop, no text, no checkerboard baked in, no extra floating rocks, no entire scene. Do not put isolated oval holes in every cell. This is a reusable tileset with open-sided terrain segments.
```

## 배치 프롬프트

```text
Create a polished visual test board showing EIGHT connected pit configurations made from the modular grass-and-stone pit terrain tiles in the reference. Reference is the pit tileset/style source. Match its lime green grassy edges, cream tan stones, dark blue charcoal chasm and cute bold painterly game style. Orthographic overhead square grid view, no isometric diamonds, no perspective foreshortening. All pits same tile width and art scale. Thin rim leaves generous visibly DARK opening, no bridges across connected cells. Connected cells merge into ONE continuous dark opening with rim ONLY along perimeter. No internal stone dividers. On a flat warm muted beige-green walkable ground. 4 columns x 2 rows of eight panels, large high-resolution landscape image. Enough breathing room per panel. Top row labels exactly "1칸", "ㄱ", "ㄷ", "ㅁ". Bottom row labels exactly "ㄹ", "ㅡ", "ㅣ", "ㅏ". Clear simple dark typography above each panel.

Use EXACT occupancy maps below, '#' means PIT, '.' means solid beige-green WALKABLE GROUND. These maps are geometry instructions, do NOT print maps or grids in image. One # is one square tile. Use subtly rounded external corners and neatly curved internal corners, preserve letter silhouettes. Each connected pit is a single unbroken trench, with grass rim following its exact perimeter.
1칸: #
ㄱ: ### / ..# / ..#
ㄷ: ### / #.. / ###
ㅁ: ### / #.# / ### (center is clearly solid walkable ground island with matching beige-green surface, NOT another hole)
ㄹ: ### / ..# / ### / #.. / ### (five rows! three horizontal runs, alternating right then left vertical connectors, no shortcuts)
ㅡ: ##### (single long horizontal trench)
ㅣ: # / # / # / # / # (single long vertical trench)
ㅏ: #.. / #.. / ### / #.. / #.. (long vertical stem on LEFT with arm extending RIGHT at center).

Each silhouette must be exact. All eight objects completely visible. No extra pits, no figures, no flowers inside the abyss, no water, no decorative title or subtitle. Never add rim across adjoining occupied tiles. This is an ART CONCEPT arrangement board, no tool UI.
```

