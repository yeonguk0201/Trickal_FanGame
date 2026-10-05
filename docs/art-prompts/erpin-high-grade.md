# 에르핀 고학년 돌격·충돌 모션 (2026-10-05)

사용자가 제공한 `18a92233e5180b16.gif`의 지팡이 들고 달리기→충돌→넘어짐→일어나기를
게임의 기존 고학년 전투 단계에 연결했다. 내장 imagegen으로 기존 걷기 에셋의 그림체·복장을 유지했다.

## 생성 지시

Create exactly FOUR full-body Erpin animation poses in a 2x2 true transparent sprite sheet. FIRST reference is exact game identity and painterly bold outlined style to preserve: blonde small princess, gold crown with blue diamond, white dress and boots with blue/gold decorations, red cape with white pompom hem, white small wing behind, ornate gold round-headed blue-crystal staff. SECOND is reference GIF motion evidence only, not its grey background or red staff. All attacks face screen LEFT; same identity/proportions/character scale in every cell, same ground baseline for planted poses. Generous fully transparent gaps between rows AND columns, ample margins around every whole character including long staff. Output ONLY four characters. No grey background, no ground shadow, no text, grid, labels, trails, additional items or effects. Preserve existing game sprite costume and face.

돌격 시트: FOUR poses in 2x2: top left preparation with staff lifted upright in front, focused determined expression, knees bending. Top right charging run LEFT with staff held upright prominently ahead of body, torso leaning forward, screen-left foot forward and right foot back. Bottom left opposite step charging LEFT, right foot forward and left foot back, cape and blonde hair flow gently behind toward RIGHT. Bottom right third passing stride charging LEFT, legs under hips, staff still firmly held upright ahead, hair and cape trailing. Actual alternating short legs, energetic cute open mouth. Staff diamond stays blue. No attack beam or detached effects.

충돌 시트: FOUR poses in 2x2: top left collision reaction, torso jerks backward toward RIGHT, legs stumbling, gripping staff, surprised face. Top right tumbles backward onto back/side, legs briefly lifted, staff still in hand above body, squeezed shut eyes. Bottom left settled fallen on ground, lying horizontal with head toward RIGHT and boots toward LEFT, hair and cape spread beneath, staff tilted safely beside body, dizzy unhappy expression. Bottom right getting up, kneeling and pushing up using staff, upright head, recovering. Depict articulated body/legs changing poses rather than rotating whole sprite. Keep full staff and hair inside canvas. No debris or stars.

생성 시트 ID: 돌격 `4b05b73c-c500-4d21-9824-a15e638d5330`, 충돌 `f18abfc3-4f3e-4cf8-bfdd-712be42e4676`.

## 적용과 전투 경계

`Assets/Resources/Characters/Erpin_HighGrade/Erpin_HighGrade_0..7.png`에 투명 1024×1024, 400 PPU로 저장했다.
기존 측면 걷기의 표시 높이와 바닥 기준으로 등록하고 시트별 동일 배율로 분리했다.
`PlayerWalkAnimator`가 Resources를 읽으므로 Scene/Prefab 재구성이 필요 없다.

- UltimateDashing: 첫 0.08초 준비(0번), 이후 1~3번을 10 FPS로 반복한다. 실제 이동의 시작을 늦추지 않는다.
- UltimateImpactRecovery: 기존 충돌 회복 시간(기본 0.4초)을 4~7번 반동·넘어짐·누움·일어나기에 배분한다.
- 고학년 포즈가 걷기·저학년 포즈보다 우선한다. 방향 변경에 맞춰 좌우 반전하며 세로 이동에는 마지막 좌우 방향을 유지한다.
- 취소/시간 종료의 coast에는 충돌 넘어짐을 재생하지 않는다. 회복 완료·사망·비활성화에는 포즈 소유권을 해제한다.
- 그림만 바꾸며 본체 Transform·Collider·피해·넉백·무적·쿨다운·이동 속도·회복 시간은 변경하지 않는다.

미리보기 `game/Logs/ErpinHighGradePreview/erpin-high-grade.gif`는 포즈 비교용이다. 실제 게임의 돌격 길이는
입력과 충돌 시점에 따라 달라지고 충돌 회복 그림은 실제 설정된 시간에 맞춰 재생된다.
Play Mode에서 Q 돌격, 방향 전환, 적/보스 충돌, Q 취소와 시간 종료를 확인한다.

## 검증

`ErpinHighGradeArtworkSetup.SetupAndVerifyBatch`에서 두 번 에셋 가져오기, 돌격 3포즈 반복·방향 전환,
정지 시간·충돌 4단계·저학년 그림 우선순위·취소/시간 종료 제외·비활성화 복원과 본체/판정 보존을
검증했다. 기존 걷기·고학년·저학년 회귀도 통과했다 (`game/Logs/erpin-high-grade-final.log`, 종료 코드 0).
기존 고학년 회귀는 층 진입의 실제 시간 보호창이 합성 시각 취소 검사에 남아 실패했다.
해당 검사에서 독립적인 층 진입 보호창을 초기화해 시나리오를 분리했다. 런타임 보호 규칙은 변경하지 않았다.

## 지팡이·팔·넘어짐 비율 수정

사용자 피드백에 따라 기존 걷기와 저학년 그림을 지팡이의 기준으로 사용한다.
돌격 첫 포즈의 빠져 보이는 자유 팔을 복원하고 모든 포즈에서 두 팔과 손을 구분한다.
지팡이는 원형 헤드·길고 가는 곧은 막대기·하단 끝장식·헤드 옆의 짧은 깃발 장식을 별도로 유지한다.
깃발이 막대기를 대체하지 않고, 헤드와 막대기는 단단히 연결되어 꺾이거나 찌그러지지 않는다.
넘어짐 1~3번에서도 온전한 지팡이를 유지한다. 누운 포즈의 몸통과 다리를 짧게 줄여 기존 큰 머리와
짧은 몸의 비율을 맞춘다. 런타임과 `.meta` GUID를 유지하면서 8개 PNG 그림만 교체한다.

수정 지시: Existing walking and low-grade skill images are authoritative staff/proportion references.
Long thin straight gold rod passes through gripping hand to pointed lower terminal; the short broad hanging
white/gold flag is a separate accessory and never substitutes for the shaft. Circular gold head has dark navy
interior, light blue faceted crystals and segmented silver-blue ornaments. Same rigid unbent staff dimensions
in every pose, both complete ends visible. Restore the second sleeved arm and gloved hand in preparation,
show two arms in every running pose, correct alternate leg lead. Preserve complete staff in recoil/tumble/fall.
For lying pose shorten torso and thighs, bring bent knees near belly, keep large round head with stubby legs,
and lay intact staff beside body without merging its head into crown/hair. Preserve costumes and transparency.

수정 시트 ID: 돌격 `f65f9e8d-47c0-45c1-8172-ec27d6885541`, 충돌 최종
`76589cae-87d5-4ba3-8463-2c3edfb1fa89` (중간 시트 `33276f41-e3b9-4c8f-81c5-9b216396ed61`).
충돌 2번째는 막대기 하단이 몸에 가려져 다시 수정했다. 지팡이를 발 왼쪽의 빈 공간으로 기울여
원형 헤드·긴 막대기·끝장식 전체가 보이도록 하고, 머리 옆의 여분 깃발 장식을 제거했다.
누운 3번째는 짧은 몸통과 굽힌 무릎으로 비율을 복원하고 지팡이를 몸 옆에 온전히 배치했다.
수정 에셋 반복 가져오기와 고학년 모션·걷기·고학년·저학년 회귀 통과: `game/Logs/erpin-high-grade-staff-fix.log`, 종료 코드 0. 8개 프레임의 팔·지팡이 구조·누운 몸 비율을 접촉 시트로 시각 확인했다.
