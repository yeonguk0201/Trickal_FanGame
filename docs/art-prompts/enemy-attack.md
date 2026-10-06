# 일반 적 공격 포즈와 파 투사체 (2026-10-04)

기존 네 적의 Idle PNG를 참조해 built-in imagegen으로 투명 배경 그림을 생성했다.
원본은 보존하고 `game/Assets/Art/Enemies/FairyKingdom/Attacking/`에 별도 에셋을 등록했다.
각 캐릭터의 2×2 시트는 Idle·준비·공격·회복 순서다. 준비·공격·회복 세 포즈를 실제 전투 단계에
연결하고 평상시에는 기존 Idle/걷기 그림을 사용한다. 포즈 0은 크기·등록 기준용이다.

시트의 투명한 행·열 간격을 찾아 분리하며, 동일 배율·원본 접지선과 본체 기준점으로 1024×1024,
400 PPU 프레임을 등록했다. 그림 자체의 포즈가 변하며 루트 Transform이나 Collider를 변형하지 않는다.
꽃/프라이팬의 뻗는 길이가 바뀌어도 본체 중심이 따라 밀리지 않도록 무기와 본체 기준점을 분리했다.
파는 512×512에 길이 400픽셀로 등록했다. 2026-10-05 가독성 요청으로 파만 400→200 PPU로
변경해 표시 길이를 약 0.3→0.6월드 단위로 2배 키웠다. 투사체 Transform 배율 0.3과 원형
Collider 반지름 0.5(월드 반지름 0.15)는 유지한다. PNG 그림과 피해·속도·수명은 변경하지 않는다.

## 공통 프롬프트

Create a polished 2 by 2 animation sprite sheet of FOUR distinct attack poses using exactly the attached character's identity, costume, colors and crisp cartoon outlines. [Character instructions below.] All poses face screen LEFT. Same camera, scale, body anchor and ground baseline within each square cell. Large transparent padding between cells, no overlap, full weapons and limbs included. No text, labels, borders, shadows, scenery or particles. Genuine redrawn articulated poses, not rotating or stretching the entire reference. Match original Korean fantasy chibi mobile game art. Transparent background.

## 캐릭터별 지시

- 불효자손 (`Enemy_Bulhyojason_Idle.png`): enchanted wooden hoe with red-orange three pointed blade, wooden handle, leaves and dangling charm. Top left idle upright exactly reference; top right anticipates by leaning back slightly raising blade; bottom left falls forward dramatically onto its belly and slams its blade toward screen LEFT on ground, wooden shaft almost horizontal; bottom right partially pushes itself back upright.
- 산사모 (`Enemy_Sansamo_Idle.png`): yellow ginseng root ninja red headband purple belt green leafy hair with long flexible flower stalk and purple flower. Top left idle exactly reference; top right curls the flower stalk backward to screen RIGHT for windup, feet planted; bottom left lashes the flower forward toward screen LEFT like a flexible whip, purple flower at front near shoulder height, torso leans forward; bottom right flower stalk swings back upright, torso recovers.
- 저혈당 요정 (`Enemy_LowBloodSugarFairy_Idle.png`): brown hair red headband green dress fairy carrying frying pan. Top left idle exactly reference; top right crouches deeply, pan drawn back preparing a dash; bottom left forward lunging DASH toward screen LEFT, pan thrust at front, torso inclined forward, legs stretched running, hair and headband trail right; bottom right feet grounded, returns gradually from the lunge.
- 고혈당 요정 (`Enemy_HighBloodSugarFairy_Idle.png`): grey blue hair purple headband blue purple dress fairy holding a green onion. Top left idle holding onion exactly reference; top right raises green onion back toward screen RIGHT above shoulder for overhand throw; bottom left throws toward screen LEFT with empty extended hand and NO onion in hand or elsewhere in cell; bottom right empty hand follows through downward recovering posture, NO onion.

## 파 투사체 프롬프트

참조: `Enemy_HighBloodSugarFairy_Idle.png`.

Create one isolated GREEN ONION / scallion projectile sprite matching exactly the leek held by the fairy in the attached image. Only the vegetable, NO character. Crisp dark brown cartoon outline, white slender stalk and small roots, long forked rich green leaves. Horizontal orientation, white bulb/root tip points screen LEFT, leafy tips extend screen RIGHT. Compact and readable mobile game projectile, full object centered with transparent padding, no text, no effects, no shadows. Transparent background.

## 생성 결과

- 불효자손: `exec-019b1ef1-b853-4e1c-ab30-e3e01421cda3.png`
- 산사모: `exec-5df89898-45c3-4a11-abea-1f229ae335c7.png`
- 저혈당 요정: `exec-1189a99e-6817-44c1-97fd-084ef0979703.png`
- 고혈당 요정: `exec-9bdcbc77-db9d-4340-bf9c-6f1a4c6d432d.png`
- 파: `exec-c472ff6b-9c87-47d8-9cee-1c2fac9cce7f.png`

Setup/Verify/Export 메뉴는 `Trickal Fan Game > Artwork`의 `Enemy Attack Animations` 및
`Export Enemy Attack Preview`이며 Unity 렌더 결과는 `game/Logs/EnemyAttackPreview/`에 저장한다.

## 불효자손 갈퀴 가림 수정 (2026-10-04)

준비·회복 포즈의 세 번째 갈퀴가 손잡이/은색 연결부에 가려지는 부분을 imagegen으로 수정했다.
세 번째 갈퀴와 붉은 끝의 윤곽을 손잡이 앞에서 보이게 하고 기존 자세·접지선·등록 크기를 유지했다.
`Bulhyojason_Attack_1.png`·`Bulhyojason_Attack_3.png`를 교체하며 기존 `.meta` GUID는 보존했다.

편집 프롬프트: Surgical correction to this exact animation sprite, keep the SAME transparent square canvas,
character position, size, tilt, wood handle, branches, leaves, metal joint, dangling tag, colors, outlines and all
other artwork unchanged. The orange-red rake head must have THREE clearly visible long triangular teeth along
its lower edge. Currently the rightmost THIRD tooth is shortened / hidden by the wooden handle and silver socket.
Redraw ONLY that rightmost tooth and local overlap so the complete orange tooth with red tip is visible IN FRONT
OF the socket/wood handle, extending downward with its dark outline clearly separated from the handle.
All THREE teeth must remain easy to count, with two deep notches between them. Keep original left and middle teeth
unchanged. This is a flat three-prong rake head, not two prongs, not four. Preserve pose exactly, no rotation,
no new perspective, no changed scale or placement, no zoom or crop. Do not move the handle to hide any tooth.
True transparent background, no shadows or glow, no text. Target 1024 by 1024 square image.

- 준비 편집: `exec-28b9de3a-e673-4389-8856-12a57a35ecbe.png`
- 회복 편집: `exec-71f99d14-dafc-4698-b803-a42627fcd031.png`

### 사용자 수정본 적용

이후 사용자가 직접 수정한 1536×1024 RGBA 시트를 제공해 불효자손 4포즈를 해당 그림으로 교체했다.
현재 기준 원본은 `Assets/Art/Enemies/FairyKingdom/Attacking/Source/Bulhyojason_Attack_Sheet.png`다.
추가 생성/그림 편집 없이 투명 간격으로 분리하고 기존 1024×1024·400 PPU·접지선으로 등록했다.
위 imagegen 갈퀴 수정본은 이전 이력이며 현재 프레임에는 사용하지 않는다. 기존 프레임 `.meta`와
전투/프리팹 참조는 보존한다. Unity 렌더 로그: `game/Logs/bulhyo-user-artwork.log`.
