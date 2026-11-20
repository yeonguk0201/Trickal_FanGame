# 새마음금고 황금 기둥 원화 v1

작성일: 2026-11-20.

## 목적과 구성

- 새마음금고가 땅에서 소환하는 황금벽·기둥의 대표 형태 후보.
- 네모난 금 기둥과 밑동을 감싸며 뾰족하게 들린 흙·바위로 지면 돌출을 표현한다.
- 지면 기준점은 기둥 밑동 중심. 정면에서 윗면과 오른쪽 옆면이 조금 보이는 시점이다.
- 최종 원화: `gold-pillar-concept.png`. 내장 image_gen으로 생성한 투명 배경 PNG.
- 이번 파일은 기둥과 지면을 함께 그린 형태 검수용 단일 이미지다. 움직임 제작 시 기둥 몸체와 지면 파편을 독립 레이어로 분리해 크기·타이밍을 조절한다.

## 검수와 적용 상태

- 사각 기둥 실루엣, 금색 면 구분, 위로 들린 흙·돌 형태를 생성 결과에서 확인했다.
- 사용자 원화 수용(2026-11-20). 피격·파괴 시트도 사용자 수용 후 연결 요청. 실제 Play에서의 크기·움직임은 별도 검수 대기.
- 런타임 적용 완료: 기존 금바위 표시를 전용 황금 기둥으로 교체했다. 상세 규격·검증은 [적용 기록](./runtime-integration.md)을 따른다.
- 최초 원화 제작 단계에서는 배치를 생략했고, 런타임 연결 후 전용 Unity 검증이 통과했다.

## 생성 프롬프트

```text
Use case: stylized-concept
Asset type: single transparent 2D game sprite concept for a summoned golden pillar obstacle in a bright Korean SD fantasy action game.
Primary request: ONE chunky rectangular GOLD PILLAR thrust upward through the ground, with sharp upturned earth and rock shards gathered tightly around its base. This is a square-section solid gold rectangular prism, flat square top, straight vertical sides, broad front face and narrow right side visible, slightly beveled edges. Height about 1.7 times its width, sturdy wall-like mass rather than slender column. Small top face visible from the game's slightly elevated front-facing camera; vertical edges remain vertical, no dramatic isometric diamond view.
Style: polished hand-drawn 2D cartoon game prop, bold clean dark warm-brown outlines, simple crisp cel shading, readable large color shapes, warm yellow gold front, amber gold side, pale gold bevel highlights. Small restrained specular accents on metal, not dazzling glow. Cute chunky proportions suitable for small SD characters.
Ground eruption: directly around the lowest fifth of the pillar, jagged pointed brown soil slabs and angular gray-brown stones tilt sharply UP and OUT as if pushed out by the pillar. Distinct pointed silhouettes, broken fresh earth, small compact rubble at the feet. Keep ground footprint flattened in perspective, base at bottom center, no long downward spikes or roots, no large floating debris. Gold pillar is clearly the dominant shape, the lifted earth is secondary and does not cover most of the gold front. Pillar and earth shown together as one representative maximum-rise pose, not animation frames.
Composition: single centered isolated object, generous transparent margin on every side, complete top and base uncropped. Genuine transparent alpha background, no backdrop, no floor plane, no baked oval cast shadow. No characters, letters, text, logos, currency symbols, ornate motifs, crowns, decorative pedestal, bricks, masonry joints, multiple pillars, panels, sprite sheet, labels, or watermark. Do not turn the gold pillar into a crystal or a rock spire.
```
