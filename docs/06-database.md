# Database Design

## 1. 개요
본 프로젝트의 Database는 게임 플레이 결과와 유저 전적 및 통계 데이터를 저장하는 역할을 담당한다.
Database는 PostgreSQL을 사용하며 초기 개발 및 운영 환경에서는 Supabase를 활용한다.
전체적인 데이터 흐름은 다음과 같다.

```text
Unity Game
    ↓
Backend API
    ↓
Validation / Business Logic
    ↓
PostgreSQL
    ↓
Backend API
    ↓
Web
```

Unity와 Web은 Database에 직접 접근하지 않는다.
모든 Database 접근은 Backend를 통해 이루어진다.

## 2. Database 설계 목표
Database 설계의 주요 목표는 다음과 같다.
- 게임 플레이 결과를 안정적으로 저장
- 유저별 전적 조회 지원
- Run 상세 정보 조회 지원
- 캐릭터별 통계 지원
- 아이템별 통계 지원
- 랭킹 조회 지원
- 향후 게임 콘텐츠 확장 지원
- 불필요하게 복잡한 구조는 피하고 MVP에 집중

## 3. 핵심 데이터 구조
본 프로젝트에서는 하나의 게임 플레이를 Run이라는 단위로 관리한다.
기본적인 관계는 다음과 같다.

```text
User
 ├──< Run
       │
       ├── Character
       │
       └──< RunItem
               │
               └── Item
 │
 └──< UserCharacterProgress >── Character
```

즉,
- 한 명의 User는 여러 개의 Run을 가질 수 있다.
- 하나의 Run은 하나의 Character를 사용한다.
- 하나의 Run은 여러 개의 Item을 획득할 수 있다.
- 하나의 Item은 여러 Run에서 사용될 수 있다.
- 한 명의 User는 캐릭터마다 하나의 UserCharacterProgress를 가진다.

## 4. ERD
MVP 기준의 기본 ERD는 다음과 같다.

```text
┌──────────────┐
│    users     │
├──────────────┤
│ id PK        │
│ nickname     │
│ created_at   │
└──────┬───────┘
       │
       │ 1:N
       ▼
┌──────────────┐
│     runs     │
├──────────────┤
│ id PK        │
│ client_run_id│
│ user_id FK   │
│ character_id │
│ started_at   │
│ ended_at     │
│ play_time    │
│ reached_floor│
│ is_cleared   │
│ kill_count   │
│ death_reason │
└──────┬───────┘
       │
       │ 1:N
       ▼
┌──────────────┐
│   run_items  │
├──────────────┤
│ id PK        │
│ run_id FK    │
│ item_id FK   │
│ floor        │
│ acquired_at  │
│ item_order   │
└──────┬───────┘
       │
       │ N:1
       ▼
┌──────────────┐
│    items     │
├──────────────┤
│ id PK        │
│ name         │
│ description  │
│ rarity       │
│ is_active    │
│ max_stacks   │
│ effect_data  │
└──────────────┘

┌──────────────────┐
│   characters     │
├──────────────────┤
│ id PK            │
│ name             │
│ description      │
│ is_active        │
└──────────────────┘

┌─────────────────────────┐
│ user_character_progress │
├─────────────────────────┤
│ id PK                   │
│ user_id FK              │
│ character_id FK         │
│ level                   │
│ experience              │
│ skill_points            │
│ low_grade_skill_level   │
│ high_grade_skill_level  │
└─────────────────────────┘
```

## 5. Table 목록
MVP에서는 다음 테이블을 우선 사용한다.

| Table | 역할 |
|---|---|
| users | 유저 정보 |
| characters | 플레이 가능한 캐릭터 정보 |
| items | 아이템 정보 |
| runs | 하나의 게임 플레이 기록 |
| run_items | Run에서 획득한 아이템 기록 |
| user_character_progress | 캐릭터별 레벨, 경험치, 스킬 포인트와 스킬 레벨 |

초기에는 이 정도로 시작한다.
필요한 경우 개발 과정에서 다음 테이블을 추가할 수 있다.
- `run_events`
- `bosses`
- `enemies`
- `rooms`
- `floors`
- `item_synergies`

단, 처음부터 모든 데이터를 Database에 저장하지 않는다.

## 6. users
유저 정보를 저장한다.
현재 프로젝트의 핵심은 로그인 시스템이 아니라 전적 검색이므로 MVP에서는 최소한의 유저 정보만 관리한다.

### Schema: users

| Column | Type | 설명 |
|---|---|---|
| id | UUID | 유저 고유 ID |
| nickname | VARCHAR | 유저 닉네임 |
| created_at | TIMESTAMP | 생성 시간 |
| updated_at | TIMESTAMP | 수정 시간 |

### 6.1 User ID
User의 식별자는 UUID를 우선 사용한다.
예: `550e8400-e29b-41d4-a716-446655440000`
닉네임을 Primary Key로 사용하지 않는다. 닉네임은 변경될 수 있기 때문이다.

### 6.2 Nickname
닉네임은 Web에서 유저 검색의 기준으로 사용한다.
예: `GET /api/users?nickname=테스트유저`
닉네임은 필요에 따라 Unique 제약을 적용한다.

## 7. characters
플레이 가능한 캐릭터 정보를 저장한다.
현재는 1종만 존재하지만 향후 캐릭터 추가를 고려하여 별도의 테이블로 관리한다.

### Schema: characters

| Column | Type | 설명 |
|---|---|---|
| id | VARCHAR / UUID | 캐릭터 ID |
| name | VARCHAR | 캐릭터 이름 |
| description | TEXT | 캐릭터 설명 |
| is_active | BOOLEAN | 사용 가능 여부 |
| created_at | TIMESTAMP | 생성 시간 |
| updated_at | TIMESTAMP | 수정 시간 |

### 7.1 Character 확장
현재:
```text
characters
  erpin
```
향후:
```text
characters
  erpin
  character_b
  character_c
```

Run에는 캐릭터 ID만 저장한다.
`runs.character_id` ↓ `characters.id`
따라서 캐릭터가 추가되더라도 runs 테이블 구조는 변경하지 않는다.

### 7.2 User Character Progress

Run 외부의 캐릭터별 성장 상태를 저장한다. 경험치는 소비 재화가 아니라 현재 레벨 구간에서
누적되는 값이며, 레벨업할 때 필요한 양만 차감하고 초과분은 다음 구간으로 이월한다.

### Schema: user_character_progress

| Column | Type | 설명 |
|---|---|---|
| id | UUID | 진행 데이터 고유 ID |
| user_id | UUID | 유저 ID |
| character_id | VARCHAR | 캐릭터 ID |
| level | INTEGER | 캐릭터 레벨, 기본 1, MVP 최대 19 |
| experience | INTEGER | 현재 레벨 구간에 남은 경험치 |
| skill_points | INTEGER | 사용하지 않은 스킬 포인트 |
| low_grade_skill_level | INTEGER | 저학년 스킬 레벨, 1~10 |
| high_grade_skill_level | INTEGER | 고학년 스킬 레벨, 1~10 |
| created_at | TIMESTAMP | 생성 시간 |
| updated_at | TIMESTAMP | 수정 시간 |

- `(user_id, character_id)`에 Unique 제약을 둔다.
- 최대 캐릭터 레벨과 레벨별 필요 경험치는 Backend 설정 또는 캐릭터 진행 데이터로 관리한다.
- 향후 최대 레벨이 늘어나도 테이블 구조를 변경하지 않는다.

## 8. items
게임에서 사용할 수 있는 아이템 정보를 저장한다.
MVP에서는 약 10종의 아이템을 구현한다.

### Schema: items

| Column | Type | 설명 |
|---|---|---|
| id | VARCHAR / UUID | 아이템 ID |
| name | VARCHAR | 아이템 이름 |
| description | TEXT | 아이템 설명 |
| rarity | VARCHAR | 희귀도 |
| is_active | BOOLEAN | 사용 가능 여부 |
| max_stacks | INTEGER | Run 내 최대 스택 |
| effect_data | JSONB | Unity와 동일한 복합 효과 계약과 필수 수치 |
| created_at | TIMESTAMP | 생성 시간 |
| updated_at | TIMESTAMP | 수정 시간 |

### 8.1 Item 데이터와 실제 효과
아이템의 실제 효과 적용은 Unity에서 게임 로직으로 처리한다.
Database에는 식별·표시 정보와 함께 Unity 계약 드리프트를 검사할 수 있도록 최대 스택과 구조화된
복합 효과 데이터도 저장한다. Backend는 이 데이터를 직접 전투 계산에 사용하지 않는다.
예:
```text
items
  id: item_attack_up
  name: 공격력 강화
  description: 공격력 증가
  rarity: COMMON
  max_stacks: 5
  effect_data: [{ "type": "AttackDamagePercent", "magnitude": 0.05 }]
```
Unity에서는 해당 `item_id`를 기준으로 실제 효과를 적용한다.

`rarity`는 다음 안정적인 문자열 계약을 사용한다.

| 게임 표시 | DB 값 |
|---|---|
| 일반 | `COMMON` |
| 고급 | `UNCOMMON` |
| 희귀 | `RARE` |
| 전설 | `EPIC` |

등급별 기본 드롭 가중치는 Unity 아이템 데이터에서 관리한다. Database의 rarity는 전적 표시,
통계와 Run 종료 경험치 계산에 사용한다.

## 9. runs
Database의 핵심 테이블이다.
하나의 Run은 플레이어가 게임을 시작해서 클리어하거나 사망할 때까지의 하나의 플레이 기록이다.

### Schema: runs

| Column | Type | 설명 |
|---|---|---|
| id | UUID | Run 고유 ID |
| client_run_id | UUID | Unity가 Run 시작 시 생성하는 멱등성 식별자 |
| request_fingerprint | CHAR(64) | `client_run_id`를 제외한 정규화 요청의 SHA-256 |
| user_id | UUID | 플레이한 유저 |
| character_id | VARCHAR / UUID | 사용 캐릭터 |
| started_at | TIMESTAMP | 게임 시작 시간 |
| ended_at | TIMESTAMP | 게임 종료 시간 |
| play_time | INTEGER | 플레이 시간(초) |
| reached_floor | INTEGER | 도달한 층 |
| is_cleared | BOOLEAN | 클리어 여부 |
| kill_count | INTEGER | 총 처치 수 |
| death_reason | VARCHAR | 사망 원인 |
| experience_gained | INTEGER | 해당 Run에 한 번만 지급된 경험치 |
| progress_snapshot | JSONB | 최초 지급 직후 API에 반환한 캐릭터 진행 결과 |
| created_at | TIMESTAMP | 기록 생성 시간 |

`client_run_id`에는 Unique 제약을 둔다. Unity가 네트워크 실패 후 같은 Run을 재전송하면 Backend는
요청 지문을 비교해 새 Run과 경험치를 만들지 않고 `experience_gained`와 `progress_snapshot`에
보존된 최초 결과를 반환한다. 같은 ID에 다른 Run 내용이 전송되면 멱등성 충돌로 거절한다.

지문은 `client_run_id` 자체를 제외한 Run 필드 전체와 `order`로 정렬한 RunItem의 `item_id`,
`floor`, `item_order`, `acquired_at`을 정규화한 뒤 계산한다. 배열 전송 순서만 다른 같은 내용은
동일 요청이지만, 획득 순서나 시각을 포함한 실제 내용이 다르면 충돌이다.

## 10. Run 상태
Run은 기본적으로 다음 두 가지 결과를 가진다.
- CLEAR
- DEATH

Database에서는 `is_cleared`로 관리한다.
- `true` → Clear
- `false` → Death

향후 필요하다면 별도의 상태 컬럼으로 확장할 수 있다.

## 11. reached_floor
플레이어가 해당 Run에서 도달한 최대 층을 저장한다.
예:
- `reached_floor = 1` → 1층에서 사망
- `reached_floor = 3`, `is_cleared = true` → 3층 최종 보스 클리어

이 데이터는 랭킹과 통계에서 활용할 수 있다.

## 12. play_time
플레이 시간을 초 단위 Integer로 저장한다.
예:
`play_time = 623`
이는 약 10분 23초를 의미한다.
초 단위로 저장하면 Backend와 Web에서 원하는 형식으로 변환할 수 있다.
예: `623` ↓ `10m 23s`

## 13. death_reason
플레이어가 사망한 경우 사망 원인을 기록한다.
예:
- ENEMY
- BOSS
- HAZARD
- UNKNOWN

MVP에서는 실제 게임에 존재하는 사망 원인만 사용한다.
클리어한 경우에는 NULL을 허용한다.
- Clear → `death_reason = NULL`
- Death → `death_reason = "BOSS"`

## 14. run_items
Run에서 획득한 아이템을 기록한다.
하나의 Run은 여러 개의 Item을 가질 수 있으므로 별도의 연결 테이블을 사용한다.

### Schema: run_items

| Column | Type | 설명 |
|---|---|---|
| id | UUID | 기록 고유 ID |
| run_id | UUID | Run ID |
| item_id | VARCHAR / UUID | 아이템 ID |
| floor | INTEGER | 획득한 층 |
| acquired_at | TIMESTAMP | 획득 시간 |
| item_order | INTEGER | 획득 순서 |

## 15. Item 획득 순서
아이템 획득 순서는 중요하게 기록한다.
예:
Run A
1. 공격력 증가
2. 이동 속도 증가
3. 투사체 증가
4. 공격 속도 증가

Database: `run_items`

| run_id | item_id | item_order |
|---|---|---|
| A | item_01 | 1 |
| A | item_02 | 2 |
| A | item_03 | 3 |
| A | item_04 | 4 |

향후 Web에서 다음과 같은 기능을 구현할 수 있다.
- "이 유저는 어떤 순서로 빌드를 구성했는가?"
- "클리어 유저들이 초반에 어떤 아이템을 많이 선택하는가?"

## 16. Item과 Run의 관계
관계는 다음과 같다.
```text
Run
 │
 ├── Item A
 ├── Item B
 ├── Item C
 └── Item D
```
Database에서는 다음과 같이 표현한다.
```text
runs
  │
  │ 1:N
  ▼
run_items
  │
  │ N:1
  ▼
items
```
즉 `runs`와 `items`는 직접 연결하지 않고 `run_items`를 통해 연결한다.

## 17. Foreign Key 관계
MVP 기준 Foreign Key 관계는 다음과 같다.
- `runs.user_id` ↓ `users.id`
- `runs.character_id` ↓ `characters.id`
- `run_items.run_id` ↓ `runs.id`
- `run_items.item_id` ↓ `items.id`
- `user_character_progress.user_id` ↓ `users.id`
- `user_character_progress.character_id` ↓ `characters.id`

전체 구조:
```text
users
  ├────< runs >──── characters
  │        │
  │        └────< run_items >──── items
  │
  └────< user_character_progress >──── characters
```

## 18. 데이터 정규화
기본적으로 관계형 Database의 구조를 유지한다.
예를 들어 Run에 다음처럼 아이템 이름을 직접 저장하지 않는다.
`runs.items = "공격력 증가, 이동속도 증가, 투사체 증가"`

대신:
`runs` ↓ `run_items` ↓ `items`
구조로 관리한다.
이렇게 하면 아이템 정보가 변경되어도 기존 Run 데이터와 중복되지 않는다.

## 19. Run Result 저장 흐름
Unity에서 Run이 종료되면 다음과 같은 데이터가 생성된다.
```json
{
  "clientRunId": "client-generated-run-uuid",
  "userId": "user-id",
  "characterId": "erpin",
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
    }
  ]
}
```
Backend는 이 데이터를 검증한 후 Database에 저장한다.

## 20. Database 저장 과정
```text
Unity
 │
 │ RunResult
 ▼
Backend API
 │
 ├── Validation
 │
 ├── User 확인
 │
 ├── Character 확인
 │
 ├── Item 확인
 │
 └── Run 생성
       │
       ├── runs INSERT
       │
       └── run_items INSERT
```
하나의 Run을 저장할 때 runs, run_items, 경험치와 캐릭터 진행 상태가 함께 저장되어야 한다.
가능하면 하나의 Transaction으로 처리한다.

## 21. Transaction
Run 저장 과정에서 일부 데이터만 저장되는 상황을 방지한다.
잘못된 상황:
- `runs` → 저장 성공
- `run_items` → 저장 실패
이 경우 Run은 존재하지만 아이템 정보가 사라질 수 있다.

따라서:
```sql
BEGIN TRANSACTION;

INSERT INTO runs ...;

INSERT INTO run_items ...;
INSERT INTO run_items ...;
INSERT INTO run_items ...;

COMMIT;
```
모든 과정이 성공해야 저장을 확정한다.
중간에 오류가 발생하면 `ROLLBACK` 하여 전체 저장을 취소한다.

### 21.1 Run 경험치와 멱등성 Transaction

```text
client_run_id 조회
  ├─ 이미 존재하고 요청 지문 일치 → 저장된 Run·경험치·진행 스냅샷 반환
  ├─ 이미 존재하고 요청 지문 불일치 → RUN_IDEMPOTENCY_CONFLICT
  └─ 없음
      → Run과 RunItem 저장
      → Backend가 획득 경험치 계산
      → UserCharacterProgress 잠금/조회
      → 필요 경험치를 순서대로 차감하며 최대 Lv.19까지 연속 레벨업
      → 상승한 레벨 수만큼 skill_points 지급
      → 진행 상태 저장
      → Commit
```

Run 저장, 경험치 지급, 스킬 포인트 지급은 하나의 Transaction에서 처리한다. 같은
`client_run_id`가 동시에 요청되어도 Unique 제약과 Transaction으로 한 번만 지급되도록 한다.

## 22. Index 설계
Web에서 자주 조회하는 데이터에는 Index를 적용한다.
초기에는 다음 Index를 우선 고려한다.

- `users.nickname`: 유저 검색 (`WHERE nickname = ?`)
- `runs.user_id`: 유저의 전적 조회 (`WHERE user_id = ?`)
- `runs.character_id`: 캐릭터별 통계 (`WHERE character_id = ?`)
- `runs.is_cleared`: 클리어 기록 조회 (`WHERE is_cleared = true`)
- `run_items.item_id`: 아이템 선택률 계산 (`GROUP BY item_id`)

## 23. Ranking Query를 고려한 Index
랭킹 기능을 고려하여 다음과 같은 컬럼을 자주 조회한다.
- `reached_floor`
- `play_time`
- `is_cleared`

예:
- 최고 도달 층 랭킹: `ORDER BY reached_floor DESC`
- 클리어 타임 랭킹: `WHERE is_cleared = true ORDER BY play_time ASC`

실제 Index는 데이터가 쌓인 이후 Query 성능을 확인하면서 최적화한다.
처음부터 모든 컬럼에 Index를 생성하지 않는다.

## 24. 통계 데이터
통계는 가능한 경우 원본 데이터를 기반으로 Backend에서 계산한다.
예:
- `runs` ↓ Backend Aggregation ↓ `Character Win Rate`
- `run_items` ↓ Backend Aggregation ↓ `Item Pick Rate`

MVP에서는 별도의 통계 테이블을 만들지 않는다.

## 25. 캐릭터 통계
예:
`전체 Run` ↓ `에르핀 사용 Run` ↓ `Clear 수 / 전체 Run 수` ↓ `Win Rate`
현재는 캐릭터가 1종뿐이지만 향후 캐릭터가 추가되면 자동으로 통계를 확장할 수 있다.

## 26. 아이템 통계
아이템 선택률은 `run_items`를 기반으로 계산한다.
예:
- 전체 Run = 1,000
- Item A 획득 Run = 300
- Item A 선택률 = 300 / 1,000 = 30%

중요한 점은 동일한 Run에서 같은 아이템을 여러 번 획득할 수 있는지 여부다.
MVP에서는 일반적으로 동일 아이템의 중복 획득을 제한하는 방향을 우선한다.
필요할 경우 `run_items`에 Unique Constraint를 추가할 수 있다.

## 27. 유저 전적 조회
유저 페이지에서 다음 데이터를 제공할 수 있다.

**User Profile**
- 닉네임
- 총 플레이 횟수
- 클리어 횟수
- 승률
- 평균 플레이 시간
- 평균 도달 층
- 최고 도달 층

이 데이터는 `users`와 `runs`를 조합하여 계산한다.

## 28. 최근 전적 조회
특정 유저의 최근 Run을 조회한다.
```sql
SELECT
    *
FROM runs
WHERE user_id = ?
ORDER BY ended_at DESC
LIMIT 10;
```
Web에서는 다음과 같이 표시할 수 있다.

**Recent Runs**
- Clear / 3F / 10:23
- Death / 2F / 07:31
- Death / 1F / 04:52

## 29. Run 상세 조회
특정 Run의 상세 페이지에서는 다음 데이터를 보여준다.

**Run Detail**
- Character
- Reached Floor
- Play Time
- Kill Count
- Result
- Death Reason

**Items**
- ├── Item A
- ├── Item B
- └── Item C

Database 조회: `runs` ↓ `run_items` ↓ `items`

## 30. 향후 확장 테이블
MVP 이후 필요할 경우 다음 테이블을 추가한다.

### 30.1 run_events
게임 중 발생한 세부 이벤트를 저장한다.
예:
```text
run_events
  id
  run_id
  event_type
  floor
  room
  timestamp
  metadata
```
이벤트 예: `ROOM_ENTER`, `ENEMY_KILL`, `ITEM_ACQUIRED`, `BOSS_START`, `BOSS_DEFEATED`, `PLAYER_DAMAGE`, `PLAYER_DEATH`
이 데이터를 활용하면 향후 매우 상세한 통계 분석이 가능하다. 단, MVP에서는 구현하지 않는다.

### 30.2 item_synergies
아이템 간 시너지 정보를 별도로 관리할 필요가 생기면 추가한다.
```text
item_synergies
  id
  item_a_id
  item_b_id
  effect
```
예: `Item A + Item B → 특수 효과`
현재는 Unity 내부에서 처리하는 것을 우선한다.

### 30.3 enemies
몬스터 데이터를 Backend에서도 관리해야 할 필요가 생기면 추가한다.
```text
enemies
  id
  name
  type
  is_active
```
다만 MVP에서는 몬스터 정보가 전적 데이터에 필수적이지 않으므로 Database에 저장하지 않는다.

## 31. Database에 저장하지 않는 데이터
게임에서 발생하는 모든 데이터를 Database에 저장하지 않는다.
예를 들어 다음 데이터는 기본적으로 Unity 내부에서만 관리한다.
- 현재 플레이어 위치
- 현재 몬스터 위치
- 현재 투사체 위치
- 실시간 HP 변화
- 실시간 전투 상태
- 방 내부의 임시 상태

이러한 데이터는 서버에 저장할 필요가 없다.

## 32. Database 저장 기준
데이터를 Database에 저장할지 여부는 다음 기준으로 판단한다.

**저장한다:**
- 게임 종료 후에도 필요한 데이터
- 전적에 필요한 데이터
- 통계에 필요한 데이터
- 랭킹에 필요한 데이터
- Web에서 조회해야 하는 데이터

**저장하지 않는다:**
- 게임 내부에서만 필요한 일시적인 상태
- 실시간 게임 상태
- Web에서 활용하지 않는 데이터
- 저장했을 때 비용 대비 가치가 낮은 데이터

## 33. MVP Database 구조
최종 MVP 구조는 다음과 같다.
```text
┌──────────────┐
│    users     │
└──┬────────┬──┘
   │        │
   │        └──────────< user_character_progress >────────── characters
   │
   │ 1:N
   ▼
┌──────────────┐
│     runs     │
└──────┬───────┘
       │
       │ 1:N
       ▼
┌──────────────┐
│   run_items  │
└──────┬───────┘
       │
       │ N:1
       ▼
┌──────────────┐
│    items     │
└──────────────┘

┌──────────────────┐
│   characters     │
└────────┬─────────┘
         │
         │ 1:N
         ▼
       runs
```

## 34. MVP Table Summary

| Table | 중요도 | 목적 |
|---|---|---|
| users | 필수 | 유저 식별 |
| characters | 필수 | 캐릭터 관리 |
| items | 필수 | 아이템 관리 |
| runs | 핵심 | 플레이 기록 |
| run_items | 핵심 | 아이템 획득 기록 |
| user_character_progress | 핵심 | 캐릭터 레벨, 경험치, 포인트와 두 스킬 레벨 |
| run_events | 추후 | 상세 플레이 로그 |
| item_synergies | 추후 | 아이템 시너지 관리 |
| enemies | 추후 | 몬스터 데이터 관리 |

## 35. 데이터 보존 원칙
게임에서 생성된 Run 데이터는 가능한 한 원본 기록을 유지한다.
예를 들어 유저의 통계가 변경되더라도 기존 Run 기록 자체를 수정하지 않는다.
```text
Run
├── Character
├── Result
├── Play Time
├── Floor
└── Items
```
통계는 원본 Run 데이터를 기반으로 다시 계산할 수 있어야 한다.

## 36. 데이터 무결성
Backend는 다음 조건을 보장해야 한다.
- 존재하지 않는 User ID를 저장하지 않는다.
- 존재하지 않는 Character ID를 저장하지 않는다.
- 존재하지 않는 Item ID를 저장하지 않는다.
- 음수인 Play Time을 저장하지 않는다.
- 유효하지 않은 Floor를 저장하지 않는다.
- 유효하지 않은 Item ID를 저장하지 않는다.
- Run과 Run Item이 불완전하게 저장되지 않도록 한다.
- `(user_id, character_id)` 진행 데이터가 중복되지 않도록 한다.
- 캐릭터 레벨은 1~19, 두 스킬 레벨은 각각 1~10 범위를 보장한다.
- 스킬 포인트가 음수가 되지 않도록 한다.
- 같은 `client_run_id`에 경험치를 두 번 지급하지 않는다.

## 37. 데이터 신뢰 경계
Unity에서 전달되는 데이터는 신뢰하지 않는다.
```text
Unity
 ↓
Untrusted Data
 ↓
Backend Validation
 ↓
Trusted Data
 ↓
Database
```
따라서 Database에 저장되는 데이터는 반드시 Backend의 검증 과정을 거친다.

## 38. Database와 Game의 관계
Unity는 Database 구조를 직접 알 필요가 없다.
Unity가 알아야 하는 것은 Backend API의 Request / Response 형식이다.
```text
Unity
 ↓
RunResult DTO
 ↓
Backend
 ↓
Database
```
예를 들어 Unity는 다음과 같은 데이터를 전송한다.
```json
{
  "characterId": "erpin",
  "reachedFloor": 3,
  "playTime": 623,
  "isCleared": true,
  "killCount": 142,
  "items": [
    "item-01",
    "item-02",
    "item-03"
  ]
}
```
Backend가 이를 Database 구조에 맞게 저장한다.

## 39. Database 설계 핵심 원칙

### 39.1 Run 중심 설계
본 프로젝트의 가장 중요한 데이터 단위는 Run이다.
`User` ↓ `Run` ↓ `Items`

### 39.2 원본 데이터 보존
통계를 미리 저장하기보다 원본 Run 데이터를 저장하고 필요할 때 계산하는 것을 우선한다.

### 39.3 MVP 우선
처음부터 모든 게임 이벤트를 Database에 저장하지 않는다.
필요한 데이터만 저장한다.

### 39.4 Backend를 통한 접근
Unity와 Web은 Database에 직접 접근하지 않는다.
- Unity → Backend → Database
- Web → Backend → Database

### 39.5 확장 가능성
현재 캐릭터 1종, 아이템 약 10종으로 시작하지만 향후 콘텐츠가 증가해도 기존 Database 구조를 크게 변경하지 않는 것을 목표로 한다.

## 40. Definition of Done
Database 설계는 다음 조건을 만족하면 MVP 기준 완료로 정의한다.
- [ ] users 테이블 정의
- [ ] characters 테이블 정의
- [ ] items 테이블 정의
- [ ] runs 테이블 정의
- [ ] run_items 테이블 정의
- [ ] user_character_progress 테이블 정의
- [ ] Foreign Key 관계 정의
- [ ] 기본 Index 정의
- [ ] Run 저장 Transaction 정의
- [ ] Run 경험치 지급과 연속 레벨업 Transaction 정의
- [ ] client_run_id 멱등성 제약 정의
- [ ] Run Result 데이터 구조 정의
- [ ] 유저 전적 조회 구조 정의
- [ ] 랭킹 조회에 필요한 데이터 정의
- [ ] 통계 계산에 필요한 데이터 정의
- [ ] 향후 확장 테이블 구분

## 41. 최종 Database 구조
```text
                         ┌──────────────┐
                         │    users     │
                         │              │
                         │ id           │
                         │ nickname     │
                         └──────┬───────┘
                                │
                                │ 1:N
                                ▼
┌────────────────┐       ┌──────────────┐
│  characters    │       │     runs     │
│                │       │              │
│ id             │◄──────│ character_id │
│ name           │       │ user_id      │
│ description    │       │ play_time    │
│ is_active      │       │ floor        │
└────────────────┘       │ clear        │
                         │ kill_count   │
                         │ death_reason │
                         └──────┬───────┘
                                │
                                │ 1:N
                                ▼
                         ┌──────────────┐
                         │  run_items   │
                         │              │
                         │ run_id       │
                         │ item_id      │
                         │ floor        │
                         │ acquired_at  │
                         │ item_order   │
                         └──────┬───────┘
                                │
                                │ N:1
                                ▼
                         ┌──────────────┐
                         │    items     │
                         │              │
                         │ id           │
                         │ name         │
                         │ description  │
                         │ rarity       │
                         │ is_active    │
                         │ max_stacks   │
                         │ effect_data  │
                         └──────────────┘
```

본 프로젝트의 Database는 하나의 플레이 세션을 Run으로 정의하고, `User` → `Run` → `RunItem` → `Item`의 관계를 중심으로 게임 플레이 데이터를 저장한다. 이를 기반으로 유저 전적, 랭킹, 캐릭터 통계, 아이템 통계 등의 Web 서비스를 제공한다.

---

## 42. ID 관리 정책

이 프로젝트는 세 가지 ID 체계를 사용한다. 출시 후 ID의 의미를 바꾸거나 재사용하지 않는다.

### 42.1 ID 형식별 사용 영역

| 형식 | 사용 영역 | 예시 | 생성 주체 |
|------|-----------|------|-----------|
| UUID | User, Run, RunItem, UserCharacterProgress | `550e8400-e29b-41d4-a716-446655440000` | 서버 자동 생성 |
| UUID | clientRunId (멱등성 키) | `00000000-0000-4000-8000-000000000001` | Unity 클라이언트 |
| 문자열 (VARCHAR) | Character | `erpin` | 개발자 수동 할당 |
| 문자열 (VARCHAR) | Item | `item-01`, `item-15` | 개발자 수동 할당 |
| SHA-256 해시 | requestFingerprint | 64자 해시 | Backend 계산 |

### 42.2 Item ID 규칙

- **형식:** `item-{숫자}` (예: `item-01`, `item-12`)
- **숫자 범위:** 01부터 시작, 0-패딩 2자리 사용
- **연속성 불요:** 중간 번호 건너뛰기 허용 (item-05 비활성, item-06 비활성 등)
- **재사용 금지:** 한 번 할당된 ID는 다른 아이템에 재사용하지 않음
- **의미 변경 금지:** 기존 ID의 효과나 이름을 완전히 다른 것으로 바꾸지 않음

현재 할당된 Item ID:

| ID | 이름 | 상태 |
|---|---|---|
| `item-01` | 급조한 목검 | 활성 |
| `item-02` | 풍선 갑옷 | 활성 |
| `item-03` | 도깨비 감투 | 활성 |
| `item-04` | 낡은 화살 | 활성 |
| `item-05` | 투사체 크기 강화 | 비활성 (레거시) |
| `item-06` | 다중 투사체 | 비활성 (레거시, 시너지 계약 보존) |
| `item-07` | 공격 범위 강화 | 비활성 (레거시) |
| `item-08` | 코미의 베개 | 활성 |
| `item-09` | 피격 반격 | 비활성 (레거시) |
| `item-10` | 추가 공격 | 비활성 (레거시) |
| `item-11` | 다야의 다이아몬드 커터 | 활성 |
| `item-12` | 녹슨 송곳 | 활성 |
| `item-13` | 에르핀의 지팡이 | 활성 |
| `item-14` | 광기의 가면 | 활성 |
| `item-15` | 장난감 망원경 | 활성 |

새 아이템 추가 시 `item-16`부터 순차 할당한다.

### 42.3 Character ID 규칙

- **형식:** 영문 소문자 문자열 (예: `erpin`)
- **최대 길이:** 64자 (VARCHAR(64))
- **재사용 금지:** 삭제된 캐릭터 ID를 새 캐릭터에 재사용하지 않음

현재 할당된 Character ID:

| ID | 이름 | 상태 |
|---|---|---|
| `erpin` | 에르핀 | 활성 |

### 42.4 Rarity 계약

희귀도는 Unity와 Database에서 다른 형식을 사용하지만 의미는 동일하다.

| 게임 표시 | Unity Enum | Database 값 |
|---|---|---|
| 일반 | `Common (0)` | `COMMON` |
| 고급 | `Uncommon (1)` | `UNCOMMON` |
| 희귀 | `Rare (2)` | `RARE` |
| 전설 | `Epic (3)` | `EPIC` |

Backend API는 문자열(`COMMON` 등)을 사용하고, Unity는 정수(0~3)를 사용한다.
변환은 Unity의 `ItemRarity` enum과 Backend의 `ItemRarity` 상수에서 처리한다.

### 42.5 ItemEffectType 계약

효과 타입은 Unity enum의 정수값으로 에셋에 저장되며 Backend는 사용하지 않는다.
새 효과 추가 시 기존 번호를 재사용하지 않고 끝에 추가한다.

| 번호 | 타입 | 용도 |
|---|---|---|
| 0~5 | 레거시 효과 | AttackDamage, MaxHealth, MoveSpeed, MultiShot, Pierce, HealOnKill |
| 6~18 | Phase G 효과 | AttackDamagePercent, CriticalChance, DistanceDamage, SplitAfterPierce 등 |

### 42.6 Unity ↔ Backend 계약 동기화

- Unity의 `ItemDefinition` 에셋과 Backend의 `ITEM_CATALOG`는 동일한 ID, 이름, 희귀도, 활성 상태를 유지해야 한다.
- `/contract-drift-check` 명령으로 불일치를 검사할 수 있다.
- 한쪽만 변경하면 Run 저장 시 `ITEM_NOT_FOUND` 오류가 발생한다.

### 42.7 ID 변경이 필요할 때

ID 자체는 변경하지 않는다. 대신:

1. **효과 조정:** 같은 ID를 유지하고 수치만 변경
2. **완전히 다른 아이템:** 새 ID 할당 (기존 ID는 비활성 처리)
3. **아이템 삭제:** `isActive = false`로 비활성화 (ID와 기록 보존)

이 정책은 기존 Run 기록의 무결성을 보장하고, 전적에서 "이 Run에서 획득한 item-01"이 항상 같은 의미를 유지하도록 한다.
