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
- 플레이어 탐지
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
  └── AI
  개별 몬스터는 공통 기능을 재사용하면서 자신만의 행동 패턴을 구현한다.

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
  구조 예시:
  Item
  ├── ItemData
  ├── ItemManager
  ├── ItemEffect
  └── Synergy
  아이템 자체의 수가 증가하더라도 기존 게임 시스템의 수정이 최소화되도록 설계한다.

## 8. Room

방 하나의 상태와 진행을 관리한다.

### 책임

- 방 진입
- 적 생성
- 전투 시작
- 적 전멸 확인
- 방 클리어
- 문 잠금 / 해제
- 보상
  상태 예시:
  Locked
  ↓
  Entered
  ↓
  Combat
  ↓
  Cleared

## 9. Floor

여러 개의 Room을 하나의 층으로 관리한다.

### 책임

- 층 시작
- 방 구성
- 방 이동
- 보스방 관리
- 층 클리어
- 다음 층 이동
  Floor
  ├── Room
  ├── Room
  ├── Room
  └── Boss Room
  Floor는 Room 내부의 세부 전투 로직을 직접 처리하지 않는다.

## 10. Boss

보스 전용 행동과 패턴을 관리한다.
일반 Enemy 시스템과 공통적인 요소를 재사용하되, 복잡한 보스 패턴은 별도로 관리한다.
Boss
├── BossBase
├── BossStats
├── BossHealth
└── BossPattern

## 11. Run System

Run은 게임 전체 플레이 세션을 관리하는 핵심 시스템이다.
Run
├── Run Start
├── Character
├── Floor
├── Items
├── Play Time
├── Kill Count
├── Death
└── Clear
책임
Run 시작
선택 캐릭터 기록
현재 층 관리
플레이 시간 기록
처치 수 기록
아이템 획득 기록
클리어 여부
사망 정보
Run 종료
결과 데이터 생성
Run이 종료되면 Backend에 전달할 RunResult를 생성한다.

## 12. Unity UI

UI는 게임 플레이 시스템과 분리한다.
주요 화면:
UI
├── Main Menu
├── Character Select
├── HUD
├── Item Selection
├── Pause
├── Game Over
└── Game Clear
UI는 게임의 실제 상태를 직접 조작하기보다는 Game System의 상태를 표시하고 사용자 입력을 전달하는 역할을 한다.
예:
Player
↓
PlayerStats
↓
HUD

## 13. Unity Data

게임에서 사용하는 데이터를 관리한다.
예상 데이터:
Data
├── CharacterData
├── EnemyData
├── ItemData
├── RoomData
└── BossData
가능한 경우 게임 로직과 데이터를 분리한다.
예를 들어 아이템의 공격력 증가량이나 이름 같은 값은 코드에 직접 하드코딩하지 않고 데이터로 관리하는 것을 우선한다.

## 14. Unity Network

Backend API와 통신하는 영역이다.

### 책임

- HTTP 요청
- JSON Serialization / Deserialization
- Run Result 전송
- 서버 응답 처리
- 통신 실패 처리
- 구조:
  Run System
  ↓
  RunResult
  ↓
  Network
  ↓
  POST /api/runs
  ↓
  Backend
  게임 플레이 로직이 HTTP 통신 코드에 직접 의존하지 않도록 분리한다.

## 15. Unity 전체 흐름

Character Select
↓
Run Start
↓
Floor
↓
Room
↓
Combat
↓
Enemy Defeated
↓
Item / Reward
↓
Next Room
↓
Boss
↓
Floor Complete
↓
Next Floor
↓
Final Boss
↓
Clear / Death
↓
RunResult
↓
Network

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

- 유저 생성
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
Character A
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
Player HP
Current Room
Current Floor
Enemy State
Current Items
Combat State

### Backend가 관리하는 데이터

서버에서 검증하고 처리해야 하는 데이터.
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
MVP에서는 Run 종료 시점에 결과 데이터를 한 번 전송하는 방식을 기본으로 한다.
게임 시작
↓
로컬에서 Run 진행
↓
게임 종료
↓
RunResult 생성
↓
Backend 전송

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
