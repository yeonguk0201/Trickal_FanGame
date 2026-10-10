# 연결형 구덩이 v2 — 연속 윤곽 렌더 (2026-10-10)

## 요청과 변경

v1의 타일 이음새·각진 안쪽 모서리·검은 큰 구덩이를 개선했다. 1칸, 가로·세로 직선, ㄱ자에서 시작하는 동일한 경계 규칙을 ㄷ·ㅁ·ㄹ·ㅏ·구불구불·꽉 찬 3×3에 적용한다.

서로 다르게 그려진 사분면을 잘라 붙이는 방식을 교체했다. 같은 Pits 부모 아래 Collider 사각형의 합집합을 구하고, 공선 꼭짓점을 제거한 다음 바깥/안쪽 모서리를 둥글게 만든다. 새 풀·돌·절벽 띠를 전체 둘레의 연속 좌표로 입히고, 내부에는 별도의 암벽 원화를 연속 좌표로 채운다. 결과는 부모별 단일 Sprite/Texture이며, 끊어진 여러 구덩이도 투명 영역으로 구분한다.

## 실제 에셋과 화면

- 최종 경계 원화: `game/Assets/Resources/PitTiles/BoundaryRibbon.png`.
- 최종 내부 원화: `game/Assets/Resources/PitTiles/CavernDepth.png`.
- 경계 생성 원본: [boundary-ribbon-source.png](./boundary-ribbon-source.png). 실제 경계 에셋은 투명 여백만 trim했다.
- [ㄹ·3×3 실제 Unity 확대 렌더](./pit-lieul-3x3-v2.png).
- [10가지 모양 실제 Unity 렌더](./pit-unity-preview-v2.png).
- 동일한 Unity 렌더러가 내보낸 투명 PNG: [1칸](./pit-single-v2.png), [가로](./pit-horizontal-v2.png), [세로](./pit-vertical-v2.png), [ㄱ](./pit-corner-v2.png), [ㄷ](./pit-c-v2.png), [ㅁ](./pit-ring-v2.png), [ㄹ](./pit-lieul-v2.png), [ㅏ](./pit-branch-v2.png), [구불구불](./pit-winding-v2.png), [3×3](./pit-3x3-v2.png).

## Unity 구성

- `PitTileArtwork` 컴포넌트와 기존 RoomPit Prefab/GUID를 유지했다. `PitContourRasterizer`가 최종 그림을 생성한다.
- RoomPit Prefab에 표시 컴포넌트를 연결한다. Collider·루트 Transform·Pit ID·물리 레이어·비행 및 투사체 규칙은 변경하지 않는다. SecretPit는 기존 표시다.
- 모서리 반경 0.30단위, 경계/절벽 폭 0.40단위. 원화에서 풀·밝은 돌 영역을 화면 두께에 맞게 배분했다.
- 기본 출력 해상도 128px/Unity 단위, 긴 변 최대 2048px. 중심 피벗, Bilinear·Clamp·mipmap 없음. 원화는 CPU 합성 때문에 Read/Write 활성화·무압축이다.
- 경계는 원화 중앙 60%를 Mirror 좌표로 순회하여 접합점에서 같은 색으로 닫힌다. 직선과 곡선 모두 같은 둘레 좌표를 쓰므로 칸 경계에 UV가 재시작하지 않는다.
- 내부는 서로 다른 스케일의 암벽 질감을 혼합한다. 3×3 가운데에 검은 단색 조각을 넣지 않는다.
- 생성 그림은 DontSave로 관리하며 재생성/해제 시 Sprite와 Texture를 정리한다. 부모 비활성화 중 GameObject 정리는 Editor delayCall로 미룬다. 방 전체 이동은 로컬 형상을 바꾸지 않으므로 재합성을 유발하지 않는다.
- 현재 배치 전제는 회전하지 않은 축 정렬 사각형 구덩이다. 기존 방·Layout importer가 쓰는 배치 형식에 해당한다.

## 검증과 검수

- Unity 6000.3.22f1 배치: `TrickalFanGame.Editor.PitTileArtworkVerification.ApplyAndVerifyBatch`.
- 최종 로그: `output/pit-contour-v2-final.log`, 종료 코드 0과 `Continuous pit contour verification passed`를 확인했다. 컴파일 오류·검증 예외·GameObject 해제 오류 없이 종료했다.
- 검증 항목: 10가지 형태, 합쳐진 3×3 Collider와 9개 칸의 픽셀 완전 일치, ㄱ자 Collider 분할 방식의 픽셀 완전 일치, 대각선 접촉의 독립 윤곽, ㅁ의 투명 중앙, 3×3 내부 색상 다양성·밝기, 단일 표시와 반복 생성 안정성, Collider/루트 크기 및 기존 RoomPit 불변조건.
- 실제 Unity 렌더를 보고 곡률·테두리/절벽 두께·내부 밝기를 조정했다. 최초 경계 합성 강도와 해제 오류를 수정한 뒤 재실행했다.
- 사용자 미술 승인을 자동 검증으로 대체하지 않는다. 실제 SampleScene Play 조작 검수와 사용자 최종 미술 검수는 대기 중이다.
- 전체 보기: `Trickal Fan Game/Artwork/Open Connected Pit Tile Preview` → `Assets/Scenes/PitTileArtworkPreview.unity`.
- ㄹ·3×3 확대 보기: `Trickal Fan Game/Artwork/Open Pit Lieul and 3x3 Review` → `Assets/Scenes/PitContourFocusPreview.unity`.
- 자동 검증 메뉴: `Trickal Fan Game/Artwork/Verify Connected Pit Tiles`.
- 실게임 확인: SampleScene Play에서 RoomPit가 있는 방을 확인한다. 연결된 구덩이에 칸별 테두리가 없어야 하며, 보행 차단·투사체 통과는 유지되어야 한다.

## 제작 도구와 프롬프트

built-in image_gen으로 새 원화 두 장을 생성했다. 원본의 투명 여백 정리는 기계적 에셋 내보내기이며, 형태별 PNG와 보드 화면은 Unity의 실제 렌더 결과다. 생성형 연결 컨셉 이미지를 검증 이미지로 사용하지 않는다.

### 경계 띠

```text
Asset type: one reusable painted terrain boundary ribbon texture for a top-down 2D fairy dungeon game. Reference image is STYLE ONLY: lime grass, warm cream-tan stones, tiny white daisies, dark charcoal slate abyss, chunky cute clean hand-painted outline style. Create ONLY ONE LONG HORIZONTAL STRAIGHT BOUNDARY STRIP spanning the entire width of the image edge to edge, NO end caps, NO corners, NO individual pits, NO panels, no atlas. The strip will be bent continuously around arbitrary curved pit contours by a renderer.

Image about 1536 wide x 512 high, landscape 3:1. Along X, a continuous irregular sequence of rounded tan rim stones with lime green moss tufts and sparse very tiny flowers, ABOVE them a thin irregular grassy outer fringe with genuine transparent space above; BELOW the rim stones, clearly visible inward-descending dark slate rock cliff faces which gradually fade to a dark blue-charcoal color near the bottom edge. Surface y layout: top 0-8% sparse transparent fringe only; 8-30% green moss and cream tan rim rocks; 30-85% dark blue slate cliff with irregular varied small vertical rock facets and fine green moss; bottom 85-100% continuous darkest slate shade #202938 but not pure black. Cliff texture reaches the entire bottom edge opaque. NO ground below. Uniform thickness across full width, no waves of the whole strip. Full width at both left/right edges, no border or margins at left/right. Use medium small stones, about 16 varied stones across width, small flowers 4 total, no gigantic boulders, no long black empty flat region. Horizontal tileable appearance, similar color and elevation at both ends, no vertical dividing lines. This is an unlit diffuse color artwork texture with crisp attractive readable brushwork, not photorealistic. The top and bottom of the strip MUST stay straight parallel overall. Genuine transparent background, no checkerboard, no text.
```

### 내부 암벽

```text
Asset type: one seamless square painted texture for the interior of a bottomless pit in a cute top-down 2D fantasy dungeon. Reference is STYLE ONLY, slate cliff interiors of its holes. Output a single square opaque 1024x1024 diffuse texture, no alpha, edge-to-edge texture only, no border, no hole silhouette, no rim, no grass, no flowers, no UI. Dark blue charcoal layered slate rock facets and subtle jagged rocky ledges descending into shadow, small and medium irregular organic polygons, softly shaded painterly cute cartoon style with dark thin outlines. Mostly readable navy slate #263347 and #303d50 facets, deeper cracks #131c2b, restrained highlights #3a4656. A dark cave depth feeling but DEFINITELY NOT flat solid black, no big black central patch, no pure black anywhere. Distribute gentle rock detail evenly throughout the whole canvas with varied sizes and subtle natural light. No central object, no large focus feature, no pattern grid, no horizon, no perspective convergence. Designed as a continuous seamless repeating texture in both directions. Match game pit cliff art, do not add crystals, glow, lava, water or floor cobblestones. This is a dim abyss wall/depth texture, not a walkable floor.
```
