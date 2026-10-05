# 크레용사용 소환 졸개 공격 에셋

2026-10-05 기존 궁병·마법사·도끼병·방패병 원본을 참조해 투명 2×2 포즈 시트를 생성했다.
Runtime에서 본체를 늘리거나 눌리지 않고 `EnemyAttackArtwork`가 기존 전투 단계에 맞는 Sprite를 선택한다.
Idle은 기존 원본, Telegraph/Active/Recovery는 각각 1/2/3번 포즈다. 0번 생성 포즈는 크기 등록 기준이다.

## 공통 생성 지시

Create exactly FOUR hand-drawn ATTACK animation poses of the exact referenced crayon minion in a 2x2 transparent sprite sheet. Preserve original identity, colors, faces, costume and painterly bold dark outlines. Ignore any accidental background RGB artifacts in the reference: output character only with true transparent alpha. All poses keep SAME orientation and weapon hand, attacks face screen LEFT; do not mirror torso or swap hands. Same body scale and body anchor and shoe baseline per cell; move articulated limbs/weapon, not rotate or squash the whole image. Generous transparent gap between rows AND columns, full weapons/limbs inside each cell, generous canvas padding. No text, labels, grids, ground, shadows, detached weapons or large effects. Four distinct poses with clear anticipation, strike and follow-through.

## 종별 동작

- 궁병: Top left idle reference pose with bow and arrow. Top right preparation: crayon body visibly bends into a gentle C curve, both feet planted, hold bow toward screen LEFT and pull bowstring and arrow backward toward screen RIGHT with other hand; arrow visibly nocked and string taut. Bottom left release: let go of string, bow relaxes, pulling hand follows back, NO arrow in bow, body springs straighter. Bottom right recovery: hands reset, torso almost upright.
- 마법사: Top left idle reference pose with curled wooden staff on screen LEFT. Top right preparation: raise the staff over the head, free hand drawn inward, feet planted. Bottom left casting: extend raised staff toward screen LEFT, free hand open, tiny turquoise light ONLY at the curled tip, torso slightly leans forward. Bottom right recovery: lower the staff back down, free hand relaxes. Preserve all two drawn face motifs and feather crown.
- 도끼병: Top left idle reference pose, axe in screen LEFT hand. Top right preparation: lift axe high above head and slightly back toward screen RIGHT, both feet planted, free hand balances. Bottom left attack: swing axe downward and forward toward screen LEFT, bent elbow and clear follow-through, torso leans forward, axe blade fully visible. Bottom right recovery: axe held low after swing before returning upright. Preserve red cap, red eyes, bands and hanging turquoise tassels.
- 방패병: Top left idle with both hands grasping the sides of the small center-front grey shield. Top right preparation: BOTH hands raise that same small shield up toward lower face. Bottom left attack: BOTH hands move shield down to waist level, torso leans slightly forward, feet planted. Bottom right recovery: BOTH hands bring shield up again to chest. Clearly distinct shield heights, preserve identical small shield size, green crayon cap and orange zigzag body pattern, no single-handed shield, no giant shield.

## 투사체 생성 지시

- 화살: One isolated arrow projectile sprite matching the arrow carried by the purple crayon archer reference. ONLY the arrow: grey triangular arrowhead points screen LEFT, slender brown wooden shaft horizontal, small purple feather fletching at screen RIGHT. Original bold dark cartoon outline and painterly shading, compact readable mobile game projectile. No bow or character, no text, no glow, no trails, no shadows. Full object centered, generous transparent padding. True transparent background.
- 마법탄: One isolated small magical bolt projectile cast by the blue crayon mage reference. ONLY the projectile: compact turquoise/cyan glowing teardrop orb with a white center, rounded leading tip toward screen LEFT, short tapering tail toward screen RIGHT. Painterly cartoon mobile game style, readable solid silhouette, modest tight cyan rim, no large diffuse glow. No character, staff, feathers, text or background. Full object centered with generous transparent padding. True transparent background.

## 적용

`Assets/Art/Bosses/FairyKingdom/Movement/MinionAttacks/`에 16개 1024×1024 포즈와 화살·마법탄 512×512 에셋을 저장한다. 모두 400 PPU다.
알파 구분선으로 분리하고 종별 동일 배율을 적용했다. 발 기준점으로 무기 길이 변화와 무관하게 등록한다.
원본 1000 PPU의 화면 크기에 맞춘 그림이며 루트 Transform·Collider·AI·공격 시간·피해·소환 규칙을 변경하지 않는다.
원거리 투사체는 기존 `EnemyProjectile` 발사 경로에서 각 졸개의 전용 그림을 사용한다. 판정은 기존 원형 반경 0.15 world units를 유지한다.

생성 시트 ID: 궁병 `8039b4e4-8c89-41f1-93c4-241d3e5b860c`, 마법사 `595ebbb9-0a26-459b-9883-62cf68abdd6f`, 도끼병 `2ed04868-ae57-48c9-a533-027e4c7d89c6`, 방패병 `ec1ddd49-8c0b-424b-8f04-c824dfa1f2cf`.
투사체 ID: 화살 `5565fdb3-cfdc-4d43-b9b5-32c67928bee4`, 마법탄 `5bc97513-3780-4cf9-9321-601be9bd12c7`.

## 궁병 동작 정정: 몸체 자체가 활 (2026-10-05)

사용자가 제공한 `20261004_164439000_iOS.MP4`의 약 11.75~12.17초를 참고했다.
위 최초 궁병 프롬프트의 손으로 별도 활을 잡고 당기는 해석은 폐기한다.
궁병 몸체가 왼쪽으로 볼록한 C자 활처럼 휘며, 몸체 상하단에 연결된 뒤쪽 시위가
화살 꼬리와 함께 오른쪽으로 당겨진다. 발사 순간 몸이 펴지고 시위가 복원된다.
팔은 보조적인 작은 움직임만 있고 장전·당김에 사용하지 않는다. 원본 보라색·복장·얼굴을 유지한다.
실제 공격 단계·화살 투사체·판정은 유지하고 궁병의 네 포즈만 같은 파일/GUID로 교체한다.

수정 생성 지시: FIRST reference defines the exact purple crayon archer identity, costume, face,
pointed purple cap, patterned bands, skirt and tiny shoes. SECOND reference is VIDEO motion evidence:
the CRAYON BODY ITSELF IS THE BOW, not a person holding a separate bow. NO separate handheld curved
wooden bow in ANY pose. Keep purple colors from the first reference. The string runs behind the crayon
on screen RIGHT, attached from upper body/cap to lower body/skirt. Arrow passes horizontally across its
middle, arrowhead points LEFT; tail meets midpoint of rear string. Tiny arms do NOT pull the arrow/string.
Idle is almost straight with arrow ready. Anticipation bends the whole body into a deep left-bulging C-shaped
bow arc, curved face and costume follow it, cap and lower hem approach one another, rear string forms a
triangle pulled toward RIGHT with arrow tail at apex. Firing straightens the body with a slight opposite
spring, string returns nearly straight and no arrow remains. Recovery settles upright with relaxed string.
Same floor baseline and physical size, no travel or jumping. Preserve original painterly dark outline style.
Four full poses in a 2x2 transparent sheet, generous gaps, no text, effects, separate bow, grid or background.
수정 시트 ID: `27ec506f-f19e-4be2-9a6d-c9e9f8eb01a4`. 내장 imagegen 사용.

Unity 재적용·전체 공격/이동·Boss-0~3·HUD-5 회귀 통과: `game/Logs/crayon-archer-body-bow.log`, 종료 코드 0. 궁병 전용 렌더 미리보기 `game/Logs/CrayonMinionAttackPreview/archer-body-bow.gif`.

## 사용자 수정 시트 적용 (2026-10-05)

사용자 제공 `20261004_172657558_iOS.png`를 재생성 없이 사용했다.
원본은 `Assets/Art/Bosses/FairyKingdom/Movement/MinionAttacks/Source/CrayonArcher_Attack_Sheet.png`에
그대로 보관한다. 알파 구분선으로 4개 포즈를 분리하고 기존 배율·발 기준점으로 등록했다.
`CrayonArcher_Attack_0..3.png`와 기존 `.meta` GUID를 유지하므로 프리팹·전투 설정은 재구성하지 않는다.
재생성 시트보다 이 사용자 수정본을 최종 기준으로 사용한다.
사용자 수정본 적용 후 `VerifyAndRenderMinionPreview` 통과: `game/Logs/crayon-archer-user-artwork.log`, 종료 코드 0. 공격 단계·전용 화살 실제 발사·방향·속도·히트박스 보존과 Unity 렌더 확인.
