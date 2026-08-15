# 기술 스택

## 1. 기술 스택 개요

본 프로젝트는 Unity 기반 게임 클라이언트와 Node.js 기반 Backend, PostgreSQL 데이터베이스, Next.js 기반 Web Service를 하나의 시스템으로 연결한다.

전체 기술 스택은 다음과 같다.

| 영역 | 기술 | 상태 |
|---|---|---|
| Game Engine | Unity | 확정 |
| Game Language | C# | 확정 |
| Backend Runtime | Node.js | 확정 |
| Backend Language | TypeScript | 확정 |
| Backend Framework | NestJS 우선 검토 | 검토 중 |
| Database | PostgreSQL | 확정 |
| Database Platform | Supabase | 확정 |
| Web Framework | Next.js | 확정 |
| Web Language | TypeScript | 확정 |
| API | REST API | 확정 |
| Data Visualization | Recharts 우선 검토 | 검토 중 |
| Deployment | 무료 / 저비용 호스팅 | 방향 확정 |

---

# 2. Game

## 2.1 Unity

### 역할

2D 로그라이크 팬게임의 게임 클라이언트 및 게임 시스템 구현에 사용한다.

### 주요 사용 영역

- 플레이어 조작
- 캐릭터 시스템
- 전투 시스템
- 몬스터 AI
- 방 시스템
- 층 시스템
- 아이템 시스템
- 보스 시스템
- 게임 상태 관리
- Run 데이터 생성
- Backend API 통신

### 선택 이유

- 2D 게임 개발에 적합
- C# 기반의 안정적인 개발 환경
- 다양한 게임 시스템을 구현하기 위한 충분한 기능 제공
- 향후 게임 프로젝트 확장 가능성
- Unity 기반 게임 개발 경험을 포트폴리오로 보여줄 수 있음

---

## 2.2 C#

Unity 게임 클라이언트의 주요 개발 언어로 사용한다.

### 주요 사용 영역

- 게임 로직
- Player Controller
- Enemy AI
- Combat System
- Item System
- Room / Floor System
- Run System
- API 통신
- 데이터 모델

---

# 3. Backend

## 3.1 Node.js

Backend 서버의 Runtime으로 사용한다.

### 역할

Unity 게임 클라이언트와 Web Service 사이에서 데이터를 처리한다.

```text
Unity
  ↓
REST API
  ↓
Node.js Backend
  ↓
PostgreSQL
```

### 주요 사용 영역

- REST API 서버
- 게임 결과 수신
- 데이터 검증
- 비즈니스 로직
- DB 접근
- 통계 데이터 집계
- Web API 제공

### 선택 이유

- TypeScript와 자연스럽게 결합 가능
- Web 개발 경험을 활용하기 좋음
- REST API 서버 구축이 용이
- 프론트엔드와 동일한 TypeScript 생태계 사용 가능
- 1인 프로젝트에서 빠르게 개발할 수 있음

---

## 3.2 TypeScript

Backend의 주요 개발 언어로 사용한다.

### 선택 이유

게임에서 전달되는 데이터와 DB 데이터를 명확한 타입으로 관리할 수 있다.

예:

```ts
type RunResult = {
  userId: string;
  characterId: string;
  reachedFloor: number;
  clear: boolean;
  playTime: number;
  killCount: number;
  items: string[];
};
```

이를 통해 다음 영역의 안정성을 높인다.

- API Request / Response
- DTO
- DB Entity / Model
- 게임 결과 데이터
- 통계 데이터
- 서비스 간 데이터 구조

또한 Web에서도 TypeScript를 사용하기 때문에 프로젝트 전체에서 타입 기반 개발 방식을 일관되게 유지할 수 있다.

---

## 3.3 Backend Framework

### 현재 상태

**NestJS 우선 검토**

Backend Framework는 아직 최종 확정하지 않는다.

### 후보

- NestJS
- Express
- Fastify

### 우선 검토 대상: NestJS

프로젝트의 Backend가 다음과 같이 여러 도메인으로 확장될 가능성이 있기 때문에 NestJS를 우선 검토한다.

```text
Backend
├── Users
├── Runs
├── Characters
├── Items
├── Statistics
└── Rankings
```

NestJS의 모듈 기반 구조가 이러한 프로젝트 구조를 관리하는 데 적합한지 검토한다.

### 최종 결정 시점

Phase 0 환경 구축 전에 최종 결정한다.

선택 시 다음 기준을 고려한다.

- 프로젝트 규모
- 구조적 명확성
- 개발 속도
- 학습 비용
- 유지보수성
- TypeScript 생태계와의 호환성
- 포트폴리오에서 설명하기 좋은 구조인지 여부

---

# 4. Database

## 4.1 PostgreSQL

프로젝트의 메인 데이터베이스로 사용한다.

### 주요 데이터

```text
User
Character
Item
Run
RunItem
```

### 선택 이유

본 프로젝트의 데이터 구조는 서로 간의 관계가 명확한 관계형 데이터 구조를 가진다.

예:

```text
User
  │
  └── Run
       │
       ├── Character
       │
       └── RunItem
              │
              └── Item
```

특히 다음과 같은 통계 데이터를 조회해야 하기 때문에 관계형 데이터베이스를 사용한다.

- 캐릭터별 플레이 횟수
- 캐릭터별 클리어율
- 아이템 선택률
- 아이템별 클리어율
- 층별 도달률
- 유저별 플레이 기록
- 랭킹

---

## 4.2 Supabase

PostgreSQL 데이터베이스의 호스팅 및 관리 플랫폼으로 사용한다.

### 역할

- PostgreSQL Database 제공
- DB 관리
- 테이블 관리
- SQL Editor
- 개발 환경에서의 데이터 확인
- 운영 환경 Database 관리

### 사용 원칙

Supabase가 제공하는 기능을 무조건 사용하는 것이 아니라, 프로젝트의 핵심 Backend 로직은 Node.js 서버에서 처리한다.

기본적인 구조는 다음과 같다.

```text
Unity
   ↓
Node.js Backend
   ↓
PostgreSQL
   ↑
Supabase
```

즉, 게임 클라이언트가 DB에 직접 접근하지 않도록 한다.

### 중요한 원칙

```text
Unity
  X
  ↓
Database 직접 접근

Unity
  ↓
Backend API
  ↓
Database
```

게임 클라이언트와 DB 사이에 Backend를 두어 데이터 검증 및 비즈니스 로직을 서버에서 처리한다.

---

# 5. Web

## 5.1 Next.js

전적 검색 및 통계 웹 서비스의 Framework로 사용한다.

### 주요 사용 영역

- 메인 페이지
- 유저 검색
- 유저 전적
- Run 상세
- 통계 페이지
- 랭킹
- 데이터 시각화

### 선택 이유

- React 기반 Framework
- TypeScript와 자연스럽게 결합 가능
- 페이지 및 라우팅 구조 관리
- 서버 / 클라이언트 렌더링 기능 활용 가능
- 향후 SEO 및 성능 최적화 가능
- 기존 프론트엔드 기술 역량을 활용할 수 있음

### 기본 구조

```text
Next.js
   ↓
Backend REST API
   ↓
PostgreSQL
```

Web에서 필요한 데이터를 Backend API를 통해 조회한다.

---

## 5.2 TypeScript

Web Service에서도 TypeScript를 사용한다.

### 사용 영역

- React Components
- API Response 타입
- 페이지 데이터
- 통계 데이터
- Chart 데이터
- UI 상태

Backend와 동일한 TypeScript를 사용하여 데이터 구조를 일관되게 관리한다.

---

# 6. API

## 6.1 REST API

Game Client와 Backend, Web Service와 Backend 사이의 통신 방식으로 REST API를 사용한다.

### 기본 구조

```text
Unity
  ↓ HTTP / JSON
Backend REST API
  ↓
PostgreSQL
```

```text
Next.js
  ↓ HTTP / JSON
Backend REST API
  ↓
PostgreSQL
```

### 초기 주요 API

```text
POST /api/runs

GET /api/users/:nickname

GET /api/users/:nickname/runs

GET /api/runs/:runId
```

통계 및 랭킹 API는 실제 데이터 구조가 확정된 후 추가한다.

---

## 6.2 데이터 형식

기본적으로 JSON을 사용한다.

예:

```json
{
  "userId": "user-001",
  "characterId": "character-001",
  "reachedFloor": 3,
  "clear": true,
  "playTime": 542,
  "killCount": 87,
  "items": [
    "item-001",
    "item-004",
    "item-008"
  ]
}
```

---

# 7. Data Visualization

## 7.1 Recharts

Web Service에서 통계 데이터를 시각화하기 위한 라이브러리로 **Recharts를 우선 검토**한다.

### 예상 사용 영역

- 캐릭터별 클리어율
- 아이템 선택률
- 층별 도달률
- 유저 플레이 기록
- 통계 차트

예:

```text
캐릭터 통계
──────────────
플레이 횟수
클리어율
평균 도달 층

아이템 통계
──────────────
선택률
클리어율

층 통계
──────────────
도달률
사망률
```

### 최종 결정

Recharts를 우선 사용하되, 실제 UI 및 데이터 구조를 구현하는 과정에서 다른 React 기반 차트 라이브러리가 더 적합하다고 판단되면 변경할 수 있다.

---

# 8. Deployment

## 8.1 기본 방향

초기 개발 단계에서는 **무료 또는 저비용 호스팅**을 우선한다.

프로젝트의 목적이 상용 서비스 운영이 아니라 포트폴리오 구축이기 때문에 초기부터 고비용 인프라를 구축하지 않는다.

### 기본 구성

```text
Game
   ↓
Backend Hosting
   ↓
Supabase PostgreSQL

Web
   ↓
Web Hosting
```

### 고려 사항

배포 플랫폼은 Phase 7에서 실제 요구사항을 기준으로 최종 결정한다.

고려 기준:

- 무료 / 저비용
- 배포 편의성
- Git 연동
- 환경 변수 관리
- HTTPS
- 서버 안정성
- Database 연결
- CORS 설정
- 로그 확인
- 재배포 편의성

---

# 9. 전체 기술 구조

최종적인 시스템 구조는 다음을 목표로 한다.

```text
┌──────────────────────────────┐
│        Unity Game            │
│                              │
│        Unity + C#            │
│                              │
│ Player / Combat / Enemy      │
│ Room / Floor / Item / Boss   │
└──────────────┬───────────────┘
               │
               │ HTTPS / JSON
               ▼
┌──────────────────────────────┐
│          Backend             │
│                              │
│    Node.js + TypeScript      │
│                              │
│    [NestJS 검토 중]           │
│                              │
│ REST API                     │
│ Validation                   │
│ Business Logic               │
│ Statistics                   │
└──────────────┬───────────────┘
               │
               │ SQL / ORM
               ▼
┌──────────────────────────────┐
│          Database            │
│                              │
│ PostgreSQL + Supabase        │
│                              │
│ User                         │
│ Character                    │
│ Item                         │
│ Run                          │
│ RunItem                      │
└──────────────┬───────────────┘
               │
               │ REST API
               ▼
┌──────────────────────────────┐
│            Web               │
│                              │
│   Next.js + TypeScript       │
│                              │
│ Search / Records / Stats     │
│ Ranking / Visualization      │
└──────────────────────────────┘
```

---

# 10. 기술 선택 원칙

## 10.1 전체 구조의 단순성

1인 개발 프로젝트이므로 불필요하게 많은 기술을 도입하지 않는다.

```text
Unity
+
Node.js
+
PostgreSQL
+
Next.js
```

를 중심으로 필요한 기술만 추가한다.

---

## 10.2 TypeScript 통일

Web과 Backend에서 TypeScript를 공통으로 사용한다.

```text
Backend
Node.js + TypeScript

        ↕

Web
Next.js + TypeScript
```

이를 통해 프로젝트 전반에서 타입 기반 개발을 유지한다.

---

## 10.3 Backend 중심 데이터 접근

Game과 Web이 Database에 직접 접근하지 않는다.

```text
Game ──X──> DB
Web  ──X──> DB

Game ──> Backend ──> DB
Web  ──> Backend ──> DB
```

Backend를 데이터 접근의 단일 진입점으로 사용한다.

---

## 10.4 최소 기술로 MVP 완성

처음부터 다음과 같은 기술을 모두 도입하지 않는다.

- Redis
- Message Queue
- Kubernetes
- Microservices
- 별도의 Analytics 서버
- 복잡한 CI/CD

필요성이 실제로 발생했을 때만 도입을 검토한다.

---

# 11. 기술 스택 결정 상태

### 확정

- Unity
- C#
- Node.js
- TypeScript
- PostgreSQL
- Supabase
- Next.js
- REST API

### 우선 검토

- NestJS
- Recharts

### 추후 결정

- ORM
- Backend Hosting
- Web Hosting
- CI/CD
- 인증 방식
- 캐싱
- 로깅 및 모니터링

---

# 12. 기술 스택 변경 원칙

개발 중 기술 스택을 변경할 수 있다.

다만 단순한 취향이나 새로운 기술에 대한 호기심만으로 변경하지 않는다.

변경이 필요한 경우 다음 기준을 검토한다.

1. 현재 기술로 요구사항을 구현하기 어려운가?
2. 개발 생산성이 크게 향상되는가?
3. 프로젝트 복잡도를 줄일 수 있는가?
4. 유지보수가 쉬워지는가?
5. 포트폴리오 관점에서 기술 선택을 설명할 수 있는가?

변경이 결정되면 해당 이유를 문서에 기록한다.

---

# 13. 최종 목표

본 프로젝트에서 기술 스택의 목적은 최신 기술을 최대한 많이 사용하는 것이 아니다.

**게임 클라이언트 → Backend → Database → Web Service**라는 전체 시스템을 안정적으로 구현하고, 각 기술을 선택한 이유와 구조를 직접 설명할 수 있는 것을 목표로 한다.

> **기술의 개수보다 기술 선택의 이유와 시스템 전체를 이해하는 것을 우선한다.**