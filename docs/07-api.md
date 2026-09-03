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
GET /api/users/search?q={nickname}
GET /api/users/:nickname
GET /api/users/:nickname/runs
PUT /api/users/:nickname/characters/:characterId/skills/:skillType
```

유저 정보 및 전적 조회, 캐릭터 스킬 강화.

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
  "clientRunId": "client-generated-run-uuid",
  "userId": "user-uuid",
  "characterId": "erpin",
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
| `clientRunId` | UUID | O | Unity가 Run 시작 시 생성하는 멱등성 식별자 |
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

clientRunId
    ↓
이미 저장된 Run이면 기존 저장·경험치 결과를 반환하는가?

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
    "runId": "run-uuid",
    "experienceGained": 400,
    "progress": {
      "characterId": "erpin",
      "level": 3,
      "experience": 120,
      "experienceToNextLevel": 900,
      "skillPoints": 2,
      "lowGradeSkillLevel": 1,
      "highGradeSkillLevel": 1
    }
  }
}
```

Unity는 생성된 Run ID와 Backend가 계산한 캐릭터 진행 결과를 표시한다. 한 Run의 경험치가 여러
레벨 조건을 충족하면 Backend는 최대 Lv.19 또는 경험치 부족 시점까지 연속 레벨업하고, 상승한
레벨 수만큼 스킬 포인트를 지급한다.

같은 `clientRunId`와 동일한 Run 내용을 재전송하면 새 Run이나 경험치를 만들지 않고
동일한 결과를 `200 OK`로 반환한다. 같은 `clientRunId`에 다른 Run 내용을 보내면
`RUN_IDEMPOTENCY_CONFLICT`로 전체 요청을 거절한다.
최초 저장은 `201 Created`를 사용한다.

동일 여부는 `clientRunId`를 제외한 모든 Run 필드와 `order`로 정렬한 아이템의 `itemId`,
`floor`, `order`, `acquiredAt`으로 계산한 SHA-256 지문으로 판정한다. 재전송에는 이후의 Run이나
스킬 강화로 바뀐 현재 진행이 아니라 최초 지급 직후 저장한 `experienceGained`와 `progress`
스냅샷을 반환한다.

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

같은 `clientRunId`에 다른 플레이 데이터가 전송된 경우 (`409 Conflict`):

```json
{
  "success": false,
  "error": {
    "code": "RUN_IDEMPOTENCY_CONFLICT",
    "message": "같은 clientRunId에 다른 플레이 데이터가 전송되었습니다."
  }
}
```

---

# 9. User API

# 9.1 Search Users

닉네임으로 전적 페이지에 진입할 유저를 검색한다.

```http
GET /api/users/search?q=test-player
```

MVP 검색 정책:

- `q`의 앞뒤 공백은 제거한다.
- 공백 제거 후 길이는 2~50자여야 한다.
- 닉네임은 대소문자를 구분해 정확히 일치한다.
- 정확 일치이므로 결과는 최대 1개이며 페이지네이션을 사용하지 않는다.
- 결과가 있다면 닉네임 오름차순으로 본 것과 동일한 안정적인 순서다.
- 응답에는 전적 이동에 필요한 공개 필드 `nickname`만 포함하며 UUID나 진행 데이터는 노출하지 않는다.

성공 응답:

```json
{
  "success": true,
  "data": [
    {
      "nickname": "test-player"
    }
  ]
}
```

일치하는 유저가 없으면 `200 OK`와 빈 배열을 반환한다.

```json
{
  "success": true,
  "data": []
}
```

`q`가 없거나, 빈 문자열이거나, 길이 범위를 벗어나면 `422 Unprocessable Entity`를 반환한다.

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "요청 데이터가 유효하지 않습니다."
  }
}
```

Database 예외는 공통 오류 계약에 따라 세부 내용을 노출하지 않고
`500 INTERNAL_SERVER_ERROR`로 반환한다.

---

# 9.2 Get User

특정 유저의 기본 정보와 주요 전적을 조회한다.

```http
GET /api/users/:nickname
```

예:

```http
GET /api/users/test-player
```

---

# 9.3 User Response

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
    },
    "characterProgress": [
      {
        "characterId": "erpin",
        "characterName": "에르핀",
        "level": 3,
        "maxLevel": 19,
        "experience": 120,
        "experienceToNextLevel": 900,
        "skillPoints": 2,
        "lowGradeSkillLevel": 1,
        "highGradeSkillLevel": 1,
        "maxSkillLevel": 10
      }
    ]
  }
}
```

---

# 9.4 User Stats

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

Run이 없는 유저는 `totalRuns`, `clears`, `winRate`, `averagePlayTime`, `averageFloor`,
`highestFloor`를 모두 `0`으로 반환한다. 닉네임에 해당하는 유저가 없으면 `404 Not Found`와
`USER_NOT_FOUND`를 반환하며 일반 Backend 실패와 구분한다.

---

# 9.5 Upgrade Character Skill

캐릭터의 미사용 스킬 포인트 1을 소비해 지정한 스킬을 1레벨 강화한다.

```http
PUT /api/users/:nickname/characters/:characterId/skills/:skillType
```

`skillType`은 다음 안정적인 값을 사용한다.

```text
LOW_GRADE
HIGH_GRADE
```

요청 본문에는 클라이언트가 원하는 다음 레벨을 전달한다.

```json
{
  "targetLevel": 2
}
```

`targetLevel`은 현재 레벨과 같거나 정확히 1 높아야 한다. 현재 레벨과 같으면 재전송으로 보고
포인트를 다시 차감하지 않은 채 현재 상태를 반환한다. 현재 레벨보다 2 이상 높거나 낮으면
`INVALID_SKILL_TARGET_LEVEL` 오류를 반환한다.

Backend는 다음을 하나의 Transaction으로 처리한다.

```text
UserCharacterProgress 조회 및 잠금
  → targetLevel과 현재 스킬 레벨 검증
  → targetLevel이 현재와 같으면 현재 결과 반환
  → skillPoints >= 1 검증
  → 대상 스킬 레벨 < 10 검증
  → skillPoints 1 차감
  → 대상 스킬 레벨 1 증가
  → 저장된 진행 상태 반환
```

성공 응답:

```json
{
  "success": true,
  "data": {
    "characterId": "erpin",
    "level": 3,
    "experience": 120,
    "experienceToNextLevel": 900,
    "skillPoints": 1,
    "lowGradeSkillLevel": 2,
    "highGradeSkillLevel": 1
  }
}
```

스킬 포인트가 없으면 `SKILL_POINT_NOT_ENOUGH`, 이미 Lv.10이면 `SKILL_LEVEL_MAX`, 진행 데이터가
없으면 `CHARACTER_PROGRESS_NOT_FOUND` 오류를 반환한다.

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

- `page` 기본값은 `1`, 허용 범위는 `1~1,000,000`의 정수다.
- `limit` 기본값은 `20`, 허용 범위는 `1~100`의 정수다.
- Web 전적 화면은 한 페이지에 10개를 요청한다.
- 정렬은 `endedAt DESC`, 같은 종료 시각에서는 `runId DESC`를 사용한다.
- 전체 Run이 없을 때도 1페이지는 유효하며 빈 배열과 `totalPages: 1`을 반환한다.
- 마지막 페이지보다 큰 `page`는 `RUN_PAGE_OUT_OF_RANGE`를 반환한다.
- 숫자가 아니거나 허용 범위를 벗어난 값은 `VALIDATION_ERROR`를 반환한다.

향후 다음과 같은 Filter를 추가할 수 있다.

```text
?result=clear
?character=erpin
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
        "id": "erpin",
        "name": "에르핀"
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
        "id": "erpin",
        "name": "에르핀"
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
    "total": 32,
    "totalPages": 2
  }
}
```

범위를 벗어난 페이지 (`422 Unprocessable Entity`):

```json
{
  "success": false,
  "error": {
    "code": "RUN_PAGE_OUT_OF_RANGE",
    "message": "요청한 전적 페이지가 범위를 벗어났습니다."
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
      "id": "erpin",
      "name": "에르핀"
    },
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
        "name": "Item A",
        "rarity": "COMMON",
        "floor": 1,
        "order": 1,
        "acquiredAt": "2026-08-14T20:02:10Z"
      },
      {
        "itemId": "item-02",
        "name": "Item B",
        "rarity": "UNCOMMON",
        "floor": 2,
        "order": 2,
        "acquiredAt": "2026-08-14T20:05:30Z"
      },
      {
        "itemId": "item-03",
        "name": "Item C",
        "rarity": "RARE",
        "floor": 3,
        "order": 3,
        "acquiredAt": "2026-08-14T20:08:45Z"
      }
    ]
  }
}
```

`items`는 `order ASC`로 반환한다. 같은 `itemId`를 여러 번 획득한 경우에도 각 획득의
`floor`, `order`, `acquiredAt`을 별도 항목으로 유지한다. Run이 없으면 `404 Not Found`와
`RUN_NOT_FOUND`를 반환하며, Database 또는 네트워크 실패와 구분한다.

`deathReason`은 `ENEMY`, `BOSS`, `HAZARD`, `UNKNOWN` 또는 클리어 Run의 `null`을 사용한다.

---

# 12. Ranking API

전체 기간의 저장 Run을 대상으로 유저당 대표 기록 하나를 반환한다.

```http
GET /api/rankings?type=highest-floor&page=1&limit=20
```

- `type`: `highest-floor`(기본값), `fastest-clear`, `most-clears`
- `page`: 1 이상, 기본값 1
- `limit`: 1~100, 기본값 20
- 범위를 벗어난 유효 페이지는 성공 응답과 빈 `data`를 반환한다.
- 잘못된 Query는 `422 VALIDATION_ERROR`를 반환한다.

`highest-floor`는 `reachedFloor DESC`, `playTime ASC`, `endedAt ASC`, `nickname ASC`,
`runId ASC` 순으로 각 유저의 대표 Run과 전체 순서를 정한다. `fastest-clear`는 클리어 Run만
대상으로 `playTime ASC` 이후 동일한 보조 정렬을 사용한다. `most-clears`는 유저별
`clears DESC`, `totalRuns DESC`, `nickname ASC`, `userId ASC` 순이다. 순위는 동률이어도
페이지 전체에서 연속된 위치 순위이며, 같은 저장 데이터에는 항상 같은 순서가 나온다.

Run 기반 랭킹 항목은 `rank`, `nickname`, `runId`, `character`, `reachedFloor`,
`playTime`, `endedAt`을 반환한다. `most-clears` 항목은 `rank`, `nickname`,
`clears`, `totalRuns`를 반환한다. 내부 유저 UUID는 공개하지 않는다. 응답의 `meta`는 `type`, `page`, `limit`, `total`,
`totalPages`를 포함한다.

---

# 13. Statistics API

통계 모집단은 별도 기간 필터가 없는 전체 저장 Run이다. 비율은 `0~100` 퍼센트, 분모가
0인 비율과 평균·최댓값은 0이다. 현재 계약은 테스트/개발 Run을 임의로 제외하지 않는다.

```http
GET /api/statistics
GET /api/statistics/users/:nickname
GET /api/statistics/characters
GET /api/statistics/items
GET /api/statistics/floors
```

전체 통계는 `totalUsers`, `totalRuns`, `totalClears`, `clearRate`, `averagePlayTime`,
`averageReachedFloor`, `highestReachedFloor`을 반환한다. 유저 통계는 `nickname`과 유저 범위의
동일 Run 지표를 반환하며 없는 닉네임은 `404 USER_NOT_FOUND`이다.

---

# 14. Character Statistics API

`GET /api/statistics/characters`는 캐릭터 카탈로그의 모든 캐릭터를 반환한다. Run이 없는
캐릭터도 0값으로 포함하며 `totalRuns DESC`, `characterId ASC`로 정렬한다. 각 항목은
`characterId`, `characterName`, `totalRuns`, `clears`, `clearRate`,
`averageReachedFloor`, `averagePlayTime`을 포함한다.

---

# 15. Item Statistics API

`GET /api/statistics/items`는 아이템 카탈로그의 모든 아이템을 반환한다. 같은 Run에서 같은
아이템을 여러 번 획득해도 고유 Run 한 건으로 센다. `acquiredRunCount`는 획득한 고유 Run 수,
`selectionRate`는 `acquiredRunCount / 전체 Run 수 × 100`,
`clearRateAfterAcquisition`은 `획득 후 클리어 Run 수 / acquiredRunCount × 100`이다.
획득 Run 수 내림차순, `itemId` 오름차순으로 정렬하며 미획득 아이템도 0값으로 포함한다.

---

# 16. Floor Statistics API

`GET /api/statistics/floors`는 1층부터 실제 최고 도달 층까지 오름차순으로 반환한다.
`reachedRunCount`는 `reachedFloor >= floor`인 Run 수, `reachRate`의 분모는 전체 Run이다.
`deathCount`는 해당 층에서 끝난 미클리어 Run 수이며 `deathRate`의 분모는 해당 층 도달 Run이다.
`clearCount`는 해당 층에 도달한 최종 클리어 Run 수이며 `clearRate`도 해당 층 도달 Run을
분모로 한다. Run이 하나도 없으면 빈 배열을 반환한다.

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
| `RUN_IDEMPOTENCY_CONFLICT` | 같은 clientRunId에 다른 Run 내용이 전송됨 |
| `CHARACTER_PROGRESS_NOT_FOUND` | 캐릭터 진행 데이터 없음 |
| `SKILL_POINT_NOT_ENOUGH` | 사용할 수 있는 스킬 포인트 부족 |
| `SKILL_LEVEL_MAX` | 대상 스킬이 최대 Lv.10 |
| `INVALID_SKILL_TYPE` | `LOW_GRADE`, `HIGH_GRADE`가 아닌 스킬 종류 |
| `INVALID_SKILL_TARGET_LEVEL` | 현재 레벨과 일치하지 않는 강화 목표 |
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
    "id": "erpin",
    "name": "에르핀"
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

Run 시작 전 캐릭터 진행 조회가 실패하면 마지막으로 성공한 로컬 진행 스냅샷을 사용한다. 저장된
스냅샷도 없으면 두 스킬을 Lv.1로 적용하고 오프라인 기본값을 사용 중임을 표시한다. 진행 중인
Run에는 시작 시 스냅샷을 유지하며 Web 등에서 강화된 값은 다음 Run부터 적용한다.

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

Run 재시도는 최초 요청과 동일한 `clientRunId`를 반드시 사용한다. Backend는 이 값을 기준으로
이미 저장된 Run인지 확인하며, 기존 Run이면 경험치와 스킬 포인트를 다시 지급하지 않고 최초 처리
결과를 반환한다. 로컬 재전송 데이터에도 `clientRunId`를 함께 보존한다.

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
GET /api/users/search?q={nickname}
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
| User Search | `/api/users/search?q={nickname}` |
| User Profile | `/api/users/:nickname` |
| User Run History | `/api/users/:nickname/runs` |
| Run Detail | `/api/runs/:runId` |
| Ranking | `/api/rankings` |
| Statistics | `/api/statistics` |
| User Statistics | `/api/statistics/users/:nickname` |
| Character Stats | `/api/statistics/characters` |
| Item Stats | `/api/statistics/items` |
| Floor Stats | `/api/statistics/floors` |

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
│   ├── GET /search?q={nickname}
│   └── GET /:nickname
│       └── GET /runs
│
├── rankings
│   └── GET /
│
└── statistics
    ├── GET /
    ├── GET /users/:nickname
    ├── GET /characters
    ├── GET /items
    └── GET /floors
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

PUT /api/users/:nickname/characters/:characterId/skills/:skillType

GET /api/users/search?q={nickname}

GET /api/users/:nickname

GET /api/users/:nickname/runs

GET /api/runs/:runId

GET /api/rankings

GET /api/statistics

GET /api/statistics/users/:nickname

GET /api/statistics/characters

GET /api/statistics/items

GET /api/statistics/floors
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
