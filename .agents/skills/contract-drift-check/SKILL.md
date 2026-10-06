---
name: contract-drift-check
description: TrickalFanGame의 Prisma·API DTO·Item·Character·Run 계약 변경이 Backend, Unity, Web, seed와 문서에 일치하는지 검사한다. 계약 검증이나 ID 불일치 조사에도 사용한다.
---

# Contract Drift Check

변경된 계약과 실제 소비자를 추적해 불일치와 미확인 범위를 보고한다. 점검 요청만으로 코드 수정이나 DB seed·마이그레이션 실행을 시작하지 않는다.

## 기준 소스 찾기

| 계약 | 현재 시작점 |
|---|---|
| Backend 아이템 | `backend/src/contracts/item-catalog.ts`의 ITEM_CATALOG, 이를 가져오는 `backend/prisma/seed.ts` |
| Backend 캐릭터 | `backend/prisma/seed.ts`, 캐릭터 조회·검증 서비스 |
| Unity 아이템 | `game/Assets/Scripts/Item/ItemDefinition.cs`, ItemKind·ItemRarity·ItemEffectType, `game/Assets/Items/` 하위 에셋과 Editor 카탈로그·Setup |
| Unity 캐릭터 | `game/Assets/Scripts/Character/CharacterDefinition.cs`, `game/Assets/Characters/` 하위 에셋 |
| Run API | `backend/src/modules/runs/dto/create-run.dto.ts`, 서비스·저장소, `game/Assets/Scripts/Network/RunDto.cs` |
| Web 소비자 | `web/src/lib/api-client.ts`와 변경 필드·엔드포인트의 실제 소비 코드 |
| 문서 | `docs/06-database.md`, `docs/07-api.md`, 해당 게임 규칙·실행 계획 |

이 경로는 검색 시작점이다. 현재 코드의 import와 타입·필드 검색으로 실제 기준과 호출자를 확인한다. seed의 문자열 전체를 ID로 추출하거나, 모든 `.asset`을 ItemDefinition으로 취급하지 않는다. Unity YAML의 m_Script GUID와 대응 `.cs.meta`를 확인해 대상 타입을 구분한다.

## ID와 아이템 메타데이터

1. Backend 카탈로그의 ID와 Unity 정의 에셋의 ID를 수집한다. 하위 폴더도 탐색하고 빈 ID·중복을 확인한다. 중복 참조 목록과 중복 정의 에셋을 구분한다.
2. 기존 ID의 의미 변경·재사용을 diff에서 확인한다. `item-`, `artifact-`, `spell-`, `single-spell-`, `jjangsem-` 등 종류별 허용 규칙은 현재 ItemDefinition과 ItemKind 구현에서 읽는다. `item-NN`만 허용하는 검사를 하지 않는다.
3. Backend에 저장·조회·검증되는 아이템과 Unity 내부 전용 아이템을 구분한다. 스펠이 Backend 카탈로그에 없다는 이유만으로 FAIL로 처리하지 않는다. Run DTO 구성과 Backend 수락 로직에서 실제 전송되는 종류를 확인한다.
4. API로 전달되는데 Backend가 수락하지 않는 ID, 빈·중복 정의, 안정 키 재사용은 FAIL이다. Backend에만 있는 ID는 비활성·레거시·미구현 정책과 실제 활성 획득 경로를 확인해 심각도를 판단한다.
5. 공유 계약의 rarity, isActive, maxStacks, effect type과 매개변수·단위·기본값을 비교한다. Unity의 숫자 enum은 이름·직렬화 번호를 해석해 Backend 문자열에 대응시키며 기존 번호 변경을 확인한다. 등급 목록은 현재 ItemRarity와 Backend 타입에서 읽는다.

## Prisma·DTO·소비자

- 변경 필드의 이름, 필수/선택 여부, null, 배열 형태, 날짜 표현, 값 범위·단위와 기본값을 실제 DTO·serializer·validator·서비스에서 비교한다. 오래된 예시 필드 목록을 정답으로 삼지 않는다.
- Prisma 변경은 DTO·서비스·저장소·마이그레이션·seed와 Unity/Web 사용처를 확인한다. 소비자가 영향을 받지 않으면 그 근거를 남긴다.
- Run 저장·보상·경험치 변경은 재전송 시 중복 생성·지급 방지와 트랜잭션 경계를 관련 테스트에서 확인한다.
- 관련 Jest 계약·DTO·서비스 테스트를 실행한다. Unity는 필요 시 [unity-verification-runner](../unity-verification-runner/SKILL.md)를 따른다. 기존 Editor 계약 검증기는 Unity 카탈로그와 에셋을 검사할 수 있지만 Backend까지 비교한다고 가정하지 않는다.

## 결과

검사한 기준 파일과 소비자, 실제 차이의 파일·필드·ID, FAIL/WARN과 이유, 실행한 테스트, 미확인 범위를 보고한다. 코드 비교는 로컬 DB가 최신 seed라는 증거가 아니므로 DB 상태를 확인하지 않았으면 별도로 밝힌다. ID나 계약을 점검 중 임의로 변경하지 않는다.
