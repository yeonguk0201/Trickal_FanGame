# 에르핀 한 손 돌진과 충돌 후 넘어짐

사용자 제작 `docs/art-references/erpin-user-reference.png`를 외형 기준으로, `C:/Users/yeonguk/Downloads/18a92233e5180b16.gif`를 동작 기준으로 사용했다. GIF는 185프레임이며 샘플 프레임을 추출하여 한 손 파지와 뒤로 넘어지는 방향을 확인했다.

내장 `image_gen`으로 두 개의 4프레임 시트를 생성했다.

- 돌진 생성 ID: `abb0b72b-6109-4f70-888b-569de08d09d9`.
- 충돌 생성 ID: `fe41cf6c-8919-4c55-b71b-44254bf9e8b0`.
- 적용 위치: `game/Assets/Resources/Characters/Erpin_HighGrade/Erpin_HighGrade_0.png`부터 `_7.png`까지.
- 구성: 준비·달리기 3프레임, 뒤로 젖혀짐·넘어짐·누움·일어나기 4프레임.
- 프리뷰: `game/Logs/ErpinHighGradePreview/erpin-one-hand-complete.gif`.

한 손으로 지팡이를 들고 다른 손은 허리 근처에서 움직인다. 지팡이 직선 축과 두 갈래 깃발을 유지하되 자연스러운 가림을 허용했다. 투명 연결 요소 분리·리사이즈·프리뷰 생성만 Pillow로 처리했다. 각 시트 내부의 동일 배율과 기존 에르핀 크기·발 기준선을 유지했다. `.meta` GUID, 400 PPU, 기존 스킬 시간 및 콜라이더를 유지한다.

## 생성 프롬프트

Unity `ErpinHighGradeArtworkSetup.SetupAndVerifyBatch` 통과: 프레임 재생, 방향 전환, 일시정지, 충돌 후 넘어짐과 회복, 걷기/저학년 우선순위, 고학년·저학년 스킬 회귀, 루트와 히트박스 유지. 로그 `game/Logs/erpin-one-hand-high-grade.log`, 종료 코드 0.

### 돌진

```text
Use attached user-created character reference as the sole character and staff design authority. Match its painted chibi style, thick dark brown outlines, oversized blonde round head and very short torso and legs, gold crown, star-highlight pink eyes, white gold-trim dress, small flat waist badge, red cape with white pompom hem, white/gold short boots, blue gems and little white wings. Second image is MOTION reference only, not costume or rendering style. Staff has gold ring, dark navy crystal face, cyan diamond, gold top cap, a straight gold shaft aligned with head and cap, two white gold-edged fabric ribbons. Natural perspective and occlusion is welcome; no need to expose every ornament. Keep rigid staff proportions consistent; no bent pole, redesigned staff or missing arm. Four full-body sprites in 2x2 grid reading left-to-right top-to-bottom, generous margins and transparent gutters, same scale in every cell, no labels, shadows, ground, dust, speed trails, watermark. True transparent background. DASH SHEET: all face LEFT, staff held in ONE HAND only, raised upright/diagonally up-left beside head in the leading hand as in GIF. Other hand is a free fist on hip or swinging near waist, never grips staff. Pose 0 ready: leaning into run, lifted staff, knees bent. Pose 1 running A one foot forward other pushes behind. Pose 2 running B passing feet with one knee raised and free arm counter-swing. Pose 3 running C opposite foot forward. Upright body with a modest forward lean; NOT two-handed thrust, not lunging with staff horizontally. Hair and cape flow right, confident determined pout/smile. Three running frames form loop with visibly alternating legs and modest vertical bounce. Staff carrying arm stays raised, relaxed firmly gripping gold pole, white ribbons naturally trail and can hide behind hair. Both arms anatomically present.
```

### 충돌 후 넘어짐

```text
Use attached user-created character reference as the sole character and staff design authority. Match its painted chibi style, thick dark brown outlines, oversized blonde round head and very short torso and legs, gold crown, star-highlight pink eyes, white gold-trim dress, small flat waist badge, red cape with white pompom hem, white/gold short boots, blue gems and little white wings. Second image is MOTION reference only, not costume or rendering style. Staff has gold ring, dark navy crystal face, cyan diamond, gold top cap, a straight gold shaft aligned with head and cap, two white gold-edged fabric ribbons. Natural perspective and occlusion is welcome; no need to expose every ornament. Keep rigid staff proportions consistent; no bent pole, redesigned staff or missing arm. Four full-body sprites in 2x2 grid reading left-to-right top-to-bottom, generous margins and transparent gutters, same scale in every cell, no labels, shadows, ground, dust, speed trails, watermark. True transparent background. COLLISION FALL SHEET: follows a one-handed staff run toward LEFT, then comically thrown backward toward RIGHT on collision, holding staff in SAME ONE hand throughout. Four sequential poses: 0 recoil backward, round head tilts right, startled wide eyes/open mouth, knees buckle, free arm opens for balance; 1 falling backward halfway to ground, torso and head rotate together, head at right and boots left/up, eyes squeezed, staff remains held and rotates rigidly with arm; 2 lying on back/side, head on right and boots to left slightly raised, dazed squeezed eyes, hair and cape spread on ground, staff angled along side partly occluded naturally; 3 pushing back up into crouch facing left with free hand supporting ground, holding staff in other hand, normal compact proportions. Keep EXACT head size, torso length, boot and pole length between frames, rotate entire body rather than stretching, no elongated body on ground. Funny gentle chibi tumble, no pain/injury. Staff may be partly hidden by body/hair but never broken; keep natural layering. IMPORTANT all poses use same drawing scale: do NOT enlarge fallen body to fill cell, horizontal poses simply have lower height and wider silhouette.
```
