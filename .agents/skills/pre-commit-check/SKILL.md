---
name: pre-commit-check
description: TrickalFanGame 커밋 범위의 Unity meta와 GUID, UTF-8, 계약, 문서 및 필요한 테스트를 점검한다. 커밋 전 점검이나 커밋해도 되는지 확인하는 요청에 사용하며 커밋·푸시를 실행하지 않는다.
---

# Pre-Commit Check

`AGENTS.md`의 규칙으로 이번 커밋 범위만 점검한다. 커밋 자체와 외부 작업은 수행하지 않는다.

## 범위

- `git status --short`와 staged/unstaged diff, 신규 파일을 확인한다. staged 파일만 전체 작업으로 단정하지 않고 사용자가 지정한 범위 또는 이번 대화의 변경을 기준으로 한다.
- 사용자·다른 작업의 변경을 보존하고, 관련 없는 파일을 자동 stage하거나 되돌리지 않는다.
- 먼저 파일 무결성과 계약을 점검한 뒤 변경을 검증하는 가장 가까운 테스트를 실행한다. 같은 최종 변경에 대한 통과 증거가 있으면 재사용한다.

## 파일과 계약

- Unity의 `game/Assets/` 아래 신규 파일과 폴더에는 대응하는 `.meta`가 필요하다. 삭제 시 대응 meta와 남은 참조를 확인한다. 이동·이름 변경은 meta도 함께 이동하고 기존 GUID를 보존한다. 파일 누락, GUID 변경·중복, 깨진 참조는 FAIL이다. Unity 프로젝트 밖의 문서에는 meta를 요구하지 않는다.
- 변경된 텍스트는 명시적 UTF-8 strict 디코딩으로 확인한다. 디코딩 실패는 FAIL이다. UTF-8 BOM은 인코딩 오류로 취급하지 않는다. 바이너리는 텍스트 검사를 적용하지 않는다.
- 커밋 대상에 실제 비밀 값이나 의도하지 않은 Library·node_modules·로그 등의 생성물이 있으면 FAIL이다. 파일명이나 `password` 같은 코드 식별자만으로 비밀 값이라고 단정하지 않는다. 보고서에 비밀 값을 출력하지 않는다.
- Prisma·DTO·카탈로그·직렬화 enum·ID 변경은 [contract-drift-check](../contract-drift-check/SKILL.md)로 영향받는 계층을 확인한다.
- 계획 문서의 완료 표시에는 자동 검증 또는 사용자가 제공한 수동 증거가 있어야 한다. 코드가 존재한다는 이유만으로 완료 체크를 권하지 않는다. 계약·사용법이 바뀌면 관련 기준 문서가 최종 동작과 맞는지 확인한다.

## 영역별 검증

| 영역 | 검증 |
|---|---|
| Backend | `backend/`에서 관련 Jest 테스트를 실행하고, 계약·컴파일 영향이 있으면 `pnpm build`도 실행 |
| Web | `web/`의 AGENTS.md를 확인하고 `pnpm lint`, 빌드 영향이 있으면 `pnpm build` 실행 |
| Unity | [unity-verification-runner](../unity-verification-runner/SKILL.md)로 관련 Editor 검증기를 찾아 실행하고 필요한 수동 확인 기록 |
| 문서·스킬만 | 참조 경로, 문서 정합성, UTF-8을 확인하고 스킬 변경은 skill-creator의 형식 검증 사용 |

Backend 테스트는 필요한 테스트 파일을 지정해 `pnpm test -- --runInBand <관련 spec>`처럼 실행한다. 테스트가 없거나 실행되지 않은 상태를 `--passWithNoTests`로 통과 증거로 만들지 않는다. Prisma 변경은 필요한 마이그레이션·생성 코드·seed 영향을 확인하되 점검만을 위해 DB에 적용하지 않는다.

## 보고

- PASS: 실제 수행한 검사·명령과 통과 근거.
- FAIL: 이번 커밋 전에 해결해야 할 결함과 파일 위치.
- WARN: 의도 확인이나 권장 조치가 필요한 사항.
- 미실행: 도구·권한·Unity 잠금 등으로 확인하지 못한 항목, 이유와 다음 확인 방법.

FAIL, WARN, 미실행을 구분한다. 계획 문서의 완료 처리나 전체 검증 통과 선언은 확인된 범위로 제한한다.
