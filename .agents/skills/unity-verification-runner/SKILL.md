---
name: unity-verification-runner
description: TrickalFanGame Unity 변경에 맞는 현재 Editor 검증기를 찾아 배치 실행하고 결과를 확인한다. 실행 제한이나 화면 검증은 정확한 메뉴·기대 결과를 안내한다. Unity 검증, 검증기 실행 또는 필요한 검증 안내 요청에 사용한다.
---

# Unity Verification Runner

현재 코드에서 검증기를 찾는다. 예전 주차 목록이나 “Unity 실행 불가”를 고정 전제로 사용하지 않는다. 안내만 요청했다면 실행하지 않고 필요한 메뉴·배치 방법을 제공한다.

## 실행 단계

검증기를 고르기 전에 지금 단계에서 무엇을 돌릴지 먼저 정한다. 사용자가 범위를 지정하면 그 지정을 따른다.

| 상황 | 실행 |
|---|---|
| 작업 중, 수정할 때마다 | 해당 조각의 단독 `Verify` 하나 |
| 커밋·ship 직전 | 해당 조각의 회귀 묶음을 최종 변경에 대해 한 번 |
| 문서·주석·원화 PNG만 변경 | Unity 배치 생략 |
| 생성기·Room 계약 등 여러 조각이 쓰는 기반 변경 | 작업 중에도 회귀 묶음 |

- 회귀 묶음은 다른 묶음을 연쇄 호출해 검증기 십수 개와 대량 seed 루프를 돌린다. 단독 `Verify`로 확인되는 수정에 쓰지 않는다.
- 배치는 띄울 때마다 컴파일·에셋 리프레시 비용이 붙는다. 같은 변경에 검증기 여러 개가 필요하면 실행을 나누지 말고 이미 있는 묶음 진입점 하나로 돌린다.
- 같은 최종 변경에 대해 통과한 결과는 재사용하고, 영향을 주는 수정이 있을 때만 다시 실행한다.

## 자주 쓰는 대응표

아래에 있으면 검색 없이 이 진입점을 쓴다. 클래스는 모두 `TrickalFanGame.Editor` 네임스페이스다. 실행 전 메서드가 아직 있는지와 Setup 호출 여부만 본문에서 확인한다. 표에 없는 변경은 아래 "검증기 선택" 절차로 찾는다.

| 변경 대상 | 단독 | 커밋 전 회귀 |
|---|---|---|
| `game/RoomLayouts/*.room.txt`, `RoomLayoutFormat`, `RoomLayoutImporter` | `RoomLayoutImporterVerification.Verify` (방 파일을 고쳐 다시 들여와야 하면 `ImportAndVerifyBatch`, 에셋 저장함) | `RoomLayoutImporterVerification.VerifyWithRegressionsBatch` |
| `FairyVillageArtworkSetup`, 요정마을 방 그림 | `FairyVillageArtworkVerification.Verify` | 순수 회귀 묶음 없음. 나머지 배치는 Setup 재실행·렌더를 포함한다 |
| `FairyKingdomArtworkSetup`, 요정왕국 에셋 연결 | `FairyKingdomArtworkVerification.Verify` | `FairyKingdomArtworkVerification.VerifyWithCombatRegressionsBatch` |
| 지면 그림자 | `GroundShadowVerification.Verify` | — |
| 구덩이 타일 그림 | `PitTileArtworkVerification.Verify` | — |
| 상태 이상 이펙트 | `StatusEffectAnimationVerification.Verify` | `StatusEffectAnimationVerification.VerifyWithRegressionsBatch` |
| 검수된 전투 이펙트 | `ReviewedCombatEffectsVerification.Verify` | `ReviewedCombatEffectsVerification.VerifyWithRegressionsBatch` |
| `DevelopmentGamePanel` | `Week20DevPanelVerification.Verify` | — |
| 전투 효과음(`CombatSfxSetup`, `PlayerCombatAudio`, `GameAudioController` 큐) | `CombatSfxVerification.Verify` (클립을 바꿔 다시 들여와야 하면 `SetupAndVerifyBatch`, Scene 저장함) | `CombatSfxVerification.VerifyWithRegressionsBatch` |
| 사용자 제공 원화 연결 | `UserArtworkVerification.Verify` | — |
| 주차 조각(`Week<NN><조각>Setup`과 그 런타임 코드) | 같은 이름의 `Week<NN><조각>Verification.Verify` | 같은 클래스의 `VerifyWithRegressionsBatch` (있을 때) |

## 검증기 선택

1. `git status --short`와 diff, 사용자가 지정한 조각에서 변경된 동작과 공용 기반의 영향 범위를 확인한다.
2. `game/Assets/Editor/`에서 변경 타입·조각 ID·불변조건으로 검색한다. 예: `rg -n 'MenuItem|public static void' game/Assets/Editor/Week22Jjangsem1Verification.cs`.
3. 후보 검증기의 본문과 의존 Setup을 읽어 실제 검사 범위, 실행 전제, 로그 성공 메시지와 실패 방식을 확인한다. `docs/05-development-setup.md`의 실행법도 대조한다.
4. 완전한 namespace·class·public static method를 코드에서 확인한다. `Verify`, `SetupAndVerifyBatch`, `VerifyWithRegressionsBatch` 등 이름만으로 배치 호환성이나 부작용을 단정하지 않는다.

## 배치 실행

- `game/ProjectSettings/ProjectVersion.txt`로 필요한 Unity 버전을 확인하고 설치된 해당 Editor 경로를 찾는다. 확인되지 않은 버전이나 메서드로 실행하지 않는다.
- Setup을 호출하는 검증기는 Scene·Prefab·에셋을 저장할 수 있다. 현재 변경과 겹치는지 확인하고, 순수 Verify가 충분하면 그것을 우선한다. 이번 구현의 구성 작업 범위를 벗어나는 재생성이 필요하면 검증을 위해 사용자 변경을 덮어쓰지 않는다.
- 배치는 Licensing Client와 Package Manager의 프로젝트 밖 상태·로컬 IPC 접근 때문에 처음부터 `exec_command`의 `sandbox_permissions: require_escalated`로 실행한다. 제한된 샌드박스에서 먼저 시험하지 않는다.
- PowerShell에서는 확인한 실행 파일을 `& <Unity 경로>`로 호출하고, `-batchmode -quit -projectPath <game 절대 경로> -executeMethod <확인한 정적 메서드> -logFile <로그 절대 경로>`를 전달한다. 그래픽을 필요로 하지 않는 검증에만 `-nographics`를 추가한다.
- 메서드가 Edit Mode·Play Mode 전환, 프레임 대기, 그래픽 렌더링이나 열린 Scene을 요구하면 본문의 지원 배치 진입점 또는 해당 전제를 사용한다. 지원되지 않으면 수동 확인으로 남긴다.
- 종료 코드와 해당 검증의 성공 로그를 함께 확인하고 컴파일 오류·예외·검증 실패를 검사한다. 로그는 UTF-8을 명시해 읽는다. 실행 전후 git diff로 생성·저장된 변경도 확인한다.

## 제한과 수동 확인

- 같은 프로젝트의 Unity 인스턴스 잠금이면 배치 미실행으로 보고한다. 열려 있는 Editor를 임의로 종료하지 않고 정확한 MenuItem 경로와 기대 로그를 제공한다.
- 설치·버전·권한·라이선스·패키지 문제는 코드 검증 실패와 구분한다. 가능한 정적 검사와 미확인 항목을 남긴다.
- 자동 검증과 실제 화면·입력 Play 확인은 별도 증거다. 자동 통과만으로 화면 항목까지 완료 처리하지 않는다.

## 보고

선택한 검증기와 이유, 실제 배치 명령·메서드, 종료 코드·성공 메시지, 정적 확인 결과와 미실행 이유를 보고한다. 수동 확인은 코드에서 읽은 정확한 메뉴 경로, 필요한 Scene·Edit/Play 상태, 조작과 기대 결과를 남긴다. 계획 문서에는 자동 검증 또는 사용자가 제공한 수동 근거가 있는 항목만 완료 표시한다.
