# Unity 검증 시간 단축 (임시 메모)

- 작성: 2026-10-11
- 상태: **1·2번 반영, 3~5번은 미착수.** 3~5번을 구현하면 이 문서는 지우고 계획·상태는
  `docs/13-development-tooling-plan.md`, 실행 방법은 `docs/05-development-setup.md`에 반영한다.
- 2026-10-13 측정 후 갱신: 병목은 seed 루프 횟수가 아니라 층 생성마다 반복되던 방 Layout 검사였다. 그것을
  고쳐 회귀 묶음이 23분 39초에서 4분 24초로 줄었다. 아래 "왜 느린가"의 루프 크기 표는 실제 비용 순서와
  다르다. 3번의 중복 제거는 이제 약 40초 절약이고 4번은 필요 없어 보인다. 기록은
  [트러블슈팅](../troubleshooting/verification-speed-and-stale-verifiers.md)에 있다.
- 근거: 코드와 기존 배치 로그를 읽은 정적 분석이다. 작성 시점에 Editor가 프로젝트를 열고 있어 배치를 돌리지
  못했으므로 **검증기별 소요 시간은 측정하지 않았다.**

## 반영한 것

1. **실행 단계 규칙.** 작업 중에는 단독 `Verify`, 커밋·ship 직전에 회귀 묶음 한 번, 문서·주석·원화 PNG만 바꾸면
   Unity 배치 생략, 공용 기반 변경이면 작업 중에도 회귀. `AGENTS.md`의 "검증과 문서화"와
   `.agents/skills/unity-verification-runner/SKILL.md`의 "실행 단계"에 넣었다.
2. **변경 대상 → 검증기 대응표.** 같은 스킬의 "자주 쓰는 대응표"에 넣었다. 새 조각이 자주 쓰이게 되면 여기에 추가한다.

## 왜 느린가

`RoomLayoutImporterVerification.VerifyWithRegressionsBatch` 기준이다.

- **묶음이 묶음을 부른다.** RoomLayout → `Week23Obstacle6` 회귀 → `Week23Obstacle5` 회귀 → `Week20Obstacle4` 회귀로
  4단계를 내려가며 검증기 18개가 돈다.
- **같은 검증기가 두 번 돈다.** `Week19Encounter4Verification`, `Week19Difficulty1Verification`,
  `Week22Terrain0Verification`은 RoomLayout이 직접 한 번, Obstacle 체인 안에서 또 한 번 불린다. 셋 다 512 seed 루프가
  있다.
- **seed·샘플 루프가 크다.** 상수 기준이며 어느 루프가 실제로 무거운지는 모른다.

  | 검증기 | 루프 크기 |
  |---|---|
  | `Week18Obstacle1Verification` | 분포 200,000회 + 최대 1,000,000 seed 탐색 |
  | `Week22Chest1Verification` | 200,000 + 60,000 샘플 |
  | `RoomLayoutImporterVerification` | 가중치 140,000 샘플 + 400 seed 층 생성 |
  | `Week23Obstacle5Verification` | 8,192 × 4회분 + 최대 100,000 seed 탐색 2곳 |
  | `Week23Obstacle6Verification` | 8,192 × 2 |
  | `Week19Spawn2Verification` | 10,000 |

- **실행마다 고정 비용이 붙는다.** `output/pit-contour-v2-review.log`에서 검증 시작 전 초기 에셋 리프레시가 12.7초,
  그중 스크립트 컴파일이 5.4초다. Editor 스크립트를 고친 뒤에는 매번 낸다.

호출이 기하급수로 불어나는 구조는 아니다. 진입점에서 닿는 호출은 총 39회이고, `Week18Obstacle2Setup.OpenGameScene`은
14번 불리지만 이미 Game Scene이면 바로 반환한다.

## 남은 방안

### 3. 회귀 묶음을 평평하게 만들고 중복 제거

공용 러너 하나가 검증기 목록을 받아 각각 한 번씩만 실행하고, 검증기별 소요 시간과 통과·실패 목록을 출력한다.
각 `VerifyWithRegressionsBatch`는 다른 묶음을 부르는 대신 러너에 목록을 넘긴다. `docs/13-development-tooling-plan.md`의
"유효한 검증만 한 번에 돌리고 통과/실패 목록을 출력하는 회귀 실행기 배치 메서드" 후보와 같은 작업이다.

- 효과: 위 중복 세 개가 사라지고, 4번을 판단할 시간 로그가 생긴다.
- 주의: 검증기끼리 실행 순서나 열린 Scene에 기대는지 확인한다. 한 검증기가 실패해도 나머지를 계속 돌릴지,
  첫 실패에서 멈출지 정한다. 종료 코드는 하나라도 실패하면 0이 아니어야 한다.

### 4. seed 루프에 빠른 모드

작업 중에는 줄인 횟수로, 커밋 전에는 전체로 돌린다. 명령행 인자나 환경 변수로 고른다.

- 3번의 시간 로그로 실제로 느린 루프만 고른다. 측정 없이 전부 줄이지 않는다.
- 분포 검사의 허용 오차(예: 1%)는 샘플 수에 묶여 있다. 횟수만 줄이면 오탐이 나므로 빠른 모드에서는 허용 오차를
  함께 넓히거나 분포 검사를 건너뛴다.
- "N seed 안에 모든 Layout이 한 번은 나온다"류의 검사는 횟수를 줄이면 실패할 수 있다. 빠른 모드 대상에서 뺀다.

### 5. 배치 실행 횟수 줄이기

배치를 띄울 때마다 고정 비용을 낸다. 2026-10-10 밤 로그에는 1~2분 간격으로 네 번 연달아 띄운 기록이 있다
(`output/pit-contour-v2-review.log` ~ `pit-contour-v2-final.log`).

- 같은 변경에 필요한 검증기는 한 번의 실행에 묶는다. 3번의 러너가 임의 목록을 받으면 자연히 해결된다.
- Editor가 열려 있으면 배치는 다중 인스턴스 잠금으로 실행되지 않는다. 이때는 열린 Editor의 메뉴로 돌리는 것이
  유일한 방법이며 컴파일 비용도 이미 치른 상태다.

## 측정 방법

Editor를 닫고 배치 명령에 `-timestamps`를 붙이면 로그 줄마다 시각이 찍힌다. 검증기마다 끝에 "passed" 로그를
남기므로 코드 수정 없이 검증기별 소요 시간을 볼 수 있다.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" -batchmode -nographics -quit -timestamps `
  -projectPath <game 절대 경로> `
  -executeMethod TrickalFanGame.Editor.RoomLayoutImporterVerification.VerifyWithRegressionsBatch `
  -logFile <로그 절대 경로>
```
