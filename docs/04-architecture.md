# Architecture

## 1. 아키텍처 개요

본 프로젝트는 크게 다음 3개의 애플리케이션으로 구성한다.

1. Unity Game Client
2. Backend Server
3. Web Service

데이터 저장소는 PostgreSQL을 사용하며 Supabase를 통해 관리한다.

전체적인 구조는 다음과 같다.

Unity Game
↓
REST API
↓
Backend
↓
PostgreSQL
↑
Backend
↑
REST API
↑
Web Service

┌──────────────────────┐
│ Unity Game │
│ │
│ Unity + C# │
└──────────┬───────────┘
│
│ REST API
▼
┌──────────────────────┐
│ Backend │
│ │
│ Node.js + TypeScript │
│ NestJS (?) │
└──────────┬───────────┘
│
│ SQL / ORM
▼
┌──────────────────────┐
│ PostgreSQL │
│ Supabase │
└──────────┬───────────┘
▲
│
│ REST API
┌──────────┴───────────┐
│ Web │
│ │
│ Next.js + TypeScript │
└──────────────────────┘

## 2. Architecture 기본 원칙

### 2.1 책임 분리

각 시스템은 자신의 역할에 집중한다.
Unity
→ 게임 플레이

Backend
→ 데이터 처리 및 비즈니스 로직

Database
→ 데이터 저장

Web
→ 데이터 조회 및 시각화
각 시스템이 다른 시스템의 역할을 직접 대신하지 않도록 한다.

## 2.2 Database 직접 접근 금지

Unity와 Web은 PostgreSQL에 직접 접근하지 않는다.
잘못된 구조:
Unity ─────────→ Database
Web ───────────→ Database
목표 구조:
Unity ──→ Backend ──→ Database

Web ────→ Backend ──→ Database
이를 통해 데이터 검증, 권한, 비즈니스 로직 등을 Backend에서 통제한다.

## 3. Unity Game Architecture

### 3.1 Unity의 책임

Unity는 실제 게임 플레이와 관련된 모든 기능을 담당한다.

#### 주요 책임

- 게임 실행
- 타이틀·홈·설정 Frontend
- 로컬 플레이어 프로필 생성과 복원
- 캐릭터 선택
- 플레이어 조작
- 전투
- 몬스터
- 아이템
- 방
- 층
- 보스
- 게임 상태
- Run 관리
- 게임 결과 생성
- Backend API 통신
  Unity는 게임 플레이의 실시간 상태를 관리하는 Client 역할을 한다.

## 4. Unity 내부 구조

Unity 내부는 기능별 책임을 분리한다.
초기 구조는 다음을 기준으로 한다.
Unity
│
├── Frontend
│ ├── Local Profile
│ ├── Home
│ ├── Character Select
│ └── Settings
│
├── Core
│
├── Game
│ ├── Player
│ ├── Enemy
│ ├── Combat
│ ├── Item
│ ├── Room
│ ├── Floor
│ └── Boss
│
├── Run
│
├── UI
│
├── Data
│
└── Network

## 5. Unity Core

Core는 여러 게임 시스템에서 공통으로 사용하는 기능을 담당한다.
예상 영역:
Core
├── GameManager
├── SceneManager
├── Event System
├── Audio
└── Utilities
단, 초기 개발 단계에서는 Core를 지나치게 거대한 공통 모듈로 만들지 않는다.
실제로 여러 시스템에서 공통으로 사용되는 기능만 Core로 이동한다.

## 6. Unity Game

게임 플레이와 직접적으로 관련된 기능을 관리한다.
Game
├── Player
├── Enemy
├── Combat
├── Item
├── Room
├── Floor
└── Boss

### 6.1 Player

플레이어 캐릭터의 행동과 상태를 관리한다.

#### 책임

- 이동
- HP
- 공격
- 피격
- 사망
- 능력치
- 아이템 효과 적용
  예상 구조:
  Player
  ├── PlayerController
  ├── PlayerStats
  ├── PlayerCombat
  ├── PlayerHealth
  └── PlayerInput

### 6.2 Enemy

일반 몬스터의 공통 기능과 개별 행동을 관리한다.

#### 책임

- 이동
- 플레이어 탐지와 전투 중 지속 경계
- 공격
- 피격
- HP
- 사망
- AI
  예상 구조:
  Enemy
  ├── EnemyBase
  ├── EnemyStats
  ├── EnemyHealth
  ├── EnemyAwareness
  └── AI
  개별 몬스터는 공통 기능을 재사용하면서 자신만의 행동 패턴을 구현한다. 방 전투 시작 또는
  피격으로 경계한 적은 같은 전투에서 감지 거리 밖으로 나갔다는 이유만으로 플레이어를 잊지 않는다.

### 6.3 Combat

전투에 관련된 공통 시스템을 담당한다.

#### 책임

- 공격
- 데미지
- 투사체
- 충돌
- 피격
- 사망
  기본 흐름:
  Attack
  ↓
  Projectile
  ↓
  Collision
  ↓
  Damage
  ↓
  Health 감소
  ↓
  Death
  전투 시스템은 Player와 Enemy에 강하게 종속되지 않도록 가능한 범위에서 독립적으로 설계한다.

## 7. Item

아이템 데이터와 효과를 관리한다.

### 책임

- 아이템 정의
- 아이템 획득
- 아이템 효과 적용
- 아이템 중첩
- 아이템 시너지
- 아티팩트 후보 생성과 3개 중 1개 선택
  구조 예시:
  Item
  ├── ItemData
  ├── ItemManager
  ├── ItemEffect
  └── Synergy
  아이템 자체의 수가 증가하더라도 기존 게임 시스템의 수정이 최소화되도록 설계한다.

## 8. Room

방 시스템은 생성된 논리 방 데이터와 실제 Unity 방 인스턴스를 분리한다. 방의 좌표·종류·연결과
Run 상태는 일반 C# 데이터가 소유하고, Prefab 인스턴스는 현재 상태를 표현하고 전투를 실행하는
View 역할을 맡는다. ScriptableObject나 Prefab 자산에 방문·클리어·아티팩트 획득 상태를 저장하지 않는다.

### 책임

- `GeneratedRoomNode`: 안정적인 room ID, 격자 좌표, 방 종류, 정의 ID와 방향별 연결
- `RoomRunState`: 방문, 완료 웨이브 수, 클리어, 클리어 보상과 아티팩트 획득 등 같은 Run에서 변하는 상태
- `RoomDefinition`: RoomType과 Layout/Encounter 후보를 제공하는 정적 제작 데이터
- `RoomProfile`: 안정 `profileId`, 내부 크기, 이동·Encounter·카메라 경계, Orthographic Size와 미니맵 실루엣
- `RoomTemplateDefinition`: 안정 `templateId`, Room Profile·Prefab·허용 RoomType과 방향별 문·안전 진입점·SpawnPoint
- `RoomPrefab`: Room Profile을 만족하는 방 영역, spawn point, 보상 지점과 4방향 문 슬롯
- `RoomStaticObstacle`: 안정 장애물 ID, solid Environment 충돌체와 비파괴·무드롭 정적 계약
- `EncounterDefinition`: 적 구성, spawn group, 웨이브, 층·Room Profile 조건과 안전거리
- `RoomController`: 방 입장, 전투 시작, 적 전멸 확인, 클리어와 문 잠금/해제
- `RoomGraphController`: 현재 방 활성화, 플레이어 배치, 카메라 전환과 방 사이 이동
- `RoomCameraFraming`: 방 크기·Orthographic Size·종횡비에서 축별 카메라 중심 경계와 추적 모드를 계산하는 순수 규칙
- `RoomCameraController`: 현재 Profile의 계산 결과로 방 진입 카메라를 즉시 유효화하고 플레이어를 경계 안에서 보간 추적
- `RoomTemplateSelector`: RoomType·연결 방향·grid AABB 호환 후보를 seed와 콘텐츠 버전으로 결정적으로 배정
- `EncounterSelector`: Room Profile·층·SpawnPoint·안전거리 호환 후보를 seed와 Encounter 콘텐츠 버전으로 결정적으로 배정
- `EncounterEnemyRoster`: Encounter의 적 역할을 기존 검증 Prefab에 매핑하며 Encounter 데이터와 Prefab 소유권을 분리

상태 예시:

```text
WAITING
  ↓ 전투방 첫 입장
COMBAT
  ↓ 클리어 조건 달성
CLEARED
```

시작방과 보물방처럼 전투가 없는 방은 RoomType 규칙에 따라 전투 상태를 생략한다. 방을 비활성화하거나
Scene 인스턴스를 제거하더라도 `RoomRunState`가 남아 있어 재방문 시 적과 아티팩트를 다시 생성하지
않아야 한다.

기존 `16 × 9`, Orthographic Size `5.25` 방은 회귀 기준으로 보존한다. 대형 방은 Room Profile이
내부 크기와 카메라 크기를 함께 제공하며, 현재 격자 배치 간격보다 커질 경우 인접 Room 인스턴스의
겹침과 전환 좌표를 검증한다. 플레이어·적 크기와 속도는 Room Profile에서 암묵적으로 배율 적용하지
않고 플레이테스트 결과에 따라 별도 설정으로 조정한다.

Room 계약 카탈로그는 profile/template ID를 대소문자 변환 없이 안정 키로 취급하고 빈 ID·중복 ID,
등록되지 않은 Profile, 누락된 Prefab과 중복 방향 슬롯을 명시적으로 거부한다. Template 검증은
제작 데이터의 문·안전 진입점·SpawnPoint·Encounter·카메라 좌표가 참조 Prefab과 일치하는지도
확인한다. 이 검증은 `RoomRunState`를 읽거나 교체하지 않으며 정적 자산에 Run 상태 필드를 두지 않는다.

카메라 중심의 축별 이동 가능 길이는 `max(0, roomSize - viewportSize)`이며, 0인 축의 최소·최대
중심은 모두 방 중앙이다. 양수인 축은 그 길이의 절반을 중앙 양쪽 경계로 사용한다. 이 계산은
Small·Basic·Wide·Tall·Large에 공통 적용하며 Profile의 16:9 제작 경계도 같은 결과와 일치해야 한다.
`RoomCameraController`는 Profile 전환 순간 새 경계 안으로 스냅한 위치에서 보간을 시작하므로 전환
중에도 이전 방 위치나 새 방의 유효 경계 밖을 거치지 않는다.

수작업 일반방 Template은 `small-standard(12 × 6.75)`, `basic-standard(16 × 9)`,
`wide-standard(24 × 9)`, `tall-standard(16 × 13.5)`, `large-standard(24 × 13.5)`다. 층별 보스
후보는 `boss-floor-1-wide`, `boss-floor-2-tall`, `boss-floor-3-large`이며 Template의 최소·최대 층
계약으로 다른 층에서 제외한다. Basic Prefab은 회귀 기준으로 유지하고 확장 Layout은 별도 Prefab과
GUID를 가진다. `RoomTemplateGeometry`는 방향별 문 슬롯과 안전 진입점에서 필수 통로를 파생하며,
`RoomTemplateDefinition`은 해당 통로 안의 SpawnPoint를 거부한다. Encounter trigger 자체는 입장
감지를 위해 통로와 겹칠 수 있으므로 적·장애물 안전 규칙과 구분한다.

`FloorGenerator`의 좌표·역할·RoomDefinition 생성이 끝난 뒤 `RoomTemplateSelector`가 Template을
배정한다. 후보는 안정 ID로 정렬하고 방의 `contentSeed`와 콘텐츠 버전에서 시작 인덱스를 파생하므로
ScriptableObject 배열 순서가 결과를 바꾸지 않는다. 배정은 방 번호 순서로 진행하며 이미 배정된 모든
방과 Profile 내부 크기 AABB가 겹치는 후보를 건너뛴다. 현재 `20 × 13` 간격에서는 Wide·Tall·Large의
일부 동일 축 인접 조합이 충돌하므로 간격을 확대하지 않고 호환 가능한 Small·Basic 후보로 폴백한다.
층별 확장 보스 후보도 충돌하면 기존 Basic 보스방을 사용한다. 선택된 Template 참조와
`templateId`는 `GeneratedRoomNode`가 소유하고, 실제 Prefab 인스턴스화는 `RoomGraphAssembler`가 맡는다.

Template 배정 뒤 `EncounterSelector`가 일반 전투방의 Encounter 후보를 안정 `encounterId` 순으로
정렬하고, 방의 content seed와 별도 Encounter 콘텐츠 버전으로 하나를 선택한다. Encounter는 적 역할과
수, 웨이브 조건, 허용 Profile·층, 안전거리와 Template의 안정 SpawnPoint ID 또는 SpawnGroup ID만
소유한다. 실제 적 Prefab과 월드 좌표는 소유하지 않는다. 현재 Template은 배열 순서에서
`spawn-01` 형식의 안정 지점 ID를 파생하고 전체 지점 그룹 `all`을 제공한다.

선택 전 검증은 Profile·층 호환, 참조 지점 존재와 개수, 같은 웨이브의 지점 중복, 모든 플레이어
안전 진입점·문 슬롯과의 최소거리를 확인한다. 런타임 선택에서는 현재 방에 실제로 연결된 방향의
문만 활성 출입구로 취급하고, SpawnGroup 후보 중 안전하며 아직 사용하지 않은 지점을 역할별 우선순위로
고른다. 추적형은 중심에 가까운 지점, 원거리형·돌진형은 바깥 지점을 먼저 사용한다. 호환 후보가
없으면 임의 폴백하지 않고 생성 자체를
실패시킨다. 선택된 참조와 `encounterId`는 `GeneratedRoomNode`가 소유하며, 적 인스턴스화·웨이브 진행·
클리어 및 재방문 상태 반영은 Room 런타임 계층이 맡는다.

`RoomGraphAssembler`는 `EncounterEnemyRoster`에서 역할 Prefab을 해석하고 모든 웨이브의 선택된
SpawnPoint Transform과 함께 `RoomController`에 전달한다. `RoomController`는 현재 웨이브의 필수 적을
등록하고 전멸 이벤트가 확인된 뒤에만 다음 웨이브를 한 번 생성한다. 완료 웨이브 수는
`RoomRunState`에 순서대로 기록하며, 방 인스턴스를 다시 구성하면 첫 미완료 웨이브부터 복원한다.
전체 웨이브가 끝나면 방을 클리어하고 `RoomClearRewardSpawner`가 SP 픽업을 한 번 생성한다. 보상
생성 여부도 `RoomRunState`에 기록하므로 같은 컨트롤러 재진입과 인스턴스 재구성에서 중복 생성하지
않는다.

첫 장애물 Template `large-central-pillar`는 Large Profile과 같은 문·카메라 계약을 재사용하되
중앙의 `RoomStaticObstacle(central-pillar)`을 Prefab Layout에 고정한다. 검증기는 장애물이 solid
`Environment` Collider이고 `Health`, `ItemDropSource`, 이동 Rigidbody가 없음을 확인한다. 플레이어와
추적형은 Rigidbody 충돌로 통과하지 못하고, 양쪽 투사체는 사선이 차단되며, 돌진형은 기존
Environment 충돌 처리로 즉시 회복 상태에 들어간다. 자동 우회나 경로 탐색은 이 정적 Room 계약의
책임이 아니며 15주차 적 이동 정책에서 별도로 확장한다.

## 9. Floor

여러 개의 Room을 상하좌우 격자 그래프로 묶어 하나의 층으로 관리한다. `FloorGenerator`는 Unity
Scene 오브젝트를 만들지 않고, seed와 생성 설정을 입력받아 순수한 `GeneratedFloorGraph`를 반환한다.
`RoomGraphAssembler`가 생성 결과를 Room Prefab 인스턴스와 방향별 문 슬롯에 적용한다.
방 수와 RoomType 같은 게임 규칙은 [게임 디자인](./03-game-design.md)을 기준으로 하고, 확장 아이디어는
[아이작식 층·방 생성 구조와 Unity MVP 설계안](./drafts/Isaac_Style_Roguelike_Floor_Room_Design.md)을 참고한다.

### 책임

- 층별 seed와 목표 방 수 설정
- 빈 상하좌우 좌표를 사용하는 Random Growth 그래프 생성
- 시작방·일반방·보물방·보스방 배정
- 시작방으로부터 거리 계산과 먼 끝방의 보스방 선택
- 전체 연결성, 좌표 중복, 문 방향, 필수 방과 재시도 상한 검증
- 현재 층의 Room Prefab 구성과 방향별 문 연결
- 보스 클리어 후 다음 층으로 향하는 단방향 전환

```text
GeneratedFloorGraph
├── FloorNumber / FloorSeed
├── StartingRoomId / BossRoomId
└── GeneratedRoomNode[]
    ├── RoomId
    ├── GridPosition
    ├── RoomType / RoomDefinitionId
    └── DirectionalConnections
```

MVP의 첫 설정은 층당 6~8방을 생성하고, 콘텐츠가 늘어난 뒤 8~12방까지 설정으로 확장한다.
`floor-XX-room-YY`는 생성 순서에 따른 안정 키이며 격자 좌표와 분리한다. 같은 seed와 콘텐츠
버전에서는 같은 room ID, 좌표, 연결, 방 종류와 콘텐츠 정의가 생성되어야 한다.

### 9.1 방향별 문 슬롯

모든 기본 Room Prefab은 좌·우·상·하 슬롯을 제공한다. 슬롯은 문 전환 트리거, 플레이어 진입점,
전투 중 차단문과 미연결 방향 봉인 표현을 묶는다.

```text
Left  ↔ Right
Up    ↔ Down
```

한 방향 슬롯에는 연결 하나만 배정한다. 생성 결과에 없는 방향은 트리거를 비활성화하고 벽으로
봉인한다. `RoomGraphAssembler`는 목적지 ID가 미리 직렬화된 문을 찾지 않고, 생성된 방향 연결을
사용 가능한 슬롯에 바인딩한다.

### 9.2 생성과 콘텐츠 선택 분리

그래프 생성과 방 내부 콘텐츠 선택은 서로 다른 책임으로 유지한다.

```text
FloorGenerator
  → 방 수·좌표·연결·RoomType
  → GeneratedFloorGraph

RoomDefinition / RoomPrefab Catalog
  → 지원 문 방향·층·난이도·Layout 후보
  → RoomGraphAssembler

Encounter Definition
  → 검증된 spawn point·몬스터 구성·웨이브·Room Profile 조건
  → RoomController
```

`FloorGenerator`는 적 Prefab을 생성하지 않고, `RoomController`는 층의 연결 구조를 결정하지 않는다.
핵심 벽·기둥·장애물과 안전 진입 영역은 수작업 Layout에 고정하고, 장애물과 spawn point를 임의
좌표에 배치하지 않는다. 후속 장식·파괴물·SpawnPoint 랜덤화도 Layout이 제공하는 안전한 후보
슬롯 안에서만 수행한다. Room Layout, 후보 슬롯과 Encounter 선택은 Run seed에서 안정적으로
파생하고, 재방문 시 `RoomRunState`를 통해 파괴 상태, 완료한 웨이브와 선택 보상을 복원하여
재추첨·재생성·중복 지급하지 않는다.

### 9.3 생성 실패 처리

생성기는 제한 횟수 안에서만 재시도한다. 각 시도는 원래 floor seed에서 안정적으로 파생한 attempt
seed를 사용하며, 상한을 넘으면 누락된 불변조건과 seed를 포함한 오류를 반환한다. 실패를 숨기거나
무한 반복하지 않는다.

### 9.4 Editor Setup과 Verification

Editor Setup은 RoomDefinition, Room Prefab, 방향 슬롯과 직렬화 참조를 구성하는 역할만 맡는다.
런타임 Random Growth, RoomType 배정, seed 생성과 재방문 규칙을 Setup 코드에 숨기지 않는다.
재실행 시 에셋·Prefab·컴포넌트를 중복 생성하지 않고 Undo와 dirty/save 처리를 제공한다.

Verification은 다음 경계를 분리해 확인한다.

- 순수 생성: 여러 seed의 결정성, 연결성, 좌표, 필수 방, 보스 거리와 재시도 상한
- Prefab 계약: 4방향 슬롯, 진입점, 봉인과 spawn/보상 기준점
- 바인딩: 생성 연결과 반대편 문 슬롯, 현재 방 단독 활성화
- Run 수명: 층 이동과 재방문 중 seed·클리어·아티팩트 상태 유지
- Setup 재실행: 중복 오브젝트·에셋·컴포넌트 없음

## 10. Boss

보스 전용 행동과 패턴을 관리한다.
일반 Enemy 시스템과 공통적인 요소를 재사용하되, 복잡한 보스 패턴은 별도로 관리한다.
Boss
├── BossBase
├── BossStats
├── BossHealth
└── BossPattern

각 층은 서로 다른 보스 정의를 사용하고 보스 패턴은 데이터와 런타임 실행을 분리한다. 패턴 정의는
예고, 실제 공격, 대응 가능한 안전 영역, 공격 후 빈틈과 다음 패턴 조건을 제공한다. 3층 최종 보스의
페이즈 전환은 HP 같은 명시적인 조건으로 상태를 바꾸며 중복 전환되지 않아야 한다. 구체적인 보스와
패턴은 사용자의 트릭컬 보스 선정 전까지 미정으로 유지한다.

## 11. Run System

Run은 게임 전체 플레이 세션을 관리하는 핵심 시스템이다.

```text
Run
├── Run Seed
├── Character
├── Generated Floor Graphs
├── Room Run States
├── Current Floor / Room
├── Artifacts
├── Play Time / Kill Count
├── Death
└── Clear
```

### 11.1 책임 분리

- `RunSession`: 새 Run 시작·종료, 캐릭터, 시작/종료 시간과 Backend 결과 전송 경계
- `RunProgress`: 불변 run seed, 현재 층·방, 처치 수와 방별 Run 상태의 소유자
- `FloorGenerator`: seed와 설정에서 결정적 층 그래프 생성
- `RoomGraphAssembler`: 생성 그래프를 현재 층의 Prefab 인스턴스와 문 슬롯에 적용
- `RoomGraphController`: 현재 방 활성화와 전환 처리

Run이 종료되면 `RunSession`이 Backend에 전달할 RunResult를 생성한다. 생성 그래프와 방별 상태는
MVP에서 로컬 Run 진행에만 사용하며 Unity와 Web이 Database에 직접 접근하지 않는다.

### 11.2 Seed 소유권과 파생

`RunSession`은 새 Run을 준비할 때 `UnityEngine.Random`과 독립적인 run seed를 한 번 생성하고
`RunProgress`에 저장한다. 같은 Run에서는 seed를 교체할 수 없다.

```text
Run Seed
  ├── Floor 1 Seed
  │     ├── Topology Seed
  │     └── Room Content Seeds
  ├── Floor 2 Seed
  └── Floor 3 Seed
```

층·방·재시도 seed는 명시적인 안정 해시 규칙으로 파생하며 프로세스마다 결과가 달라질 수 있는
런타임 `GetHashCode()`에 의존하지 않는다. 그래프 알고리즘의 random stream과 방 콘텐츠 선택
stream을 분리하여 한쪽의 추첨 횟수 변경이 다른 결과를 불필요하게 모두 바꾸지 않게 한다.
고정 seed override는 Setup과 Verification에서만 사용한다.

### 11.3 초기화 순서

초기화는 다음 순서를 보장한다.

```text
RunSession
  → run seed 생성·RunProgress 초기화
  → FloorGenerator가 3개 층의 GeneratedFloorGraph 생성·검증
  → 생성 결과와 RoomRunState 등록
  → RoomGraphAssembler가 첫 층 Room Prefab 구성
  → RoomGraphController가 시작방 하나만 활성화
  → 플레이어·카메라·RunProgress를 시작방에 배치
```

MonoBehaviour의 임의 `Awake()` 순서에 생성 성공 여부를 맡기지 않는다. 현재 실행 순서 속성은
안전망으로 유지하되, 상위 Run 초기화 경로가 각 단계의 성공·실패를 명시적으로 확인해야 한다.

### 11.4 층 인스턴스 수명

3개 층의 논리 그래프와 상태는 Run 동안 유지한다. Unity Room 인스턴스는 현재 층 단위로 구성하고
현재 방 하나만 활성화한다. 다음 층으로 이동한 뒤 이전 층으로 돌아가지 않는 MVP 규칙에서는 이전
층 View를 제거하거나 풀로 반환할 수 있지만, 논리 상태와 결과 기록은 Run 종료까지 유지한다.
첫 구현은 안전성을 우선해 현재 층의 모든 방을 한 번에 구성하고 최적화는 측정 후 진행한다.

## 12. Unity UI

UI는 게임 플레이 시스템과 분리한다. 기준 해상도는 `1920 × 1080`, 화면 비율은 16:9로 하며
해상도가 달라져도 같은 기준 배치와 안전 영역에서 비례 조정한다. Canvas Scaler, 최소 확인
해상도와 비-16:9 창 처리의 상세 규칙은 [UI 규격](./16-ui-spec.md)을 따른다.

Frontend Scene은 전투 오브젝트를 생성하지 않고 타이틀·로컬 프로필·홈·캐릭터 선택·스킬 강화와
설정을 담당한다. Game Scene은 Room·전투·HUD·일시정지와 Run 결과를 담당한다.

주요 화면:
UI
├── Frontend
│   ├── Title
│   ├── Local Profile Registration
│   ├── Home
│   ├── Character Select
│   ├── Skill Upgrade
│   └── Settings
└── Game
    ├── HUD
    ├── Artifact Selection
    ├── Pause
    └── Run Result
UI는 게임의 실제 상태를 직접 조작하기보다는 Game System의 상태를 표시하고 사용자 입력을 전달하는 역할을 한다.
예:
Player
↓
PlayerStats
↓
HUD

고정 HP/SP·스킬·미니맵·아티팩트와 보스 HUD는 Screen Space Canvas에서 표시한다. 피해·회복
숫자는 영향받은 전투 객체의 월드 위치에 생성하고 풀로 재사용한다. `Esc` 일시정지는 UI가 직접
각 객체를 멈추는 대신 Game System의 공통 일시정지 상태를 전환하고, 전투 시스템과 쿨타임이 그
상태를 참조한다.

로컬 프로필 저장은 UI 컴포넌트가 직접 처리하지 않는다. 프로필 서비스가 등록 요청 전에
`clientProfileId`를 만들고 `clientProfileId`, `userId`와 닉네임을 저장·복원한다. UI는 등록·로딩·오류
상태만 표시한다. 스킬 강화도 기존 Backend 진행 계약을 통해 수행하며 UI가 포인트나 레벨을 직접
변경하지 않는다.

## 13. Unity Data

게임에서 사용하는 데이터를 관리한다.

데이터는 정적 제작 데이터, Run 생성 데이터, 가변 Run 상태와 Scene 실행 객체로 구분한다.

| 구분 | 예 | 형태와 소유권 |
|---|---|---|
| 정적 제작 데이터 | CharacterDefinition, ItemDefinition, RoomDefinition, RoomProfile, EncounterDefinition | ScriptableObject 또는 Catalog, 에셋 값만 저장 |
| 로컬 프로필 | clientProfileId, 서버가 발급한 userId와 대소문자를 보존한 닉네임 | Unity 로컬 저장, 계정 인증 정보가 아님 |
| Run 생성 데이터 | GeneratedFloorGraph, GeneratedRoomNode, GridPosition, 방향 연결, 선택된 Room·Encounter ID | 일반 C# 데이터, seed로 재현 가능 |
| 가변 Run 상태 | RoomRunState, 현재 층·방, 완료 웨이브, 선택 보상, 획득 아티팩트 | RunProgress가 소유, Run 종료까지 유지 |
| Scene 실행 객체 | RoomController, Door Slot, 적·보상 인스턴스, 카메라 기준점 | Prefab/MonoBehaviour, 논리 데이터를 표시하고 실행 |

ScriptableObject에는 `IsVisited`, `IsCleared`, `HasRewarded` 같은 Run 상태를 저장하지 않는다. 같은
RoomDefinition을 여러 방이 선택해도 상태를 공유해서는 안 된다. Scene 객체를 다시 만들 때는 생성
데이터와 `RoomRunState`를 적용해 동일한 문 연결, 클리어와 아티팩트 획득 상태를 복원한다.

아이템의 공격력 증가량이나 이름 같은 정적 값도 코드에 직접 하드코딩하지 않고 데이터로 관리한다.
보물방이 제공하는 아티팩트는 기존 Item 데이터 계약을 재사용한다. 상점과 엘리프 경제는 후속
기능이며 Floor 생성 데이터에 구매 로직을 포함하지 않는다.

## 14. Unity Network

Backend API와 통신하는 영역이다.

### 책임

- HTTP 요청
- JSON Serialization / Deserialization
- 로컬 플레이어 등록과 프로필 조회
- 캐릭터 진행 조회와 스킬 강화
- Run Result 전송
- 서버 응답 처리
- 통신 실패 처리
- 구조:
  Frontend / Run System
  ↓
  User Registration / Progress / RunResult
  ↓
  API Client
  ├── POST /api/users
  ├── GET /api/users/:nickname
  ├── PUT /api/users/:nickname/characters/:characterId/skills/:skillType
  └── POST /api/runs
  ↓
  Backend
  게임 플레이 로직이 HTTP 통신 코드에 직접 의존하지 않도록 분리한다.

## 15. Unity 전체 흐름

EXE 실행
↓
Frontend Scene: Title
↓
로컬 프로필 확인 또는 POST /api/users
↓
Home / Skill Upgrade / Settings
↓
Character Select
↓
GameLaunchRequest (`userId`, nickname, characterId)
↓
Run Start
↓
Game Scene 전환
↓
Run Seed와 3개 층 논리 그래프 생성
↓
첫 층 Room Prefab 구성
↓
시작방 활성화
↓
격자 탐색과 일반방 전투
↓
보물방 아티팩트 후보 3개 중 1개 선택
↓
먼 끝방의 Boss
↓
Floor Complete
↓
다음 층 그래프 View 구성
↓
Final Boss
↓
Clear / Death
↓
RunResult
↓
Network
↓
Run Result UI
↓
Frontend Scene의 Home 또는 Character Select

## 16. Backend Architecture

### 16.1 Backend의 책임

Backend는 게임을 실행하는 역할이 아니라 게임에서 발생한 데이터를 검증하고 저장하며 Web에서 사용할 수 있는 형태로 제공하는 역할을 담당한다.

#### 주요 책임

- API 제공
- Request Validation
- Business Logic
- Database 접근
- Run 데이터 저장
- 유저 전적 조회
- 통계 계산
- 랭킹 조회

## 17. Backend 내부 구조

NestJS를 사용할 경우 다음과 같은 모듈 구조를 우선 검토한다.
Backend
│
├── Common
│
├── Users
├── Runs
├── Characters
├── Items
├── Statistics
├── Rankings
│
├── Database
└── Config
실제 Framework가 변경될 경우에도 기능별 책임 분리라는 개념은 유지한다.

## 18. Backend Module

### 18.1 Users

유저 관련 기능을 담당한다.

#### 예

- 닉네임 기반 로컬 플레이어 등록
- User와 초기 캐릭터 진행 데이터의 원자적 생성
- 유저 조회
- 닉네임 검색
- 유저 전적 조회

### 18.2 Runs

게임 플레이 기록을 담당하는 핵심 모듈이다.

#### 예

- POST /runs
- GET /runs/:id
- GET /users/:nickname/runs

#### 책임

- Run Result 수신
- 데이터 검증
- Run 저장
- Run 상세 조회

### 18.3 Characters

캐릭터 관련 데이터를 관리한다.
현재는 1종만 존재하지만 향후 확장을 고려한다.
에르핀
Character B
Character C

### 18.4 Items

아이템 관련 데이터를 관리한다.

#### 예

- 아이템 목록
- 아이템 정보
- 아이템 통계
- 아이템 선택률

### 18.5 Statistics

게임 전체의 통계를 제공한다.

#### 예

- 캐릭터 승률
- 평균 도달 층
- 아이템 선택률
- 아이템 클리어율
- 평균 플레이 시간

### 18.6 Rankings

랭킹 데이터를 제공한다.

#### 예

Clear Time Ranking

1. User A
2. User B
3. User C
   또는
   Highest Floor Ranking

4. User A
5. User B
6. User C

## 19. Backend 데이터 흐름

### Run 저장

Unity
↓
POST /api/runs
↓
Controller
↓
Validation
↓
Service
↓
Database
↓
Response
각 계층의 책임을 분리한다.
Controller
→ HTTP 처리

Service
→ Business Logic

Repository / ORM
→ Database 접근

## 20. Backend 데이터 검증

Unity에서 전달되는 데이터는 신뢰하지 않는 것을 원칙으로 한다.
예:
{
"reachedFloor": 9999,
"playTime": -100
}
같은 비정상적인 값이 전달될 가능성을 고려한다.
따라서 Backend에서 다음을 검증한다.

- 필수 필드
- 데이터 타입
- 허용 범위
- 존재하는 Character인지
- 존재하는 Item인지
- 비정상적인 값인지

## 21. Web Architecture

### 21.1 Web의 책임

Web Service는 Backend에서 제공하는 데이터를 사용하여 유저 전적과 게임 통계를 보여준다.

#### 주요 책임

- 유저 검색
- 전적 조회
- Run 상세 조회
- 랭킹
- 게임 통계
- 차트 시각화
  Web에서는 게임 로직을 직접 처리하지 않는다.

## 22. Web 내부 구조

Next.js App Router 기준으로 다음 구조를 우선 검토한다.
Web
│
├── app
│ ├── page
│ ├── search
│ ├── users
│ ├── runs
│ ├── rankings
│ └── statistics
│
├── components
│
├── features
│
├── lib
│
├── types
│
└── hooks
실제 프로젝트 규모에 따라 구조는 조정한다.

## 23. Web Page 구조

### Home

/
서비스의 메인 페이지.

### User Search

/search
유저 닉네임 검색.

### User Profile

/users/:nickname
유저의 전적을 보여준다.
예:
User A

Total Runs
Win Rate
Average Floor
Average Play Time

Recent Runs

### Run Detail

/runs/:runId
특정 Run의 상세 정보를 보여준다.
Character
Play Time
Reached Floor
Clear / Death
Kill Count
Items
Death Reason

### Ranking

/rankings
전체 유저의 랭킹을 보여준다.

### Statistics

/statistics
게임 전체 통계를 보여준다.

## 24. Web Data Flow

Web은 Backend API를 통해 데이터를 가져온다.
User
↓
Next.js Page
↓
API Client
↓
Backend REST API
↓
Database
↓
Backend Response
↓
Next.js
↓
UI

## 25. Web Components

UI는 재사용 가능한 Component로 분리한다.
예:
components
├── UserSearch
├── UserProfile
├── RunCard
├── RunList
├── RankingTable
├── StatCard
└── Charts

## 26. 전체 시스템 Architecture

최종적인 시스템은 다음과 같이 구성한다.
┌──────────────────┐
│ Unity Client │
│ │
│ Unity + C# │
└────────┬─────────┘
│
│ HTTPS
│ JSON
▼
┌──────────────────┐
│ Backend │
│ │
│ Node.js │
│ TypeScript │
│ NestJS (?) │
└────────┬─────────┘
│
│ SQL / ORM
▼
┌──────────────────┐
│ PostgreSQL │
│ Supabase │
└────────┬─────────┘
▲
│
│ REST API
│
┌────────┴─────────┐
│ Web │
│ │
│ Next.js │
│ TypeScript │
└──────────────────┘

## 27. 전체 데이터 흐름

### 27.1 게임 플레이

Player
↓
Room
↓
Combat
↓
Item
↓
Floor
↓
Boss
↓
Clear / Death

### 27.2 게임 데이터 생성

Game State
↓
Run System
↓
RunResult

### 27.3 게임 데이터 저장

RunResult
↓
Unity Network
↓
POST /api/runs
↓
Backend
↓
Validation
↓
Service
↓
PostgreSQL

### 27.4 Web 전적 조회

User
↓
Search
↓
Next.js
↓
GET /api/users/:nickname
↓
Backend
↓
PostgreSQL
↓
Response
↓
Next.js
↓
User Profile

### 27.5 통계 조회

Web
↓
GET /api/statistics
↓
Backend
↓
Database Query
↓
Aggregation
↓
JSON
↓
Chart

## 28. 시스템 간 책임

기능 Unity Backend Database Web
플레이어 이동 O X X X
전투 O X X X
몬스터 AI O X X X
아이템 효과 O X X X
Run 생성 O X X X
Run 검증 일부 O X X
Run 저장 X O O X
전적 조회 X O O O
통계 계산 X O O O
차트 표시 X X X O
랭킹 조회 X O O O

## 29. 중요한 데이터 경계

### Unity가 관리하는 데이터

게임 실행 중 실시간으로 변하는 데이터.
Local Profile Reference (`userId`, nickname)
Run Seed
Generated Floor Graphs
Room Run States
Player HP
Current Room
Current Floor
Enemy State
Current Items
Combat State

### Backend가 관리하는 데이터

서버에서 검증하고 처리해야 하는 데이터.
Nickname Validation / Duplicate Check
Run Result
User Record
Statistics
Ranking

### Database가 관리하는 데이터

영구적으로 저장해야 하는 데이터.
Users
Characters
Items
Runs
RunItems

### Web이 관리하는 데이터

사용자에게 보여주기 위한 UI 상태.
Search Query
Selected Filter
Chart State
Pagination
UI State

## 30. 데이터 소유권 원칙

각 데이터는 가능한 한 명확한 소유자를 가진다.
예:
현재 HP
→ Unity

Run 결과
→ Backend

영구 Run 기록
→ Database

차트 표시 상태
→ Web
한 시스템에서 관리해야 하는 데이터를 다른 시스템이 직접 수정하지 않는다.

## 31. 게임과 서버의 연결 시점

게임 플레이 중 모든 데이터를 서버에 실시간으로 전송하지 않는다.
MVP에서는 최초 로컬 플레이어 등록, Frontend의 진행 조회·스킬 강화와 Run 종료 시점에만 필요한
요청을 보낸다. 실시간 위치·전투 상태는 서버로 전송하지 않는다.

최초 실행
↓
clientProfileId 생성·로컬 저장
↓
닉네임 등록과 초기 진행 생성 또는 같은 요청 결과 복원
↓
`userId`·nickname 로컬 저장
↓
게임 시작
↓
로컬에서 Run 진행
↓
게임 종료
↓
RunResult 생성
↓
Backend 전송

Run seed, 층 그래프와 방별 방문·클리어 상태는 MVP에서 로컬 Run 진행과 재현 검증을 위한 Unity
데이터다. 현재 Run Result API와 Database 계약에는 추가하지 않는다. 향후 버그 재현이나 통계 요구가
명확해질 때 별도 계약 변경으로 검토하며, 그 경우 Backend DTO·서비스·Database·Unity DTO·Web
소비 코드를 함께 갱신한다.

### 이유

- 네트워크 요청 감소
- 구현 복잡도 감소
- 서버 의존성 감소
- 게임 플레이 중 네트워크 문제의 영향 최소화
- 포트폴리오 프로젝트의 범위 축소
  향후 필요할 경우 주요 이벤트 로그를 서버에 추가로 저장할 수 있다.

## 32. 향후 확장 가능성

현재 구조는 MVP를 기준으로 하지만 다음과 같은 확장을 고려한다.

### Game

추가 캐릭터
추가 아이템
추가 몬스터
추가 층
추가 게임 모드
층당 8~12방 확장
상점과 엘리프 경제
열쇠와 잠긴 보물방
이벤트방·비밀방과 미니맵 탐색 기능 고도화

### Backend

인증
실시간 통계
이벤트 로그
캐싱
관리자 기능

### Web

상세 통계
빌드 분석
아이템 조합 분석
캐릭터 비교
기간별 통계
단, MVP 단계에서는 구현하지 않는다.

## 33. Architecture 핵심 원칙

본 프로젝트의 아키텍처는 복잡한 기술을 사용하는 것보다 명확한 책임 분리를 우선한다.
Unity
→ 게임을 실행한다.

Backend
→ 데이터를 검증하고 처리한다.

Database
→ 데이터를 저장한다.

Web
→ 데이터를 보여준다.
최종적으로 다음 구조를 유지하는 것을 목표로 한다.
┌───────────┐
│ Unity │
│ Game Play │
└─────┬─────┘
│
▼
┌───────────┐
│ Backend │
│ Logic │
└─────┬─────┘
│
▼
┌───────────┐
│ PostgreSQL│
│ Data │
└─────┬─────┘
▲
│
┌─────┴─────┐
│ Web │
│ Display │
└───────────┘
각 시스템은 자신의 역할에 집중하고, 시스템 간 통신은 명확한 API 경계를 통해 이루어진다.
