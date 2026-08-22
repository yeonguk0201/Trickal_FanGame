# API Specification

## 1. 개요

본 프로젝트의 Backend API는 Unity Game Client와 Web Service 사이의 데이터 통신을 담당한다.

전체적인 통신 구조는 다음과 같다.

```text
Unity Game
    │
    │ REST API
    ▼
Backend
    │
    │ Database Access
    ▼
PostgreSQL
    ▲
    │
    │ REST API
    │
Web Service
```

Unity와 Web은 PostgreSQL에 직접 접근하지 않는다.

모든 데이터 접근은 Backend API를 통해 이루어진다.

---

# 2. API 설계 목표

API는 다음의 목적을 가진다.

- Unity에서 게임 플레이 결과를 서버에 저장
- 특정 유저의 전적 조회
- 특정 Run의 상세 정보 조회
- 전체 유저 랭킹 조회
- 게임 전체 통계 조회
- 캐릭터별 통계 조회
- 아이템별 통계 조회
- 향후 기능 확장이 가능한 구조 유지

---

# 3. API 기본 규칙

## 3.1 Base URL

개발 환경:

```text
http://localhost:3000/api
```

운영 환경:

```text
https://{backend-domain}/api
```

실제 도메인은 배포 단계에서 결정한다.

---

# 4. HTTP Method

REST API의 기본적인 HTTP Method를 사용한다.

| Method | 용도 |
|---|---|
| `GET` | 데이터 조회 |
| `POST` | 데이터 생성 |
| `PATCH` | 데이터 일부 수정 |
| `DELETE` | 데이터 삭제 |

MVP에서는 주로 `GET`과 `POST`를 사용한다.

---

# 5. Response Format

모든 API Response는 가능한 한 일관된 형태를 유지한다.

성공:

```json
{
  "success": true,
  "data": {}
}
```

실패:

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "에러 메시지"
  }
}
```

단, 필요에 따라 Pagination 등의 메타데이터를 추가할 수 있다.

예:

```json
{
  "success": true,
  "data": [],
  "meta": {
    "page": 1,
    "limit": 20,
    "total": 100
  }
}
```

---

# 6. HTTP Status Code

주요 HTTP Status Code는 다음을 사용한다.

| Status | 의미 |
|---|---|
| `200` | 요청 성공 |
| `201` | 데이터 생성 성공 |
| `400` | 잘못된 요청 |
| `404` | 리소스를 찾을 수 없음 |
| `409` | 데이터 충돌 |
| `422` | 데이터 검증 실패 |
| `500` | 서버 내부 오류 |

---

# 7. API 목록

MVP에서는 다음 API를 우선 구현한다.

## Game API

```text
POST /api/runs
```

게임 플레이 결과 저장.

---

## User API

```text
GET /api/users/:nickname
GET /api/users/:nickname/runs
```

유저 정보 및 전적 조회.

---

## Run API

```text
GET /api/runs/:runId
```

특정 Run 상세 조회.

---

## Ranking API

```text
GET /api/rankings
```

전체 랭킹 조회.

---

## Statistics API

```text
GET /api/statistics
GET /api/statistics/characters
GET /api/statistics/items
```

게임 전체 통계 조회.

---

# 8. Game API

# 8.1 Create Run

게임이 종료되었을 때 Unity가 플레이 결과를 서버에 저장한다.

```http
POST /api/runs
```

### Request

```json
{
  "userId": "user-uuid",
  "characterId": "character-a",
  "gameVersion": "0.1.0",
  "startedAt": "2026-08-14T20:00:00Z",
  "endedAt": "2026-08-14T20:10:23Z",
  "playTime": 623,
  "reachedFloor": 3,
  "isCleared": true,
  "killCount": 142,
  "deathReason": null,
  "items": [
    {
      "itemId": "item-01",
      "floor": 1,
      "order": 1,
      "acquiredAt": "2026-08-14T20:02:10Z"
    },
    {
      "itemId": "item-02",
      "floor": 2,
      "order": 2,
      "acquiredAt": "2026-08-14T20:05:30Z"
    },
    {
      "itemId": "item-03",
      "floor": 3,
      "order": 3,
      "acquiredAt": "2026-08-14T20:08:45Z"
    }
  ]
}
```

---

# 8.2 Create Run Request Fields

| Field | Type | Required | 설명 |
|---|---|---:|---|
| `userId` | UUID | O | 유저 ID |
| `characterId` | string | O | 캐릭터 ID |
| `gameVersion` | string | O | 게임 버전 |
| `startedAt` | datetime | O | 게임 시작 시간 |
| `endedAt` | datetime | O | 게임 종료 시간 |
| `playTime` | integer | O | 플레이 시간(초) |
| `reachedFloor` | integer | O | 도달한 최대 층 |
| `isCleared` | boolean | O | 클리어 여부 |
| `killCount` | integer | O | 총 처치 수 |
| `deathReason` | string/null | X | 사망 원인 |
| `items` | array | O | 획득 아이템 목록 |

---

# 8.3 Item Request

각 아이템은 다음 정보를 전달한다.

```json
{
  "itemId": "item-01",
  "floor": 1,
  "order": 1,
  "acquiredAt": "2026-08-14T20:02:10Z"
}
```

| Field | Type | Required | 설명 |
|---|---|---:|---|
| `itemId` | string | O | 아이템 ID |
| `floor` | integer | O | 획득한 층 |
| `order` | integer | O | 획득 순서 |
| `acquiredAt` | ISO 8601 string | O | Run 시작 시각과 종료 시각 사이의 UTC 획득 시각 |

---

# 8.4 Run Validation

Backend는 Unity에서 전달된 데이터를 그대로 저장하지 않는다.

다음 항목을 검증한다.

```text
userId
    ↓
존재하는 User인가?

characterId
    ↓
존재하는 Character인가?

itemId
    ↓
존재하는 Item인가?

acquiredAt
    ↓
startedAt 이상, endedAt 이하인가?

playTime
    ↓
0 이상인가?

reachedFloor
    ↓
허용된 범위인가?

killCount
    ↓
0 이상인가?

isCleared
    ↓
deathReason과 상태가 일치하는가?
```

---

# 8.5 Create Run Response

성공:

```http
HTTP/1.1 201 Created
```

```json
{
  "success": true,
  "data": {
    "runId": "run-uuid"
  }
}
```

Unity는 생성된 Run ID를 저장할 수 있다.

---

# 8.6 Create Run Error

존재하지 않는 캐릭터:

```json
{
  "success": false,
  "error": {
    "code": "CHARACTER_NOT_FOUND",
    "message": "존재하지 않는 캐릭터입니다."
  }
}
```

존재하지 않는 아이템:

```json
{
  "success": false,
  "error": {
    "code": "ITEM_NOT_FOUND",
    "message": "존재하지 않는 아이템입니다."
  }
}
```

잘못된 데이터:

```json
{
  "success": false,
  "error": {
    "code": "INVALID_RUN_DATA",
    "message": "유효하지 않은 플레이 데이터입니다."
  }
}
```

---

# 9. User API

# 9.1 Get User

특정 유저의 기본 정보와 주요 전적을 조회한다.

```http
GET /api/users/:nickname
```

예:

```http
GET /api/users/test-player
```

---

# 9.2 User Response

```json
{
  "success": true,
  "data": {
    "id": "user-uuid",
    "nickname": "test-player",
    "stats": {
      "totalRuns": 32,
      "clears": 12,
      "winRate": 37.5,
      "averagePlayTime": 582,
      "averageFloor": 2.4,
      "highestFloor": 5
    }
  }
}
```

---

# 9.3 User Stats

유저 페이지에서 다음 통계를 제공한다.

```text
Total Runs
Clears
Win Rate
Average Play Time
Average Floor
Highest Floor
```

통계는 `runs` 데이터를 기반으로 Backend에서 계산한다.

---

# 10. User Run History API

특정 유저의 최근 플레이 기록을 조회한다.

```http
GET /api/users/:nickname/runs
```

---

# 10.1 Query Parameters

Pagination을 기본적으로 지원한다.

```text
?page=1&limit=20
```

예:

```http
GET /api/users/test-player/runs?page=1&limit=20
```

향후 다음과 같은 Filter를 추가할 수 있다.

```text
?result=clear
?character=character-a
?sort=latest
```

MVP에서는 필요한 기능만 구현한다.

---

# 10.2 User Run History Response

```json
{
  "success": true,
  "data": [
    {
      "runId": "run-001",
      "character": {
        "id": "character-a",
        "name": "Character A"
      },
      "reachedFloor": 3,
      "playTime": 623,
      "isCleared": true,
      "killCount": 142,
      "endedAt": "2026-08-14T20:10:23Z"
    },
    {
      "runId": "run-002",
      "character": {
        "id": "character-a",
        "name": "Character A"
      },
      "reachedFloor": 2,
      "playTime": 451,
      "isCleared": false,
      "killCount": 87,
      "endedAt": "2026-08-14T19:40:00Z"
    }
  ],
  "meta": {
    "page": 1,
    "limit": 20,
    "total": 32
  }
}
```

---

# 11. Run API

# 11.1 Get Run Detail

특정 Run의 상세 정보를 조회한다.

```http
GET /api/runs/:runId
```

예:

```http
GET /api/runs/run-uuid
```

---

# 11.2 Run Detail Response

```json
{
  "success": true,
  "data": {
    "id": "run-uuid",
    "user": {
      "id": "user-uuid",
      "nickname": "test-player"
    },
    "character": {
      "id": "character-a",
      "name": "Character A"
    },
    "startedAt": "2026-08-14T20:00:00Z",
    "endedAt": "2026-08-14T20:10:23Z",
    "playTime": 623,
    "reachedFloor": 3,
    "isCleared": true,
    "killCount": 142,
    "deathReason": null,
    "items": [
      {
        "itemId": "item-01",
        "name": "Item A",
        "floor": 1,
        "order": 1
      },
      {
        "itemId": "item-02",
        "name": "Item B",
        "floor": 2,
        "order": 2
      },
      {
        "itemId": "item-03",
        "name": "Item C",
        "floor": 3,
        "order": 3
      }
    ]
  }
}
```

---

# 12. Ranking API

전체 유저의 랭킹을 조회한다.

```http
GET /api/rankings
```

---

# 12.1 Ranking Type

MVP에서는 다음 랭킹을 우선 지원한다.

```text
highest-floor
fastest-clear
```

Query Parameter:

```http
GET /api/rankings?type=highest-floor
```

또는

```http
GET /api/rankings?type=fastest-clear
```

---

# 12.2 Highest Floor Ranking

가장 높은 층에 도달한 유저를 기준으로 정렬한다.

```http
GET /api/rankings?type=highest-floor
```

정렬:

```text
reachedFloor DESC
```

동일 층인 경우 추가적인 정렬 기준을 사용할 수 있다.

예:

```text
reachedFloor DESC
playTime ASC
```

---

# 12.3 Fastest Clear Ranking

클리어한 Run 중 플레이 시간이 짧은 순으로 정렬한다.

```http
GET /api/rankings?type=fastest-clear
```

조건:

```text
isCleared = true
```

정렬:

```text
playTime ASC
```

---

# 12.4 Ranking Response

```json
{
  "success": true,
  "data": [
    {
      "rank": 1,
      "nickname": "player-a",
      "character": {
        "id": "character-a",
        "name": "Character A"
      },
      "reachedFloor": 5,
      "playTime": 812,
      "runId": "run-001"
    },
    {
      "rank": 2,
      "nickname": "player-b",
      "character": {
        "id": "character-a",
        "name": "Character A"
      },
      "reachedFloor": 4,
      "playTime": 921,
      "runId": "run-002"
    }
  ]
}
```

---

# 13. Statistics API

게임 전체의 통계를 조회한다.

```http
GET /api/statistics
```

---

# 13.1 Statistics Response

```json
{
  "success": true,
  "data": {
    "totalUsers": 120,
    "totalRuns": 1520,
    "totalClears": 420,
    "winRate": 27.63,
    "averagePlayTime": 581,
    "averageReachedFloor": 2.7
  }
}
```

---

# 14. Character Statistics API

캐릭터별 통계를 조회한다.

```http
GET /api/statistics/characters
```

---

# 14.1 Character Statistics Response

```json
{
  "success": true,
  "data": [
    {
      "characterId": "character-a",
      "characterName": "Character A",
      "totalRuns": 1520,
      "clears": 420,
      "winRate": 27.63,
      "averageFloor": 2.7,
      "averagePlayTime": 581
    }
  ]
}
```

현재는 캐릭터가 1종이므로 하나의 데이터만 존재한다.

향후 캐릭터가 추가되면 자동으로 여러 캐릭터의 통계를 반환한다.

---

# 15. Item Statistics API

아이템별 통계를 조회한다.

```http
GET /api/statistics/items
```

---

# 15.1 Item Statistics Response

```json
{
  "success": true,
  "data": [
    {
      "itemId": "item-01",
      "itemName": "Item A",
      "pickCount": 540,
      "pickRate": 35.52,
      "clearCount": 180,
      "clearRate": 33.33
    },
    {
      "itemId": "item-02",
      "itemName": "Item B",
      "pickCount": 420,
      "pickRate": 27.63,
      "clearCount": 140,
      "clearRate": 33.33
    }
  ]
}
```

---

# 16. Item Statistics 계산

아이템 선택률은 다음과 같이 계산한다.

```text
Item Pick Rate
=
해당 아이템을 획득한 Run 수
/
전체 Run 수
× 100
```

예:

```text
전체 Run = 1,000

Item A 획득 Run = 300

Pick Rate
= 300 / 1,000 × 100
= 30%
```

---

# 17. Item Clear Rate

특정 아이템을 획득한 Run 중 클리어한 비율을 계산한다.

```text
Item Clear Rate
=
해당 아이템을 획득한 후 클리어한 Run 수
/
해당 아이템을 획득한 전체 Run 수
× 100
```

주의할 점은 이 수치를 아이템의 실제 성능을 증명하는 지표로 해석하지 않는 것이다.

플레이어 실력, 아이템 선택 상황 등의 변수가 존재하기 때문이다.

Web에서는 통계 지표로만 제공한다.

---

# 18. Error Response

모든 API는 일관된 Error Response를 사용한다.

```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "에러 메시지"
  }
}
```

---

# 19. Error Code

MVP에서 사용할 주요 Error Code:

| Code | 설명 |
|---|---|
| `INVALID_REQUEST` | 잘못된 요청 |
| `VALIDATION_ERROR` | 데이터 검증 실패 |
| `USER_NOT_FOUND` | 유저 없음 |
| `CHARACTER_NOT_FOUND` | 캐릭터 없음 |
| `ITEM_NOT_FOUND` | 아이템 없음 |
| `RUN_NOT_FOUND` | Run 없음 |
| `INVALID_RUN_DATA` | 잘못된 Run 데이터 |
| `INTERNAL_SERVER_ERROR` | 서버 내부 오류 |

---

# 20. API Validation

모든 외부 요청은 Backend에서 검증한다.

특히 Unity에서 전달되는 Run 데이터는 신뢰하지 않는다.

```text
Unity
 ↓
Request
 ↓
DTO Validation
 ↓
Business Validation
 ↓
Database
```

---

# 21. DTO

Backend에서는 API Request / Response를 명확하게 정의한다.

예:

```text
CreateRunRequest
CreateRunResponse

GetUserResponse
GetUserRunsResponse

GetRunResponse

GetRankingResponse

GetStatisticsResponse
```

DTO는 API와 내부 Database 구조를 분리하기 위한 경계 역할을 한다.

---

# 22. API와 Database의 분리

API Response가 Database의 구조와 반드시 동일할 필요는 없다.

예를 들어 Database:

```text
runs
├── user_id
├── character_id
└── character_id
```

Web Response:

```json
{
  "character": {
    "id": "character-a",
    "name": "Character A"
  }
}
```

Backend에서 필요한 데이터를 조합하여 Web에 적합한 형태로 반환한다.

---

# 23. Unity와 API의 관계

Unity는 다음 API를 사용한다.

```text
POST /api/runs
```

MVP에서는 게임 플레이 중 지속적으로 API를 호출하지 않는다.

기본적인 통신 시점:

```text
Game Start
    ↓
Local Game Play
    ↓
Clear / Death
    ↓
RunResult 생성
    ↓
POST /api/runs
```

---

# 24. Unity Network Error 처리

게임 종료 시 서버 통신이 실패할 가능성을 고려한다.

예:

```text
게임 종료
 ↓
RunResult 생성
 ↓
API 요청
 ↓
실패
```

이 경우 게임 결과가 완전히 사라지지 않도록 Unity에서 최소한의 로컬 저장을 고려한다.

예:

```text
RunResult
 ↓
Local Save
 ↓
API 전송
 ↓
성공
 ↓
Local Data 삭제
```

단, 로컬 저장은 MVP 개발 상황에 따라 후순위로 구현할 수 있다.

---

# 25. API Retry

네트워크 오류가 발생했을 경우 무한 재시도하지 않는다.

기본 전략:

```text
Request
 ↓
실패
 ↓
Retry 1
 ↓
실패
 ↓
Retry 2
 ↓
실패
 ↓
로컬 저장
```

구체적인 Retry 횟수와 Backoff 전략은 구현 단계에서 결정한다.

---

# 26. API Versioning

초기에는 다음 형태를 사용한다.

```text
/api/...
```

API 구조가 크게 변경될 경우 Versioning을 적용한다.

예:

```text
/api/v1/runs
/api/v1/users/:nickname
```

MVP에서는 Versioning을 반드시 적용하지 않는다.

---

# 27. API Security

MVP에서는 최소한의 보안을 적용한다.

### 필수

- HTTPS
- Request Validation
- SQL Injection 방어
- CORS 설정
- 환경 변수로 Secret 관리
- Rate Limit 검토

---

# 28. Unity API 인증

초기 MVP에서는 복잡한 로그인 시스템을 구현하지 않는다.

게임에서 전적 저장에 필요한 최소한의 식별 방식만 사용한다.

향후 필요할 경우 다음과 같은 인증 시스템을 추가한다.

```text
JWT
OAuth
Supabase Auth
```

인증 시스템은 MVP 이후 확장한다.

---

# 29. Web API 호출

Web은 Backend API를 통해 데이터를 조회한다.

예:

```text
User Search
    ↓
GET /api/users/:nickname
```

```text
Recent Runs
    ↓
GET /api/users/:nickname/runs
```

```text
Run Detail
    ↓
GET /api/runs/:runId
```

```text
Ranking
    ↓
GET /api/rankings
```

```text
Statistics
    ↓
GET /api/statistics
```

---

# 30. API와 Web Page Mapping

| Web Page | API |
|---|---|
| Home | `/api/statistics` |
| User Search | `/api/users/:nickname` |
| User Profile | `/api/users/:nickname` |
| User Run History | `/api/users/:nickname/runs` |
| Run Detail | `/api/runs/:runId` |
| Ranking | `/api/rankings` |
| Statistics | `/api/statistics` |
| Character Stats | `/api/statistics/characters` |
| Item Stats | `/api/statistics/items` |

---

# 31. API 전체 구조

최종적인 API 구조는 다음과 같다.

```text
/api
│
├── runs
│   ├── POST /
│   └── GET /:runId
│
├── users
│   └── GET /:nickname
│       └── GET /runs
│
├── rankings
│   └── GET /
│
└── statistics
    ├── GET /
    ├── GET /characters
    └── GET /items
```

---

# 32. 전체 데이터 흐름

## 32.1 게임 데이터 저장

```text
Unity
 ↓
RunResult
 ↓
POST /api/runs
 ↓
Backend
 ↓
Validation
 ↓
runs
 ↓
run_items
 ↓
PostgreSQL
```

---

## 32.2 유저 전적 조회

```text
Web
 ↓
GET /api/users/:nickname
 ↓
Backend
 ↓
users
 +
runs
 ↓
Statistics Calculation
 ↓
JSON Response
 ↓
Web
```

---

## 32.3 Run 상세 조회

```text
Web
 ↓
GET /api/runs/:runId
 ↓
Backend
 ↓
runs
 +
run_items
 +
items
 +
characters
 ↓
JSON Response
 ↓
Web
```

---

## 32.4 통계 조회

```text
Web
 ↓
GET /api/statistics/items
 ↓
Backend
 ↓
run_items
 +
items
 +
runs
 ↓
Aggregation
 ↓
JSON Response
 ↓
Chart
```

---

# 33. MVP API Scope

MVP에서는 다음 API만 구현한다.

### 필수

```text
POST /api/runs

GET /api/users/:nickname

GET /api/users/:nickname/runs

GET /api/runs/:runId

GET /api/rankings

GET /api/statistics

GET /api/statistics/characters

GET /api/statistics/items
```

---

# 34. 향후 API 확장

향후 필요에 따라 다음 API를 추가할 수 있다.

```text
GET /api/characters
GET /api/items
GET /api/enemies
GET /api/bosses
```

상세 게임 로그:

```text
GET /api/runs/:runId/events
```

빌드 분석:

```text
GET /api/statistics/builds
```

기간별 통계:

```text
GET /api/statistics/daily
GET /api/statistics/weekly
GET /api/statistics/monthly
```

---

# 35. API 설계 원칙

## 35.1 API는 시스템 간 계약이다

API는 Unity와 Backend, Web과 Backend 사이의 명확한 통신 규칙이다.

```text
Unity
 ↕
API Contract
 ↕
Backend
 ↕
API Contract
 ↕
Web
```

---

## 35.2 Database 구조를 API에 그대로 노출하지 않는다

Database는 내부 구현이다.

API는 외부 시스템이 사용할 수 있는 데이터 구조를 제공한다.

```text
Database
    ↓
Backend
    ↓
DTO
    ↓
API Response
```

---

## 35.3 Validation은 Backend에서 수행한다

Client에서 전달된 데이터는 신뢰하지 않는다.

```text
Client Data
 ↓
Validation
 ↓
Business Logic
 ↓
Database
```

---

## 35.4 MVP에서는 필요한 API만 만든다

처음부터 모든 기능을 API로 만들지 않는다.

게임과 Web의 실제 기능 구현에 필요한 API부터 만든다.

---

# 36. Definition of Done

API 설계는 다음 조건을 만족하면 MVP 기준 완료로 정의한다.

- [ ] Run 저장 API 정의
- [ ] User 조회 API 정의
- [ ] User Run History API 정의
- [ ] Run 상세 조회 API 정의
- [ ] Ranking API 정의
- [ ] 전체 Statistics API 정의
- [ ] Character Statistics API 정의
- [ ] Item Statistics API 정의
- [ ] Request DTO 정의
- [ ] Response DTO 정의
- [ ] Error Response 정의
- [ ] HTTP Status Code 정의
- [ ] Validation 기준 정의
- [ ] Unity ↔ Backend 데이터 흐름 정의
- [ ] Web ↔ Backend 데이터 흐름 정의

---

# 37. 최종 API 구조

```text
                    ┌──────────────────┐
                    │   Unity Client   │
                    └────────┬─────────┘
                             │
                             │ POST /api/runs
                             ▼
                    ┌──────────────────┐
                    │     Backend      │
                    │                  │
                    │ Validation       │
                    │ Business Logic   │
                    │ DTO              │
                    └────────┬─────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │   PostgreSQL     │
                    └────────┬─────────┘
                             ▲
                             │
                    ┌────────┴─────────┐
                    │     Backend      │
                    └────────┬─────────┘
                             │
                             │ GET
                             ▼
                    ┌──────────────────┐
                    │       Web        │
                    └──────────────────┘
```

> **본 프로젝트의 API는 Unity에서 생성된 Run 데이터를 Backend를 통해 저장하고, 저장된 데이터를 Web에서 전적·랭킹·통계 형태로 조회할 수 있도록 하는 것을 핵심으로 한다.**
