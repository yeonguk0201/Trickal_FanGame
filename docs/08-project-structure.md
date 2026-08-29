# Project Structure

## 1. 개요

본 프로젝트는 하나의 저장소 안에서 다음 세 가지 시스템을 함께 개발한다.

- Game: Unity Client
- Backend: REST API와 Database 접근
- Web: 전적 검색과 통계 UI

각 시스템은 역할과 책임을 분리한다.

```text
┌─────────────────────────────────────────────┐
│                   Project                   │
│                                             │
│  ┌────────────┐  ┌────────────┐  ┌───────┐ │
│  │    Game    │  │  Backend   │  │  Web  │ │
│  │   Unity    │  │ Node.js    │  │Next.js│ │
│  │    C#      │  │TypeScript  │  │  TS   │ │
│  └────────────┘  └────────────┘  └───────┘ │
│                                             │
│                  ┌──────────┐               │
│                  │   Docs   │               │
│                  └──────────┘               │
└─────────────────────────────────────────────┘
```

## 2. 전체 Repository 구조

최상위 구조는 다음을 기본으로 한다.

```text
project-root/
│
├── .agents/
│   └── skills/            # 저장소 전용 Codex 워크플로
│
├── game/
│   └── ...
│
├── backend/
│   └── ...
│
├── web/
│   └── ...
│
├── docs/
│   └── ...
│
├── AGENTS.md              # 저장소 공통 개발 규칙
├── .gitignore
├── README.md
└── package.json
```

각 디렉토리의 역할은 다음과 같다.

| Directory | 역할 |
|---|---|
| `game/` | Unity 게임 클라이언트 |
| `backend/` | REST API 서버 |
| `web/` | 전적 검색 웹 서비스 |
| `docs/` | 프로젝트 설계 및 개발 문서 |
| `.agents/skills/` | 저장소에서 반복 사용하는 Codex 작업 워크플로 |
| `AGENTS.md` | 인코딩, 계약, Unity 안전성과 검증에 관한 저장소 공통 규칙 |


## 3. Game Structure

Unity 프로젝트는 실제로 사용하는 기능 단위 폴더를 기준으로 확장한다. 아직 구현하지 않은 시스템을 위해 빈 폴더를 미리 만들지 않는다.

```text
game/
├── Assets/
│   ├── Characters/
│   ├── Editor/
│   ├── Items/
│   ├── Prefabs/
│   ├── Rooms/
│   │   ├── Definitions/
│   │   ├── Prefabs/        # Phase F-5에서 추가
│   │   └── Catalogs/       # 카탈로그가 실제로 필요할 때 추가
│   ├── Scenes/
│   │   └── SampleScene.unity
│   ├── Scripts/
│   │   ├── Character/
│   │   ├── Combat/
│   │   ├── Enemy/
│   │   ├── Item/
│   │   ├── Network/
│   │   ├── Player/
│   │   ├── Room/
│   │   └── Run/
│   └── Settings/
├── Packages/
└── ProjectSettings/
```

현재 방 정의 에셋은 `Assets/Rooms/Definitions/`, 캐릭터와 아티팩트 데이터는 각각 `Assets/Characters/`, `Assets/Items/`에 둔다. Phase F-5의 4방향 방 Prefab은 범용 `Assets/Prefabs/`가 아니라 방 도메인에 가까운 `Assets/Rooms/Prefabs/`에 둔다.

## 4. Game Scripts

Unity C# 코드는 현재 존재하는 기능 경계를 따른다.

| 디렉터리 | 책임 |
|---|---|
| `Character/` | 캐릭터 정의와 캐릭터별 능력 |
| `Combat/` | 체력, 피해, 투사체 등 공용 전투 규칙 |
| `Enemy/` | 적 이동, 공격, 사망과 스폰 |
| `Item/` | 아티팩트 정의, 효과, 획득 처리 |
| `Network/` | Backend API DTO와 통신 |
| `Player/` | 플레이어 입력, 이동, 공격과 상태 |
| `Room/` | 방 정의, 층 그래프 생성, 배치, 이동과 방 상태 |
| `Run/` | Run 시작·종료와 Run 수명주기 |

범용 `Data/`, `Stage/`, `GameFlow/` 폴더를 별도로 만들지 않는다. 생성 결과는 `Room`, Run 수명주기는 `Run`, API 계약은 `Network`처럼 소유 기능 가까이에 둔다. 파일 수가 적을 때는 평면 구조를 유지하고, 책임이 실제로 나뉠 때만 하위 폴더를 추가한다.
## 5. 공용 코드

현재 `Scripts/Core/`는 두지 않는다. 공용 코드라도 실제 소유 기능에 배치하며, 여러 기능이 공유하고 독립 수명주기가 확인된 코드만 별도 공용 영역으로 승격한다. `GameManager`, `ServiceLocator` 같은 전역 관리자는 구체적인 필요와 초기화 계약 없이 추가하지 않는다.

## 6. Player와 Character

`Player/`는 입력, 이동, 공격, SP와 플레이 중 상태를 담당한다. `Character/`는 에르핀을 포함한 캐릭터 정의 데이터와 선택 UI를 담당한다. 캐릭터가 늘어나더라도 공통 전투·이동 코드를 캐릭터별 폴더에 복제하지 않는다.

## 7. Enemy

`Enemy/`는 일반 적과 보스의 런타임 행동을 함께 관리한다. 현재 `BossController`도 이 폴더에 있으며, 보스 구현 규모가 커져 독립된 제작·검증 흐름이 생길 때만 `Enemy/Boss/` 하위 폴더를 만든다.

## 8. Combat

`Combat/`은 Player와 Enemy가 공유하는 피해 계산, `Health`, `IDamageable`, 투사체와 넉백 계약을 담당한다. 공격 주체별 행동은 `Player/`와 `Enemy/`에 두고, 공용 판정만 `Combat/`에 둔다.

## 9. Item과 아티팩트

코드의 기존 영문 안정 키와 타입명은 호환성을 위해 `Item`을 유지할 수 있지만, 게임 디자인 용어는 아티팩트를 사용한다. `Item/`은 아티팩트 정의, 획득, 인벤토리, 효과와 보상 드롭을 담당하고 정적 에셋은 `Assets/Items/`에 둔다. 보물방은 방 종류 이름이며, 보물방의 MVP 보상은 아티팩트다. 엘리프 재화와 상점은 도입 시 별도 소유 책임을 정한다.
## 10. Room

`Scripts/Room/`은 방의 정적 정의, seed 기반 층 그래프 생성, 런타임 배치와 이동을 담당한다. Phase F-5에서 책임이 커지면 다음 구조를 목표로 하되, 기존 파일은 기능 변경과 함께 안전하게 이동할 이유가 생길 때만 정리한다.

```text
Room/
├── Generation/
│   ├── FloorGenerator.cs
│   ├── FloorGenerationSettings.cs
│   ├── GeneratedFloorGraph.cs
│   ├── GeneratedRoomNode.cs
│   ├── GridPosition.cs
│   ├── RoomDoorDirection.cs
│   └── StableSeedDerivation.cs
├── Runtime/
│   ├── RoomGraphAssembler.cs
│   ├── RoomGraphController.cs
│   ├── RoomNode.cs
│   ├── RoomRunState.cs
│   ├── RoomController.cs
│   ├── RoomDoorSlot.cs
│   ├── RoomDoorway.cs
│   ├── DoorController.cs
│   └── RoomCameraController.cs
└── Authoring/
    ├── RoomDefinition.cs
    ├── RoomPrefabCatalog.cs
    └── EncounterDefinition.cs   # 별도 조우 데이터가 필요할 때 추가
```

구조별 책임은 다음과 같다.

- `Generation/`: Unity Scene 오브젝트를 직접 조작하지 않는 순수 생성 데이터와 알고리즘
- `Runtime/`: 생성 결과를 Prefab 인스턴스와 문 슬롯에 바인딩하고 현재 방 활성화·이동·재방문 상태를 관리
- `Authoring/`: 검증된 방 Prefab, 방 종류, 몬스터 후보와 스폰 지점을 연결하는 제작 데이터

현재 평면 구조의 `FloorGenerator`, `RoomGraphAssembler`, `RoomGraphController`, `RoomDefinition` 등은 위 책임을 이미 나누어 가진다. Phase F-5 구현만을 위해 전부 이동할 필요는 없으며, 새 파일부터 적절한 하위 폴더에 두어도 된다. 폴더와 namespace를 반드시 일치시키지 않아도 되며 기존 `TrickalFanGame.Room` namespace를 유지할 수 있다.

`RoomDefinition`은 검증된 방 레이아웃과 콘텐츠 후보를 참조하고, `GeneratedRoomNode`는 `roomId`, 그리드 좌표, 방 종류, 연결 방향과 선택된 콘텐츠를 담는다. `RoomRunState`는 방문·클리어·아티팩트 수령 상태만 보유한다. 안정 키인 `floor-XX-room-YY`와 배치 좌표를 분리하여 좌표나 seed가 바뀌어도 ID 형식을 유지한다.

`Assets/Rooms/`의 목표 구조는 다음과 같다.

```text
Rooms/
├── Definitions/     # 현재 RoomDefinition 에셋
├── Prefabs/         # 4방향 문 슬롯을 갖는 검증된 방 Prefab
├── Catalogs/        # Prefab·방 종류 선택 카탈로그가 도입될 때
└── Encounters/      # 몬스터 조우를 별도 에셋으로 분리할 때
```

`Prefabs/`만 Phase F-5의 필수 추가 대상이다. `Catalogs/`와 `Encounters/`는 실제 데이터 타입을 도입하기 전에는 만들지 않는다.

## 11. Floor와 Run 소유권

현재 별도의 `Scripts/Stage/`를 만들지 않는다. 층은 방 그래프의 집합이므로 생성과 이동 책임은 `Room`에 두고, 전체 Run과 층 수명주기는 `Run`이 소유한다. `StageGenerator`와 `FloorGenerator`처럼 같은 책임의 클래스를 중복해서 만들지 않는다.

```text
Run/
├── RunSession.cs
├── PlayerDeathReason.cs
└── RunProgress.cs          # 장기 목표 위치
```

현재 `RunProgress.cs`는 역사적으로 `Scripts/Room/`에 있지만 역할상 `Run/`이 목표 위치다. Phase F-5에서 이동이 꼭 필요하지는 않으며, 이동한다면 Unity가 참조를 보존하도록 파일과 `.meta`를 함께 옮기고 namespace 변경은 별도 검증 가능한 작업으로 취급한다.

초기화 소유권은 `RunSession → RunProgress → FloorGenerator → RoomGraphAssembler → RoomGraphController` 순서를 유지한다. `RunSession`은 새 Run의 seed를 한 번 확정하고, 같은 Run의 층 이동과 재방문에서는 기존 `RunProgress`와 생성 결과를 재사용한다.
12. Combat
전투 시스템에서 공통으로 사용하는 기능을 관리한다.
Combat/
├── DamageInfo.cs
├── DamageType.cs
├── Health.cs
├── HitDetector.cs
├── Projectile.cs
└── AttackSystem.cs
가능하면 Player와 Enemy가 동일한 전투 시스템을 공유할 수 있도록 설계한다.
예:
IDamageable
    │
    ├── Player
    ├── Enemy
    └── Boss
13. UI
게임 내 UI를 관리한다.
UI/
├── Common/
├── Title/
├── CharacterSelect/
├── InGame/
├── Result/
└── Components/
예:
UI/
├── CharacterSelect/
│   └── CharacterSelectUI.cs
│
├── InGame/
│   ├── HUD.cs
│   ├── HealthBar.cs
│   ├── ItemDisplay.cs
│   └── Minimap.cs
│
└── Result/
    └── ResultUI.cs
## 14. Data 배치 원칙

범용 `Scripts/Data/` 폴더는 만들지 않는다. 데이터는 소유 기능과 수명주기에 따라 배치한다.

| 데이터 | 위치 |
|---|---|
| 방 제작 데이터 | `Assets/Rooms/Definitions/` 및 `Scripts/Room/Authoring/` |
| seed에서 파생된 층 그래프 | `Scripts/Room/Generation/` |
| 방문·클리어·아티팩트 수령 상태 | `Scripts/Room/Runtime/` 또는 소유권 정리 후 `Scripts/Run/` |
| 아티팩트·캐릭터 정적 데이터 | `Assets/Items/`, `Assets/Characters/` |
| Backend 요청·응답 DTO | `Scripts/Network/` |

로컬 생성용 `runSeed`, 그리드 좌표와 문 연결 정보는 게임 내부 데이터다. 현재 Run 저장 API에 필요하지 않으므로 Backend DTO나 Web 타입에 추가하지 않는다. 서버에 저장할 요구가 확정될 때 `docs/06-database.md`와 `docs/07-api.md`의 계약부터 함께 변경한다.
15. Network
Backend API와 통신하는 기능을 관리한다.
Network/
├── ApiClient.cs
├── ApiConfig.cs
├── RunApi.cs
├── ApiResponse.cs
└── NetworkError.cs
게임 로직이 HTTP 통신을 직접 수행하지 않도록 한다.
나쁜 구조:
GameManager
 ↓
UnityWebRequest
권장 구조:
GameManager
 ↓
RunApi
 ↓
ApiClient
 ↓
Backend
## 16. Game Flow

현재 별도 `GameFlow/` 폴더나 `GameSession`, `RunManager`를 추가하지 않는다. `RunSession`이 새 Run 시작·종료를, `RunProgress`가 seed·현재 층·방별 런타임 상태를 소유하고 각 기능 컨트롤러를 명시적으로 초기화한다.

```text
RunSession
  → RunProgress
  → FloorGenerator
  → RoomGraphAssembler
  → RoomGraphController
  → RoomController / Combat / Reward
```

화면 전환을 포함한 독립 상태 머신이 실제로 필요해질 때만 `GameFlow/`를 도입한다. 그 전에는 기존 Run·Room 시스템과 같은 책임의 전역 관리자를 만들지 않는다.

### 16.1 Editor Setup과 Verification

현재 `Assets/Editor/`의 `Week*Setup.cs`와 `Week*Verification.cs` 명명 방식을 유지한다. Phase F-5에서도 최소한 다음 책임을 분리한다.

- Setup: 방 Prefab, 4방향 문 슬롯, 정의 에셋과 직렬화 참조를 재실행 가능하게 구성
- 생성 검증: 같은 seed의 동일 결과, 다른 seed의 변화, 방 수·연결성·필수 방·보스 거리 불변조건 확인
- Prefab 계약 검증: 연결 방향과 문 슬롯 대응, 스폰 지점, 중복 문과 누락 참조 확인
- Run 바인딩 검증: 현재 방만 활성화되고 재방문 상태와 seed 기반 구성이 유지되는지 확인

런타임 생성 규칙은 Editor Setup에 넣지 않고 `FloorGenerator`와 런타임 코드에 둔다. 기존 Editor 파일 수가 관리하기 어려워질 때 `Assets/Editor/Room/` 같은 기능 하위 폴더를 도입할 수 있으나, Phase F-5 때문에 기존 파일 전체를 이동하지 않는다.

Setup은 재실행해도 오브젝트·에셋·컴포넌트를 중복 생성하지 않아야 한다. Scene이나 Prefab의 대규모 YAML 직접 편집보다 Editor 구성 코드를 우선하며, 핵심 불변조건이 깨지면 Verification이 명시적으로 실패해야 한다.
17. Backend Structure
Backend는 Node.js + TypeScript를 기반으로 구성한다.
Framework는 NestJS를 우선 검토한다.
backend/
│
├── src/
│   │
│   ├── modules/
│   │   ├── users/
│   │   ├── runs/
│   │   ├── rankings/
│   │   ├── statistics/
│   │   ├── characters/
│   │   └── items/
│   │
│   ├── common/
│   │   ├── dto/
│   │   ├── exceptions/
│   │   ├── guards/
│   │   ├── interceptors/
│   │   └── utils/
│   │
│   ├── config/
│   │
│   ├── database/
│   │
│   ├── app.module.ts
│   └── main.ts
│
├── test/
├── prisma/
│   └── schema.prisma
│
├── package.json
├── tsconfig.json
├── .env
├── .env.example
└── README.md
18. Backend Modules
Backend는 기능 단위 Module로 분리한다.
modules/
├── users/
├── runs/
├── rankings/
├── statistics/
├── characters/
└── items/
각 Module은 가능한 한 독립적인 책임을 가진다.
19. Runs Module
가장 중요한 Backend Module이다.
runs/
├── runs.controller.ts
├── runs.service.ts
├── runs.repository.ts
├── dto/
│   ├── create-run.dto.ts
│   └── run-response.dto.ts
└── entities/
    └── run.entity.ts
역할:
POST /api/runs
GET /api/runs/:runId
20. Users Module
users/
├── users.controller.ts
├── users.service.ts
├── users.repository.ts
├── dto/
└── entities/
역할:
GET /api/users/:nickname
GET /api/users/:nickname/runs
21. Statistics Module
통계 계산을 담당한다.
statistics/
├── statistics.controller.ts
├── statistics.service.ts
├── statistics.repository.ts
└── dto/
예:
전체 통계
캐릭터 통계
아이템 통계
22. Rankings Module
랭킹 조회를 담당한다.
rankings/
├── rankings.controller.ts
├── rankings.service.ts
├── rankings.repository.ts
└── dto/
예:
최고 도달 층
최단 클리어 시간
23. Backend Layer
Backend 내부는 기본적으로 다음 계층으로 나눈다.
Controller
    ↓
Service
    ↓
Repository
    ↓
Database
각 역할:
Controller
HTTP Request / Response를 담당한다.
Service
실제 비즈니스 로직을 담당한다.
Repository
Database 접근을 담당한다.
24. Backend 의존성 방향
권장 방향:
Controller
    ↓
Service
    ↓
Repository
    ↓
Database
반대로 Database가 Controller를 알면 안 된다.
Database
   X
Controller
25. Web Structure
Web은 Next.js + TypeScript를 사용한다.
web/
│
├── app/
│   ├── page.tsx
│   │
│   ├── users/
│   │   └── [nickname]/
│   │       ├── page.tsx
│   │       └── loading.tsx
│   │
│   ├── runs/
│   │   └── [runId]/
│   │       └── page.tsx
│   │
│   ├── ranking/
│   │   └── page.tsx
│   │
│   └── statistics/
│       └── page.tsx
│
├── components/
│   ├── common/
│   ├── user/
│   ├── run/
│   ├── ranking/
│   └── statistics/
│
├── lib/
│   ├── api/
│   ├── utils/
│   └── constants/
│
├── types/
│   ├── user.ts
│   ├── run.ts
│   ├── ranking.ts
│   └── statistics.ts
│
├── hooks/
│
├── public/
│
├── package.json
├── tsconfig.json
├── next.config.ts
├── .env.local
└── .env.example
26. Web Page Structure
MVP 기준 주요 페이지:
/
├── Home
│
├── /users/[nickname]
│   └── User Profile
│
├── /runs/[runId]
│   └── Run Detail
│
├── /ranking
│   └── Ranking
│
└── /statistics
    └── Statistics Dashboard
27. Web Components
Page와 UI Component를 분리한다.
예:
User Page
    │
    ├── UserProfile
    ├── UserStats
    ├── RunHistory
    └── RunCard
통계 페이지:
Statistics Page
    │
    ├── OverviewCard
    ├── CharacterWinRateChart
    ├── ItemPickRateChart
    └── FloorDistributionChart
28. Web API Layer
Web에서 Backend API 호출은 lib/api에서 관리한다.
lib/
└── api/
    ├── client.ts
    ├── users.ts
    ├── runs.ts
    ├── rankings.ts
    └── statistics.ts
예:
components
    ↓
lib/api/users.ts
    ↓
Backend API
Component에서 직접 fetch()를 반복해서 작성하지 않는다.
나쁜 예:
const response = await fetch(
  "https://backend.example.com/api/users/test"
);
권장:
const user = await getUser("test");
29. Web Types
Backend API Response에 대응하는 TypeScript Type을 정의한다.
예:
types/
├── user.ts
├── run.ts
├── ranking.ts
└── statistics.ts
예:
export interface UserStats {
  totalRuns: number;
  clears: number;
  winRate: number;
  averagePlayTime: number;
  averageFloor: number;
  highestFloor: number;
}
## 30. 전체 프로젝트 구조

```text
project-root/
├── game/                         # Unity 프로젝트
├── backend/                      # Node.js + TypeScript API
├── web/                          # Next.js + TypeScript
├── docs/
│   ├── 00-project-overview.md
│   ├── 01-tech-stack.md
│   ├── 02-roadmap.md
│   ├── 03-game-design.md
│   ├── 04-architecture.md
│   ├── 05-development-setup.md
│   ├── 06-database.md
│   ├── 07-api.md
│   ├── 08-project-structure.md
│   ├── 09-first-month-plan.md
│   ├── 10-second-month-plan.md
│   ├── 11-artifact-reference.md
│   ├── 12-skill-system-plan.md
│   ├── 13-development-tooling-plan.md
│   └── drafts/                   # 검토 전 참고안, 공식 계약이 아님
├── .agents/skills/               # 저장소 전용 작업 스킬
├── AGENTS.md
├── .gitignore
└── README.md
```
31. 시스템 간 의존성
각 시스템의 의존성은 다음과 같이 유지한다.
                    ┌──────────────┐
                    │     Game     │
                    │    Unity     │
                    └──────┬───────┘
                           │
                           │ REST API
                           ▼
                    ┌──────────────┐
                    │   Backend    │
                    │   Node.js    │
                    └──────┬───────┘
                           │
                           │ SQL / ORM
                           ▼
                    ┌──────────────┐
                    │  PostgreSQL  │
                    └──────┬───────┘
                           ▲
                           │
                           │ API
                           │
                    ┌──────┴───────┐
                    │     Web      │
                    │   Next.js    │
                    └──────────────┘
32. 직접 Database 접근 금지
Game과 Web은 Database에 직접 접근하지 않는다.
잘못된 구조:
Unity ──────────→ PostgreSQL
Web ────────────→ PostgreSQL
권장 구조:
Unity ──→ Backend ──→ PostgreSQL

Web ────→ Backend ──→ PostgreSQL
이를 통해 데이터 검증과 비즈니스 로직을 Backend에서 통제한다.
33. Game과 Backend의 책임 분리
Game:
게임 플레이
게임 상태
전투
맵
아이템
캐릭터
Run 데이터 생성
Backend:
데이터 검증
데이터 저장
전적 계산
통계 계산
랭킹 계산
API 제공
Game에서 통계를 계산하거나 Database를 직접 조작하지 않는다.
34. Backend와 Web의 책임 분리
Backend:
데이터
비즈니스 로직
통계 계산
API
Web:
화면
사용자 인터랙션
데이터 시각화
API 데이터 표현
예:
Backend
"아이템 A 선택률 = 32.5%"
        ↓
Web
Chart로 시각화
35. 데이터 흐름
Game → Backend
Player
 ↓
Game Play
 ↓
Run
 ↓
Clear / Death
 ↓
RunResult
 ↓
POST /api/runs
 ↓
Backend Validation
 ↓
Database
Backend → Web
Web Request
 ↓
GET /api/statistics
 ↓
Backend
 ↓
Database
 ↓
Aggregation
 ↓
JSON
 ↓
Web
 ↓
Chart / Table / UI
36. Naming Convention
Unity
Class:
PascalCase
예:
PlayerController
GameManager
RunManager
변수:
camelCase
예:
playerHealth
currentFloor
runData
Backend
파일:
kebab-case
예:
runs.controller.ts
runs.service.ts
create-run.dto.ts
Class:
PascalCase
예:
RunsController
RunsService
CreateRunDto
Web
React Component:
PascalCase
예:
UserProfile
RunCard
StatisticsChart
파일:
PascalCase.tsx
또는 프로젝트 전체 규칙에 맞춰 일관성 있게 유지한다.
37. 환경 변수
Secret이나 환경별 설정은 코드에 직접 작성하지 않는다.
Backend:
backend/.env
예:
DATABASE_URL=
PORT=
CORS_ORIGIN=
Web:
web/.env.local
예:
NEXT_PUBLIC_API_URL=
Unity에서도 API 주소와 환경별 설정을 별도 Config로 관리한다.
38. 환경 분리
최소한 다음 환경을 고려한다.
Development
Production
향후 필요하면:
Development
Staging
Production
으로 확장한다.
39. Local Development
개발 시에는 다음과 같이 실행한다.
┌──────────────┐
│ Unity        │
│ Game Client  │
└──────┬───────┘
       │
       │ localhost
       ▼
┌──────────────┐
│ Backend      │
│ localhost    │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ Supabase     │
│ PostgreSQL   │
└──────────────┘

┌──────────────┐
│ Next.js      │
│ localhost    │
└──────┬───────┘
       │
       ▼
    Backend
40. Git Repository
가능하면 하나의 Git Repository에서 전체 프로젝트를 관리한다.
project-root
│
├── game
├── backend
├── web
└── docs
이 구조를 사용하면 Game → Backend → Web의 변경사항을 하나의 프로젝트에서 추적할 수 있다.
41. Commit Convention
Commit 메시지는 가능한 한 명확하게 작성한다.
예:
feat: add player movement
feat: add run result API
feat: add user statistics page

fix: fix enemy collision
fix: fix run save validation

refactor: simplify room manager

docs: update database schema
docs: add API specification
42. Branch Strategy
1인 개발 프로젝트이므로 복잡한 Git Flow는 사용하지 않는다.
기본적으로:
main
  │
  └── feature/*
예:
feature/player-movement
feature/room-system
feature/run-api
feature/user-profile
기능이 완료되면 main에 Merge한다.
## 43. 개발 시 구조 변경 원칙
초기 구조를 무조건 유지할 필요는 없다.
개발하면서 다음과 같은 문제가 발견되면 구조를 변경할 수 있다.
코드가 지나치게 복잡함
특정 모듈의 책임이 너무 많음
의존성이 꼬임
재사용이 어려움
테스트하기 어려움
중요한 것은 폴더 구조 자체보다 책임과 의존성의 명확성이다.

Unity의 `.cs`, `.asset`, Prefab, Scene을 이동할 때는 해당 `.meta`를 함께 이동하여 GUID와 직렬화 참조를 보존한다. 같은 내용을 새 경로에 다시 만들고 원본을 지우는 방식은 사용하지 않는다. 구조 정리와 namespace 변경은 가능하면 분리하고, Setup 재실행과 관련 Verification으로 참조 보존을 확인한다.

사용자 변경이 있는 작업 트리에서는 먼저 `git status --short`를 확인하고 관련 없는 변경을 보존한다. 기존 Scene·Prefab의 대규모 텍스트 수정 대신 작은 직렬화 변경이나 재실행 가능한 Editor Setup을 우선한다.
44. 과도한 추상화 금지
MVP 단계에서는 미래의 모든 확장을 고려하여 복잡한 구조를 만들지 않는다.
예를 들어 캐릭터가 현재 1종이라고 해서 처음부터 복잡한 Character Framework를 만들 필요는 없다.
현재:
Character
향후:
Character
├── 에르핀
├── Character B
└── Character C
실제 확장이 필요해지는 시점에 구조를 확장한다.
45. 현재 프로젝트의 확장 포인트
현재 프로젝트는 다음 부분을 확장할 수 있도록 설계한다.
Character
1종
 ↓
다수의 캐릭터
Item
10종
 ↓
20종
 ↓
50종
Enemy
기본 몬스터
 ↓
다양한 몬스터
Stage
1~3층
 ↓
다수의 층
API
MVP API
 ↓
인증
 ↓
상세 통계
 ↓
추가 게임 데이터
Web
전적 검색
 ↓
상세 분석
 ↓
빌드 분석
 ↓
랭킹
 ↓
통계 대시보드
## 46. MVP 기준 실제 구조

현재 Unity Script 구조는 `Character`, `Combat`, `Enemy`, `Item`, `Network`, `Player`, `Room`, `Run`으로 구성되어 있다. Phase F-5는 이 경계를 유지하면서 `Room` 내부에 생성·런타임·제작 책임을 추가한다.

```text
game/Assets/
├── Editor/                       # Week 단위 Setup과 Verification
├── Rooms/
│   ├── Definitions/             # 현재 존재
│   └── Prefabs/                 # Phase F-5 필수 추가
└── Scripts/
    ├── Character/
    ├── Combat/
    ├── Enemy/
    ├── Item/
    ├── Network/
    ├── Player/
    ├── Room/                    # 필요 시 Generation/Runtime/Authoring 확장
    └── Run/
```

층별 방 수가 6–8개에서 향후 8–12개로 늘어나더라도 폴더를 방 수만큼 만들지 않는다. 방의 차이는 `RoomDefinition`, Prefab, 생성 데이터로 표현하고 공통 런타임 코드를 공유한다.
47. 프로젝트 개발 순서와 Structure
프로젝트는 다음 순서로 실제 구조를 만들어간다.
1. Repository 생성
        ↓
2. Unity 프로젝트 생성
        ↓
3. Backend 프로젝트 생성
        ↓
4. Web 프로젝트 생성
        ↓
5. Supabase / PostgreSQL 연결
        ↓
6. Backend ↔ Database 연결
        ↓
7. 기본 API 구현
        ↓
8. Unity ↔ Backend 통신
        ↓
9. 게임 핵심 기능 구현
        ↓
10. Run 데이터 저장
        ↓
11. Web 전적 조회
        ↓
12. 통계 / 랭킹 구현
48. 개발 우선순위
기능을 구현할 때는 다음 우선순위를 유지한다.
게임 핵심 루프
        ↓
Run 데이터 생성
        ↓
Backend 저장
        ↓
Web 조회
        ↓
통계
        ↓
폴리싱
즉, 게임의 재미를 먼저 완성하려고 하기보다
게임
 ↓
데이터
 ↓
서버
 ↓
웹
전체 사이클이 빠르게 연결되는 것을 우선한다.
49. End-to-End 기준
이 프로젝트의 가장 중요한 개발 기준은 다음이다.
캐릭터 선택
    ↓
방 입장
    ↓
몬스터 전투
    ↓
아이템 획득
    ↓
다음 방
    ↓
보스
    ↓
클리어 / 사망
    ↓
RunResult 생성
    ↓
Backend 전송
    ↓
Database 저장
    ↓
Web 접속
    ↓
유저 검색
    ↓
전적 표시
이 전체 흐름이 동작하면 프로젝트의 핵심 기술적 목표가 달성된 것이다.
50. Definition of Done
Project Structure 문서는 다음 조건을 만족하면 완료로 정의한다.

Game 디렉토리 구조 정의

Backend 디렉토리 구조 정의

Web 디렉토리 구조 정의

Docs 디렉토리 구조 정의

Unity Script 책임 분리

Backend Module 책임 분리

Web Page / Component 구조 정의

시스템 간 의존성 정의

API 접근 방식 정의

Environment Variable 구조 정의

Git 구조 정의

Naming Convention 정의

MVP 초기 구조 정의

향후 확장 포인트 정의
51. 문서 역할

| 문서 | 역할 |
|---|---|
| `00-project-overview.md` | 프로젝트가 무엇인지 정의한다. |
| `01-tech-stack.md` | 사용하는 기술과 선택 기준을 정의한다. |
| `02-roadmap.md` | 장기 개발 순서와 Phase 완료 조건을 정의한다. |
| `03-game-design.md` | 게임 규칙과 콘텐츠 범위를 정의한다. |
| `04-architecture.md` | 시스템 연결과 데이터 흐름을 정의한다. |
| `05-development-setup.md` | 개발 환경과 구현된 도구의 실행 방법을 정의한다. |
| `06-database.md` | 저장 데이터와 Database 계약을 정의한다. |
| `07-api.md` | 시스템 사이의 API 계약을 정의한다. |
| `08-project-structure.md` | 실제 코드와 디렉터리의 책임을 정의한다. |
| `09-first-month-plan.md` | 첫 달 실행 순서와 결과를 기록한다. |
| `10-second-month-plan.md` | 둘째 달 실행 순서와 완료 조건을 관리한다. |
| `11-artifact-reference.md` | 원작 아티팩트 조사 근거를 기록한다. |
| `12-skill-system-plan.md` | 전투 스킬, 아이템과 메타 시스템의 확정 계약을 관리한다. |
| `13-development-tooling-plan.md` | 생성기, 검증기, 계약 검사, 텔레메트리와 MCP 도입 계획을 관리한다. |
| `drafts/` | 외부 참고안과 검토 전 설계를 보관한다. 공식 계약은 번호가 붙은 기준 문서에 반영된 내용만 따른다. |

52. 다음 단계
`00`~`08` 문서로 설계 단계의 1차 목표를 완료하고, `09` 이후 문서에서 실행 계획과 후속 설계를 관리한다.
다음 단계는 실제 개발 환경 구축이다.
[설계]

00 ~ 08
   ↓
[환경 구축]

Git
Unity
Node.js
TypeScript
Backend
Next.js
Supabase
PostgreSQL
   ↓
[개발]

Phase 0
   ↓
Phase 1
   ↓
Phase 2
   ↓
...
53. 환경 구축 전에 확인할 것
환경 구축을 시작하기 전에 다음 항목만 확인한다.
[ ] Git Repository 생성
[ ] Unity 버전 결정
[ ] Unity 프로젝트 생성
[ ] Node.js 버전 결정
[ ] Backend Framework 결정
[ ] Backend 프로젝트 생성
[ ] Next.js 프로젝트 생성
[ ] Supabase 프로젝트 생성
[ ] PostgreSQL 연결 확인
[ ] 환경 변수 구성
54. 개발 시작 조건
다음 조건을 만족하면 실제 게임 개발에 들어간다.
Game
    ↓
Unity 프로젝트 정상 실행

Backend
    ↓
서버 정상 실행
    ↓
Database 연결 성공

Web
    ↓
Next.js 정상 실행

Integration
    ↓
Backend → Database 연결 성공
    ↓
간단한 API Request / Response 성공
이후 첫 번째 실제 개발 목표는 다음과 같다.
Unity
 ↓
간단한 게임 데이터 생성
 ↓
POST /api/runs
 ↓
Backend
 ↓
PostgreSQL
이 흐름을 먼저 성공시키고 본격적인 게임 시스템 개발을 시작한다.
55. 최종 개발 방향
본 프로젝트는 다음 방향으로 개발한다.
              ┌──────────────┐
              │    Unity     │
              │              │
              │ Game Client  │
              └──────┬───────┘
                     │
                     │ Run Data
                     ▼
              ┌──────────────┐
              │   Backend    │
              │              │
              │ REST API     │
              │ Validation   │
              │ Statistics   │
              └──────┬───────┘
                     │
                     ▼
              ┌──────────────┐
              │  PostgreSQL  │
              │              │
              │ Game Data    │
              │ Run Data     │
              └──────┬───────┘
                     │
                     │ API
                     ▼
              ┌──────────────┐
              │     Web      │
              │              │
              │ User Search  │
              │ Ranking      │
              │ Statistics   │
              └──────────────┘
프로젝트의 핵심은 단순히 Unity 게임을 완성하는 것이 아니라, 게임에서 발생한 데이터를 Backend를 통해 저장하고 이를 Web에서 실제 전적과 통계로 활용하는 End-to-End 시스템을 완성하는 것이다.
