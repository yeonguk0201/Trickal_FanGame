# 황금용사용 소환 모션 v1 (2026-10-21)

- 범위: 에셋 초안만 제작. Unity 임포트·프리팹·런타임 연결 없음.
- 파일: `crayon-hero-golden-summon-v1-sheet.png`, 2×2 균일 셀, 투명 PNG.
- 순서: 좌상 준비 → 우상 칼 뻗기 → 좌하 소환 지시 자세 유지 → 우하 회수. 왼쪽을 향한다.
- 외형 참조: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Awakened.png`.
- 모션 참조: 일반 용사용 `crayon-hero-summon-v1-sheet.png`.
- 흰금색 투구 보석·흰 다이아·노란 망토·금색 장갑·빛 날개를 반영했다.
- 제작 도구: 내장 image_gen. 최초 생성 후 여백·외곽 정리 편집 1회.
- 검수: 네 포즈와 전신 포함 여부를 시각 확인. 투명 PNG 형식 확인. 외곽에 미세한 유색 잔여 픽셀이 남아 있어 최종 연결 전 추가 정리가 필요하다. 프레임별 위치·크기 보정과 실제 재생 검수는 미실시. 사용자 미술 검수 대기.
- PPU·피벗·재생 시간 미설정. 원화만 추가하여 Unity 배치는 생략했다.

## 생성 프롬프트

Use case: identity-preserve. Create a golden awakened version of the FOUR FRAME SUMMON COMMAND sprite sheet in image 2, using image 1 as definitive GOLDEN CHARACTER appearance reference. Image 1: identity, colors, equipment, wings reference. Image 2: edit target, preserve its 2x2 layout, pose sequence and wide transparent gutters. Replace each normal knight with the exact awakened knight: ivory pale gold faceted crown crystal, white diamond forehead jewel, gold winged helmet, white angular face/simple black dot eyes, YELLOW scarf and yellow cape, gold gloves/bracers, white-gold armor, brown belt and boots, ivory white broad sword with gold leaf emblem, gold hilt and WHITE diamond, brown gold shield with white wings emblem, TWO pale ivory light wings behind shoulders. NO blue helmet crystal, red gem, red scarf or red cape. Thick dark brown outline and clean faceted illustrated shading matching references. All four face LEFT three-quarter view; sword on screen left, shield on screen right, never mirror. Order: top-left ready sword diagonally upright, top-right arm extending sword toward left almost horizontally, bottom-left arm fully extended and sword perfectly horizontal pointing LEFT as authoritative summon command, bottom-right returning sword to upright ready stance. Feet planted, not attacking. Keep head size consistent and align feet baseline and head center within each cell. Entire sword, light wings, cape, boots fit fully inside own equal square cell with generous minimum 10 percent transparent margins. Use smaller character scale as needed to accommodate wings, same scale in all cells. True transparent alpha background. Exactly four figures. No detached particles, magic circles, slash trails, text, grid lines, shadow, background glow or scenery. The wings should be compact well-defined ivory shapes like image 1, not a screen-filling aura. Asset only, 2x2 square sprite atlas.

## 후속 편집 프롬프트

Clean and re-layout this exact golden knight 2x2 sprite sheet. Remove ALL detached red/yellow/white speckles, noisy pixels, colored fringes and background residue around every character. Absolutely clean true transparent background, only four clean character silhouettes with clean antialiased contours. Wings are solid clean pale ivory shapes with gold outline, no external glow/no particle aura. Preserve character design, colors and exact four sword poses. Uniformly shrink all four complete figures to 78 percent current size, centered within each own equal quadrant. Keep identical head scale and same relative foot baseline across cells. At least 10 percent cell-width EMPTY TRANSPARENT margin between each complete figure and EVERY cell edge including fully extended sword and cape and wings. Wide transparent cross-shaped gutters. Preserve all sword tips and wings uncropped. No red anywhere, no detached pixels, no grid, no labels, no background. This is clean final game sprite artwork, four frames.


## Unity 연결 (2026-10-21)

황금 용사용: 사용자 첨부 `20261021_054502814_iOS.png`의 4포즈를 `CrayonHero_Golden_Summon_0~3.png`로 분리.

투명 영역 잡점을 정리하고 기존 본체의 크기·발 기준선에 등록했다. 400 PPU·중앙 pivot이며 기존 에셋과 GUID는 보존했다.
소환은 준비→지시→실제 소환 유지→회수, 점프는 준비→도약→정점→하강→착지→반동→복귀 순서다.
`BossSummonJumpAnimationVerification.Verify` 배치 통과(종료 코드 0). 실제 Play 미술 검수는 미실시.
수동: `Trickal Fan Game > Debug > Open Boss Test Room`에서 Play 후 크레용사용/새마음금고를 선택한다.
크레용사용은 일반·황금 소환 모션과 좌우 방향, 새마음금고는 연속 점프의 크기·발 기준선·착지 반동을 확인한다.
