---
name: unity-verification-runner
description: 현재 변경에 해당하는 Unity Editor 검증기 목록을 생성하고 실행 가이드를 제공한다. 검증기 메뉴 경로, 기대 결과, 체크리스트를 출력한다. "Unity 검증", "검증기 실행", "verification" 요청 시 사용.
allowed-tools: Bash, Glob, Grep, Read
---

# Unity Verification Runner

Unity Editor에서 수동으로 실행해야 하는 검증기를 안내하고 체크리스트를 제공한다.

## 목적

Claude는 Unity Editor를 직접 실행할 수 없으므로:
1. 현재 변경에 해당하는 검증기 식별
2. 메뉴 경로와 기대 결과 안내
3. 사용자가 결과를 보고하면 문서에 반영

## 검증기 목록

### 전체 검증기

| 검증기 | 메뉴 경로 | 관련 파일 |
|---|---|---|
| PlayerStats & Item | `Trickal Fan Game > Verify PlayerStats and Item Effects` | `Scripts/Item/`, `Scripts/Player/PlayerStats.cs`, `PlayerInventory.cs` |
| Damage Context | `Trickal Fan Game > Verify Damage Context and Projectile` | `Scripts/Combat/DamageContext.cs`, `DamageCalculator.cs`, `Projectile.cs` |
| Player Combat Events | `Trickal Fan Game > Verify Player Combat Events` | `Scripts/Combat/PlayerCombatEvents.cs` |
| Room Graph | `Trickal Fan Game > Verify Fixed Room Graph` | `Scripts/Room/RoomNode.cs`, `RoomGraphController.cs`, `RoomDoorway.cs` |
| Lower Grade Skill | `Trickal Fan Game > Verify Phase C Lower Grade Skill` | `Scripts/Player/PlayerSkill.cs`, `PlayerSP.cs`, `HomingSkillProjectile.cs` |
| High Grade Skill | `Trickal Fan Game > Verify Phase D High Grade Skill` | `Scripts/Player/PlayerSkill.cs` (고학년 돌진) |

## 변경 파일 → 검증기 매핑

### 자동 매핑 규칙

```text
변경 파일 패턴                          → 필요한 검증기
─────────────────────────────────────────────────────────────
Scripts/Item/*                          → PlayerStats & Item
Scripts/Player/PlayerStats.cs           → PlayerStats & Item
Scripts/Player/PlayerInventory.cs       → PlayerStats & Item

Scripts/Combat/DamageContext.cs         → Damage Context
Scripts/Combat/DamageCalculator.cs      → Damage Context
Scripts/Combat/Projectile.cs            → Damage Context

Scripts/Combat/PlayerCombatEvents.cs    → Player Combat Events

Scripts/Room/RoomNode.cs                → Room Graph
Scripts/Room/RoomGraphController.cs     → Room Graph
Scripts/Room/RoomDoorway.cs             → Room Graph
Scripts/Room/RoomCameraController.cs    → Room Graph

Scripts/Player/PlayerSkill.cs           → Lower Grade Skill, High Grade Skill
Scripts/Player/PlayerSP.cs              → Lower Grade Skill
Scripts/Combat/HomingSkillProjectile.cs → Lower Grade Skill
```

## 실행 가이드

### 검증기 실행 방법

1. Unity Editor에서 프로젝트 열기
2. 메뉴바에서 `Trickal Fan Game` 클릭
3. 해당 검증기 메뉴 선택
4. Console 창에서 결과 확인

### 성공 시 출력 예시

```
PlayerStats verification passed: stat ownership, HP and attack behavior,
item stacking, both synergy acquisition orders, stack caps, and kill
healing values are valid.
```

### 실패 시 출력 예시

```
InvalidOperationException: PlayerStats should own the base max HP
synchronized to Health.
```

## 체크리스트 형식

변경 내용을 분석한 후 다음 형식으로 체크리스트를 제공한다:

```markdown
## Unity 수동 검증 체크리스트

### 필수 검증 (변경된 파일 기준)

- [ ] **PlayerStats & Item Effects**
  - 메뉴: `Trickal Fan Game > Verify PlayerStats and Item Effects`
  - 기대 결과: "PlayerStats verification passed: ..." 메시지
  - 변경 이유: `PlayerInventory.cs` 수정됨

- [ ] **Damage Context**
  - 메뉴: `Trickal Fan Game > Verify Damage Context and Projectile`
  - 기대 결과: 검증 통과 메시지
  - 변경 이유: `DamageCalculator.cs` 수정됨

### 권장 검증 (영향받을 수 있음)

- [ ] **Player Combat Events**
  - 메뉴: `Trickal Fan Game > Verify Player Combat Events`
  - 이유: 전투 관련 변경이 이벤트 흐름에 영향 가능

### Play Mode 확인

- [ ] 게임 시작 → 아이템 획득 → 스탯 변화 확인
- [ ] 적 처치 → 이벤트 발생 확인
- [ ] 방 이동 → 상태 유지 확인

### 결과 보고

검증 완료 후 다음 형식으로 결과를 알려주세요:
- 통과: "PlayerStats 검증 통과"
- 실패: "Damage Context 검증 실패: [오류 메시지]"
```

## 실행 흐름

```text
1. 사용자 요청
   "Unity 검증 필요한 거 알려줘" 또는 "/unity-verification-runner"

2. 변경 파일 분석
   git status 또는 최근 작업 내용 기반

3. 검증기 매핑
   변경 파일 → 해당 검증기 식별

4. 체크리스트 출력
   메뉴 경로, 기대 결과, 변경 이유 포함

5. 사용자 실행 & 결과 보고

6. 문서 반영 (선택)
   docs/10-second-month-plan.md 체크박스 갱신
```

## 검증기별 상세 정보

### 1. PlayerStats & Item Effects

**파일:** `Week6ItemVerification.cs`

**검증 내용:**
- PlayerStats가 기본 스탯(HP, 공격력, 이동속도)의 단일 소유자인지
- 아이템 획득 시 스탯이 올바르게 변경되는지
- 아이템 스택이 정상 동작하는지
- 시너지(MULTI_SHOT + PIERCE)가 양방향 순서로 활성화되는지
- 스택 상한이 적용되는지
- HealOnKill이 정상 동작하는지

**실패 시 확인:**
- PlayerStats 컴포넌트가 플레이어에 있는지
- ItemDefinition 에셋이 올바른 경로에 있는지

### 2. Damage Context

**파일:** `Week7DamageContextVerification.cs`

**검증 내용:**
- DamageContext가 올바른 값으로 생성되는지
- DamageCalculator가 스탯을 정확히 반영하는지
- 투사체가 DamageContext를 올바르게 전달하는지

### 3. Player Combat Events

**파일:** `Week7PlayerCombatEventsVerification.cs`

**검증 내용:**
- 적 처치 시 OnEnemyKilled 이벤트 발생
- HealOnKill 등 이벤트 소비자가 정상 동작

### 4. Room Graph

**파일:** `Week7RoomGraphVerification.cs`

**검증 내용:**
- RoomNode가 고유 roomId를 가지는지
- 양방향 연결이 올바른지
- 방 활성화/비활성화가 정상 동작하는지
- 클리어 상태가 보존되는지

### 5. Lower Grade Skill

**파일:** `Week7LowerGradeSkillVerification.cs`

**검증 내용:**
- SP 획득 및 소모
- 저학년 스킬 발동 및 피해
- 호밍 투사체 동작

### 6. High Grade Skill

**파일:** `Week7HighGradeSkillVerification.cs`

**검증 내용:**
- 고학년 돌진 스킬 발동
- 돌진 중 상태 및 제한
- 쿨타임 동작

## 스킬 호출 예시

```
사용자: "Unity 검증해야 할 거 알려줘"
사용자: "/unity-verification-runner"
사용자: "PlayerInventory 수정했는데 뭐 검증해야 해?"
사용자: "검증기 체크리스트 줘"
```

## 관련 파일

| 역할 | 경로 |
|---|---|
| 검증기 코드 | `game/Assets/Editor/Week*Verification.cs` |
| 개발 도구 계획 | `docs/13-development-tooling-plan.md` |
| 실행 계획 | `docs/10-second-month-plan.md` |
