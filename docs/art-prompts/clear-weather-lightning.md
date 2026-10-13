# 날씨는 맑음 카드 — 화면 상단 번개

작성: 2026-10-13. 런타임 연결 v1, 사용자 미술 검수 대기.

## 목적과 구성

- 기존 기본 공격 10회 실제 적중 → 같은 적에게 공격력 150% 번개 피해 규칙을 유지한다. 사망한 적에는 추가 번개를 만들지 않는다.
- 기존 짧은 `effect-lightning` 표시 호출을 `ClearWeatherLightning.Play`로 교체했다. 원본 프레임은 보존한다.
- 사용자의 요청: 화면 맨 위에서 적에게 내려치는 가느다란 번개.
- LineRenderer 경로 + 전용 URP 2D 셰이더. 밝은 아이보리 중심·푸른 가장자리의 단일 본체이며 보조 입자나 화면 플래시는 없다.
- 발생 순간 Main Camera의 viewport y=1.03에서 적 위치 + y=0.35까지 연결한다. 카메라가 없으면 12 월드 유닛 위를 사용한다.
- 굵기는 0.075 월드 유닛으로 길이와 독립적이다. 18ms 하강, 70ms까지 최대 밝기, 240ms까지 잔광 소멸. 피해는 기존 즉시 판정을 유지하며 시각적 하강만 짧게 재생한다.
- 경로 꺾임은 시각 시간으로 재현되며 게임 난수를 소비하지 않는다. 벽으로 자르지 않고 화면 위에서 내려온다. 양 끝점은 고정하며 긴 경로에서도 굵기를 늘리지 않는다.
- 대상이 번개 피해로 사망해도 해당 타격 위치에서 마저 재생한다. 시전자 사망·비활성화·삭제 또는 수명 만료 시 제거하고, 대상 Scene에 배치해 Scene 해제 시 함께 제거한다.

## Scene 미리보기

`Trickal Fan Game/Effects/Preview Clear Weather Lightning` → `Open / reset Scene preview`.

- 런타임 코드와 셰이더를 그대로 사용한다. Length, Width, Impact position, Burst time, Playback speed, Loop playback, Restart를 제공한다.
- 반복 체크 해제로 일시정지하고 시간 슬라이더를 앞뒤로 이동할 수 있다. 단일 본체라 별도 보조 레이어는 없다.
- 열기 전에 캐릭터·배경 SpriteRenderer 오브젝트를 선택하면 그림만 임시 Scene으로 복사한다. 선택 없이 열면 효과 단독 보기다. Tilemap·게임 스크립트·피해 로직은 복사하지 않는다.
- 원본 Scene과 에셋을 저장하지 않는다. 굵기 등 조절은 임시 값이며 런타임 기본값을 변경하지 않는다.
- 창 닫기·Play 전환·스크립트 리로드에서 임시 Scene과 재질을 정리한다.
- 기존 프레임 A/B 전환은 제공하지 않는다. 실제 카메라·이동 중 타격과 최종 미술 검수는 Play에서 확인한다.

## 검증과 수동 확인

- Unity 6000.3.22f1 배치 자동 검증 통과(2026-10-13), 종료 코드 0. `Clear Weather Lightning verification passed` 및 기존 Artifact-1 통과 로그 확인. 로그: `tmp/clear-weather-lightning-verification.log`.
- 전용 자동 검증: `Trickal Fan Game/Effects/Verify Clear Weather Lightning`.
- 배치 진입점: `TrickalFanGame.Editor.ClearWeatherLightningVerification.Verify`.
- 검사: 화면 위 시작점, 하강 방향, 정확한 타격 끝점, 시간 역행 재현성, 수명 종료, 셰이더 오류 및 기존 카드 적중·피해·처치 규칙.
- Play: Game Scene에서 `F1 → — Artifact — → Show artifact drops → Drop 날씨는 맑음 카드`로 획득한다. 활성·유효 Artifact 에셋 자동 수집 대상임을 코드와 에셋으로 확인했다.
- 기본 공격 10회 적중 후 화면 위부터 맞은 적까지 가는 번개가 보이고, 중앙·상단·벽 근처 적과 연속 발동에서도 캐릭터를 과하게 가리지 않는지 확인한다.
- 대표 형태·움직임·Scene 미리보기 실제 표시·Play 미술 승인은 아직 확인하지 않았다. 기능 연결과 미술 승인은 구분하며 계획 완료 체크는 변경하지 않는다.
