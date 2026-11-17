# 새마음금고 젤리 발사 v1

보스 본체의 꿀빛을 참고한 대표 원화. `puddle.png`는 큰 웅덩이와 작은 웅덩이에 공용으로 쓸 후보이며, `projectile.png`는 웅덩이를 남기는 포물선 젤리탄의 후보이다. 두 그림 모두 중심 기준의 단일 프레임이다.

내장 ImageGen으로 투명 PNG를 생성했다. 실제 게임 크기 합성, 움직임, Unity 연결 및 Play 검수는 대표 형태 검토 이후 진행할 단계다.

## 생성 프롬프트

### 웅덩이

Use case: illustration-story. A single isolated 2D game VFX sprite for a Korean chibi fantasy dungeon game. Reference image is the boss, for its honey-gold jelly body palette and crisp cartoon shading only. Render genuinely transparent RGBA, centered with even clear padding. Warm honey-yellow top light, amber bottom shading, clean illustrated edges, pale cream specular shapes. Readable at 40 pixels. No scenery, characters, text, treasure chest, coin, gemstone, detached particles, cast shadow, checkerboard or halos. Ground hazard, a settled sticky golden jelly puddle. Nearly top down, close to circular footprint (width to height 1.08:1) suitable for a circular slow area. Thin flat irregular softly scalloped perimeter, shallow glossy translucent-looking syrup with a few broader reflections hugging the edge. Lower contrast thinner calm center so units stand out over it. Keep it visually very flat, as a spill on the floor, no raised mound or splash crown.

### 투사체

Use case: illustration-story. A single isolated 2D game VFX sprite for a Korean chibi fantasy dungeon game. Reference image is the boss, for its honey-gold jelly body palette and crisp cartoon shading only. Render genuinely transparent RGBA, centered with even clear padding. Warm honey-yellow top light, amber bottom shading, clean illustrated edges, pale cream specular shapes. Readable at 40 pixels. No scenery, characters, text, treasure chest, coin, gemstone, detached particles, cast shadow, checkerboard or halos. Airborne lobbed projectile, one plump round asymmetrical wobbling honey-gold jelly blob with a mild teardrop pinch, almost circular silhouette and visually balanced to be readable without rotation in any travel direction. Squishy translucent-looking golden gelatin with a bright cream curved highlight and a darker amber soft rim, smooth broad planes and a couple tiny inner bubbles. Clearly reads as a jelly glob, no motion lines or tail.
## 투사체 v2

v2 투사체 후보는 `projectile-v2.png`이다. 새마음금고 본체의 보물에서 금화와 붉은 보석을 따와 하나의 실루엣으로 구성했다. 내장 ImageGen으로 만든 투명 단일 프레임이며 Unity 연결 전 원화다.

### 생성 프롬프트

Use case: illustration-story. Reference image shows Saemaeum Vault boss with coins and colorful gems. Draw exactly one isolated 2D dungeon game projectile sprite: one thick shiny GOLD COIN with a large faceted RUBY set into the middle, clearly visible face-on with a slight three-quarter view showing its gold rim thickness. The coin and ruby together form a single compact loot-like silhouette. Gold coin is the main recognizable shape with concentric raised metallic rims, simple thick dark warm brown contour and shiny yellow edge highlights. Saturated ruby-red gemstone has bold angular facets and a tiny pale highlight. Match the illustrated chibi line and colors of the gold coins and gems on the reference sprite. Simple and readable at 32 pixels. Perfectly centered with a modest margin on every side, genuine transparent RGBA. No slime coating, chest, character, additional floating pieces, motion trails, halo, letters, cast shadow, ground or scene.

## 투사체 v3

부채꼴 연사에 적용한 원화는 `projectile-v3.png`이다. 새마음금고 본체의 붉은 보석을 참고해 루비 한 알을 내장 ImageGen으로 생성했다. 투명 단일 프레임 원화다.

### 생성 프롬프트

Create one isolated 2D chibi dungeon game projectile sprite: a single loose ruby-red faceted gemstone, matching the red loose gemstones in the supplied boss artwork reference. Compact chunky rounded-polygon silhouette with a slightly pointed lower corner. Three-quarter view revealing broad angular vivid red, crimson, and light pink facets. Dark warm brown outline of moderate thickness and one small pale highlight. Playful crisp illustrated shading, readable at 24 pixels. Center the single gem, filling roughly 80 percent of a square canvas. True transparent background. No coin, metal rim, mount, gold, jelly, glow, stars, particles, shadow, motion lines, lettering, or scenery.

### Unity 적용

- 젤리 발사의 포물선 투사체는 첨부 이미지와 동일한 `projectile.png` 원화인 `Assets/Resources/CombatEffects/saemaeum-jelly-projectile.png`를 사용한다. 990 PPU에 기존 젤리 발사 크기 배율을 적용한다.
- 접근 투척의 부채꼴 연사는 루비 원화인 `Assets/Resources/CombatEffects/saemaeum-ruby-projectile.png`를 사용한다. 850 PPU에 공용 보스 투사체 원화 월드 배율 0.375를 적용한다.
- 두 에셋은 단일 Sprite, 중앙 피벗, Bilinear, 무압축, 최대 2048로 임포트한다.

`Trickal Fan Game > Week 15 > Verify Boss-2 Saemaeum Vault`에서 두 패턴의 스프라이트를 검사한다. 화면은 `Trickal Fan Game > Debug > Open Boss-2 Test Room`에서 Play로 확인한다. 노란 젤리는 포물선으로 날아가 착지점에 웅덩이를 남기고, 붉은 보석은 부채꼴로 직진해야 한다.
