# 에르핀 고학년 돌진·충돌 새 버전

사용자가 제공한 캐릭터 기준 시트와 원화의 디자인을 유지하고, `18a92233e5180b16.gif`의 달리기와 충돌 후 넘어짐을 참고했다. 내장 `image_gen`으로 4×2 투명 스프라이트 시트를 생성했다. 기존 런타임 에셋과 코드는 교체하지 않았다.

결과: `output/imagegen/erpin-dash-v2/erpin-high-grade-sheet.png`.

개별 프레임: `erpin-high-grade-00.png`~`erpin-high-grade-07.png` (512×512 RGBA). 미리보기: `erpin-high-grade-preview.gif`. 타이밍과 순서는 같은 폴더의 `frames.json`에 저장했다. 원본 시트는 1774×887 RGBA이며 알파 투명 영역을 확인했다. 그림은 생성 도구로 만들고, Pillow는 시트 분리·캔버스 등록·GIF 조립에만 사용했다. 인접한 돌진 두 포즈의 외곽선이 붙은 부분은 알파가 가장 낮은 경계로 분리했다.

분리 후 접촉 시트로 8개 캐릭터와 지팡이가 잘리지 않는지 확인했다. 생성 결과에는 충돌의 놀람 표시와 회복의 물음표가 포함되어 있다. 게임 런타임 적용과 Unity 검증은 수행하지 않았다.

읽는 순서: 준비 → 돌진 보폭 A → 중간 보폭 → 보폭 B → 충돌 반동 → 뒤로 넘어짐 → 바닥에 누움 → 앉아서 회복. 돌진 1~3번 프레임은 반복 구간이다. GIF는 타이밍과 포즈 검토용이며 게임 적용 검증을 의미하지 않는다.

## 최종 생성 프롬프트

```text
Use case: stylized-concept
Asset type: 2D Unity character animation sprite sheet, eight frames.
Create Erpin's high-grade charging dash and collision motion using the provided references. Reference 1 (character turnaround sheet) is authoritative design. Reference 2 (single character illustration) is authoritative rendering style, proportions, face and staff details. Reference 3 (GIF contact sheet) is MOTION reference only, read the successive actions, ignore grey background and small frame numbers. Preserve big round head, extremely short torso and stubby legs, blonde hair, gold crown with cyan diamonds, pink eyes with star highlights, white short gold trimmed dress, dark belt, gold blue waist badge, white gold boots, red cape with white pompoms, small white wings. Smooth softly painted cel highlights, bold dark brown contours, close to reference 2.
Output exactly EIGHT distinct complete full body sprites in an evenly spaced 4 columns x 2 rows grid on genuine transparent background. Equal cell size, generous gutters, consistent head scale. Each sprite and entire staff fits inside its own cell. No text, labels, grid lines, shadows, scenery, effects.
Reading order left to right top then bottom:
1. preparation facing LEFT in side three-quarter, knees bent, leaning slightly forward, holding upright staff ahead with both hands, alert open-mouth determined cute expression;
2. charging LEFT, leading foot left, trailing foot right, torso leaning, staff almost upright just ahead of face, hair and cape stream RIGHT;
3. charging passing stride, short legs bend under hips, slight bounce, identical staff grip and face;
4. charging opposite stride, other leg now forward, hair and cape stream RIGHT. Frames 2,3,4 must be a coherent run cycle.
5. collision recoil: abrupt stop moving left, upper body jerks RIGHT, staff tilts slightly left but stays held, wide surprised eyes and mouth, knees buckle;
6. backward tumble onto back toward RIGHT, boots lift toward LEFT, eyes squeezed shut, arms keep staff, articulated pose not simply rotated standing image;
7. lying on side/back, head toward RIGHT, boots LEFT with knees tucked close to very short body, eyes closed, hair and red cape spread under her, intact staff held diagonally beside body;
8. recovering from fall: low seated/kneeling pose, pushes torso up with staff, sheepish dazed face, still facing LEFT.
Staff is same rigid single prop in every frame: round gold head containing dark navy centre and cyan faceted diamonds, segmented pale blue outer ornaments and cyan top cap, two separate white gold edged hanging banners below head, one long thin straight gold shaft terminating in pointed gold foot. Never bend shaft, distort ring, add staff, detach hand, merge crown with ring, or lengthen body. Natural occlusion is allowed; keep mechanically plausible hand grips, both sleeved arms, and two short legs. Keep all costume details and consistent palette across frames. No enemies or impact objects. Use actual transparent alpha background, no checkerboard painted into image.
```
