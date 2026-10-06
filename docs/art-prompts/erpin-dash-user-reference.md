# 사용자 기준 시트 기반 에르핀 돌진 재제작

## 기준과 결과

- 사용자 제작 기준 시트 보관: `docs/art-references/erpin-user-reference.png`.
- 입력 원본: `C:/Users/yeonguk/OneDrive/ChatGPT 이미지 2026년 10월 5일 오전 04_48_40.png`.
- 내장 `image_gen` 사용, 생성 ID: `eb5b0aab-7055-4774-83e7-53dc433ab7d2`.
- 적용: `game/Assets/Resources/Characters/Erpin_HighGrade/Erpin_HighGrade_0.png`부터 `_3.png`까지. 준비 1프레임과 돌진 반복 3프레임이다.
- 기존 `.meta` GUID와 400 PPU를 유지했다. 충돌 후 넘어지는 4~7번 프레임은 이번 범위에서 교체하지 않았다.
- 프리뷰: `game/Logs/ErpinHighGradePreview/erpin-dash-user-reference.gif`.

지팡이와 두 갈래 깃발은 기준 디자인을 따르되 머리카락과 손 등에 가려지는 부분은 자연스럽게 가린다. 모든 장식을 억지로 노출하지 않는다. 양손 파지, 짧은 몸 비율, 뒤로 흐르는 머리카락과 망토를 포함한다.

시트는 투명 영역의 연결 요소를 기준으로 분리하고 기존 에르핀 기준 크기와 발 위치에 등록했다. 이미지 등록과 리사이즈만 Pillow로 처리했으며 그림 수정은 이미지 생성 도구로 수행했다.

## 프롬프트

Unity `ErpinHighGradeArtworkSetup.SetupAndVerifyBatch` 검증 통과. 돌진/충돌 프레임, 방향 전환, 일시정지, 플레이어 루트와 히트박스 유지, 걷기/저학년 모션 우선순위와 고학년·저학년 스킬 회귀를 확인했다. 로그: `game/Logs/erpin-dash-user-reference.log`, 종료 코드 0.

```text
Create a transparent 2D game sprite animation sheet of Erpin based STRICTLY on the attached USER-MADE character reference. Four full-body poses in a clean 2x2 grid, equal spacious cells, no labels or lines. All facing LEFT in side/three-quarter profile, orthographic game sprite style, thick dark brown outlines and softly painted cel highlights matching reference. This is a dash charge with her staff raised/braced ahead, not a spell cast.
Pose reading order: top-left preparation leaning forward, both hands gripping staff near chest, knees bent; top-right running stride A forward foot extended, rear leg pushes, head tilted into charge; bottom-left stride B feet passing under torso with a small natural bounce; bottom-right stride C opposite foot extended, consistent charge silhouette. Three running poses make a loop. Head and body size consistent in every frame. Her oversized head and very short torso/legs stay chibi, no long body. Hair and red pompom-edged cape trail toward right, gentle changes across run frames, expressive determined cheeky face, star-highlight pink eyes, tiny fang.
Hold the staff diagonally ahead/up toward left with a natural firm two-hand grip; show both sleeves/hands, allowing farther arm partial occlusion. Main round gold head with cyan crystal, cyan inlay top cap and gold pole form one straight rigid weapon axis. Two white gold-edged ribbons attach below head. Treat the staff as an ordinary prop integrated into action, NOT an exploded technical diagram. Let the grip, head, hair, body and perspective naturally occlude ribbons and pole where appropriate. Do not force every ornament visible in every frame or spread them to show details. Match exact user reference staff head and cap silhouettes without redesign. No offset/bent connectors.
Preserve reference crown, blonde long hair, white short gold-trim dress, small flat waist badge, black waistband, tiny white-and-gold boots, blue gems, red cape and small white wings. Compact consistent silhouette and scale. NO dramatic stretch or squash. Four complete characters with staff tips uncut, substantial transparent gutters between cells and margin on all sides. No trails, dust, glows, ground, typography, watermarks, reference-sheet labels. True transparent background.
```
