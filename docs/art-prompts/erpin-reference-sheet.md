# 에르핀 기준 레퍼런스 시트

결과: `docs/art-references/erpin-user-reference.png`.
내장 `image_gen` 사용. 최종 생성 ID: `b845329f-14d5-4750-88d5-c0a72581182f`.
디자인 검토용이며 게임 모션에는 적용하지 않았다.

## 원본과 교정 기준

- `game/Assets/Art/Characters/Player/Character_Erpin.png`: 얼굴과 의상 디자인.
- `C:/Users/yeonguk/Desktop/Erphin_skill.png`: 첫 번째 프레임을 지팡이 구조 기준으로 사용.
- `C:/Users/yeonguk/Desktop/Erphin_walking.png`: 앞·옆·뒤 실루엣과 짧은 몸 비율.

초기 시트의 옆으로 꺾인 연결부, 하나뿐인 깃발, 두껍고 입체적인 허리 장식은 잘못된 해석이었다. 초기 프롬프트는 후속 제작 기준으로 사용하지 않는다.

상단 보석 탭·중앙 크리스탈·금색 봉은 같은 직선 축이다. 지팡이를 세우면 헤드 전체를 회전한다. 흰색 바탕과 금색 테두리의 깃발은 두 갈래이며 봉과 별개다. 상단 탭은 사각형 계열 금색 프레임이다. 허리 장식은 작고 납작한 의상 장식이다.

최종 시트를 시각적으로 확인했다. 지팡이 확대도에 일직선 축과 두 갈래 깃발을 표현했고 허리 장식의 두꺼운 받침대를 제거했다. 원본에 없는 반대쪽 측면은 추정한 디자인이다. 원본과 차이가 있으면 원본이 우선한다. 아직 사용자 디자인 승인 전의 수정안이다.

## 교정 프롬프트

```text
Use case: precise-object-edit
Correct the attached Erpin reference sheet (image 3) using the ORIGINAL low-grade firing FIRST frame (image 1) and original portrait (image 2) as the authority. Keep the sheet layout, chibi character proportions, face, costume colors and four-view organization. The previous sheet misinterpreted the staff and waist ornament: fix those throughout ALL views AND detail panels.

STAFF ALIGNMENT IS THE HIGHEST PRIORITY: in image 1, draw one straight axis from the TOP-LEFT GEM TAB through the CENTER OF THE MAIN CYAN CRYSTAL and down the GOLD SHAFT to the pointed bottom tip. Those three parts are COLLINEAR, one uninterrupted straight rigid staff. In a diagonal staff pose this axis runs from upper-left to lower-right; in an upright staff pose rotate the complete head so the tab and crystal line up with the upright shaft. The round head is centered on the shaft axis. Absolutely remove the previous offset elbow, dogleg, angled branch, or side connector. No offset head attached to the side of a pole. The shaft should exit the head along the very same line as tab and crystal. Make this alignment visibly unambiguous in FULL STAFF and STAFF HEAD detail drawings; a fine dashed straight construction line connecting tab, crystal and shaft is welcome.

HANGING RIBBONS / FLAGS: NOT A SINGLE pendant. Copy the two distinct white fabric pieces edged in gold visible in image 1. One descends below the ring on the left of the gold shaft, widening to an angular gold-edged pointed tip. A SECOND distinct gold-edged white ribbon extends from beside the lower-right head and sweeps outward/back, as visibly shown behind the character's bangs in image 1. Show BOTH in the isolated full staff, head detail and relevant character views. These are cloth ribbons attached near the head; they are not the pole. Preserve their actual reference shapes, no invented blue gem on the ribbons. Main pole stays straight even where ribbons curve. Head retains round gold frame, dark navy face, cyan faceted crystal and smaller shards, silver-blue external curved segments, top gem tab. Use label TWO RIBBONS instead of HANGING ORNAMENT (WHITE). No offset rigid elbow anywhere.

WAIST ORNAMENT: copy its small, flat, thin gold framed clothing applique from the originals. It sits flush with the short skirt/waistband, with a central blue diamond and two small flat blue side stones. Not three upright freestanding crystals on a thick platform. Remove chunky 3D tray, crystal cluster, raised pyramids or trophy appearance. In all four characters and the accessory detail make it modest and flat. The enlarged detail can show its shape but must remain a thin flat badge.

Keep the rest of the character and layout unchanged. Clean warm-white background. Original inputs' black shapes and RGB vertical stripes are corrupt transparent-pixel background, not part of art. Do not include them. No animation frames. All staff drawings must agree with corrected collinear construction.
```

```text
Edit image 1 reference sheet. Images 2 and 3 are original design authorities. Two remaining fixes must be made visibly, not just labeled.
1. Every upright staff MUST have the gold gem tab centered at TWELVE O'CLOCK directly above the crystal, and shaft directly below at SIX O'CLOCK. Rotate the whole head with its silver segments so the original upper-left tab now sits at the TOP CENTER when pole is vertical. Central diamond long axis must also be vertical. The tab's blue gem center, large crystal center and shaft centerline must share EXACT same x coordinate. In enlarged STAFF HEAD, move/rotate the gem tab from upper-left to TOP CENTER. Red dashed guide must pass through center of the BLUE GEM in tab, center of large crystal and shaft. This also applies to ALL FOUR character views, rotating as needed to match slight pole tilt. In diagonal FULL STAFF, keep tab, crystal and pole exactly collinear on the diagonal, and rotate the crystal accordingly. Rename label TOP-LEFT GEM TAB (ON AXIS) to GEM TAB (ON AXIS) to prevent confusion: top-left is its position in original diagonal art, not in a vertical prop. Keep both white/gold fabric ribbons from previous correction. No bent or offset connectors.
2. WAIST ORNAMENT: it is NOT upright crystals in a tray. Replace enlarged waist detail with a SMALL FLAT 2D embroidered clothing badge matching original image 3 waist. Three blue inlaid FLAT diamond shapes, thin gold outlined white cloth trapezoid/chevron backing. No thick platform, no standing gems, no drop shadow, no extruded edge, no trophy or dish, no decorative legs. Make the detail drawing about HALF its current width, modest. Replace the waist badge on FRONT, LEFT, RIGHT character figures too: smaller, flush against skirt, with thin gold edging and FLAT blue inlay. Original low-grade firing image 2 shows how understated and flat it is.
Preserve all other sheet content, layout, compact chibi character identity and colors. Ignore corrupt background in originals.
```

```text
Edit image 1 reference sheet with only two final prop corrections. Image 2 is original authority.
A. LOWER LEFT FULL STAFF: replace the diagonal staff drawing entirely with a VERTICAL straight full staff, complete from gem tab to bottom tip, same as the corrected central STAFF HEAD drawing but at smaller scale and showing a longer pole. Tab, large crystal and straight gold pole must all share one perfectly vertical centerline. Absolutely no diagonal staff in this panel. Keep TWO white gold-edged ribbons.
B. EVERY GEM TAB ON EVERY STAFF: the original gem tab is NOT a pointed diamond shaped badge. Restore its original gold-framed WIDE QUADRILATERAL / short trapezoidal rectangular cap with a cyan triangular shard inlay, as shown above-left of ring in image 2. When staff is vertical rotate that original cap so it sits horizontally ABOVE the circular head, centered at twelve o'clock. Do not change it into a diamond pointed ornament. All four character staffs and both staff detail drawings must use this same WIDE GOLD TAB WITH CYAN TRIANGULAR INLAY. Keep tab inlay center vertically collinear with main crystal and shaft. Keep main large crystal and straight shaft as in current central corrected drawing.
Preserve all other current sheet features, flat small waist badge, layout, labels, colors, short chibi proportions. This is a faithful original design model sheet, not a weapon redesign.
```
