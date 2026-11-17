# 보스룸 차원문 v1

## 목적과 사용처
- 보스 처치 후 다음 층으로 이동하는 FloorAdvancePortal의 대표 원화 후보.
- 밝은 SD 게임에 맞춘 세로형 차원문, 빛나는 외곽과 안쪽으로 이어지는 공간.
- 정면 방향. 실제 표시 크기와 발 기준점은 게임 화면 검수에서 결정한다.

## 제작 구성
- 최종 후보: portal-concept.png. 내장 image_gen으로 생성한 투명 배경 단일 원화.
- 핵심 차원문만 제작. 지면 빛과 독립 입자는 포함하지 않는다.
- 생성 지시: cute SD fantasy game, upright oval dimensional doorway, thick luminous swirling rim, darker magical depth, broad curved inward flow, polished cartoon art, isolated transparent background; no characters, scenery, stone frame, ground platform, shadow, detached particles or text.
- 원본 생성 파일: exec-4064f8a1-eda1-46ab-adac-20c7dfa6c6e4.png.

## 검수와 검증
- 생성 결과에서 세로 타원형 실루엣, 밝은 외곽과 보라·파랑 내부 흐름을 확인.
- 사용자 미술 검수 대기. 실제 배경·캐릭터 크기 합성, 움직임, Play 검수는 미실시.
- 원화와 문서만 추가하여 Unity 배치는 생략.

## 최종 적용 상태
- 원화 후보만 저장. 런타임과 Scene은 변경하지 않음.
- Unity 연결 시 외곽, 내부 흐름, 지면 빛의 독립 제어를 검토하고 PPU·피벗·정렬을 확정한다.

## 4프레임 일렁임 후보 (2026-11-16)
- 사용자 요청에 따라 기존 대표 원화를 참조해 `portal-idle-4frames.png`를 생성.
- 2열 × 2행, 좌상 → 우상 → 좌하 → 우하 순서로 반복한다.
- 첫 생성본은 외곽 잔광이 셀 경계에 닿아 채택하지 않고, 여백을 늘린 두 번째 생성본을 저장했다.
- 최종 생성 원본: exec-1f2db082-ae07-49ca-9c96-9cc0c8113a88.png.
- 테두리와 내부 흐름의 작은 형태 변화가 있는 4단계 후보. 연속 재생의 자연스러움은 사용자 검수 대기이며 확정하지 않는다.
- `preview.html`에서 2~12 fps로 재생 및 일시정지 가능. 초기 속도는 6 fps(한 주기 약 0.67초).
- Unity 연결과 실제 Play 확인은 미실시. 원화 시트·미리보기·기록만 추가했다.
- 원본 크기: 1312×1199 RGBA. 높이가 홀수이므로 현재 미리보기는 높이를 2등분한 소수 좌표로 표시한다. Unity 정식 분할 전 짝수 높이 캔버스 정리와 프레임 기준점 보정이 필요하다.

## Unity 적용 (2026-11-16)
- `Resources/CombatEffects/boss-room-portal-idle.png`와 meta 추가. RGBA, Bilinear, Clamp, 무압축, mipmap 없음, Max Size 2048.
- `FloorAdvancePortal`에서 656×599 셀 4개를 좌상·우상·좌하·우하 순서로 분할한다. 홀수 높이의 가운데 투명 1행은 제외하고 원본을 보존한다. PPU 220, 중심 피벗.
- `RoomGraphAssembler`가 1·2층 보스룸 ContentRoot 중앙에 생성한다. 최종 보스의 런 종료 흐름은 유지.
- 전투 전 렌더러/트리거 비활성. 클리어 시 0.65초 SmoothStep 알파 등장, 6 fps 반복. 등장 완료 후 이동 트리거 활성.
- 매 물리 프레임 포탈 주변 아이템·일회용 스펠·권능·회복·자원·SP 드롭을 검사한다. 반경 1.65 + 드롭 콜라이더 외접 반경 + 0.1 거리로 밀어내며 늦게 생성되는 드롭도 처리한다. 중심과 정확히 겹치면 아래쪽으로 이동한다.
- 정적 검사: `git diff --check` 통과.
- 검증 메뉴: `Trickal Fan Game/Effects/Verify Boss Room Portal`.
- 회귀 배치: `TrickalFanGame.Editor.FloorPortalArtworkVerification.VerifyWithRegressionsBatch` (전용 검사 + 기존 Reward-3 보상·층 이동 검사).
- Play 수동 확인: Game Scene → F1 → `— Boss room —` → `Boss F1` 또는 `Boss F2`. 처치 전 포탈이 보이지 않는지 확인하고 `Kill current wave` 또는 실제 처치 후 방 중앙에서 서서히 나타나는지 확인한다. 중앙 드롭이 포탈 바깥으로 이동해 획득 가능한지, 포탈 진입 시 다음 층으로 이동하는지 확인한다.
- 실제 Play 미술 검수는 대기 중.
- 최종 자동 검증: Unity 6000.3.22f1 배치 종료 코드 0. `Boss room portal verification passed` 및 `Week 16 Reward-3 verification passed` 확인. 로그: `game/Logs/portal-verification.log`.
