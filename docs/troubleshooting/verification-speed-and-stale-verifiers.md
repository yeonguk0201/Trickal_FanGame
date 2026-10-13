# Unity 검증이 20분 넘게 걸리고 낡은 검증기가 섞여 있던 문제

- 기록일: 2026-10-13
- 환경: Unity 6000.3.22f1 배치 실행(`-batchmode -quit -executeMethod`)

## 1. 증상

- 방·적 관련 작업 하나를 검증하는 데 10분 이상 걸렸다. 단독 `Verify` 하나가 2~4분이었고, 한 작업이
  보통 2~3개를 건드렸다.
- 커밋 전 회귀 묶음 `Week25Trap1Verification.VerifyWithRegressionsBatch`는 23분 39초를 돈 뒤 마지막
  부근의 `Week22Chest0Verification`에서 실패했다. 같은 자리에서 두 번 연속 실패했다.

## 2. 조사

### 2.1 틀린 가설

처음에는 루프 횟수가 큰 검증기를 의심했다. `Week18Obstacle1Verification`의 분포 200,000회와 최대
1,000,000 seed 탐색이 대표였다. 배치에 `-timestamps`를 붙여 검증기별 시간을 재 보니 이 검증기는
0.13초였다. 루프 한 번이 산술 한 줄이라 횟수가 커도 비용이 없었다.

### 2.2 측정

`-timestamps`는 로그 줄마다 시각을 찍는다. 검증기마다 끝에 "passed" 로그를 남기므로 코드 수정 없이
검증기별 소요 시간이 나온다.

| 검증기 | 수정 전 | 수정 후 |
|---|---|---|
| `Week25Trap1` | 79초 | 11초 |
| `RoomLayoutImporter` | 85초 | 9초 |
| `Week19Encounter4` | 98초 | 11초 |
| `Week19Spawn2` | 95초 | 13초 |
| `Week19Difficulty1` | 100초 | 11초 |
| `Week22Terrain0` | 174초 | 18초 |
| `Week20Obstacle4` | 96초 | 14초 |
| `Week20Special3` | 191초 | 18초 |
| `Week23Obstacle5` | 51초 | 42초 |

긴 공백은 모두 seed마다 층 전체를 생성하는 `FloorGenerator.TryGenerateForSeed` 루프 구간이었다.
40 seed를 따로 재니 층 하나 생성 154.9ms 중 136.6ms가 `RoomContractCatalog.TryValidate`였고, 그
대부분(135.1ms)이 방 템플릿 33개의 `TryValidateLayout`이었다.

## 3. 원인

### 3.1 속도

`RoomTemplateSelector.TryAssign`은 호출될 때마다 방 카탈로그 전체를 검증한다. 그 안의 장애물 Layout
검사는 방마다 이동 가능 영역을 flood-fill하고 시야를 샘플링한다. 게임에서는 런당 한 번이라 문제가
없지만 검증기는 같은 카탈로그로 400~1,500번 반복했다. 방이 늘수록 더 느려지는 구조였다.

### 3.2 낡은 검증기

- 회귀 묶음은 첫 실패에서 멈춘다. 낡은 검증기 하나가 뒤쪽에 있으면 매번 전체 시간을 쓰고 실패하며,
  그 뒤의 검증기는 실행되지 않아 다른 문제를 가린다.
- `Week22Chest0Verification`은 상자 Prefab이 자리표시 그림의 색과 폭을 갖길 기대했다. 요정왕국 원화가
  연결된 뒤로는 통과할 수 없었다.
- 이 검증기를 고쳐 묶음이 끝까지 가자 `Week20DevPanelVerification`이 `EnsureSecretPitPrefab()`으로
  `SecretPit.prefab`의 원화를 자리표시 그림으로 덮어쓰는 것이 드러났다. 앞의 실패가 가리고 있었다.

## 4. 해결

- `RoomTemplateDefinition.TryValidateLayout`이 검사 결과를 재사용한다. 키는 검사가 읽는 값 전체(템플릿
  ID, 이동·전투 영역, 문, SpawnPoint, 장애물·구덩이·바닥 함정의 ID와 사각형)의 정확한 비트다. Prefab은
  매번 다시 읽으므로 Layout을 고치면 다시 검사한다. 검증기가 방을 일부러 망가뜨려 거부를 확인하는
  검사도 그대로 동작한다.
- `Week22Chest0Verification`은 원화가 연결된 상자에서는 흰색과 원화 맞춤 폭을 기준으로 본다.
- `Week20DevPanelVerification`은 Prefab을 다시 만들지 않고 읽기만 한다.

결과: 층 생성 154.9ms → 25.5ms, 회귀 묶음 23분 39초 → 4분 24초(종료 코드 0). `Difficulty-1`의 통계값이
수정 전후로 같아 생성 결과는 바뀌지 않았다.

## 5. 전체 검증기 점검

`Verify()` 156개를 한 프로세스에서 차례로 실행하며 검증기마다 `Assets`·`ProjectSettings`의 파일 변화를
기록했다(9분 55초). 실패한 것은 하나씩 별도 프로세스로 다시 돌렸다.

- **에셋을 쓰는 `Verify`는 `Week8GridFloorVerification` 하나였다.** `Week8GridFloorSetup.Setup()`을 두 번
  호출해 `SampleScene.unity`, `room-grid-base.prefab`, 방 정의 5개를 덮어썼다.
- 검증기 17개가 `Physics2DSettings.asset`이나 `TimeManager.asset`을 수정됨으로 표시한다. 저장해도 내용은
  바뀌지 않았다. 값을 바꿨다가 되돌리는 것이라 무해하다.
- 156개 중 40개가 현재 프로젝트 상태에서 통과하지 못했다.

### 5.1 조치한 것

- HUD 계열 11개는 원인이 하나였다. Week 22의 상점 오버레이가 클릭을 받는데, `Week13Hud1Verification`과
  `Week13Hud2Verification`은 일시정지·보상 선택 화면만 예외로 두고 있었다. `ShopView`를 예외에 추가했다.
- Week 7·8의 고정 9방 그래프와 초기 생성기 검증기 7개는 대상이 없어져 삭제했다:
  `Week7ThreeFloorGraph`, `Week7FloorEncounter`, `Week7FloorTransition`, `Week8RandomRoom`,
  `Week8RandomRoomBinding`, `Week8RunSeed`, `Week8GridFloor`.
- `Week8FloorDifficultyVerification`은 삭제한 검증기를 부르는 한 줄만 뺐다. 층별 배율 검사는
  `Hp1HealthUnitsVerification` 회귀가 계속 쓴다.

### 5.2 낡은 검증기가 잡고 있던 실제 회귀

`Week15Enemy3Verification`은 돌진형 Prefab의 추적 시간이 0보다 커야 한다는 검사에서 실패하고 있었다.
낡은 기준이 아니라 실제 회귀였다.

- 2026-09-28 커밋 `65a0568`(반칸 체력)에서 `ChargingEnemy.prefab`의 `pursuitDuration`이 1.1 → 0,
  `recoveryDuration`이 0.75 → 0.6, `chargeCooldown`이 0 → 1.5로 바뀌었다. `Week7ChargingEnemySetup`이
  쓰는 값과 같다.
- `Week7ChargingEnemySetup`(서 있다가 돌진)과 `Week15Enemy3Setup`(쫓아오다가 돌진)이 같은 Prefab을
  덮어쓴다. 체력 작업에서 Week 7 Setup이 다시 실행되며 Enemy-3 동작이 꺼졌고, 2주 넘게 알아차리지
  못했다.
- Prefab의 세 값을 Enemy-3 값으로 되돌렸다. 돌진 속도 9.5(Tune-1)는 유지했다.
  `Week7ChargingEnemySetup`은 컨트롤러를 새로 붙일 때만 값을 설정한다.
- 복구 후 `Week15Enemy3`, `Week7ChargingEnemy`, `Week18Obstacle0`, `Week19Tune1` 검증기가 단독으로
  통과했다. Play 체감은 아직 확인하지 않았다.

### 5.3 남은 것

단독 실행에서도 실패하며 아직 조치하지 않았다.

| 검증기 | 실패 원인 | 판단 |
|---|---|---|
| `Week15Boss1`(+`Boss2`·`3`·`4`) | 부하가 보스 그림을 임시 재사용하길 기대 | 낡음. 한 줄 수정 대상 |
| `Week14Room4`(+`Room5`) | 순서를 뒤집은 생성기 사본에 층 설정이 빠짐 | 검사 구성이 낡음. 불변조건은 유효 |
| `Week14Room8` | 카메라가 가로 추적 끝에 닿지 않음 | 미확인 |
| `Week15Enemy0` | 경계 상태의 돌진형이 범위 밖에서 돌진을 시작하지 않음 | 미확인 |
| `Week14Encounter1`·`2`·`3`, `Week14Room6`·`7` | 초기 Encounter 3개 구성을 기대 | 낡음. 폐기 후보 |
| `Week15Enemy2` | 옛 보스 Prefab에 공격 연출 컴포넌트가 없길 기대 | 낡음 |
| `Week15Enemy5`, `Week15EnemyRoleColor` | 초기 스프라이트와 임시 역할 색을 기대 | 낡음. 폐기 후보 |
| `Week17Resource0` | 픽업이 Pit 레이어와 충돌하지 않길 기대 | 낡음. 허용 레이어 추가 대상 |
| `Week13Hud7B` | 재개 버튼 크기 260×52를 기대 | 낡음으로 추정 |
| `Week6Item` | 시험용 아이템 정의를 획득하지 못함 | 미확인 |

`Week13FrontendVerification`은 FrontendScene이 열려 있어야 하는 실행 전제이며 고장이 아니다.

## 6. 재발 방지 기준

- 검증이 느리면 루프 횟수를 줄이기 전에 `-timestamps`로 검증기별 시간을 잰다. 횟수가 큰 루프와 실제로
  느린 루프는 달랐다.
- 게임에서 한 번 하는 검증을 검증기가 seed 루프로 반복하면 그 비용이 seed 수만큼 곱해진다. 순수한
  검사는 읽은 값이 같을 때 결과를 재사용한다.
- 실패하는 검증기를 묶음에 남겨 두지 않는다. 고치거나 뺀다. 뒤에 있는 검증기의 문제를 가린다.
- 실패하는 옛 검증기를 지우기 전에 실패 원인을 읽는다. 40개 중 하나는 실제 회귀였다.
- 이름이 `Verify`인 메서드는 Setup을 호출하지 않는다. Setup이 필요하면 `SetupAndVerifyBatch`로 나눈다.
- 같은 Prefab을 쓰는 Setup이 둘 이상이면 뒤 주차의 값을 앞 주차 Setup이 덮어쓰지 않게 한다. 기존
  컴포넌트의 조정값은 유지하고 새로 붙일 때만 기본값을 넣는다.
- 원화를 연결하는 Setup이 자리표시 색·스프라이트·크기를 바꾸면, 그 값을 직접 비교하던 옛 검증기를 같은
  조각에서 함께 고친다.
