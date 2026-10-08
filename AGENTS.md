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
- UI 글꼴은 `Week13FrontendSetup.FontPath`의 TMP 글꼴 에셋(원본 글꼴 ONE Mobile POP, `DefaultFontSetup`)을 쓴다. 다른 글꼴
  에셋을 새로 만들지 않는다.
- 직접적인 Scene/Prefab 대규모 텍스트 편집보다 Editor 구성 코드나 작은 직렬화 변경을 우선 검토한다.
- 검증기는 핵심 불변조건이 깨지면 명시적으로 실패하도록 작성한다.
- 아티팩트나 스펠(일회용 스펠·짱셈스펠 포함)을 구현하면 같은 조각에서 Game Scene 개발 패널(`DevelopmentGamePanel`, `F1`)로
  바로 얻을 수 있게 한다. `— Artifact —`·`— Spell slot —`의 `Drop` 목록은 에셋을 자동 수집하므로 목록에 나오는지 확인하고,
  자동 수집되지 않는 종류나 확인에 필요한 상태 표시는 패널에 추가한다. 수동 확인 항목에는 패널 경로를 적는다.
- Unity 배치 실행은 Licensing Client와 Package Manager가 프로젝트 밖의 `%LOCALAPPDATA%\Unity` 상태 및 로컬 IPC에 접근하므로, 제한된 샌드박스에서 먼저 실행하지 말고 처음부터 권한이 허용된 실행을 요청한다.
- Unity 배치 검증시 보통 같은 프로젝트를 연 Unity 인스턴스가 있어서 실행이 차단될 때가 있음. 이는 코드 실패가 아니라 Unity의 다중 인스턴스 잠금이니 넘어가도 무방함.

## 이펙트 제작

- 이펙트는 여러 독립된 효과를 겹쳐 구성해도 된다. 하나의 이미지나 스프라이트 시트에 모든 요소를 합칠 필요가 없다.
- 크기·방향·타이밍·투명도를 다르게 조절해야 하는 요소는 별도 에셋·표시 레이어로 제작하고 Unity에서 합성한다.
- 검광과 지면 충격은 분리한다. 검광의 높이·두께와 지면 균열·돌 파편의 크기는 각각 독립적으로 조절한다. 검광을 키우려고 돌 파편까지 함께 확대하지 않는다.
- 용사용 내려찍기는 위에서 내려오는 검광과 바닥에 닿을 때의 지면 충격을 별도 효과로 구성한다. 지면 효과의 상하 방향·바닥 기준점과 캐릭터 가독성을 보존한다.

## 검증과 문서화

- 변경 위험에 비례하여 가장 가까운 자동 테스트부터 실행한다.
- Backend 변경은 `backend/`에서 관련 Jest 테스트와 필요 시 `pnpm build`를 실행한다.
- Web 변경은 `web/`에서 `pnpm lint`와 필요 시 `pnpm build`를 실행한다.
- Unity 자동 검증을 실행할 수 없으면 가능한 정적 검사를 수행하고, 정확한 메뉴 경로와 기대 결과를 수동 검증 항목으로 남긴다.
- 자동 검증 또는 사용자가 제공한 수동 확인 증거가 있는 항목만 계획 문서에서 완료 처리한다.
- 새 개발 도구의 계획과 상태는 `docs/13-development-tooling-plan.md`, 실제 실행 방법은 구현 후 `docs/05-development-setup.md`에 기록한다.

## 커밋과 PR

- 커밋 제목은 `type(scope): 영어 요약 (조각 ID)` 형식을 쓴다. 예: `feat(item): melune card duplication (Jjangsem-1)`.
  type은 `feat`, `fix`, `tune`, `docs`, `refactor`, `test`, `chore` 중에서 고른다. scope와 조각 ID는 해당할 때만 붙인다.
- 작업은 `dev/<주차 또는 주제>` 브랜치에서 하고, `main`에는 직접 커밋하지 않고 PR로 병합한다.
- PR 본문은 `.github/pull_request_template.md`를 따른다. 검증 항목은 실제로 실행한 것만 체크한다.
- 관련 없는 변경은 같은 PR에 섞지 않는다. 새 Unity 에셋의 `.meta`는 같은 커밋에 포함한다.

## 작업별 스킬 사용

- 커밋·푸시·PR 요청에는 `.agents/skills/ship/SKILL.md`를 사용하고, 사용자가 요청한 단계까지만 수행한다.
- 커밋 전에는 `.agents/skills/pre-commit-check/SKILL.md`로 이번 커밋 범위를 점검한다.
- Prisma, API DTO 또는 Item·Character·Run ID 계약 변경 시 `.agents/skills/contract-drift-check/SKILL.md`를 사용한다.
- Unity 변경 검증 시 `.agents/skills/unity-verification-runner/SKILL.md`로 관련 검증기와 실행 방법을 확인한다.
- `.claude/hooks/protect-main.js`는 Claude 설정에 연결된 훅이다. Codex에서 자동 실행된다고 가정하지 않고, 커밋·푸시 전에 브랜치와 대상 ref를 직접 확인한다.
