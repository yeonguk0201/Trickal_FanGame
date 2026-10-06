# 크레용사용 2페이즈 진입 모션

사용자가 제공한 `20260923_175223655_iOS.png`를 각성 본체로 적용한다. 흰금색 왕관 보석·흰 다이아 장식·노란 목도리와 망토·금색 장갑·등 뒤 빛 날개가 핵심이며, 기존 파란 보석/빨간 망토에 금색을 곱하는 표현은 제거했다.

## 적용 범위

- 원본 보존: [제공된 각성 이미지](./crayon-awakened-reference.png).
- 게임 본체: `game/Assets/Art/Bosses/FairyKingdom/Movement/CrayonHero_Awakened.png`.
- 전환 4프레임: 같은 폴더 `CrayonHero_Awaken_0..3.png`, 1024×1024, 중앙 pivot, 400 PPU.
- [최종 전환 포즈](./crayon-awakening-final-sheet.png). 0은 검을 들기 시작하고, 1은 머리 위로 검을 쭉 뻗으며 작은 날개가 나타난다. 2는 검을 위로 뻗은 채 날개를 펼치며 각성 모습으로 바뀐다. 3은 제공된 본체와 동일한 픽셀로 정착한다.
- `CrayonHeroBoss.prefab`에 본체와 4프레임을 연결했고, Movement Setup 재실행에도 연결을 유지한다.
- 본체 교체와 진입 모션에 이어 [각성 전용 공격](./crayon-hero-golden-attacks.md)을 연결했다. 2페이즈 걷기는 전용 프레임이 만들어질 때까지 각성 본체를 유지한다. 일반 모드의 걷기·공격 프레임으로 되돌아가지 않는다.

HP 50% 전환의 기존 1.25초 무적 시간을 그대로 사용한다. 실제 `StateEndsAt`으로 진행률을 계산하며, 별도 반복 재생 타이머를 사용하지 않는다.

| 전환 시작 후 시간 | 표시 |
|---|---|
| 0–0.3125초 | 일반 모습으로 검을 들기 시작 |
| 0.3125–0.625초 | 머리 위로 검과 팔을 쭉 뻗음, 작은 빛 날개가 나타남 |
| 0.625–0.9375초 | 검을 위로 유지하며 날개를 활짝 펼치고 각성 모습으로 교체, 짧은 밝은 섬광과 3.5% 표시 확대가 가라앉음 |
| 0.9375–1.25초 | 원본 각성 모습으로 정착 |
| 전환 종료 후 | 각성 본체 유지, 기존 2페이즈 전투 속도 적용 |

확대/섬광은 표시 자식에만 적용한다. 물리 루트·Collider·피해 시점은 변경하지 않는다. 본체의 SpriteRenderer 색상은 흰색이며 기존 금색 곱하기를 사용하지 않는다.

## 생성 및 재현

내장 `image_gen`으로 검을 위로 뻗고 날개를 펼치는 포즈를 생성했다. 입력 1은 사용자가 제공한 각성 이미지, 입력 2는 `CrayonHero_Walk_0.png` 일반 모습이다. [생성 시트](./crayon-awakening-sheet.png)와 [간격 보정 시트](./crayon-awakening-padded-sheet.png)를 보존한다. 최종 3프레임에는 제공된 원본 본체를 사용한다.

최종 사용 프롬프트:

```text
Use case: identity-preserve. Transparent 2x2 sprite atlas of FOUR full-body chibi knight animation poses for awakening. Input1: EXACT awakened identity: ivory white-gold crown crystal and ivory forehead diamond, yellow scarf/yellow cape/golden gloves, white leaf-engraved blade and gold hilt, brown/gold shield with ivory wing pattern, brown boots and TWO pale ivory light wings BEHIND shoulders. Input2: exact normal identity with blue crown crystal, red forehead gem, red scarf/cape, blue tunic, silver sword. Keep proportions, thick brown outlines, faceted illustration style, sword LEFT hand/screen left, shield RIGHT hand/screen right, same face. Actual alpha transparent background, no smoky glow, no background streaks, no detached particles. All poses and wings contained inside their own cells, 15% outer cell margins and huge central transparent gutters, complete boots and sword tips. Reading order top-left to top-right to bottom-left to bottom-right: 0 NORMAL knight starting to lift sword upwards from shoulder, knees braced, no wings. 1 NORMAL knight raises sword arm FULLY STRAIGHT UP, arm extending above head and blade pointing VERTICALLY straight upward, tiny folded ivory light wings begin emerging behind shoulders; crown STILL BLUE and cape STILL RED. 2 EXACT AWAKENED knight STILL holds sword arm FULLY STRAIGHT UP with sword tip pointing VERTICALLY UP well above crown, ivory crown diamond and YELLOW cape now changed, TWO ivory light wings are fully spread horizontally from back left/right. This is the climax: fully outstretched raised sword, open wings, gold-white transformation. Do not lower sword in this pose! 3 EXACT AWAKENED knight lowers sword into the same battle idle pose of input1, wings relaxed. Stable boot baseline each row and same head size in all four frames. No letters, no grid lines, no copies overlapping. Sword above head MUST be clearly visible in poses1 and2, blade not slanting down or crossing the face.
```

생성 시 배경·diffuse glow를 제거하도록 요청했다. 제공된 원본의 투명 픽셀에는 검정·흰색 RGB가 남아 있으나 alpha=0이다. 게임 출력은 실제 연결된 불투명 본체만 분리하며, 크기/위치는 보석 너비와 부츠 바닥을 기준으로 등록한다. 각성 원본의 보석 등록 좌표는 `(636,91,250,220)`이며, 칼을 뻗은 각성 포즈의 크기 보정 좌표는 `(351,624,101,80)`이다. 첫 생성보다 작아 보이던 각성 포즈의 크기를 보정해 변신 시 본체가 줄어드는 현상을 줄였다.

```powershell
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-awakened-reference.png -Attack Awakened
./scripts/split-crayon-attacks.ps1 -SourcePath docs/art-prompts/crayon-awakening-sheet.png -Attack Awaken -PaddedSourcePath docs/art-prompts/crayon-awakening-padded-sheet.png -IvoryCrystal 351,624,101,80
./scripts/verify-crayon-awakening.ps1
```

## 검증

5 PNG의 1024 캔버스·8px 이상 바깥 여백·연결된 본체·이웃 포즈 잔상 없음, GUID와 Prefab 참조, PPU/pivot 검사를 통과했다. 칼을 뻗은 각성 포즈가 전용 이미지인지, 마지막 포즈가 제공된 기본 자세와 정확히 같은지도 검사했다.

이번 수정 후 Unity 6000.3.22f1 배치에서 `TrickalFanGame.Editor.CrayonHeroAttackAnimationVerification.VerifyAwakening`을 실행해 종료 코드 0과 성공 로그를 확인했다. 전환 4구간이 실제 전환 종료 시각을 따르는지, 금색 곱하기 없이 각성 본체를 유지하는지, 일반 페이즈로 초기화되는지를 검증했다. 로그: `game/Logs/CrayonHeroAwakeningVerification.log`. 실제 Play Mode 화면 확인은 별도다.

열린 Editor에서 `Trickal Fan Game > Artwork > Verify Crayon Hero Awakening`을 실행한다. 기대 로그: `Crayon Hero awakening verification passed: timed snap transformation, supplied golden body, no color multiplication and phase reset.` 이후 BossTestScene 또는 새 Run에서 HP 50% 전환의 준비 → 순간 교체 → 정착과 전환 후 본체 유지 여부를 Play Mode로 확인한다.
