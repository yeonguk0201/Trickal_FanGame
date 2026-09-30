---
name: pre-commit-check
description: 커밋 전에 빼먹기 쉬운 검증 항목을 자동으로 확인한다. Unity .meta 파일, 인코딩, 테스트 실행, ID 계약, 문서 정합성을 점검하고 누락된 항목을 리포트한다. "커밋 전 검증", "pre-commit", "검증해줘" 요청 시 사용.
allowed-tools: Bash, Glob, Grep, Read
---

# Pre-Commit Check

커밋하기 전에 빼먹기 쉬운 항목을 검증하고 누락을 사전에 방지한다.

## 검증 흐름

```text
1. git status로 변경 파일 수집
2. 계층별 분류 (game/, backend/, web/, docs/)
3. 각 계층 검증 실행
4. 리포트 출력 (FAIL/WARN/PASS)
```

## 검증 항목

### 1. Unity 변경 시 (game/)

#### 1.1 .meta 파일 검증

새로 추가된 `.cs`, `.asset`, `.prefab`, `.unity` 파일에 대응하는 `.meta` 파일이 있는지 확인한다.

```bash
# 새 파일 중 .meta 누락 확인
git status --porcelain game/
```

- 새 파일에 `.meta` 없으면 → FAIL
- `.meta`만 단독 추가/삭제 → WARN
- 삭제된 파일의 `.meta`도 삭제 필요 → FAIL

#### 1.2 Unity 검증기 안내

변경 영역에 따라 해당 검증기를 안내한다:

| 변경 파일 패턴 | 검증 메뉴 |
|---|---|
| `Scripts/Item/`, `PlayerStats`, `PlayerInventory` | `Trickal Fan Game > Verify PlayerStats and Item Effects` |
| `DamageContext`, `DamageCalculator` | `Trickal Fan Game > Verify Damage Context` |
| `PlayerCombatEvents` | `Trickal Fan Game > Verify Player Combat Events` |
| `RoomNode`, `RoomGraphController`, `RoomDoorway` | `Trickal Fan Game > Verify Fixed Room Graph` |
| `PlayerSkill`, `HomingSkillProjectile`, `PlayerSP` | `Trickal Fan Game > Verify Lower Grade Skill` |

### 2. Backend 변경 시 (backend/)

#### 2.1 테스트 실행

```bash
cd backend && pnpm test --passWithNoTests
```

- 테스트 실패 → FAIL
- Prisma 스키마 변경 시 마이그레이션 확인 → WARN

#### 2.2 ID/계약 변경

- `prisma/seed.ts`에서 Item/Character ID 변경 시 → Unity 에셋 일치 확인 필요
- DTO 변경 시 → Unity `RunDto.cs`, Web 소비 코드 확인 필요

### 3. Web 변경 시 (web/)

```bash
cd web && pnpm lint && pnpm build
```

- lint 오류 → FAIL
- build 실패 → FAIL

### 4. 공통 검증

#### 4.1 인코딩

- UTF-8 아닌 파일 → WARN
- BOM 있는 UTF-8 → WARN (권장: BOM 없는 UTF-8)

#### 4.2 민감 정보

- `.env`, `credentials`, `secret`, `password` 패턴 포함 파일이 staged → FAIL
- API 키/토큰 패턴 → WARN

#### 4.3 큰 파일

- 1MB 이상 바이너리 → WARN (의도적인지 확인)

### 5. 문서 정합성

#### 5.1 계획 문서 체크박스

`docs/10-second-month-plan.md` 확인:
- `[x]` 항목에 해당 코드가 없음 → WARN
- 코드 추가했는데 `[ ]` 그대로 → WARN (체크 권장)

#### 5.2 새 ID 추가

- 새 Item ID → `docs/03-game-design.md` 반영 권장
- 새 API 엔드포인트 → `docs/07-api.md` 반영 권장

## 리포트 형식

```markdown
# Pre-Commit Check Report

## 변경 요약
- Unity: N files
- Backend: N files
- Web: N files
- Docs: N files

## 검증 결과

### FAIL (커밋 전 수정 필요)
- [ ] 항목...

### WARN (권장 조치)
- [ ] 항목...

### PASS
- [x] 항목...

## Unity 수동 검증 체크리스트
1. [ ] 메뉴 > 검증기 실행
2. [ ] 기대 결과 확인

## 다음 단계
1. FAIL 수정
2. WARN 검토
3. Unity 검증기 실행 후 커밋
```

## 실행 예시

사용자가 다음과 같이 요청하면 이 스킬을 사용한다:
- "커밋 전 검증해줘"
- "pre-commit check"
- "/pre-commit-check"
- "검증해줘"
- "커밋해도 될까?"

## 제한 사항

- Unity Editor 직접 실행 불가 → 검증기 메뉴 경로와 기대 결과만 안내
- 바이너리 파일 내용 검사 불가
- Git hook 자동 실행은 별도 스크립트 필요
