---
name: contract-drift-check
description: Unity와 Backend 간 ID 및 계약 불일치를 검사한다. Item ID, Character ID, 등급 enum, DTO 필드가 양쪽에서 일치하는지 확인하고 드리프트를 리포트한다. "계약 검증", "ID 불일치", "contract check" 요청 시 사용.
allowed-tools: Bash, Glob, Grep, Read
---

# Contract Drift Check

Unity와 Backend 간 ID 및 계약 불일치를 사전에 검출한다.

## 검증 목적

- Unity Asset에 정의된 ID가 Backend seed에 없으면 → 런타임에 `ITEM_NOT_FOUND` 거절
- Backend seed에만 있고 Unity에 없으면 → 해당 아이템이 게임에서 드롭 불가
- 등급(Rarity) enum 불일치 → 가중치 드롭 로직 오류

## 검증 흐름

```text
1. Backend ID 수집
   → backend/prisma/seed.ts에서 Item/Character ID 추출

2. Unity ID 수집
   → game/Assets/Items/*.asset에서 itemId 추출
   → game/Assets/Characters/*.asset에서 characterId 추출

3. 집합 비교
   → Backend에만 있는 ID
   → Unity에만 있는 ID
   → 양쪽에 있는 ID (정상)

4. 추가 계약 검증
   → 등급 enum 일치
   → 중복/빈 ID 검출
   → DTO 필드 일치 (선택)

5. 리포트 출력
```

## 검증 항목

### 1. Item ID 검증

**Backend 소스:**
```
backend/prisma/seed.ts
```

Item 배열에서 ID 추출:
```typescript
const items = [
  ['item-01', '공격력 강화', '...', 'COMMON'],
  // ...
]
```

**Unity 소스:**
```
game/Assets/Items/*.asset
```

각 .asset 파일에서 `itemId:` 필드 추출:
```yaml
itemId: item-01
```

**검증 규칙:**
- Backend seed에 있는 모든 Item ID는 Unity에 .asset 파일이 있어야 함 → 없으면 WARN
- Unity .asset의 itemId는 Backend seed에 있어야 함 → 없으면 FAIL
- 빈 itemId → FAIL
- 중복 itemId → FAIL

### 2. Character ID 검증

**Backend 소스:**
```typescript
const character = {
  id: 'erpin',
  // ...
};
```

**Unity 소스:**
```
game/Assets/Characters/*.asset
```

**검증 규칙:**
- 동일한 규칙 적용

### 3. 등급(Rarity) Enum 검증

**허용 값:**
```
COMMON | UNCOMMON | RARE | EPIC
```

**검증:**
- Backend seed의 rarity 값이 허용 목록에 있는지
- Unity에서 등급을 사용한다면 enum 값 일치 확인

### 4. 중복/빈 ID 검출

- 같은 ID가 여러 .asset 파일에 정의됨 → FAIL
- itemId가 빈 문자열이거나 공백만 있음 → FAIL
- ID 형식이 `item-NN` 패턴을 벗어남 → WARN (권장 형식)

### 5. DTO 필드 일치 (확장)

**Run 생성 시 필수 필드:**

| 필드 | Unity DTO | Backend DTO |
|---|---|---|
| clientRunId | `RunDto.clientRunId` | `CreateRunDto.clientRunId` |
| characterId | `RunDto.characterId` | `CreateRunDto.characterId` |
| items | `AcquiredItemDto[]` | `CreateRunItemDto[]` |
| startedAt | ISO 8601 | ISO 8601 |
| endedAt | ISO 8601 | ISO 8601 |

## 실행 방법

### 자동 검증

```bash
# 1. Backend Item ID 추출
grep -oP "'\w+-\d+'" backend/prisma/seed.ts | tr -d "'" | sort -u

# 2. Unity Item ID 추출
grep -h "itemId:" game/Assets/Items/*.asset | sed 's/.*itemId: //' | sort -u

# 3. 비교
comm -23 <(backend_ids) <(unity_ids)  # Backend에만 있음
comm -13 <(backend_ids) <(unity_ids)  # Unity에만 있음
```

### 스킬 호출

```
/contract-drift-check
```
또는 "계약 검증해줘", "ID 불일치 확인", "contract check"

## 리포트 형식

```markdown
# Contract Drift Check Report

## Item ID 비교

### Backend seed (11개)
item-01, item-02, item-03, item-04, item-05, item-06,
item-07, item-08, item-09, item-10, item-11

### Unity Assets (6개)
item-01, item-02, item-03, item-06, item-08, item-11

### 불일치

#### WARN: Backend에만 있음 (Unity 미구현)
- item-04 (공격 속도 강화)
- item-05 (투사체 크기 강화)
- item-07 (공격 범위 강화)
- item-09 (피격 반격)
- item-10 (추가 공격)

#### FAIL: Unity에만 있음 (Backend 누락)
- (없음)

## Character ID 비교

### Backend seed
erpin

### Unity Assets
erpin

### 불일치
- (없음) ✓

## 등급 Enum 검증
- COMMON ✓
- RARE ✓
- EPIC ✓

## 중복/빈 ID
- (없음) ✓

## 요약
- FAIL: 0
- WARN: 5 (미구현 아이템)
- PASS: Character ID 일치, 등급 일치
```

## 주의 사항

- Backend seed에만 있고 Unity에 없는 것은 "미구현"이므로 WARN
- Unity에만 있고 Backend에 없는 것은 런타임 오류 발생하므로 FAIL
- 새 아이템 추가 시 양쪽 모두 반영해야 함

## 관련 파일

| 역할 | 경로 |
|---|---|
| Backend Item seed | `backend/prisma/seed.ts` |
| Unity Item 에셋 | `game/Assets/Items/*.asset` |
| Unity Item 정의 | `game/Assets/Scripts/Item/ItemDefinition.cs` |
| Backend Character seed | `backend/prisma/seed.ts` |
| Unity Character 에셋 | `game/Assets/Characters/*.asset` |
| Unity Run DTO | `game/Assets/Scripts/Network/RunDto.cs` |

## 도구 계획 연동

이 스킬은 `docs/13-development-tooling-plan.md`의 **T3 (Unity ↔ Backend 계약 검사)** 항목과 연동된다. 아이템 등급이나 메타 데이터 계약을 확장하기 전에 실행을 권장한다.
