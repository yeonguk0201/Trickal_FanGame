# 기본 공격 착탄 스파크 · 치명타 히트스톱

작성: 2026-10-14. 런타임 연결 v2, 사용자 Play 수용(2026-10-14).

## 목적과 사용처

- 플레이어 기본 공격 탄이 적에게 닿는 순간의 타격감을 보강한다. 피해·넉백·상태 이상 규칙은 바꾸지 않는다.
- 착탄 스파크: 기본 공격 탄(`Projectile`)이 적에게 실제 피해(보호막 포함)를 준 모든 적중에서 재생한다. 분열·튕김 탄도 같다.
- 히트스톱 + 약한 카메라 흔들림: 플레이어의 **치명타**가 적에게 들어갔을 때만 재생한다. 처치·일반 적중에는 넣지 않는다.

## 제작 구성

원화 없이 코드 메시 + 전용 URP 2D 셰이더(`Resources/CombatEffects/HitSpark.shader`)로 만든다.

| 레이어 | 구성 | 조절 값 (`HitSpark`) |
|---|---|---|
| 별 | 착탄점에서 "팍" 튀어나오는 8가시 별. 30ms에 125%까지 커졌다가 70ms에 제 크기로 돌아오고, 100ms부터 가시만 남기며 줄어 190ms에 사라진다. 조금씩 회전한다. 가장자리는 또렷하다. | `SpikeCount`, `StarSize`, `StarPopPeak`, `StarSettle`, `StarHoldEnd`, `StarEnd`, `StarOvershoot` |
| 고리 | 15~150ms 동안 반경의 30%→95%로 빠르게 퍼지며 가늘어지고 사라지는 얇은 고리. | `RingStart`, `RingEnd`, `RingSegments` |
| 줄기 7개 | 탄이 날아온 반대쪽 150° 부채꼴로 튀는 방추형 줄기. 머리가 빠르게 나갔다 감속하고 꼬리가 따라붙어 끝점으로 사라진다. | `StreakCount`, `SpreadDegrees`, `SpreadJitterDegrees` |

- 전체 반경 `DefaultRadius` 0.7, 수명 220ms. 세 레이어는 한 메시 안에서 고리 → 줄기 → 별 순서로 그린다.
- 생성 시 25ms 지난 모습으로 시작해(`LeadTime`), 치명타 히트스톱이 멈춘 프레임에 별이 거의 최대 크기로 보인다.
- 색은 에르핀 기본 공격 구슬(`Erpin_Basic_Orb.png`)의 노란색에 맞췄다: 흰 중심 → 밝은 노랑 → 진한 노랑(`Core`, `Mid`, `Deep`). 캐릭터별 색 분기는 없다.
- v1(줄기 6개 + 네 갈래 섬광, 반경 0.45, 주황 가장자리)은 사용자 Play 확인에서 약하고 단순하다는 평가를 받아 2026-10-14에 위 구성으로 교체했다.
- 줄기 각도·길이 변화는 발생 순번으로 정해지며 게임 난수를 소비하지 않는다. 적의 SpriteRenderer 정렬 +3, 적의 Scene에 배치한다. 적이 그 타격으로 죽어도 끝까지 재생한다.
- 히트스톱(`HitStop`): 30ms 동안 time scale 0.02. 0이 아니므로 일시정지 판정(`Time.timeScale <= 0`)에 걸리지 않는다. 350ms 안에는 다시 걸리지 않아 연속 치명타가 끊겨 보이지 않는다. 처음 값(50ms, 200ms)은 Play 확인에서 격하다는 평가를 받아 줄였다. time scale이 1이 아닐 때(일시정지·결과 화면)는 시작하지 않으며, 도중에 다른 화면이 time scale을 가져가면 건드리지 않는다.
- 카메라 흔들림(`CameraShake`): 최대 0.06 월드 유닛, 140ms 감쇠. 히트스톱 중에도 움직이도록 unscaled 시간을 쓴다. 카메라 추적이 흔들림을 따라가지 않게 매 프레임 오프셋을 되돌린 뒤 다시 더한다.

## Scene 미리보기

`Trickal Fan Game/Effects/Preview Hit Spark` → `Open / reset Scene preview`.

- 런타임 메시·셰이더를 그대로 사용한다. Radius, Shot direction, Impact position, Burst time, Playback speed, Loop playback, Restart를 제공한다.
- 열기 전에 캐릭터·배경 SpriteRenderer 오브젝트를 선택하면 그림만 임시 Scene으로 복사한다. 원본 Scene과 에셋을 저장하지 않는다.
- 줄기·섬광 레이어 단독 표시와 히트스톱·카메라 흔들림 미리보기는 제공하지 않는다. 이들은 Play에서 확인한다.

## 검수와 검증

- Unity 6000.3.22f1 배치 자동 검증 통과(2026-10-14), 종료 코드 0. `Hit feedback verification passed` 로그 확인.
- 전용 자동 검증: `Trickal Fan Game/Effects/Verify Hit Feedback` (`TrickalFanGame.Editor.HitFeedbackVerification.Verify`).
- 검사: 셰이더 로드·오류, 줄기가 탄 반대쪽으로만 튀고 반경 안에 머무는지, 시간 역행 재현성, 생성 프레임 표시, 수명 종료, 히트스톱의 복원·재발동 간격·일시정지 양보, 흔들림의 크기 상한과 종료, 개발 패널 회귀.
- 자동 검증이 보지 않는 것: 치명타 이벤트 연결(`CombatVisualFeedback.HandleCritical`), 실제 카메라에서의 흔들림, 스파크의 화면 표시.
- Play: Game Scene에서 적을 기본 공격으로 맞혀 스파크를 확인한다. `F1 → — Player — → CRIT 100%`를 누르면 이번 Run의 모든 직접 타격이 치명타가 되어 히트스톱과 흔들림을 매번 볼 수 있다.
- Play 확인 항목: 스파크가 탄이 온 쪽으로 튀는지, 연사·분열 탄 동시 발생에서 적을 가리지 않는지, 치명타에서 짧게 멈추고 화면이 약하게 흔들리는지, 히트스톱 도중 `Esc` 일시정지 후 재개해도 속도가 정상인지.

## 최종 적용 상태

- 2026-10-14 사용자 Play 확인: v2 스파크의 모양·노란색과 줄인 히트스톱을 수용했고, 히트스톱 중 일시정지와 무적 상태에서 문제가 없었다.
- Scene 미리보기 창의 실제 조작과 커밋 전 회귀 묶음(`ReviewedCombatEffectsVerification.VerifyWithRegressionsBatch`)은 확인하지 못했다. 회귀 묶음은 같은 프로젝트를 연 Unity 인스턴스 잠금으로 실행되지 않았다.
