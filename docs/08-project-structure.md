# Project Structure

## 1. 개요

본 프로젝트는 하나의 저장소 안에서 다음 세 가지 시스템을 함께 개발한다.

```text
Game
Backend
Web
각 시스템은 역할과 책임을 분리한다.
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
2. 전체 Repository 구조
최상위 구조는 다음을 기본으로 한다.
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
각 디렉토리의 역할은 다음과 같다.
Directory	역할
game/	Unity 게임 클라이언트
backend/	REST API 서버
web/	전적 검색 웹 서비스
docs/	프로젝트 설계 및 개발 문서
.agents/skills/	저장소에서 반복 사용하는 Codex 작업 워크플로
AGENTS.md	인코딩, 계약, Unity 안전성과 검증에 관한 저장소 공통 규칙


3. Game Structure
Unity 프로젝트는 다음과 같은 구조를 기본으로 한다.
game/
│
├── Assets/
│   │
│   ├── Art/
│   │   ├── Characters/
│   │   ├── Enemies/
│   │   ├── Bosses/
│   │   ├── Items/
│   │   ├── Environment/
│   │   └── UI/
│   │
│   ├── Audio/
│   │   ├── BGM/
│   │   └── SFX/
│   │
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Enemies/
│   │   ├── Bosses/
│   │   ├── Items/
│   │   ├── Rooms/
│   │   └── UI/
│   │
│   ├── Scenes/
│   │   ├── Boot.unity
│   │   ├── Title.unity
│   │   ├── CharacterSelect.unity
│   │   └── Game.unity
│   │
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── Player/
│   │   ├── Enemy/
│   │   ├── Boss/
│   │   ├── Item/
│   │   ├── Room/
│   │   ├── Stage/
│   │   ├── Combat/
│   │   ├── UI/
│   │   ├── Data/
│   │   ├── Network/
│   │   └── GameFlow/
│   │
│   ├── ScriptableObjects/
│   │   ├── Characters/
│   │   ├── Enemies/
│   │   ├── Items/
│   │   └── Stages/
│   │
│   ├── Resources/
│   │
│   └── Settings/
│
├── Packages/
├── ProjectSettings/
└── README.md
4. Game Scripts
Unity 내부의 C# 코드는 기능별로 분리한다.
Scripts/
│
├── Core/
├── Player/
├── Enemy/
├── Boss/
├── Item/
├── Room/
├── Stage/
├── Combat/
├── UI/
├── Data/
├── Network/
└── GameFlow/
5. Core
게임 전체에서 공통으로 사용하는 시스템을 관리한다.
Core/
├── GameManager.cs
├── SceneLoader.cs
├── ServiceLocator.cs
└── GameConstants.cs
역할:
게임 초기화
공통 서비스 관리
Scene 전환
전역 상태 관리
Core는 특정 게임 콘텐츠에 강하게 의존하지 않도록 한다.
6. Player
플레이어 캐릭터와 관련된 기능을 담당한다.
Player/
├── PlayerController.cs
├── PlayerMovement.cs
├── PlayerCombat.cs
├── PlayerHealth.cs
├── PlayerStats.cs
└── PlayerInput.cs
현재 캐릭터는 1종만 구현한다.
그러나 향후 캐릭터가 추가될 수 있으므로 특정 캐릭터에 종속된 구조를 피한다.
Player
 └── Character
현재:
에르핀
향후:
에르핀
Character B
Character C
7. Enemy
일반 몬스터와 관련된 기능을 담당한다.
Enemy/
├── EnemyController.cs
├── EnemyHealth.cs
├── EnemyMovement.cs
├── EnemyAttack.cs
├── EnemyAI.cs
└── EnemySpawner.cs
몬스터의 종류가 늘어나더라도 공통 기능과 개별 기능을 분리한다.
예:
EnemyController
      │
      ├── EnemyMovement
      ├── EnemyAttack
      └── EnemyAI
8. Boss
보스 관련 시스템을 관리한다.
Boss/
├── BossController.cs
├── BossHealth.cs
├── BossAttack.cs
├── BossPattern.cs
└── BossPhase.cs
보스는 일반 몬스터보다 복잡한 패턴을 가질 수 있으므로 별도 영역으로 관리한다.
9. Item
아이템 및 아이템 효과를 관리한다.
Item/
├── ItemController.cs
├── ItemEffect.cs
├── ItemPickup.cs
├── ItemManager.cs
└── Synergy/
    └── ItemSynergyManager.cs
아이템 데이터는 ScriptableObject로 관리하는 것을 우선한다.
예:
ScriptableObjects/
└── Items/
    ├── AttackUp.asset
    ├── MoveSpeedUp.asset
    └── ProjectileUp.asset
10. Room
로그라이크의 방 단위 시스템을 담당한다.
Room/
├── RoomController.cs
├── RoomGraphController.cs
├── RoomNode.cs
├── RoomDoorway.cs
├── RoomCameraController.cs
├── FloorGenerator.cs         # Phase F 결정적 층별 방 그래프 생성
├── RoomDefinition.cs         # 검증된 방 단위와 몬스터 프리팹 조합 데이터
├── RoomGraphAssembler.cs     # 생성 결과를 검증된 씬 방과 RoomGraphController에 바인딩
├── RoomState.cs
└── RoomType.cs
방은 다음과 같은 상태를 가질 수 있다.
Normal
Cleared
Reward
Boss
11. Stage
층 단위 진행을 관리한다.
Stage/
├── StageManager.cs
├── FloorManager.cs
├── StageGenerator.cs
└── StageResult.cs
게임의 기본 흐름:
Floor
 ↓
Room
 ↓
Room
 ↓
Room
 ↓
Boss
 ↓
Next Floor
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
14. Data
게임 플레이 데이터를 관리한다.
Data/
├── GameData.cs
├── RunData.cs
├── PlayerData.cs
├── ItemData.cs
└── ResultData.cs
특히 RunData는 Backend API와 연결되는 핵심 데이터 구조다.
예:
RunData
├── runId
├── userId
├── characterId
├── playTime
├── reachedFloor
├── isCleared
├── killCount
└── items
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
16. GameFlow
게임 전체 진행 흐름을 관리한다.
GameFlow/
├── GameState.cs
├── GameStateMachine.cs
├── GameSession.cs
└── RunManager.cs
게임의 주요 상태:
Boot
 ↓
Title
 ↓
Character Select
 ↓
Game Start
 ↓
Room
 ↓
Combat
 ↓
Reward
 ↓
Next Room
 ↓
Boss
 ↓
Clear / Death
 ↓
Result
 ↓
Send Result
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
30. 전체 프로젝트 구조
최종적으로 다음 구조를 목표로 한다.
project-root/
│
├── game/
│   └── Unity Project
│
├── backend/
│   └── Node.js + TypeScript
│
├── web/
│   └── Next.js + TypeScript
│
├── docs/
│   ├── 00-project-overview.md
│   ├── 01-tech-stack.md
│   ├── 02-roadmap.md
│   ├── 03-game-design.md
│   ├── 04-architecture.md
│   ├── 05-development-setup.md
│   ├── 06-database.md
│   ├── 07-api.md
│   └── 08-project-structure.md
│
├── .gitignore
└── README.md
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
43. 개발 시 구조 변경 원칙
초기 구조를 무조건 유지할 필요는 없다.
개발하면서 다음과 같은 문제가 발견되면 구조를 변경할 수 있다.
코드가 지나치게 복잡함
특정 모듈의 책임이 너무 많음
의존성이 꼬임
재사용이 어려움
테스트하기 어려움
중요한 것은 폴더 구조 자체보다 책임과 의존성의 명확성이다.
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
46. MVP 기준 실제 구조
처음부터 모든 폴더와 파일을 만들 필요는 없다.
초기에는 다음 정도로 시작한다.
project-root/
│
├── game/
│   └── Assets/
│       └── Scripts/
│           ├── Core/
│           ├── Player/
│           ├── Enemy/
│           ├── Item/
│           ├── Room/
│           ├── Stage/
│           ├── Combat/
│           ├── UI/
│           ├── Data/
│           ├── Network/
│           └── GameFlow/
│
├── backend/
│   └── src/
│       ├── users/
│       ├── runs/
│       ├── rankings/
│       └── statistics/
│
├── web/
│   ├── app/
│   ├── components/
│   ├── lib/
│   └── types/
│
└── docs/
필요해질 때 세부 폴더를 추가한다.
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
```
