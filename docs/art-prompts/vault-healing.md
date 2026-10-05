# 새마음금고 보석 회복 모션 (2026-10-06)

- 생성: 내장 `image_gen.imagegen`, 투명 배경. 참조: `game/Assets/Art/Bosses/FairyKingdom/Movement/Vault_Body.png`.
- 생성 원본: [vault-heal-sheet.png](./vault-heal-sheet.png). 실제 출력은 1402×1122이며, 포즈 사이 여백을 기준으로 분리했다.
- 적용: `game/Assets/Art/Bosses/FairyKingdom/Movement/Vault_Heal_0..3.png`, 각 640×512, 중앙 pivot, 400 PPU. 원본 본체의 불투명 영역과 바닥 기준선에 맞춰 정렬했고 낮은 씹기 포즈는 조금 웅크린다.
- 예고에서 보석을 들고 돌아서고, 회복 중에는 등 보이는 두 포즈를 0.125초마다 교대로 재생한다. Recovery 시작에 돌아오는 포즈를 0.18초 보여준 뒤 기존 정면 `Vault_Hop_0`으로 복원한다.
- 회복량·회복 pulse·패턴 시간은 그대로 유지한다. 바닥 보물 레이어와 물리 판정은 기존 구성을 사용한다. 새 전투 시작·비활성화·다른 패턴·페이즈 전환은 먹기 표시를 초기화한다.
- 프리팹에 직접 연결되어 있으며, `Trickal Fan Game > Artwork > Setup Boss Movement Animations`를 재실행해도 회복 프레임을 다시 연결한다.

## 생성 프롬프트

Use case: identity-preserve. Asset type: transparent game boss animation sprite sheet. Reference is the exact red and gold treasure chest yellow slime king boss. Create one 2 by 2 atlas with FOUR equally sized 640x512 cells (total 1280x1024), true transparent background, no text/grid/shadows. Exact same bold dark outlines, clean highlights, yellow slime, red curved chest lid, gold trim and crown as reference. Character size consistent with reference occupying central 80 percent, same centered registration and bottom baseline in every cell. Do not include loose ground treasure piles. Top-left: boss turns away, rear three-quarter view, chest front and face mostly obscured, short yellow hand carrying a small blue gem toward its hidden mouth. Top-right: full rear view, face entirely hidden, show rounded red wooden rear panel gold trim, curved red open lid and crown peeking above, short yellow shoulders hunched forward eating the gem behind the chest, body slightly lowered. Bottom-left: SAME rear orientation as top-right, next chewing pose, shoulders raised slightly and shifted right, crown bobbed and lid jiggled slightly, gem now hidden, conveys funny greedy chomp chomp without long arms. Bottom-right: three-quarter returning toward viewer, recognizable smiling yellow face and short hands emerging, no gem now, returns toward original front pose. Rear views must read clearly as THE BACK OF THIS CHEST, not a closed generic chest or a second face. Preserve identity, proportions and details as much as anatomically possible. Four distinct registered full-body poses, no overlap, ample transparent margin. This will animate front(original) -> turn(top-left) -> chew alternating(top-right,bottom-left) -> return(bottom-right) -> front(original).

## 검증 상태와 수동 확인

Unity 배치 `TrickalFanGame.Editor.VaultHealingAnimationVerification.Verify`를 시도했으나 열린 Editor와 프로젝트 잠금이 있는 상태에서 검증 진입 전에 종료했다. 자동 통과로 처리하지 않았다. 새 에셋 크기·알파·meta GUID·프리팹 참조와 이번 변경의 diff 공백을 정적으로 확인한다.

열린 Unity에서 컴파일 완료 후 `Trickal Fan Game > Artwork > Verify Vault Healing Animation`을 실행한다. 기대 로그는 `Vault healing animation verification passed: turn, rear chewing, healing amount, front restoration and cancellation.`이며, 기존 회귀는 `Artwork > Verify Boss Movement Animations`와 `Week 15 > Verify Boss-2 Saemaeum Vault`로 확인한다.

화면 확인: `Trickal Fan Game > Debug > Open Boss-2 Test Room` 후 Play한다. HP가 절반을 넘는 1페이즈에서 보스를 조금 공격해 회복 패턴을 기다린다. 보석을 들고 뒤돌아 어깨·왕관이 쿰척쿰척 들썩이는 동안 HP가 올라가고, 끝나면 정면으로 돌아와야 한다. 먹기 중 바닥 보석은 바닥에 남고 몸체가 점프하지 않아야 한다. 2페이즈 전환이나 사망 때 뒷모습이 남지 않아야 한다. 실제 Play 확인은 아직 남아 있다.
