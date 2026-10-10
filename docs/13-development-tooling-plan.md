# 개발 생산성·검증 인프라 계획

> 기준일: 2026-08-25
>
> 성격: 게임 기능 로드맵과 병행하는 개발 지원 트랙
>
> 원칙: MVP 기능 구현을 막는 선행 프로젝트로 만들지 않고, 반복 비용이나 검증 위험을 실제로 줄일 때 도입한다.

---

## 1. 이 문서의 역할

이 문서는 1~6주차 개발에서 확인된 반복 작업을 자동화하고, Unity → Backend → Web 사이의 검증 누락을 줄이기 위한 도구 계획을 관리한다.

- 게임 기능의 범위와 완료 조건은 [개발 로드맵](./02-roadmap.md), [둘째 달 실행 로드맵](./10-second-month-plan.md), [스킬 & 메타 시스템 계획](./12-skill-system-plan.md)을 따른다.
- 이 문서는 개발 방법을 지원하는 도구의 착수 조건, 산출물과 완료 조건을 정의한다.
- 구현된 도구의 실제 실행 방법은 [개발 환경 설정 가이드](./05-development-setup.md)에 기록한다.
- 실제로 생성된 디렉터리와 책임은 [Project Structure](./08-project-structure.md)에 반영한다.

## 2. 운영 원칙

1. 같은 수동 절차가 두 번 이상 반복되거나, 누락 시 데이터 손상·회귀 위험이 있을 때 자동화를 검토한다.
2. 게임 기능과 함께 검증 가능한 작은 도구부터 만들고 범용 프레임워크를 미리 설계하지 않는다.
3. 개발 도구 작업은 현재 기능의 완료를 직접 돕는 범위로 제한한다.
4. 도구가 검출한 오류는 재현 방법과 실패 원인을 사람이 확인할 수 있게 출력한다.
5. 텍스트 입력과 출력은 UTF-8을 명시하고 Secret이나 개인 식별 정보를 로그에 남기지 않는다.
6. 구현되지 않은 도구의 디렉터리, 명령 또는 API를 확정된 사용법처럼 문서화하지 않는다.

## 3. 전체 순서와 상태

| 단계 | 항목 | 상태 | 권장 시점 | MVP 선행 조건 |
|---|---|---|---|---|
| T0 | 저장소 전용 기능 개발 스킬 | 구성 완료·적용 검증 예정 | 7주차 시작 전 | 아니오 |
| T1 | 루트 `AGENTS.md` 개발 규칙 | 완료 | 7주차 시작 전 | 아니오 |
| T2 | Unity 콘텐츠 생성기와 검증기 정리 | 완료 — Phase E-2 Setup 재실행 및 검증기 통과, 2026-08-26 | 7주차 방·몬스터 구현과 병행 | 아니오 |
| T3 | Unity ↔ Backend 계약 검사 | 예정 | 아이템 등급·메타 데이터 계약 확장 전 | 관련 계약 확장에는 권장 |
| T4 | 플레이테스트 텔레메트리 | 예정 | 전투·방 이벤트 안정 후, 8주차 밸런싱 전 | 밸런싱에는 권장 |
| T5 | Unity MCP | 조건부 | 수동 Editor 조작이 측정 가능한 병목이 된 뒤 | 아니오 |
| T6 | Web UI 상태 테스트 | 완료 — Vitest·Testing Library 6개 테스트 통과, 2026-09-04 | 12주차 통계·랭킹 마감 | 아니오 |
| T7 | 방 Layout 대량 제작 도구(맵 에디터) | 제안 — 미착수, 2026-10-07 ([아래 절](#방-layout-대량-제작-도구-제안-2026-10-07)) | 방 종류 확장을 콘텐츠 범위에 넣을 때 | 아니오 |

## 4. T0 — 저장소 전용 기능 개발 스킬

### 목적

계획 확인 → 영향 범위 조사 → 구현 → 검증 → 체크리스트 갱신 절차를 기능마다 일관되게 반복한다.

### 산출물

- `.agents/skills/trickal-feature-cycle/SKILL.md`

### 검증·출하 보조 스킬 (2026-10-05)

- `.agents/skills/ship/SKILL.md`: 요청된 단계까지 검증·커밋·푸시·PR 처리.
- `.agents/skills/pre-commit-check/SKILL.md`: 커밋 범위의 파일 무결성·계약·테스트 근거 확인.
- `.agents/skills/contract-drift-check/SKILL.md`: 현재 Backend 카탈로그·seed와 Unity·Web 계약 조사.
- `.agents/skills/unity-verification-runner/SKILL.md`: 현재 Editor 검증기 검색·배치 실행·수동 확인 안내.
- 상태: Codex용 절차 구성과 4개 스킬의 이름·frontmatter·참조 경로, 관련 8개 파일의 strict UTF-8 및 diff 공백 검사 통과. 실제 기능의 커밋·PR 및 Unity 배치에 적용하는 운영 검증은 예정.
- 계약 검사 스킬은 조사 절차이며 T3의 독립 자동 검사 도구 완료를 의미하지 않는다.
- Claude 스킬·훅은 유지하며, Codex의 main 보호는 브랜치·푸시 대상 직접 확인 절차로 명시한다.

### 완료 조건

- [x] 저장소에서 자동 발견 가능한 위치에 스킬이 존재한다.
- [x] Unity, Backend, Web 영향 확인과 검증 절차가 포함된다.
- [x] 검증된 항목만 계획서에서 완료 처리하도록 규정한다.
- [ ] 7주차 기능 하나에 실제 적용한 뒤 누락된 규칙을 회고한다.

## 5. T1 — 루트 AGENTS.md 개발 규칙

### 목적

특정 기능과 관계없이 모든 작업에서 지켜야 하는 저장소 불변 규칙을 짧게 유지한다.

### 포함 범위

- UTF-8
- 기존 사용자 변경 보존
- 계획 문서와 의존성 우선순위
- 안정 ID와 계층 간 계약 확인
- Unity Editor Setup과 `.meta` 안전성
- 검증 전 체크리스트 완료 금지

### 제외 범위

- 특정 주차의 세부 구현 절차
- 몬스터·아이템의 밸런스 값
- 텔레메트리 이벤트 전체 스키마
- 아직 만들지 않은 MCP 도구 명세

### 완료 조건

- [x] 저장소 루트에 `AGENTS.md`가 존재한다.
- [x] 장기 불변 규칙과 기능별 워크플로가 구분되어 있다.

## 6. T2 — Unity 콘텐츠 생성기와 검증기 정리

### 목적

기존 `Week3~6*Setup.cs`와 `Week6ItemVerification.cs`에서 반복되는 Scene, Prefab, ScriptableObject 구성과 불변조건 검사를 재사용 가능한 단위로 정리한다.

### 구현 원칙

- 7주차 방 또는 몬스터 기능을 구현하면서 필요한 공통 부분만 추출한다.
- 런타임 게임 규칙은 Editor 코드로 이동하지 않는다.
- Setup은 재실행해도 중복 생성되지 않아야 한다.
- 누락된 Prefab, Component, ID와 직렬화 참조는 명시적인 오류로 보고한다.
- Unity Test Framework로 안정적으로 검증할 수 있는 로직은 Editor 메뉴 검증보다 테스트를 우선한다.

### 후보 산출물

13주차 추가 도구(2026-09-07): `Week13FrontendSetup`, `Week13FrontendVerification`과 Play Mode 배치 검증기를 구현했다. Frontend 단독 구성, Setup 재실행·GUID 유지, 전투 Scene 보존, 잘못된 참조·중복·전투 오브젝트 거부, 해상도별 TMP 경계와 미리보기 출력을 담당한다. 실행 방법과 수동 확인 항목은 [개발 환경 설정 §17](./05-development-setup.md)에 기록한다. 실제 검증 결과는 [13주차 기록](./15-fourth-month-plan.md)의 Frontend 기반 구현 항목에 남긴다.

13주차 추가 도구(2026-09-08): 닉네임 등록과 재실행 흐름을 반복 검증하기 위한 로컬 프로필
초기화 도구를 추가했다. Editor 메뉴와 Development Build의 Frontend 전용 버튼을 제공하며,
`userId`와 닉네임만 삭제하고 멱등 재등록에 필요한 `clientProfileId`와 다른 로컬 설정은 보존한다.
정식 빌드에는 런타임 초기화 버튼을 포함하지 않는다.

20주차 추가 도구(2026-10-01): Special-3 수동 확인에서 비밀방 위치와 폭탄·구덩이 드롭이 플레이 중
보이지 않아, Game Scene에 개발용 패널(`DevelopmentGamePanel`, `F1`)을 추가했다. Editor와 Development
Build에서만 자동 생성되고 정식 빌드에는 포함하지 않는다. 1단계 범위는 seed 지정 재시작(1층 비밀방 seed 찾기),
폭탄·열쇠·엘리프 지급, HP·SP 회복, 무적 토글, 현재 웨이브 처치, 비밀방·숨김 통로 미니맵 표시와 벽 강조,
다음 장애물 구덩이 드롭 고정, 비밀방·인접 방 이동이다. 모든 조작은 자원 지갑·Health·방 사망 처리·방 이동의
기존 경로를 거쳐 저장·중복 지급 규칙을 우회하지 않는다. 적·장애물 배치 같은 2단계 도구는 방 상태 저장과
Layout 검증을 우회하므로 Game Scene이 아닌 별도 테스트 씬(아이템 테스트 룸 방식)에서 진행한다(사용자 결정).
- [x] 1단계 개발 패널 훅 자동 검증 (`Week20DevPanelVerification.Verify`, 2026-10-01; 패널 UI 조작은 수동 확인 대기)
- [x] 21주차 Spell-0(2026-10-02): `— Spell slot —` 섹션 추가. 보유 아이템·명상 남은 시간 표시와 일회용 아이템 `Drop` 버튼(Editor 전용, 보조 Run 표시). 픽업은 일반 슬롯 획득 경로로 줍는다. 패널 회귀 `Week20DevPanelVerification.Verify` 통과, 버튼 조작은 수동 확인 대기
- [x] 21주차 Spell-1(2026-10-02): `— Spell slot —` 보유 표시 줄에 저놈 잡아라 활성 시 `Room ATK +N%` 추가. `Drop` 목록은 일회용 에셋을 자동 수집하므로 새 버튼 코드는 없다. 패널 회귀 `Week20DevPanelVerification.Verify` 통과, 표시 확인은 수동 확인 대기
- [x] 21주차 Spell-3(2026-10-02): 보유 표시 줄에 막판 스퍼트 활성 시 `Room ASPD +N% MS +N%` 추가(저놈 잡아라 `Room ATK`와 별도 표시). Spell-2 그건 내 잔상은 `Drop` 자동 수집만 사용하며 패널 코드 변경 없음. 패널 회귀 `Week20DevPanelVerification.Verify` 통과, 표시 확인은 수동 확인 대기
- [x] 22주차 Chest-1(2026-10-03): `— Chest —` 섹션에 `Chest Normal`·`Chest Golden`·`Chest Diamond` 버튼 추가. 현재 방의 안전 위치에 `dev-chest-NN` 상자를 실제 상자 경로(방 상태 기록·1회 개봉·층 이탈 소멸)로 놓고 보조 Run으로 표시한다. 패널 회귀 `Week20DevPanelVerification.Verify`와 생성 경로 `Week22Chest1Verification` 통과, 버튼 조작은 수동 확인 대기
- [x] 22주차 Flight-0(2026-10-03): `— Chest —` 아래 `Golden exclusive` 줄에 비행 여부 표시와 황금 전용 아티팩트 `Drop` 버튼 추가(보조 Run, 일반 `ItemPickup` 획득 경로). Chest-2 전까지 가짜 날개를 얻는 유일한 경로다. 패널 회귀 `Week20DevPanelVerification.Verify`와 획득 경로 `Week22Flight0Verification` 통과, 버튼 조작은 수동 확인 대기
- [x] 23주차 Obstacle-5(2026-10-06): `— Obstacle —` 섹션 추가. 현재 방에서 플레이어와 가장 가까운 부서지지 않은 후보 슬롯의 ID·종류를 표시하고 `Make <종류>` 버튼으로 특수 장애물 표의 종류로 바꾼다(보조 Run, 방을 재구성하면 seed의 종류로 돌아간다). 패널 회귀 `Week20DevPanelVerification.Verify`와 종류 동작 `Week23Obstacle5Verification` 통과, 버튼 조작은 수동 확인 대기. Obstacle-6의 폭발 상자·랜덤박스·수집품 상자도 같은 표에 있어 버튼이 자동으로 늘어난다(패널 코드 변경 없음)
- [x] 23주차 Enemy-6(2026-10-06): `— Obstacle —`에 `Next broken random box releases enemies` 토글 추가. 켜면 적을 내보낼 수 있는 종류(셰이디의 랜덤박스)의 다음 파괴가 seed 결과 대신 쥬비 5마리를 내보내고 한 번 쓰면 꺼진다(보조 Run). 패널 회귀 `Week20DevPanelVerification.Verify`는 Enemy-6 회귀 배치에 포함, 토글 조작은 수동 확인 대기
- [x] 23주차 Passive-1(2026-10-06): `— Artifact —` 섹션 추가. `Show artifact drops (N)`을 켜면 활성 아티팩트 에셋 전체(황금 전용 제외, Editor 전용 자동 수집)의 `Drop <이름> (보유/최대)` 버튼이 나오고, 일반 `ItemPickup` 획득 경로로 줍는다(보조 Run). 새 아티팩트는 패널 코드 변경 없이 목록에 나온다. 앞으로 아티팩트·스펠 구현 조각은 개발 패널 획득 경로를 함께 넣는다(`AGENTS.md`). 패널 회귀 `Week20DevPanelVerification.Verify` 통과, 버튼 조작은 수동 확인 대기
- [x] 23주차 Obstacle-7(2026-10-06): `— Obstacle —` 맨 위에 `Hold a burn artifact (trees catch fire)` 토글과 `Nearest tree <ID>: n/4 hits [BURNING]   burned this Run N` 줄 추가. 화상 아티팩트가 아직 없어 토글이 보유를 대신한다(보조 Run, `PlayerStats.DevelopmentForceBurnSource`). 화상 아티팩트가 구현되면 `— Artifact —`의 `Drop`으로 얻어 같은 줄로 확인한다. 패널 회귀 `Week20DevPanelVerification.Verify`(플래그 기본값 꺼짐)와 `Week23Obstacle7Verification` 통과, 토글 조작은 수동 확인 대기
- [ ] 2단계: 적·장애물 배치 전용 테스트 씬

- 공통 Editor 구성 유틸리티
- 방·몬스터 콘텐츠 Setup
- 콘텐츠 참조 및 상태 불변조건 검증기
- 필요 시 Edit Mode 또는 Play Mode 테스트

후보 이름과 디렉터리는 첫 실제 사용 사례를 구현하면서 확정한다.

### 완료 조건

- [x] 7주차 콘텐츠 하나가 정리된 생성 경로를 실제로 사용한다. (Phase E-2 원거리 몬스터)
- [x] 같은 Setup을 두 번 실행해도 중복 오브젝트나 에셋이 생성되지 않는다. (사용자 Unity 확인, 2026-08-26)
- [x] 필수 참조 누락과 잘못된 설정을 검증기가 실패로 보고한다. (`RangedEnemy.prefab` 누락·잘못된 픽스처 실패 확인 후 최종 통과)
- [x] 수동으로만 확인 가능한 항목과 자동 검증 항목이 구분되어 있다. (`Scripts/README.md`의 Setup·검증기·Play Mode 절차)
- [x] Phase F 방 정의 Setup 재실행과 결정적 그래프의 seed·ID·연결·중복·누락 불변조건을 전용 검증기로 확인한다. (`Verify Phase F-1 Random Room Graph`, 2026-08-28)
- [x] Phase F 생성 그래프 바인딩 Setup 재실행과 방 정의·몬스터·보상 활성화·출입구·멱등성 불변조건을 전용 검증기로 확인한다. (`Verify Phase F-2 Generated Room Graph Binding`, 2026-08-28)
- [x] 랜덤 층과 Backend 저장에서 분리된 아이템 테스트 씬에서 시작 스택·적 Prefab·좌표를 설정하고 Play 중 아이템 추가·적 재배치를 반복할 수 있다. (`Open or Create Item Test Room`, `Verify Item Test Room` Edit Mode·Play Mode 통과, 2026-09-01)

### 중단 기준

공통화 대상이 한 곳에서만 사용되거나 기능 구현보다 추상화 비용이 크면 해당 기능에 필요한 검증만 추가하고 범용화는 연기한다.

## 7. T3 — Unity ↔ Backend 계약 검사

### 목적

Unity Asset과 Backend 데이터·DTO가 서로 다른 ID 또는 enum·필드 계약을 사용하는 문제를 실행 전에 찾는다.

### 1차 검사 범위

- Unity `ItemDefinition.itemId`와 Backend Item seed ID 집합
- Character ID 집합
- `COMMON / UNCOMMON / RARE / EPIC` 등급 값
- 중복 ID, 빈 ID와 허용되지 않은 ID 형식

### 후속 검사 범위

- Unity Run DTO와 Backend Run 생성 DTO의 필수 필드
- `clientRunId` 멱등성 계약
- 획득 순서, 획득 시각과 사망 원인 규칙

### 구현 원칙

- 읽기 전용 검사로 시작한다. 불일치를 자동 수정하지 않는다.
- 사람이 읽을 수 있는 차이와 실패 종료 코드를 함께 제공한다.
- 문서의 예시 문자열이 아니라 실제 Unity Asset과 Backend seed·코드를 비교한다.
- 로컬과 CI에서 같은 결과가 나오도록 Unity Library나 개인 환경에 의존하지 않는다.

### 완료 조건

- [ ] 정상 계약에서 종료 코드 0을 반환한다.
- [ ] 누락, 중복 또는 불일치 ID를 재현했을 때 실패한다.
- [ ] 오류가 발생한 계층과 값을 출력한다.
- [ ] 실행 명령이 `docs/05-development-setup.md`에 기록된다.

## 8. T4 — 플레이테스트 텔레메트리

### 목적

8주차 밸런스를 감으로만 조정하지 않도록 Run과 전투 이벤트를 로컬 파일로 수집하고 비교 가능한 형태로 만든다.

### 도입 순서

1. 공통 피해·처치 이벤트와 방 전환 이벤트를 먼저 안정화한다.
2. 이벤트 이름, 공통 필드와 `schemaVersion`을 정의한다.
3. 개발 빌드 또는 Editor에서 로컬 JSONL 같은 append-only 형식으로 기록한다.
4. 한 Run 요약을 생성하고 여러 Run의 지표를 비교한다.
5. 필요할 때 CSV 변환이나 시각화를 추가한다.

### 최소 이벤트 후보

- Run 시작·종료
- 방 진입·클리어
- 적 생성·처치
- 플레이어 피격·사망
- 아이템 획득
- SP 획득·사용
- 저학년·고학년 스킬 사용과 피해

### 최소 공통 필드 후보

- `schemaVersion`
- 개발용 Run 식별자
- 이벤트 종류
- Run 시작 후 경과 시간
- 현재 층과 방
- 관련 Character, Item, Enemy ID
- 피해량, 회복량 또는 상태 변화 값

최종 스키마는 공통 전투 이벤트 구현 후 확정한다.

### 완료 조건

- [ ] 한 번의 전체 Run이 중단 없이 기록된다.
- [ ] 연속 두 Run의 데이터가 섞이지 않는다.
- [ ] 로그 파싱 실패 없이 Run별 핵심 지표를 계산할 수 있다.
- [ ] 로깅 실패가 게임 진행을 멈추지 않는다.
- [ ] Secret과 불필요한 개인 식별 정보를 기록하지 않는다.
- [ ] Release에서 비활성화하거나 명시적으로 제어할 수 있다.

## 9. T5 — Unity MCP

### 목적

Codex가 Unity Editor의 실제 상태를 의미 단위로 조회하고 검증 루프를 실행할 수 있게 한다.

### 착수 조건

다음 조건을 모두 만족할 때 별도 구현 계획을 확정한다.

- [ ] 코드 수정 후 Unity Editor 수동 검증이 기능마다 반복된다.
- [ ] Editor 실행, 메뉴 호출, Console 확인 또는 Game View 캡처가 한 사이클에서 10~15분 이상의 병목이 된다.
- [ ] 필요한 도구가 최소 3개 이상 실제 반복 사례로 확정된다.
- [ ] Editor Script, Unity Test Runner 또는 CLI batch mode만으로 충분히 해결되지 않는다.

### 초기 도구 후보

- Console 로그와 컴파일 오류 조회
- Edit Mode·Play Mode 테스트 실행
- Scene 열기
- Play Mode 시작·중지
- GameObject와 직렬화 필드 조회
- Game View 캡처

### 안전 원칙

- 로컬 Editor와 저장소 범위로 접근을 제한한다.
- 임의 Shell 실행 도구를 제공하지 않는다.
- 읽기 도구를 먼저 만들고 Scene 저장이나 Asset 변경은 별도 명시적 도구로 분리한다.
- Play Mode 시작·중지와 저장처럼 상태를 바꾸는 호출은 결과와 실패 상태를 명확히 반환한다.

### 완료 조건

- [ ] 착수 조건을 실제 작업 기록으로 확인한다.
- [ ] 최소 도구 집합과 권한 경계를 별도 설계한다.
- [ ] 대표적인 코드 수정 → Unity 검증 사이클 한 건을 자동화한다.
- [ ] 수동 방식보다 반복 시간 또는 누락 위험이 줄었음을 확인한다.

## 10. T6 — Web UI 상태 테스트

### 목적

실제 Database를 비우거나 API를 임의로 고장 내지 않고 통계·랭킹 화면의 빈 데이터, 전체·부분
오류, 로딩과 다시 시도 복구를 반복 검증한다.

### 구현 범위

- Vitest와 Testing Library의 `jsdom` 환경
- 통계의 0 요약과 캐릭터·아티팩트·층 빈 상태
- 통계·랭킹의 일부 API 실패와 정상 섹션 유지
- 전체 API 실패 후 로딩 표시와 다시 시도 성공
- 일부 데이터와 긴 캐릭터·아티팩트·닉네임 표시

### 완료 조건

- [x] 실제 Database를 변경하지 않는 고정 입력을 사용한다. (컴포넌트 초기 상태와 API mock, 2026-09-04)
- [x] 빈 데이터와 전체·부분 오류를 구분해 검증한다. (Web UI 테스트 6개 통과, 2026-09-04)
- [x] 다시 시도 중 로딩과 성공 복구를 검증한다. (통계·랭킹 복구 테스트, 2026-09-04)
- [x] `pnpm test`, `pnpm lint`, `pnpm build`가 통과한다. (2026-09-04)

## 11. 실행 순서

```text
7주차 기능 구현에 trickal-feature-cycle 적용
  → 방·몬스터 작업 중 Unity 생성기/검증기 정리
  → 아이템 등급·메타 계약 확장 전 계약 검사
  → 공통 전투·방 이벤트 안정 후 텔레메트리
  → 8주차 반복 플레이 데이터로 밸런스 조정
  → 12주차 통계·랭킹 빈 상태와 오류 복구를 Web UI 테스트로 고정
  → Unity 수동 검증이 병목일 때만 MCP 착수
```

## 12. 다음 점검 시점

### Boss-2 즉시 전투 테스트룸 — 2026-09-19

2층까지 진행하는 반복 비용을 줄이기 위해 `Boss2TestRoomSetup` 메뉴를 추가했다. 최초 메뉴 실행 시
ItemTestScene에서 독립된 Boss2TestScene을 생성하며, Play 시작 시 새마음금고 한 마리와 기본 플레이어로
즉시 전투한다. 체력 초기화·적 재생성 버튼은 기존 테스트룸 기능을 재사용한다. 일반 게임의 시작 흐름과
빌드 Scene 목록은 변경하지 않는다. 구성·생성 검증 메뉴를 제공하며 현재 열린 Unity의 프로젝트 잠금으로
배치 검증은 미실행 상태다. 실행 방법은 `docs/05-development-setup.md`의 Boss-2 즉시 전투 항목을 따른다.

### Unity 검증기 정리 후보 — 2026-09-29

- Play-1(2026-10-01): 개발용 층별 실시간 측정·종료 기록·보조 도구 사용 표시와 `F1` 기록 복사를 추가했다.
  `Week20Play1Verification.VerifyBatch`는 시간 누적·종료 후 동결과 현재 Room-8·자원·장애물·특수방·보상
  검증을 Scene별로 분리 실행하며 수동 seed 후보를 출력한다. 실제 전체 Run 자동 플레이는 후속 후보로
  유지한다. Unity 배치 검증 통과·종료 코드 0을 확인했다. 측정·회귀 검증만으로 Play-1을 완료 처리하지 않는다.

- Floor-1(2026-10-01): `Week20Floor1Verification`에 층별 규모 검증과 관련 회귀를 묶는
  `VerifyWithRegressionsBatch`를 추가했다. Room-0의 이전 seed 해시는 활성 층별 설정을 잠시 분리한
  이전 생성 경로에서 확인하고 원래 설정을 복원한다. Special-4의 상점 추가 전후 동일성도 이전 생성
  경로에서 검사하며, Floor-1에서는 총 방 수 예약 때문에 상점 유무에 따라 일반 방 수가 달라진다.
  Difficulty-1 검증의 거리 계산은 숨김 통로를 제외하도록 현재 계약에 맞췄다. 전체 구형 검증기 정리는
  별도 후보로 유지하며 이 회귀 묶음이 전체 검증기 실행을 대체하지 않는다.

`game/Assets/Editor`에 `*Verification.cs`가 96개 쌓였다. 개수 자체는 빌드·런타임 비용이 없어 문제가 아니지만,
원래 실패하는 검증이 섞이면 새 회귀를 놓치고, 예전 Setup을 다시 실행하면 최신 카탈로그를 되돌린다.
아래는 전체 배치를 돌리지 않은 **정적 1차 분류**이며, 실제 정리 전에 전체 실행으로 확인한다.

| 분류 | 대상 | 근거 | 후속 조치 후보 |
| ---- | ---- | ---- | -------------- |
| 실패 확인 | Week14 Room-7 | 카탈로그에 `pillar-crossfire` Encounter가 없어 조회 실패 (2026-09-29 실행) | Encounter 포함 여부 결정 후 검증 갱신 |
| 실패 확인 | Week15 Enemy-0·2·3·5 | 프리팹·추적 기본값 변경으로 실패 (Obstacle-0 작업 중 확인) | 현재 값 기준으로 기대값 갱신 또는 폐기 |
| 갱신·실행 통과 | Week14 Room-8 | 예전 6~8방·폐기된 pillar-crossfire·스폰 2개 고정 기대값 | Floor-1 층별 총 방 수·현재 Encounter 네 패턴·실제 두 번째 웨이브 규모로 갱신, Play-1 준비 배치에서 통과 (2026-10-01) |
| 오래된 가정 | Week14 Encounter-1·2 | Encounter 수 `== 3` 고정 | 필수 현재 ID 포함 조건으로 갱신 검토 |
| 오래된 가정 | Week14 Encounter-3 | 웨이브·스폰 수 고정 기대값 | 실행해 확인 |
| 최신 계약 | Week16~18 (Artifact·Content·Item·Reward·Spell·Test, Resource-0·1·3, Obstacle-0·1·2·3), Hp1~5 | 이번 달 작업에서 통과 | 유지 |
| 확인 필요 | Week6~8, Phase G·H, Week13 Frontend·HUD·Setting·Flow, Week14 Room-0~6·Artwork, Week15 Boss·Enemy-1·4·RoleColor, ItemTestRoom, ErpinWalkAnimation | 최근 실행 기록 없음. Week7·8 초기 방·층 검증은 이후 Room·Encounter 계약이 대체했을 가능성이 높음 | 전체 실행 후 유지·갱신·폐기 결정 |

카탈로그를 통째로 다시 쓰는 Setup(`ConfigureTemplates`·`ConfigureEncounters`·Roster 호출):
Week14 Encounter-1·2·3, Room-4·6·7, Week15 Enemy-5, Week18 Obstacle-2. 이 중 Room-6·7과 Obstacle-2는 기존 등록을
보존하고 버전을 낮추지 않게 수정됐다(2026-09-29). 나머지는 재실행하면 이후 콘텐츠가 빠질 수 있으므로
기존 등록 보존으로 고치거나 `[MenuItem]`을 제거해 실수 실행을 막는다.

정리 시점 후보: Obstacle-3 전후 또는 20주차 Floor-1·Play-1 전. 유효한 검증만 한 번에 돌리고 통과/실패 목록을
출력하는 회귀 실행기 배치 메서드를 함께 검토한다.

- T2: 첫 7주차 방 또는 몬스터 콘텐츠 구현 직후
- T3: 아이템 등급이나 `clientRunId` 계약 구현 직전
- T4: 8주차 반복 플레이 테스트 시작 전
- T5: 8주차 회고 또는 Unity 수동 검증 시간이 누적될 때

각 점검에서는 도구를 만들었는지가 아니라 실제 개발 시간, 누락 위험 또는 재현성이 개선됐는지를 기준으로 다음 투자를 결정한다.

## 요정마을 Artwork 적용 도구 (2026-10-01)

사용자가 제공한 네 장의 타일·벽·문 이미지를 기존 방 계약에 맞춰 적용하는 Editor Setup과 검증기를 추가한다.
`FairyVillageArtworkSetup`은 모든 RoomPrefab의 시각 구성만 갱신한다. 2026-10-02 사용자 미감 피드백에 따라
공통 전경을 분리한 연결 구간으로 교체하며, 문·모서리 비율을 보존하고 긴 벽·잔디 구간을 함께 늘린다.
바닥의 반복·반전 배치를 제거하고, 방향별 문과 봉인 벽을 같은 좌표의 상태 에셋으로 구성한다.
`FairyVillageArtworkVerification`은 두 번 적용하여 중복·참조 변동과 Collider·GUID 변경을 검사하고 문 상태를 검증한다.
별도 렌더 진입점은 시각 검토용 PNG를 만든다. 세부 범위와 수동 확인은
[22-fairy-village-artwork.md](./22-fairy-village-artwork.md), 실행 메뉴는
[05-development-setup.md](./05-development-setup.md)를 따른다. 기존 콘텐츠 Setup 이후에는 Artwork 적용을 다시 실행한다.

## 일반 적 공격 Artwork 도구 (2026-10-04)

`EnemyAttackAnimationSetup`은 일반 적 4종의 준비·공격·회복 포즈와 고혈당 요정의 파 투사체를
프리팹에 연결한다. 반복 적용하며 기존 GUID와 전투 수치를 유지한다. 원본 일치 조건과 보스 경계
제거로 소환 졸개/보스가 일반 적 공격 그림을 상속하는 것을 막는다.
`EnemyAttackAnimationVerification`은 실제 전투 단계와 이동 표시의 우선순위, 물리 Transform·Collider
고정, 넉백/비활성화 복원, 졸개 제외와 실제 발사 경로의 파 Sprite·색상·방향·속도·판정 크기를 검사한다.
`ExportPreview`는 네 단계의 Unity 렌더를 `game/Logs/EnemyAttackPreview/`에 저장한다.
메뉴와 Play 확인 항목은 [개발 환경](./05-development-setup.md)의 일반 적 공격 모션을 따른다.

## 적·보스 이동 Artwork 도구 (2026-10-03)

`EnemyMovementAnimationSetup`과 `BossMovementAnimationSetup`은 기존 프리팹의 표시용 컴포넌트와
프레임·분리 레이어만 구성한다. 각각의 Verification은 반복 적용·GUID·물리 판정 보존·정지와 공격 억제·
재활성화를 검사한다. 보스 검증은 실제 새마음금고 점프의 바닥 보물 고정·착지 반동과 Boss-0~3 회귀도
확인한다. 두 렌더 진입점으로 Unity에서 주기별 PNG를 내보낸다. 자동 검증과 렌더는 통과했으며 실제
Play Mode 가독성 확인은 남아 있다. 실행 메뉴와 배치는 [개발 환경 안내](./05-development-setup.md),
범위와 체크리스트는 [요정왕국 적·보스 계획](./18-fairy-kingdom-enemy-boss-plan.md)을 따른다.

2026-10-04 일반 적 도구에 크레용사용 소환 졸개 4종의 전용 걷기 에셋 적용과 공통 검증을 추가했다.
별도 미리보기 메뉴로 네 포즈 주기의 Unity PNG를 생성하며, 기존 보스 소환 회귀와 함께 통과했다.

2026-10-05 `EnemyAttackAnimationSetup.SetupMinions`에 졸개 4종의 전용 공격 포즈와 화살·마법탄 연결을
추가했다. 일반 적 Setup도 함께 구성한다. 검증 대상은 8종이며 졸개 고유 그림과 실제 발사 경로를
검사한다. `Export Crayon Minion Attack Preview`는 반복 구성·전투·보스 소환 회귀와 4단계 렌더를 수행한다.
실행 메뉴와 Play 확인은 [개발 환경](./05-development-setup.md)의 졸개 공격 항목을 따른다.
2026-10-05 `ErpinProjectileArtworkSetup`을 추가했다. 기존 기본공격·저학년 프리팹의 GUID와 전투
설정을 유지하면서 Sprite·tint·표시 크기와 기본공격의 축소된 충돌 배율을 구성한다. 반복 구성 뒤
실제 SpriteRenderer 크기와 CircleCollider 크기, 공통 판정·저학년·분열탄·사거리 회귀를 검사한다.
실행 메뉴는 [개발 환경](./05-development-setup.md)의 에르핀 구체 항목을 따른다.
2026-10-05 `ErpinHighGradeArtworkSetup`은 Resources의 돌격·충돌 8포즈를 반복 가져오고
실제 `PlayerActionState` 단계에 따른 포즈 선택·좌우 전환·일시정지·회복·비활성화 복원과
본체/히트박스 보존을 검사한다. 배치는 기존 걷기·고학년·저학년 회귀를 포함한다.
고학년 테스트의 층 진입 보호창과 합성 시간 취소 검사를 분리했다. 실행 메뉴는
[개발 환경](./05-development-setup.md)의 에르핀 고학년 모션 항목을 따른다.

## 검수한 공통 전투 이펙트 검증 (2026-10-06)

`ReviewedCombatEffectsVerification`은 Resources 이미지·프레임 경계·셰이더, 실제 HP/SP 회복 이벤트,
보스 피격 표시의 원본 색상 보존과 비활성화 복원, 점프 착지 그림자 및 취소 시 제거를 검사한다.
`VerifyWithRegressionsBatch`는 저학년 스킬·크레용사용 공격·보스 및 일반 적 이동 검사도 실행한다.
Unity 6000.3.22f1 배치 종료 코드 0과 성공 로그를 확인했다. Play 화면 검수는 남아 있다.
선택 에셋과 적용 범위는 [이펙트 기록](./art-prompts/reviewed-combat-effects.md),
실행 메뉴와 수동 확인은 [개발 환경](./05-development-setup.md)을 따른다.

## 방 Layout 대량 제작 도구 제안 (2026-10-07)

상태: **제안 — 미착수**. 아래 포맷·임포터·에디터 창은 아직 구현되지 않았으며 확정된 사용법이 아니다.
착수 시점과 범위는 사용자가 정한다.

### 사용자가 원하는 것 (2026-10-07)

- 장애물·구덩이 같은 방 안 배치를 아이작(The Binding of Isaac)처럼 여러 종류로 만들고 싶다.
- 이 게임에도 맵 에디터 같은 도구를 둘 수 있는지 확인하고 싶다.
- 작업 방식은 **에이전트가 맵을 한꺼번에 많이 만들고, 사용자가 그중에서 고르고 손보는 것**이다. 이렇게 하면 맵 종류를
  빠르게 늘릴 수 있다고 본다.
- 지금 바로 만들지는 않고 나중에 진행한다.

### 현재 기반과 병목

- 방 템플릿(`RoomTemplateDefinition`)과 방 Prefab이 분리되어 있고 프로필별 후보에서 seed로 선택한다. 현재 템플릿은
  19개다(`game/Assets/Rooms/Templates`).
- 부서지는 장애물은 0.5 격자 위 1×1, 구덩이는 격자 정렬 직사각형이라 격자 기반 작성과 맞는다.
- `RoomObstacleLayout.TryValidate`가 문 통로 미차단, 문 사이 연결, SpawnPoint 도달 가능, 원거리 사선 확보(35% 이상)를
  검사하므로 대량 생성한 Layout 중 깨진 것을 자동으로 걸러낼 수 있다.
- 장애물 슬롯은 위치만 고정이고 종류는 `ObstacleVariantTable`이 seed로 정한다.
- 병목: Layout 하나마다 좌표를 C# Setup 코드에 직접 적고 있다(`Week22Terrain0Setup`, `Week23Obstacle6Setup` 등,
  파일당 180~350줄). 이 방식으로는 수십 개를 만들기 어렵고 사용자가 직접 고치기도 불편하다.

### 제안한 구성

1. **텍스트 Layout 포맷**: 방 하나를 글자 격자로 적는 파일. 장애물·구덩이·나무·SpawnPoint를 글자로 표시한다.
   에이전트가 한 번에 여러 개를 쓸 수 있고 사용자가 텍스트 편집기로 고칠 수 있다.
2. **임포터**: 파일을 읽어 방 Prefab과 템플릿 에셋을 생성·갱신하고 `RoomObstacleLayout` 검증을 실행한다. 실패한
   Layout은 이유와 함께 거부한다. 재실행 시 ID 기준으로 갱신하여 중복 에셋을 만들지 않는다.
3. **Unity 에디터 창**: 격자를 클릭해 배치를 칠하고 검증 결과를 바로 표시한다. 저장 결과는 1번 포맷이다.

권장 순서는 1·2번과 시범 Layout 10개 안팎으로 흐름을 먼저 확인한 뒤 3번을 붙이는 것이다. 1·2번만으로도
"대량 생성 후 사용자가 손보는" 흐름은 성립한다.

### 착수 전에 정할 것

- 스폰 지점의 역할(`SpawnPointPlacementRole`) 지정 방식: 위치 기반 자동 추정 후 수동 수정안을 제안했다.
- Layout 난이도 보정: 현재 장애물 Layout은 모두 +1이다. 수가 늘면 장애물 밀도 기반 산정 규칙이 필요하다.
- ID 계약: 템플릿 ID와 장애물 ID는 Run 저장·seed 재현에 쓰이는 안정 키다. 이미 사용된 Layout을 고치면 새 ID로
  추가하거나 Room 콘텐츠 버전을 올려야 한다.
- 프로필(basic/small/wide/tall/large)별 격자 크기와 문 위치를 포맷에서 어떻게 고정할지.
- 현재 달 필수 작업과의 우선순위.

### 한계

검증기는 Layout이 깨지지 않았는지만 확인하고 재미는 판단하지 못한다. 대량 생성한 Layout의 선별과 조정은
실제 Play 확인이 필요하다.

### 구덩이 이음새 검수 v2 (2026-10-10)

- [x] 사분면 타일 대신 연속 윤곽·절벽 원화·내부 암벽 원화로 단일 표시를 구성했다. ㄹ·꽉 찬 3×3 확대 검수 메뉴와 10종 실제 Unity 렌더를 추가했다.
- [x] Unity 자동 검증에서 3×3 및 ㄱ의 Collider 분할 방식에 따른 픽셀 완전 일치, ㅁ 중앙 투명도, 3×3 내부 색상 다양성·밝기, 대각선 분리, 중복 방지 및 충돌 범위 보존을 확인했다. 최종 실행 기록은 `output/pit-contour-v2-final.log`와 [작업 기록](./art-prompts/pit-tiles-v2/README.md)을 따른다.
- [ ] 실제 SampleScene Play 조작 확인과 사용자 최종 미술 검수.
- 사용법은 [개발 환경](./05-development-setup.md)의 구덩이 v2 항목을 따른다.

### 에셋별 공용 그림자 검수 도구 (2026-10-10)

- [x] `GroundShadowPreviewExporter.ExportBatch`로 실제 프리팹·변형·상태 76종의 Unity 합성 및 바닥/몸체/그림자 레이어를 생성했다. Unity 6000.3.22f1 성공 종료 코드 0과 이미지 생성을 확인했다 (`output/ground-shadow-review-export.log`). 원본 씬·프리팹은 저장하지 않는다.
- [x] `scripts/build-ground-shadow-review.py`로 검색·종류 필터·개별 너비/두께/위치/진하기 조절·원본 비교·초기화·값 전달용 로컬 페이지와 모음 이미지를 생성했다. 브라우저에서 검색, 진하기/위치 변경 및 초기화를 확인했다.
- [x] 사용자 제공 67종 검수 수치를 공용 JSON 및 상태별 런타임 선택으로 반영했다. 실제 Unity 재렌더와 67종 전 수치 일치, 미지정 9종 불변, 동일 오브젝트의 색상·개폐 전환과 수동 설정 보존을 검증했다 (`output/ground-shadow-profile-apply.log`, `output/ground-shadow-profile-transitions.log`, 종료 코드 0).
- 실행 및 보관 범위는 `docs/05-development-setup.md`의 에셋별 그림자 검수 페이지를 따른다.
