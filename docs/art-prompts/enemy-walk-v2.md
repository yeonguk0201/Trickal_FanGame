# 일반 적 걷기 프레임 생성 기록

2026-10-03. 기본 내장 `image_gen` 도구, `transparent_background: true`로 생성했다.
각 요청에는 해당 적의 `Enemy_{name}_Idle.png` 한 장만 원본 외형 참조로 전달했다.
한 장짜리 그림의 메시를 휘는 걷기 대신, 실제로 양발이 교대하는 접지·통과 포즈를 만들었다.

## 최종 에셋

`game/Assets/Art/Enemies/FairyKingdom/Walking/{name}_Walk_{0..3}.png`

- `Sansamo`: 산사모
- `LowBloodSugarFairy`: 저혈당 요정
- `HighBloodSugarFairy`: 고혈당 요정

최종 2×2 시트를 행 우선 순서로 분리했다. 각 주기에 동일한 배율을 적용하고, 원본 Sprite의
1000 PPU 외형 크기와 접지선을 기준으로 512×512, 400 PPU 캔버스에 배치했다.
프레임별 높이를 각각 맞춰 늘리거나 메시를 변형하지 않는다. 원본 Idle PNG는 보존한다.
원본 생성 시트는 로컬 검토용 `output/enemy-walk-v2/`에 보존한다.

## 최종 프롬프트

아래 공통 본문의 `{character description}`에는 다음 설명을 각각 넣었다.

### Sansamo

```text
This is a round ginseng warrior with two short pointed root-feet, a red headband, purple belt, leaves and flower. Keep its confident face and body. Articulate the TWO root-feet as SHORT LEGS, planted and raised alternately.
```

### LowBloodSugarFairy

```text
This is a short-legged chibi fairy with eyes hidden under bangs, brown hair, green dress, red ribbon, frying pan, and a small white wing. Keep exact head, face, palette, costume, weapon and original painterly thick-outline style.
```

### HighBloodSugarFairy

```text
This is a short-legged chibi fairy with eyes hidden under bangs, purple-gray hair, lavender dress, violet ribbon, green leek, and a small white wing. Keep exact head, face, palette, costume, weapon and original painterly thick-outline style.
```

### 공통 본문

```text
Use case: identity-preserve. Create a production four-frame WALK animation atlas of EXACT character in reference image. {character description}
IMPORTANT: This is walking toward the viewer in place. Two DIFFERENT feet must visibly alternate, not a repeated animation of the same leg. Keep torso and head facing exactly the reference camera angle, constant size and horizontal alignment; pelvis subtle bob only. FOUR equal square cells in a TWO-COLUMN TWO-ROW square grid, in row-major order. All poses must be CLEARLY distinct:
TOP LEFT (frame A): SCREEN-LEFT leg reaches FORWARD toward viewer and DOWN, sole planted nearest viewer; SCREEN-RIGHT leg is BACK with heel lifted, clearly shorter due to depth.
TOP RIGHT (frame B): SCREEN-LEFT foot stays planted under body bearing weight; SCREEN-RIGHT knee lifts toward viewer and RIGHT FOOT is OFF ground, passing by the planted left foot.
BOTTOM LEFT (frame C): SCREEN-RIGHT leg reaches FORWARD toward viewer and DOWN, sole planted nearest viewer; SCREEN-LEFT leg is BACK with heel lifted, clearly shorter due to depth. This MUST be the opposite of frame A.
BOTTOM RIGHT (frame D): SCREEN-RIGHT foot stays planted under body bearing weight; SCREEN-LEFT knee lifts toward viewer and LEFT FOOT is OFF ground, passing by planted right foot. This MUST be opposite of frame B.
Each leg has rigid shoe/root volume and a natural knee/root articulation, no rubber melting, no stretching entire image. Hip/hem follows gait slightly. Upper body, face, hair, held item, flower and silhouette remain nearly identical and registered across all four frames. Generous transparent margin in each cell, keep the ENTIRE character within the CENTRAL 75% of each cell, no clipping, no contact/overlap between cells. Character size is identical in all four cells. No shadow, text, labels, numbers, grid, checkerboard, scenery or new accessories. True transparent alpha backdrop. Four frames only, 2x2 layout.
```

## 검증과 남은 확인

`EnemyMovementAnimationVerification.ExportPreview`로 Setup 두 번 실행, 프레임 순서·루프·기준점,
Idle 복원, 공격·넉백 억제, 벽 정지, 방 재활성화, 물리 판정 보존과 보스 상속 제외를 검증했다.
실제 Unity 렌더는 `game/Logs/EnemyMovementPreviewV2/`에 있으며,
`enemy-movement-v2.gif`는 16개 주기별 렌더를 연결한 미리보기다.

이 프레임은 원본 카메라 구도의 한 방향 걷기다. 방향별 앞·뒤·옆 프레임은 포함하지 않는다.
Play Mode에서 이동 속도에 맞는 재생 리듬과 Idle 전환, 실제 전투 중 가독성은 추가 확인이 남아 있다.
