# 세로 2칸 나무 (2026-10-10)

- 생성: built-in `image_gen`, 기존 나무 원화를 스타일 참고로 사용, 투명 배경.
- 저장: `game/Assets/Art/Drafts/FairyKingdom/Obstacles/obstacle-tree.png` (887×1774). 기존 `.meta` GUID 보존.
- 표시: 가로 1·세로 2 Unity 단위, 중심은 배치 칸보다 y +0.5. 아래 끝은 충돌 박스 아래 끝과 일치한다.
- 충돌: 기존 root의 1×1 BoxCollider2D 하나, offset 0. 위 칸에는 충돌을 추가하지 않는다. 파괴·화상·비행 차단 규칙 유지.
- 기존 tree-grove 4개와 Layout importer/Setup 재실행 경로에 적용. 런타임에는 알파 경계로 자른 원화를 같은 크기로 맞춘다.
- 정적 확인: PNG 투명 배경, 카탈로그 경계, GUID, 4개 표시 Transform과 하단 충돌 구조 확인.
- Unity 6000.3.22f1 배치 `TrickalFanGame.Editor.FairyKingdomArtworkVerification.Verify`: 기존 Unity 실행 중 종료 코드 1, 성공 로그 없음. 자동 검증 통과로 처리하지 않는다. 로그: `output/tree-geometry-verification.log`.
- 수동: Edit Mode에서 `Trickal Fan Game/Artwork/Verify Fairy Kingdom 45 Sprites` 실행. 나무 1×2 표시·하단 1×1 충돌·상단 통과 검사가 통과해야 한다. `Assets/Scenes/SampleScene.unity` Play에서 tree-grove 방의 아래 칸은 막히고 위 칸은 통과하는지, 나무 크기와 파괴·화상 표시를 확인한다. 실제 Play 미술 검수는 미완료.

## 사용한 프롬프트

Use case: style-transfer. Asset type: single transparent 2D Unity game obstacle sprite. Reference image is STYLE reference: preserve the cute fairy kingdom hand-painted cartoon look, warm brown outlines, green rounded leafy clusters, small cream flowers, golden brown bark. Redesign this tree as a tall natural tree for a ONE TILE WIDE, TWO TILES HIGH upright silhouette (visible silhouette aspect ratio width:height exactly 1:2). Full tree, canopy at top, clearly readable long solid trunk and modest roots in lower half. Wider foliage at top but stay within one tile width. Root base horizontally centered, compact, no extra ground or shadow. Orthographic slightly top-down dungeon game perspective consistent with reference. Generous transparent padding around complete silhouette, no clipping. No grid, no labels, no text, no background, no extra objects. Actual transparent alpha. This is a new taller tree drawing, not a vertically stretched version of the reference.
