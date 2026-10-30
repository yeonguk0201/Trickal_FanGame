# 부스러기 분홍 크림 투사체 v1

- 목적: 1층 부스러기 보스 투사체의 대표 원화 검수.
- 형태: 오른쪽의 둥근 앞머리, 왼쪽으로 가늘어지는 S자 웨이브와 뾰족한 꼬리.
- 재질: 분홍 크림에 파스텔 노랑·민트·코랄 색의 작은 각진 팝핑캔디 조각.
- 제작: 내장 image_gen, 실제 투명 배경 PNG. 기존 CreamObstacle은 색감·외곽선·명암 참고용.
- 원화 파일: `game/Assets/Art/Bosses/FairyKingdom/Boss_Buseureogi_CreamProjectile_v1.png`
- 상태: 대표 원화는 사용자 호응 후 추가 프레임 제작 요청(2026-10-30). 추가 프레임의 상태는 아래에 기록.

## 추가 프레임 (2026-10-30)

- 대표 원화는 사용자 호응 후 추가 프레임 제작 요청. 내장 image_gen으로 원본을 편집해 3프레임을 개별 생성했으며 원본은 보존했다.
- 파일: 같은 폴더의 `Boss_Buseureogi_CreamProjectile_v1_Frame1.png`, `Boss_Buseureogi_CreamProjectile_v1_Frame2.png`, `Boss_Buseureogi_CreamProjectile_v1_Frame3.png`.
- 순서 후보: 원본 → Frame1(가로 늘림·꼬리 하강) → Frame2(세로 팽창·반대 웨이브) → Frame3(원형 복귀·꼬리 상승) → 원본.
- 3장 모두 1983×793 RGBA PNG, 모서리 알파 0 확인.
- 공통 임포트: 단일 Sprite, PPU 1000, 중앙 피벗, Bilinear, Clamp, Max Size 2048, 무압축, 알파 사용. 실제 게임 표시 크기와 피벗 보정은 연결 시 결정.
- 정지 이미지에서 둥근 오른쪽 앞머리·뾰족한 왼쪽 꼬리·캔디 8개를 확인. 연속 재생 시 기준점·캔디 위치의 흔들림과 실제 크기 가독성은 미확인.
- 상태: 사용자가 Frame1~3을 최종 투사체로 확정(2026-10-30). 원본은 루프에서 빼고 3프레임만 쓴다.

## Unity 연결 (2026-10-30)

- 루프: Frame1 → Frame2 → Frame3 → Frame1, 초당 10프레임(`BossProjectile.AuthoredFramesPerSecond`). 원본 `Boss_Buseureogi_CreamProjectile_v1.png`는 대표 원화로만 보존.
- 임포트: PPU 1000, 커스텀 피벗 (0.8, 0.5) = 크림 앞머리 중심. 판정점(투사체 위치)이 앞머리에 오고 꼬리가 뒤로 늘어진다.
- 표시: 자식 `Artwork` 렌더러를 비행 방향으로 회전, 월드 배율 0.75(`AuthoredArtworkWorldScale`), 틴트 없음. 판정 크기(`ProjectileSizing.BossScale`)는 그대로.
- 연결: `BuseureogiBossPatternRuntime.projectileFrames`. 구성은 `Trickal Fan Game/Effects/Setup Buseureogi Cream Projectile`(Boss-1 Setup도 같이 지정), 검증은 `Trickal Fan Game/Effects/Verify Buseureogi Cream Projectile`.
- 프레임이 없는 보스(새마음 금고 등)는 기존처럼 보스 스프라이트를 빨갛게 칠한 투사체를 쓴다.
- 손으로 만든 `.meta` 4개가 마지막 줄바꿈 없이 끝나 Unity가 PNG를 무시하던 문제를 GUID 유지한 채 수정.
- 검증 상태: 배치 실행은 열린 Unity 인스턴스 잠금으로 미실행. 수동 확인 필요 — 위 Setup 메뉴 실행 후 Verify 메뉴 통과, Game Scene에서 1층 보스 접근 사격 시 크림 머리가 플레이어 쪽을 향해 3프레임으로 꿈틀대는지 Play 미술 검수.

### 추가 프레임별 실제 프롬프트

#### Frame1

Edit the reference sprite to make ONE additional animation frame for the same pink whipped-cream projectile. Preserve identical cartoon art style, plum outline thickness, colors, eight embedded candy crystals with their same colors and relative positions, rounded leading head on RIGHT and sharp trailing tip on LEFT. Preserve the reference canvas dimensions, transparent background, overall placement, scale and head center. It must read as the exact same object deforming gently, not a new design. Keep the front blunt and round, the tail pointed and wavy. No detached bits, glow, shadow, text or additional objects. Frame 1 of 3 added in-between loop frames: gently stretch the cream horizontally and compress vertically by about 5 percent around the stationary head center. The left tail tip dips slightly downward; shift its S-wave subtly along the tail. The candies ride on the cream surface.

#### Frame2

Edit the reference sprite to make ONE additional animation frame for the same pink whipped-cream projectile. Preserve identical cartoon art style, plum outline thickness, colors, eight embedded candy crystals with their same colors and relative positions, rounded leading head on RIGHT and sharp trailing tip on LEFT. Preserve the reference canvas dimensions, transparent background, overall placement, scale and head center. It must read as the exact same object deforming gently, not a new design. Keep the front blunt and round, the tail pointed and wavy. No detached bits, glow, shadow, text or additional objects. Frame 2 of 3 added in-between loop frames: gently squash the cream horizontally and expand vertically by about 5 percent around the stationary head center. The left tail forms the opposite wave phase, with its pointed tip curling down and its middle curving upward. Keep the silhouette compact and recognizable. The candies ride on the cream surface.

#### Frame3

Edit the reference sprite to make ONE additional animation frame for the same pink whipped-cream projectile. Preserve identical cartoon art style, plum outline thickness, colors, eight embedded candy crystals with their same colors and relative positions, rounded leading head on RIGHT and sharp trailing tip on LEFT. Preserve the reference canvas dimensions, transparent background, overall placement, scale and head center. It must read as the exact same object deforming gently, not a new design. Keep the front blunt and round, the tail pointed and wavy. No detached bits, glow, shadow, text or additional objects. Frame 3 of 3 added in-between loop frames: return the cream body almost to the original proportions, the left tail tip curls slightly upward and the wave passes back toward its original phase, ready to loop smoothly to the reference. The candies ride on the cream surface.


## 생성 프롬프트

Use case: stylized-concept. Create one isolated 2D game projectile sprite for the first-floor Buseureogi crumb boss. Reference image is style/color guidance only: match its saturated strawberry-pink cream, dark plum bold outline, clean cartoon cel shading and creamy highlights. New silhouette: horizontally flying dollop of pink whipped cream, broad rounded bulbous leading head on the RIGHT, tapering behind to the LEFT into a gently wavy S-curved tail with one sharp pointed tip. Long compact teardrop, about 2.5 times as long as tall. Clearly round blunt front and pointed wavy rear, no pointed nose. A handful of small irregular angular popping-candy crystals embedded in cream, pastel yellow, mint, coral; these are crunchy faceted candy bits, not long sprinkles. Simple readable shapes suitable at small game scale. Side view, single complete projectile centered with generous transparent padding, no ground, no shadow, no aura, no detached particles, no speed lines, no face, no text, no watermark. Actual transparent background. This is a representative still sprite for art review, not a sheet.
