# 용사용 소환 모션 에셋 v1 (2026-10-21)

- 범위: 에셋만 제작. Unity 임포트·프리팹·런타임 연결 없음.
- 파일: `crayon-hero-summon-v1-sheet.png`, 1254×1254, 2×2 균일 셀(627×627), 투명 PNG.
- 순서: 좌상 준비 → 우상 앞으로 뻗기 → 좌하 수평 지시 자세 유지 → 우하 회수.
- 방향: 왼쪽. 기존 파란 투구·금색 날개·붉은 망토·은색 검·방패 유지.
- 제작: 내장 image_gen. 원본 외형 참조 후 셀 간 여백 확장 편집.
- 검수: 네 포즈와 경계 여백 시각 확인. 사용자 미술 검수 대기. 발 기준선과 머리 위치에 소폭 차이가 있으므로 실제 애니메이션 연결 시 정렬 보정 필요. PPU·pivot·재생 시간은 미설정. Unity 배치 생략(원화만 제작).

## 생성 프롬프트

Use case: stylized-concept. Create a production 2D game character summon animation sprite sheet using the supplied image as the exact character identity and art-style reference. Four frames arranged in an evenly spaced 2x2 grid, reading top-left, top-right, bottom-left, bottom-right. Exact same cute stocky crayon knight: white angular face with simple black dot eyes, blue faceted helmet with gold wings and red forehead gem, blue tunic, brown gloves boots belt, red scarf and cape, gold edged brown shield with white wing emblem, broad silver sword with gold hilt and red gem. Preserve thick dark brown outlines and faceted painterly shading. All four face LEFT in consistent three-quarter view, sword on screen LEFT and shield on RIGHT. This is a calm authoritative SUMMON COMMAND motion, not a sword attack. Frame 1 sword held diagonally upward in ready stance, elbow bent. Frame 2 extend sword arm forward to the left, blade almost horizontal, commanding troops. Frame 3 fully extended arm with sword pointing straight LEFT horizontally at shoulder height, confident command pose held, small cape follow-through. Frame 4 withdraw sword partway toward original ready pose. No duplicate arms, no added weapons. Keep same helmet size and body size, head center and foot ground line at identical local cell coordinates; feet planted throughout. Full body and entire blade visible within each cell with generous transparent margins, no overlap between cells, at least 10 percent empty margin per cell edge. Exactly four distinct figures, equal square cells in a square sheet. Genuine transparent alpha background. No text, numbers, grid, shadows, scenery, magic circles, glow, particles, silhouettes, slash trails or summoned units. Only the fully colored character performing the motion. Aim 2048x2048 sheet.

## 간격 보정 프롬프트

Edit this exact four-pose sprite sheet only to fix layout for slicing. Preserve exact art and four poses. Square canvas, four equal cells 2x2. Scale down ALL four characters uniformly to 70 percent current size, including swords and capes. Move each character such that the face center is at local cell x=58 percent, and the bottom of both feet is local cell y=80 percent. Identical head size across all frames. Every complete figure including sword and cape must fit inside its OWN cell with at least 8 percent transparent margins on all four edges. Especially upper-right sword must stay right of vertical canvas midpoint; bottom-left cape must stay left of vertical canvas midpoint. No crossing cell boundaries. Substantial empty transparent cross-shaped gutters. Do not change poses, designs, colors, outlines or character identity. True transparent alpha background, no text or grid.


## Unity 연결 (2026-10-21)

일반 용사용: 사용자 첨부 `20261021_054057825_iOS.png`의 4포즈를 `CrayonHero_Summon_0~3.png`로 분리.

투명 영역 잡점을 정리하고 기존 본체의 크기·발 기준선에 등록했다. 400 PPU·중앙 pivot이며 기존 에셋과 GUID는 보존했다.
소환은 준비→지시→실제 소환 유지→회수, 점프는 준비→도약→정점→하강→착지→반동→복귀 순서다.
`BossSummonJumpAnimationVerification.Verify` 배치 통과(종료 코드 0). 실제 Play 미술 검수는 미실시.
수동: `Trickal Fan Game > Debug > Open Boss Test Room`에서 Play 후 크레용사용/새마음금고를 선택한다.
크레용사용은 일반·황금 소환 모션과 좌우 방향, 새마음금고는 연속 점프의 크기·발 기준선·착지 반동을 확인한다.
