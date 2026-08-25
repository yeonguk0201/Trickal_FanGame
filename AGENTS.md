# Repository Instructions

## Encoding

- 모든 텍스트 파일은 UTF-8로 생성, 편집, 저장한다.
- 스크립트에서 텍스트 파일을 읽거나 쓸 때는 UTF-8 인코딩을 명시한다.
- Windows 기본 코드 페이지인 CP949/EUC-KR에 의존하지 않는다.

## 작업 범위와 기준 문서

- 사용자 변경과 관련 없는 작업을 보존하고, 수정 전에 `git status --short`를 확인한다.
- 사용자가 지정한 범위와 완료 조건을 최우선으로 따른다.
- 현재 실행 범위는 `docs/10-second-month-plan.md`, 시스템 의존성은 `docs/12-skill-system-plan.md`, 장기 범위는 `docs/02-roadmap.md`를 기준으로 확인한다.
- 날짜보다 확정된 시스템 의존성 순서를 우선한다.
- 필수 MVP 흐름이 안정되기 전에는 선택 기능, 폴리싱, 랜덤화가 필수 작업을 밀어내지 않게 한다.

## 데이터와 계약

- Item, Character, Run 관련 영문 ID는 안정 키로 취급하며 기존 ID의 의미를 바꾸거나 재사용하지 않는다.
- Prisma 또는 API 계약을 변경하면 Backend DTO와 서비스뿐 아니라 Unity DTO, Web 소비 코드, seed, 관련 문서에 미치는 영향을 함께 확인한다.
- Run 저장, 보상, 경험치처럼 재전송될 수 있는 작업은 중복 생성이나 중복 지급을 방지한다.
- Unity와 Web은 Database에 직접 접근하지 않고 Backend API를 사용한다.

## Unity

- 런타임 규칙을 Editor Setup 코드에 넣지 않는다. Setup 코드는 Scene, Prefab, ScriptableObject와 직렬화 참조 구성에 집중한다.
- Editor Setup은 재실행 시 중복 오브젝트나 에셋을 만들지 않도록 작성하고, 가능한 경우 Undo와 dirty/save 처리를 제공한다.
- Scene, Prefab, ScriptableObject와 `.meta` 파일의 GUID 참조를 보존한다.
- 직접적인 Scene/Prefab 대규모 텍스트 편집보다 Editor 구성 코드나 작은 직렬화 변경을 우선 검토한다.
- 검증기는 핵심 불변조건이 깨지면 명시적으로 실패하도록 작성한다.
- Unity 배치 검증시 보통 같은 프로젝트를 연 Unity 인스턴스가 있어서 실행이 차단될 때가 있음. 이는 코드 실패가 아니라 Unity의 다중 인스턴스 잠금이니 넘어가도 무방함.

## 검증과 문서화

- 변경 위험에 비례하여 가장 가까운 자동 테스트부터 실행한다.
- Backend 변경은 `backend/`에서 관련 Jest 테스트와 필요 시 `pnpm build`를 실행한다.
- Web 변경은 `web/`에서 `pnpm lint`와 필요 시 `pnpm build`를 실행한다.
- Unity 자동 검증을 실행할 수 없으면 가능한 정적 검사를 수행하고, 정확한 메뉴 경로와 기대 결과를 수동 검증 항목으로 남긴다.
- 자동 검증 또는 사용자가 제공한 수동 확인 증거가 있는 항목만 계획 문서에서 완료 처리한다.
- 새 개발 도구의 계획과 상태는 `docs/13-development-tooling-plan.md`, 실제 실행 방법은 구현 후 `docs/05-development-setup.md`에 기록한다.
