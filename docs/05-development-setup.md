# 개발 환경 설정 가이드

### 돌진형 진단 로그 (2026-09-29)

돌진형의 `Minimum Charge Distance` 기본값은 1유닛이다. 예고 시작 전에 플레이어 몸통까지의
여유 거리와 첫 장애물 접촉 거리를 검사한다. 예고 후 근접·엄폐로는 취소하지 않는다.
현재 미끄러짐 허용은 벽면 기준 30°(법선 기준 60°)이며, 아래 과거 80° 설명을 대체한다.
일반 적·부스러기·소환몹의 공통 공격 단계 배율은 모두 1로 고정했다.

Play Mode에서 돌진형 인스턴스를 선택하고 Inspector의 `Charging Enemy Controller` 컴포넌트에서
`Development > Log Charge Diagnostics`를 켠다. 여러 적을 관찰하려면 Play 전 ChargingEnemy Prefab에서 켠다.
Console에서 `[Charge]`로 필터링한다. Editor와 Development Build에서만 로그를 출력한다.
`WindupStarted`, `DashStarted`, `WallSlide`, `WallImpact`, `DoorHit`, `PlayerHit`,
`DurationElapsed`, `InterruptedByTargetOrActionState`를 위치·방향·법선·충돌체·인스턴스 ID와 함께 기록한다.
예고 후 벽 뒤로 이동해도 `DashStarted`가 나오고 실제 충돌 또는 시간 만료 때 종료되어야 한다.
예고선은 몸통 반경으로 계산한 첫 Environment 접촉까지의 직선 구간이며 이후 미끄러짐은 표시하지 않는다.
검증 메뉴: `Trickal Fan Game > Week 18 > Verify Obstacle-0 Enemy Obstacle Avoidance`.
아래 기존 검증 설명 중 '예고 종료 시 막힘 회복'은 폐기되었으며, 지금은 예고 후 경로 차단에도
돌진 실행 및 실제 접촉 후 회복을 검사한다.

## 1. 목적

이 문서는 트릭컬 팬게임의 Game, Backend, Web 개발 환경을 동일한 방식으로 구성하기 위한 기준이다.

목표는 다음과 같다.

- 새 개발 환경에서도 같은 버전과 명령으로 프로젝트를 실행한다.
- Unity → Backend → Database → Web의 데이터 흐름을 로컬에서 검증한다.
- Secret과 운영 설정을 Git 저장소에 포함하지 않는다.
- Database 스키마 변경 이력을 하나의 방식으로 관리한다.

이 문서는 Windows 11 개발 환경을 기준으로 작성한다.

---

## 2. 확정 기술 조합

| 영역 | 선택 | 기준 |
|---|---|---|
| Game Engine | Unity 6.3 LTS | 프로젝트 기간 중 Editor 버전을 고정한다. |
| Game Language | C# | Unity 게임 로직과 API 통신에 사용한다. |
| JavaScript Runtime | Node.js 24 LTS | Backend와 Web의 공통 런타임이다. |
| Package Manager | pnpm 11 | Backend와 Web의 의존성을 일관되게 설치한다. |
| Backend | NestJS | REST API, 요청 검증, 비즈니스 로직을 담당한다. |
| ORM / Migration | Prisma | PostgreSQL 모델, DB 접근, 마이그레이션을 관리한다. |
| Web | Next.js App Router | 전적 검색 및 통계 화면을 제공한다. |
| Database Platform | Supabase PostgreSQL | 관리형 PostgreSQL 데이터베이스를 제공한다. |

Node.js, pnpm, NestJS, Prisma, Next.js의 세부 버전은 프로젝트 생성 시점의 안정 버전으로 `package.json`과 lockfile에 고정한다. Unity Editor의 세부 버전은 `game/ProjectSettings/ProjectVersion.txt`를 기준으로 고정한다.

---

## 3. 책임과 연결 규칙

```text
Unity Game (C#)
    │ HTTPS / REST
    ▼
NestJS Backend (TypeScript)
    │ Prisma
    ▼
Supabase PostgreSQL
    ▲
    │ REST
Next.js Web (TypeScript)
```

- Unity는 Backend API만 호출하며 Database에 직접 연결하지 않는다.
- Next.js는 Backend API만 호출하며 Database에 직접 연결하지 않는다.
- Prisma Client는 Backend에서만 사용한다.
- Supabase는 PostgreSQL 호스팅과 관리 도구로 사용한다. MVP에서 Unity 또는 Web의 Supabase SDK 직접 호출은 사용하지 않는다.

---

## 4. 사전 설치 항목

### 필수

| 도구 | 설치 기준 | 확인 명령 |
|---|---|---|
| Git | 최신 안정 버전 | `git --version` |
| Node.js | 24 LTS | `node --version` |
| pnpm | 11.x | `pnpm --version` |
| Unity Hub | 최신 안정 버전 | Unity Hub 실행 |
| Unity Editor | Unity 6.3 LTS + Windows Build Support (IL2CPP) | Unity Hub에서 확인 |
| IDE | Visual Studio Community + Game development with Unity 또는 JetBrains Rider | C# 파일 열기 |
| Supabase 계정 | 개발용 프로젝트 생성 가능 | Supabase Dashboard 로그인 |

### 선택

| 도구 | 사용하는 시점 |
|---|---|
| Docker Desktop | Supabase 전체 로컬 스택을 실행할 때 |
| Supabase CLI | 로컬 Supabase, 프로젝트 연결, 타입 생성이 필요할 때 |
| Postman 또는 Bruno | API를 Unity 연결 전에 수동 검증할 때 |

MVP 초기에는 원격 Supabase 개발 프로젝트를 사용한다. Docker와 Supabase CLI는 첫 Database 연결이 완료된 뒤 필요할 때 추가한다.

---

## 5. Node.js와 pnpm 설정

### 5.1 Node.js

Node.js 24 LTS를 설치한 뒤 새 PowerShell에서 다음을 확인한다.

```powershell
node --version
npm --version
```

Node.js 18은 사용하지 않는다. 지원이 종료된 런타임은 새 프로젝트의 기준으로 삼지 않는다.

### 5.2 pnpm

Windows에서는 Winget으로 pnpm 11을 설치한다.

```powershell
winget install --exact --id pnpm.pnpm
pnpm --version
```

각 Node 프로젝트의 `package.json`에는 생성 후 실제 사용하는 pnpm 버전을 `packageManager` 필드로 기록한다.

```json
{
  "packageManager": "pnpm@11.x.x"
}
```

`npm install`과 `yarn install`은 프로젝트 디렉터리에서 사용하지 않는다. `pnpm-lock.yaml`만 의존성 lockfile로 관리한다.

pnpm 11은 의존성의 설치 스크립트를 기본적으로 보류할 수 있다. 반드시 필요한 신뢰 의존성만 각 프로젝트의 `pnpm-workspace.yaml`에 명시적으로 허용한다. 현재 Backend는 `unrs-resolver`, `@prisma/engines`, `prisma`만 허용한다. 새 의존성이 설치 스크립트 승인을 요청하면 패키지의 역할을 확인한 뒤 목록에 추가한다.

---

## 6. 초기 디렉터리 구조

프로젝트 루트는 다음 구조를 사용한다.

```text
TrickalFanGame/
├── .agents/              # 저장소 전용 Codex 스킬
├── game/                  # Unity 프로젝트
├── backend/               # NestJS + Prisma
├── web/                   # Next.js App Router
├── docs/
├── AGENTS.md              # 저장소 공통 개발 규칙
├── .gitignore
└── README.md
```

Backend와 Web은 독립 Node 프로젝트로 시작한다. 공통 패키지가 실제로 필요해질 때만 pnpm workspace 도입을 검토한다.

---

## 7. Backend 초기 구성

### 7.1 생성

프로젝트 루트에서 NestJS 프로젝트를 생성한다.

```powershell
pnpm dlx @nestjs/cli new backend --package-manager pnpm
```

생성 후 다음을 실행한다.

```powershell
Set-Location backend
pnpm start:dev
```

초기 서버는 `http://localhost:3000`에서 실행된다. Web과 동시에 실행할 때는 Backend 포트를 `3001`로 변경하고, `PORT=3001`을 `.env`에 명시한다.

### 7.2 필수 초기 설정

- 전역 ValidationPipe를 활성화한다.
- CORS 허용 Origin을 환경 변수로 관리한다.
- `/api/health` health check endpoint를 만든다.
- API의 전역 prefix를 `/api`로 고정한다.
- `Runs`, `Users`, `Statistics`, `Rankings`는 기능 단위 NestJS Module로 추가한다.

---

## 8. Prisma와 Supabase PostgreSQL 구성

### 8.1 Supabase 프로젝트

Supabase Dashboard에서 개발용 프로젝트를 하나 생성한다.

- 프로젝트 이름: `trickal-fan-game-dev`
- Region: 사용자와 가까운 리전
- Database password: 비밀번호 관리자에 보관하고 Git에 기록하지 않는다.
- Production 프로젝트는 개발 프로젝트와 분리한다.

### 8.2 Prisma 설치와 초기화

Backend 디렉터리에서 Prisma를 설치하고 초기화한다.

```powershell
pnpm add @prisma/client
pnpm add -D prisma
pnpm exec prisma init --datasource-provider postgresql
```

`backend/.env`의 `DATABASE_URL`에는 Supabase에서 제공한 서버 전용 PostgreSQL 연결 문자열을 설정한다. 브라우저와 Unity에 이 값을 전달하지 않는다.

### 8.3 마이그레이션 소유권

Database DDL의 단일 기준은 Prisma Migrate이다.

```text
Prisma schema 변경
    ↓
Prisma migration 생성
    ↓
개발 DB 적용
    ↓
검토 후 운영 DB 적용
```

- `prisma/schema.prisma`와 `prisma/migrations/`는 Git으로 관리한다.
- Supabase SQL Editor에서 임의로 테이블 구조를 바꾸지 않는다.
- Supabase CLI의 migration 기능과 Prisma Migrate를 병행하지 않는다.
- 복잡한 통계나 랭킹 Query는 Prisma 모델을 유지하되, 필요하면 Backend Repository에서 파라미터화한 Raw SQL을 사용한다.

---

## 9. Web 초기 구성

프로젝트 루트에서 Next.js App Router 프로젝트를 생성한다.

```powershell
pnpm create next-app web --ts --eslint --app --src-dir --use-pnpm
```

개발 서버를 실행한다.

```powershell
Set-Location web
pnpm dev
```

Web은 기본적으로 `http://localhost:3000`을 사용한다. 따라서 Backend는 `3001`을 사용한다.

초기 페이지 구조는 다음을 기준으로 한다.

```text
web/src/app/
├── page.tsx
├── users/[nickname]/page.tsx
├── runs/[runId]/page.tsx
├── ranking/page.tsx
└── statistics/page.tsx
```

Next.js는 별도의 Database 접근 계층을 만들지 않는다. `src/lib/api/`에 Backend API 호출 함수를 두고, 모든 전적 데이터는 NestJS에서 가져온다.

---

## 10. Unity 초기 구성

Unity Hub에서 다음 옵션으로 프로젝트를 생성한다.

| 항목 | 값 |
|---|---|
| Editor | Unity 6.3 LTS |
| Template | 2D (URP는 시각 효과가 실제로 필요해질 때만 선택) |
| Location | 프로젝트 루트의 `game/` |
| Version Control | Visible Meta Files |
| Asset Serialization | Force Text |

`Visible Meta Files`와 `Force Text`는 Unity Asset과 Scene 변경을 Git에서 추적하고 충돌을 줄이기 위해 필수로 설정한다.

API 주소는 Script에 하드코딩하지 않는다. 개발 환경에서는 별도 Config Asset 또는 설정 파일로 `http://localhost:3001/api`를 관리한다. 배포 환경 주소는 Build 설정에 따라 분리한다.

---

## 11. 환경 변수 규칙

### 11.1 Backend

`backend/.env.example`을 Git에 포함한다.

```dotenv
NODE_ENV=development
PORT=3001
DATABASE_URL=postgresql://<user>:<password>@<host>:5432/postgres
CORS_ORIGIN=http://localhost:3000
```

실제 값이 담긴 `backend/.env`는 Git에 포함하지 않는다.

### 11.2 Web

`web/.env.example`을 Git에 포함한다.

```dotenv
NEXT_PUBLIC_API_URL=http://localhost:3001/api
```

Web에 `DATABASE_URL`, Supabase database password, service role key를 넣지 않는다.

### 11.3 Unity

- 개발 API 주소: `http://localhost:3001/api`
- 운영 API 주소: 배포 후 별도 설정
- Secret, DB 연결 문자열, Supabase service role key: Unity 프로젝트에 저장 금지

---

## 12. 로컬 포트와 실행 순서

| 서비스 | 주소 | 역할 |
|---|---|---|
| Backend | `http://localhost:3001` | REST API |
| Backend API | `http://localhost:3001/api` | Unity/Web API base URL |
| Web | `http://localhost:3000` | 전적 및 통계 UI |
| Supabase | 원격 개발 프로젝트 | PostgreSQL |

개발 시 실행 순서:

```text
1. Supabase 개발 프로젝트가 사용 가능한지 확인
2. Backend 환경 변수 설정
3. Prisma migration 적용
4. Backend 실행 및 /api/health 확인
5. Web 실행 및 Backend API 호출 확인
6. Unity에서 API 주소를 설정
7. Unity → Backend 테스트 Run 저장 확인
```

---

## 13. 초기 검증 절차

### 13.1 Backend

- [ ] `pnpm start:dev`로 Backend가 실행된다.
- [ ] `GET /api/health`가 200 응답을 반환한다.
- [ ] 잘못된 요청이 일관된 Error Response를 반환한다.

### 13.2 Database

- [ ] Prisma가 Supabase PostgreSQL에 연결된다.
- [ ] 초기 migration이 성공한다.
- [ ] `users`, `characters`, `items`, `runs`, `run_items` 테이블을 확인할 수 있다.
- [ ] 샘플 User, Character, Item seed를 만들 수 있다.

### 13.3 Web

- [ ] `pnpm dev`로 Web이 실행된다.
- [ ] Web에서 `NEXT_PUBLIC_API_URL`의 Backend health check를 호출할 수 있다.
- [ ] CORS 설정으로 브라우저 요청이 차단되지 않는다.
- `web/`에서 `pnpm test`를 실행하면 실제 Database를 변경하지 않고 통계·랭킹의 빈 데이터,
  전체·부분 오류, 로딩과 다시 시도 복구, 일부 데이터와 긴 이름을 검증한다.
- Web 변경 후 `pnpm test`, `pnpm lint`, `pnpm build`를 순서대로 실행한다.

### 13.4 Unity 연동

- [ ] Unity Editor에서 프로젝트가 오류 없이 열린다.
- [ ] Unity가 `/api/health`를 호출할 수 있다.
- [ ] 테스트 Run을 `POST /api/runs`로 저장할 수 있다.
- [ ] 저장된 Run을 Web API를 통해 조회할 수 있다.

---

## 14. 보안 및 Git 규칙

- `.env`, `.env.local`, Unity의 Secret 설정 파일은 `.gitignore`에 포함한다.
- `.env.example`에는 키 이름만 넣고 실제 Secret은 넣지 않는다.
- Supabase database password와 service role key는 Backend 운영 환경에만 저장한다.
- Unity와 Web은 public API 주소 외의 Secret을 갖지 않는다.
- `node_modules/`, Next.js `.next/`, Unity `Library/`, `Temp/`, `Logs/`, `Build/`는 Git에서 제외한다.

---

## 15. 자주 발생하는 문제

| 증상 | 확인할 내용 |
|---|---|
| `pnpm`이 Node 18 오류를 표시함 | Node.js 24 LTS 설치 후 새 터미널을 연다. |
| Web에서 CORS 오류 발생 | Backend `CORS_ORIGIN`이 `http://localhost:3000`인지 확인한다. |
| Prisma 연결 실패 | `DATABASE_URL`의 비밀번호, host, SSL 요구 사항을 Supabase 연결 정보와 대조한다. |
| Unity에서 localhost 연결 실패 | Unity Editor와 Backend가 같은 PC에서 실행 중인지, API base URL에 `/api`가 포함됐는지 확인한다. |
| migration 충돌 | Supabase SQL Editor로 직접 DDL을 변경했는지 확인하고 Prisma migration 이력을 기준으로 정리한다. |

---

## 16. 환경 구축 완료 조건

다음 조건을 만족하면 환경 구축을 완료로 정의한다.

- [ ] Node.js 24 LTS와 pnpm 11이 설치되어 있다.
- [ ] Unity 6.3 LTS 프로젝트가 `game/`에서 정상 실행된다.
- [ ] NestJS Backend가 `backend/`에서 정상 실행된다.
- [ ] Prisma migration으로 Supabase PostgreSQL 스키마를 생성할 수 있다.
- [ ] Next.js App Router Web이 `web/`에서 정상 실행된다.
- [ ] Web → Backend, Unity → Backend 요청이 모두 성공한다.
- [ ] 테스트 Run을 저장하고 Web에서 조회할 수 있다.
- [ ] Secret이 Git에 포함되지 않는다.

---

## 17. 개발 보조 도구

### 13주차 Frontend Flow

- `Trickal Fan Game > Week 13 > Setup Frontend Flow`: Edit Mode에서 실행한다. 수정 중인 Scene의 저장 여부를 확인한 뒤 `Assets/Scenes/FrontendScene.unity`를 생성하거나 갱신하고 단독으로 연다. 기존 이름의 UI 오브젝트와 Scene GUID를 재사용한다. Frontend를 첫 번째 활성 Build Scene으로 등록하며 기존 `SampleScene`은 전투용 Game Scene으로 보존한다. 다른 Build Scene 항목도 보존한다.
- `Trickal Fan Game > Week 13 > Setup Flow-4 Run Launch`: Game Scene의 기존 `RunSession` 옆에 조기 Bootstrap을 중복 없이 구성하고, 이전 전투 Scene 캐릭터 선택 UI와 진행 조회 참조를 연결한 뒤 Frontend 구성도 갱신한다. 완료 후 Frontend Scene을 연다.
- `Trickal Fan Game > Week 13 > Verify Flow-4 Run Launch`: Frontend의 단일 Scene 전환기와 Game Scene의 단일 Bootstrap, `RunSession`·진행 조회·이전 선택 UI·방 그래프·인벤토리 참조 및 Build Settings를 검사한다.
- `Trickal Fan Game > Week 13 > Verify Frontend Flow`: Frontend를 연 상태에서 실행한다. 시작 Scene 순서, 전투·Run 컴포넌트 부재, Canvas·입력 시스템 중복, 화면·버튼 참조, 캐릭터 정의의 유효성·고유 ID, 1920×1080 Scaler와 좌우 64·상하 54 안전 여백을 검사한다. Edit Mode에서는 720p·1080p·1440p와 비16:9 크기의 실제 RectTransform/TMP 경계도 별도 미리보기 Scene에서 검사한다. 실패 시 예외가 발생하며 성공 로그는 `Week 13 Frontend verification passed`다.
- `Trickal Fan Game > Week 13 > Verify Skill-1 and Skill-2`: Frontend Scene의 320×520 세로형 캐릭터 카드, 진행 조회의 로딩·빈 상태·실패, 레벨·경험치·포인트·두 스킬 표시, 목표 레벨 강화 요청의 중복 차단과 성공·오류 갱신을 검사한다.
- `Trickal Fan Game > Week 13 > Export Home Previews (720p and 1080p)`: `game/Logs/Week13FrontendPreviews/home-1280x720.png`, `home-1920x1080.png`를 만든다. 별도 미리보기 Scene에서 홈 UI를 렌더링하므로 현재 Scene은 바뀌지 않는다. 이 이미지는 배치 확인용이며 실제 Overlay Canvas의 Game View 확인을 대체하지 않는다.
- `Trickal Fan Game > Development > Clear Local Profile (Editor)`: Editor Play Mode에서 사용하는
  `userId`와 닉네임을 삭제한다. 확인 창에서 승인한 경우에만 실행하며 `clientProfileId`와 음량 등
  다른 PlayerPrefs는 유지한다. Play Mode 중 실행하면 Frontend Scene을 다시 연다.
- Development Build의 Frontend 좌상단에는 `DEV: RESET LOCAL PROFILE` 버튼이 나타난다. `RESET`을
  다시 눌러 확인하면 해당 EXE의 `userId`와 닉네임을 삭제하고 Frontend Scene을 다시 연다.
  `clientProfileId`를 유지하므로 같은 닉네임을 제출하면 Backend의 멱등 등록 응답을 검증할 수 있다.
  이 상태에서 다른 닉네임을 제출하면 같은 프로필 ID의 변경 요청이므로
  `PROFILE_IDEMPOTENCY_CONFLICT`가 발생한다. 대소문자 구분을 별도 사용자처럼 시험하려면 서로 다른
  `clientProfileId`가 필요하다.
  이 버튼은 정식 빌드에는 나타나지 않는다.
- 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13FrontendVerification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번 실행, GUID·Game Scene 보존, 잘못된 참조·중복 버튼·전투 오브젝트·시작 순서의 거부까지 검사한다. 렌더링 미리보기가 포함되어 `-nographics`를 사용하지 않는다. 검증 실패는 비정상 종료 코드로 반환된다.
- Skill-1~2 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13SkillUpgradeVerification.SetupAndVerifyBatch -logFile <로그 경로>`. Frontend Setup을 두 번 실행해 Scene GUID를 보존한 뒤 조회·강화 상태와 전체 Frontend 레이아웃 회귀를 검사한다.
- Play Mode 자동 검증: 구성 후 별도 Unity 프로세스에서 `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13FrontendPlayVerification.RunBatch -logFile <로그 경로>`를 사용한다. `-quit`는 넣지 않는다. 타이틀 선행, 프로필 재시도·저장, 홈 진입, 데이터 기반 에르핀 카드, 선택 전 확인 차단, 단일 확정 이벤트, 뒤로가기·재진입 상태 초기화와 Flow-4 전 Run 미생성을 검사한 뒤 성공 0/실패 1로 종료한다.
- Flow-4 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Flow-4 Setup 두 번, 두 Scene GUID와 Bootstrap 단일성, Game 참조 및 전체 Frontend 레이아웃 회귀를 검사한다.
- Flow-5 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow5Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Flow-5 Setup 두 번, 두 Scene GUID, 전체 화면 결과 UI·전환 단일성·Frontend Scene 격리와 Flow-4 회귀를 검사한다.
- Flow-5 Play Mode 통합: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow5PlayVerification.RunBatch -logFile <로그 경로>`. 실제 사망 종료 후 서버 연결 실패 시 결과를 즉시 표시하고 요청을 로컬 큐에 보관하는지, 동일 `clientRunId` 자동 복구와 성장 캐시 반영, 확정 성장 순차 연출·건너뛰기와 캐릭터 선택/홈 복귀 상태 정리를 검사하며 완료 시 검증기가 Editor를 종료한다.
- Flow-4 Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow4PlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. Frontend 확정부터 Game Scene까지 이동하여 로컬 사용자·캐릭터 전달, 단일 Run과 UUID `clientRunId`, 새 seed·첫 방·빈 인벤토리, 이전 선택 UI 비활성화와 종료 전 `POST /runs` 미호출을 검사한다.
- HUD-1 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-1 HP and SP`. Game Scene에 HP 바, 반복 SP 슬롯과 저학년 스킬 사용 가능 표시를 구성하고 실제 플레이어 상태에 연결한다.
- HUD-1 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번, Game Scene GUID·계층 단일성, HP 피해·회복, SP 획득·소비·최대치 증가, 저학년 스킬 사용 가능 조건과 Flow-4 회귀를 검사한다.
- HUD-2 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-2 Skill States`. Game Scene 우하단에 저학년·고학년 임시 아이콘, 실제 입력 키, 사용 가능 상태와 고학년 쿨타임 표시를 구성한다.
- HUD-2 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. HUD-1 구성을 보존하며 Setup 두 번, Game Scene GUID·계층 단일성, 두 스킬의 실제 입력 가능 조건, 고학년 사용 중·쿨타임 중간값·종료 경계, `Time.timeScale = 0` 정지와 Flow-4 회귀를 검사한다.
- HUD-3A 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3A Artifact List`. Game Scene 좌하단에 실제 인벤토리와 연결된 48×48 임시 아티팩트 아이콘, 스택 배지와 10종 초과 `+N` 표시를 구성한다.
- HUD-3A 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3AVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 빈 상태, 중복 획득 스택, 11종 획득 순서·10개 제한·`+1`, 재구성 상태 유지, Setup 두 번과 HUD-2·Flow-4 회귀를 검사한다.
- HUD-3B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3B Artifact Acquisition Toast`. Game Scene 중앙의 단일 TMP 메시로 28px 이름과 그 아래 보조색 20px 효과 설명을 구성하고 동적 설명에 필요한 TMP 글리프를 Build용 폰트 아틀라스에 추가한다.
- HUD-3B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 이름·설명의 실제 2줄 TMP 메시와 선호 높이, 폰트 글리프, 1.5초 unscaled 시간 경계, 연속 획득 큐, 최대 스택 거부, 입력·시간 불변, Setup 두 번과 HUD-3A·HUD-2·Flow-4 회귀를 검사한다.
- HUD-3B Build에서 이름만 보이고 효과 설명이 사라지는 문제의 조사·해결 과정은 [트러블슈팅 §2](./troubleshooting/17-troubleshooting.md#2-unity-build에서-tmp-두-번째-줄이-표시되지-않는-문제)에 기록한다.
- HUD-3C 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3C Pause Artifact List`. Game Scene에 `Esc` 일시정지·복귀와 10종 초과분을 포함한 전체 아티팩트 이름·스택·설명 스크롤 목록을 구성한다.
- HUD-3C 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3CVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 빈 목록, 12종 전체 획득 순서, 초과 항목 스택·설명, timeScale·전투 입력 루프 정지와 상태 보존, 중첩 거부, Setup 두 번과 HUD-3B·HUD-3A·HUD-2·Flow-4 회귀를 검사한다.
- HUD-4A 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-4A Minimap Foundation`. Game Scene 우상단 220×180 영역에 실제 생성 그래프의 격자 방향대로 방과 연결선을 구성하고 현재 방을 `P`와 외곽선으로 강조한다.
- HUD-4A 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud4AVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 고정 seed 생성 그래프에서 시작방과 인접방 이동 후의 현재 방, 상·하·좌·우 상대 위치와 연결선 수를 검사하고 Setup 두 번, Scene GUID·계층 단일성과 HUD-3C·HUD-7A·Flow-4 회귀를 확인한다.
- HUD-4B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-4B Explored Minimap`. 방문한 방과 그 인접 미방문방을 층 그래프에 누적하고, `?` 미확인·`P` 현재·`V` 클리어·`S/T/B` 특수 문 표식을 구성한다. 인접한 보물방·보스방은 문 종류와 함께 `T`·`B`를 공개하지만 실제 진입 전에는 `V`를 표시하지 않는다.
- HUD-4B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud4BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 보물방까지 실제 그래프 경로를 이동하며 누적 공개, 이전 방 유지, 특수 문 공개, 보물방 선클리어와 실제 방문 분리, 진입 전 `V` 은닉, 패널 자동 맞춤과 HUD-4A·HUD-3C·HUD-7A·Flow-4 회귀를 검사한다.
- HUD-4C 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-4C Floor Name`. 미니맵 머리글은 `요정의 숲 · N층`을 상시 표시하고, 첫 층과 다른 층 진입 때만 같은 문구를 화면 중앙보다 180px 위에 1.5초 표시한다. 같은 층의 방 이동에는 알림을 다시 표시하지 않으며 일반 방 이름·번호도 노출하지 않는다.
- HUD-4C 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud4CVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 숫자 층 이름과 미래의 층별 고유 이름 대체, 초기화 순서가 달라도 첫 층 알림 보장, 같은 층 방 이동 시 무알림, 층 전환 시 갱신, 1.5초 unscaled 시간 경계, 단일 TMP 메시 생성·높이, 입력·시간 불변, Setup 두 번과 HUD-4B·Flow-4 회귀를 검사한다.
- HUD-5 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-5 Boss Status`. Game Scene 하단 중앙 720×64 영역에 현재 방의 활성 보스 이름, 현재·최대 HP 게이지와 페이즈를 표시한다. 현재 임시 보스는 실제 구현 상태에 맞춰 `보스`, `페이즈 1 / 1`을 사용한다.
- HUD-5 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud5Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 활성 보스방에서만 표시, 피해·페이즈 즉시 갱신, 처치·일반 방 복귀 시 숨김, 입력 비차단과 하단 안전 영역, Setup 두 번, Scene GUID와 HUD-4C·Flow-4 회귀를 검사한다.
- HUD-7B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-7B Pause Menu`. HUD-3C 일시정지 화면을 실제 `PlayerStats` 상세 수치, 전체 아티팩트 목록과 `게임으로 돌아가기` 버튼을 갖춘 메뉴로 확장하고 Game Scene EventSystem을 구성한다.
- HUD-7B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud7BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 실시간 상세 스탯, 일시정지 중에만 활성화되는 UI 입력, 첫 포커스·팝업 내부 제한·이전 선택 복원, 다른 시간정지 화면 중첩 거부, Setup 두 번과 HUD-3C·Flow-4 회귀를 검사한다.
- HUD-7C 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud7CVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 홈 나가기 확인 UI와 안전 행동 첫 포커스, Setup 두 번, Game Scene GUID와 HUD-7B·Flow-5 회귀를 검사한다.
- HUD-7C Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud7CPlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. 확인 취소 시 같은 Run·일시정지·API 요청 0회를 유지하고, 확정 시 결과 저장·보상 없이 Run 상태를 정리해 Frontend 홈의 시간과 첫 포커스를 복원하는지 검사한 뒤 Editor를 종료한다.
- HUD-7D 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud7DVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 같은 캐릭터 재시작 버튼과 공용 확인 UI, Setup 두 번, Game Scene GUID와 HUD-7C 회귀를 검사한다.
- HUD-7D Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud7DPlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. 취소 시 기존 Run ID·seed·진행을 유지하고, 확정 시 결과 저장 없이 같은 캐릭터와 새 Run ID·seed, 1층 1번 방·처치 0·빈 인벤토리로 시작하는지 검사한 뒤 Editor를 종료한다.
- Setting-1A 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup Setting-1A Audio`. `Assets/Audio`의 홈·전투 BGM과 UI 클릭음을 임포트 규칙에 맞추고 Frontend·Game Scene의 영속 오디오 컨트롤러에 연결한다. 같은 경로의 음원 교체 후에도 이 메뉴를 다시 실행한다.
- Setting-1A 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Setting1AVerification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번, 음원 임포트 설정, Scene별 단일 컨트롤러·두 Source, Scene GUID, BGM 라우팅과 저장 음량 계산을 검사한다.
- Setting-1A Play Mode: Unity `-batchmode -nographics -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Setting1APlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. 실제 버튼 클릭음, 즉시 음량 적용, `Frontend → Game → Frontend` BGM 전환과 저장값 재로드를 검사한 뒤 Editor를 종료한다.
- Setting-1B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup Setting-1B Display`. Frontend·Game 카메라와 두 UI 기준 프레임에 중앙 16:9 비율 제어를 중복 없이 구성하고 기본 빌드 화면을 1920×1080 테두리 없는 전체 화면, 크기 조절 불가 창으로 설정한다.
- Setting-1B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Setting1BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 지원 해상도·저장 계약, 16:9·4:3·울트라와이드 뷰포트 계산, 두 Scene 구성, Setup 두 번과 Scene GUID를 검사한다.
- Setting-1B Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Setting1BPlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. 설정 UI에서 1280×720 창 모드를 적용·재로드하고 Game Scene 전환 뒤 카메라와 HUD 기준 프레임을 검사한 뒤 Editor를 종료한다.
- Room-0 Basic 방 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room0RegressionVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 기존 Basic Prefab GUID, `16 × 9` 방과 `20 × 13` 격자, 카메라·문·안전 진입점·SpawnPoint, 고정 seed의 3층 결과, 양방향 이동·전투 클리어·재방문 상태를 함께 검사한다.
- Room-1 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Room-1 Profile and Template Contract`. 기존 Prefab이나 Scene을 수정하지 않고 `basic` Room Profile과 `basic-standard` Room Template 에셋을 생성·갱신한다.
- Room-1 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 재실행 GUID, Profile·Template 필드와 Prefab 좌표 일치, 누락·중복 ID 거부, Run 상태 분리와 전체 Room-0 회귀를 검사한다.
- Room-2 축별 카메라 경계: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Small·Basic 고정, Wide 가로, Tall 세로, Large 양축 추적과 극단 좌표 제한, 잘못된 입력 거부, 방 로컬 좌표 런타임 적용 및 Room-0~1 회귀를 검사한다.
- Room-3 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Room-3 Small Basic Wide Templates`. 기존 Basic Prefab을 보존하면서 Small `12 × 6.75`와 Wide `24 × 9` Profile·Prefab·Template을 생성·갱신한다.
- Room-3 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 재실행 GUID, 세 Template의 벽·문·안전 진입점·Encounter·SpawnPoint·카메라 계약, 필수 통로 침범 거부와 Room-0~2 회귀를 검사한다.
- Room-4 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Room-4 Seeded Template Selection`. Game Scene의 `FloorGenerator`에 콘텐츠 버전 1과 Small·Basic·Wide Template 카탈로그를 중복 없이 연결한다.
- Room-4 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 같은 seed·버전 결정성, 카탈로그 순서 독립성, RoomType·방향·AABB 필터, 128 seed의 세 Template 선택과 겹침 방지, 콘텐츠 버전 변화·누락 호환 실패 및 Room-0~3 회귀를 검사한다.
- Room-5 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Room-5 Runtime Room Transitions`. Room-4 카탈로그를 보존하고 런타임 조립기가 선택된 Small·Basic·Wide Prefab과 Profile을 사용하도록 준비하며 플레이어 물리 보간을 켜 Wide 카메라의 렌더 프레임 추적을 안정화한다.
- Room-5 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room5Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 세 크기의 실제 Prefab 인스턴스, 플레이어 물리 보간, 서로 다른 Profile 사이 양방향 이동과 반대편 안전 진입점, 현재 방 단독 활성화, 카메라 유효 경계, RunProgress·미니맵 실루엣 갱신, Setup 재실행과 Room-0~4 회귀를 검사한다.
- Encounter-1 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Encounter-1 Contract`. Small·Basic·Wide와 1~3층을 허용하는 추적형·원거리형·돌진형 단일 역할 Encounter 에셋을 생성·갱신하고 Game Scene의 `FloorGenerator`에 콘텐츠 버전 1 카탈로그를 연결한다.
- Encounter-1 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Encounter1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 안정 ID, 적 역할·수, Profile·층, SpawnPoint/Group, 안전거리, 웨이브·클리어 조건, 128 seed 결정성·카탈로그 순서 독립성, 잘못된 참조·거리·중복 ID 실패, Setup 재실행 GUID와 Room-0~5 회귀를 검사한다.
- Encounter-2 구성: Unity 메뉴 `Trickal Fan Game > Week 14 > Setup Encounter-2 Mixed Encounters`. 역할 로스터를 기존 추적형·원거리형·돌진형 Prefab에 연결하고 `pressure-chaser-ranged`, `lane-charging-ranged`, `crossfire-ranged` 세 Encounter를 콘텐츠 버전 2 카탈로그로 구성한다.
- Encounter-2 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Encounter2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 256 seed 선택, 활성 출입구 안전거리, 고유 SpawnPoint, 역할 우선 배치, 실제 Prefab 인스턴스, 로스터 실패 경로, Setup 재실행 GUID와 Room-0~5 회귀를 검사한다.
- Encounter-3 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Encounter3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 2~3층 2웨이브 선택, 필수 적 전멸 경계, 웨이브 단일 시작·종료, 완료 웨이브 복원, 클리어 드롭 1회 추첨(검증용 100% 표), 재진입·방 재구성 중복 방지, Setup 재실행 GUID와 Room-0~5 회귀를 검사한다.
- Room-6 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room6Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Tall 세로·Large 양축 카메라, 1~3층별 Wide/Tall/Large 보스 후보, Template 층 범위, 512 seed 결정성·AABB 비겹침·후보 선택, Tall/Large Encounter 해석, Setup 재실행 GUID와 Encounter-3·Room-0~5 회귀를 검사한다.
- Room-7 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room7Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Large 중앙 고정 기둥의 비파괴·무드롭 계약, 네 방향 우회 통로, 플레이어 이동 sweep, 양쪽 투사체 사선, 추적 Rigidbody와 돌진 정지, 1,024 seed의 `pillar-crossfire` 호환 선택, Setup 재실행 GUID와 Room-0~6 회귀를 검사한다.
- Room-8 3층 Run 종합 자동 검증: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room8Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 256개 3층 seed의 Template·Encounter·웨이브 결정성과 전체 후보 노출, 이종 크기 좌우문 양방향 전환의 단일 활성 방·카메라 첫 프레임 제한, 부분 웨이브 복원과 층 재로딩 뒤 클리어·SP 보상 단일성을 Room-0~7 회귀와 함께 검사한다.
- Room-8 렌더 잔상 수동 확인: `FrontendScene`에서 Play 후 Wide/Large 방의 좌·우 카메라 추적 끝에서 Basic/Small/Tall 방으로 이동하고 다시 돌아온다. 전환 직후와 카메라 정지 뒤 모두 넘어온 쪽 화면 끝에 이전 방 사각형, 방 사이 빈 공간과 비활성 방이 보이지 않아야 한다. Game 카메라는 `SolidColor`와 배경 알파 `1`이어야 한다. Room-8 완료 시에는 이 화면 확인을 자동 Play Mode 검증과 함께 남긴다.
- Encounter-2 수동 확인: `FrontendScene`에서 Play 후 일반 전투방을 진행한다. 추적형+원거리형에서는 추적형이 접근하는 동안 원거리형에게 파고들 수 있어야 하고, 돌진형+원거리형에서는 돌진 예고 후 옆으로 피할 공간이 남아야 한다. 원거리형 3마리 방에서는 입장 위치에 투사체가 즉시 겹치거나 모든 회피 방향이 동시에 막히지 않아야 한다. 세 조합 모두 적이 출입구 위에 생성되지 않고 전멸 시 문이 열려야 한다.
- Room-8 물리 보간 Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week14Room8PlayVerification.RunBatch -logFile <로그 경로>`. `-quit`와 `-nographics`는 넣지 않는다. 별도 빈 Scene에서 실제 Room Prefab·문 트리거·보간 Rigidbody로 Wide/Large ↔ Basic/Small/Tall의 좌우 왕복 24개 전환을 실행하고, 첫 5프레임의 표시/물리 좌표 일치·안전 진입점·카메라 경계·단일 방 활성·보간 설정 보존을 검사한다. 게임 Run 생성이나 API 요청 없이 성공 0/실패 1로 자동 종료한다.
- Enemy-0 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Enemy0Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 세 일반 적 Prefab에 공유 행동 컨텍스트를 중복 없이 구성하고 GUID를 보존하며, 방 전투 시작·피격 경계, 감지 거리 밖 지속 추적, 플레이어·적 사망, 넉백, 방 비활성화 정리와 기존 추적형·원거리형·돌진형 행동 회귀를 검사한다.
- Enemy-3 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Enemy3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번의 Prefab GUID·컴포넌트 단일성, 추격 → 방향 고정 예고선 → 돌진 → 회복 → 즉시 재추격, Large 방 대비 돌진 거리, 벽 충돌과 넉백·사망·방 비활성화 정리 및 Enemy-0~1·기존 적 밸런스 회귀를 검사한다.
- Enemy-4 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Enemy4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 기존 원거리 Prefab 보존과 신규 저격형 Prefab의 Setup 재실행 GUID·컴포넌트 단일성, 재배치 → 방향 고정 조준선 → 발사 → 회복, 근거리 사격, 선택적 좌우 이동, 시간·막힘 방향 반전, 플레이어보다 느린 후퇴, 넉백·사망·방 비활성화 정리 및 Enemy-0~1·기존 적 밸런스 회귀를 검사한다.
- 일반 적 임시 식별 색상: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15EnemyRoleColorVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 불효자손 갈색·산사모 초록·저혈당 파랑·고혈당 분홍을 멱등 적용하고, 네 Prefab GUID, 색상 쌍 구분 거리, 저혈당 대기색과 돌진 예고·위험색 분리 및 Enemy-2~4 행동 회귀를 검사한다.
- Enemy-5 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Enemy5Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 사용자 제공 투명 PNG 4종의 Sprite 임포트와 Prefab·역할 로스터 연결, Setup 두 번의 에셋·Prefab·Encounter GUID, Small의 저격형 제외, Large의 저격·돌진 웨이브 분리, 1,024 seed 노출과 Enemy-2~4 행동 회귀를 검사한다.
- Boss-0 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Boss0Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 큰 임시 보스 Prefab의 GUID와 Setup 재실행 안정성, 콘텐츠 seed 기반 패턴 순서, 예고 → 실행 → 후딜 → 재사용 대기 시간 경계, 즉시 반복 방지, HP 3단계 HUD, 세 보스 Room Profile의 충돌 여유, 사망·방 비활성화 시 소환물·투사체 정리와 HUD-5 회귀를 검사한다.
- Boss-2 구성: Unity 메뉴 `Trickal Fan Game > Week 15 > Setup Boss-2 Saemaeum Vault`. 부스러기 Prefab을 보존하면서 사용자 에셋 `Boss_SaemaeumGeumgo.png`, 새마음금고 전용 Prefab, 접근 투척·연속 점프·보물 회복과 2페이즈 수치를 멱등 구성하고 Run 조립기의 2층 보스 슬롯에 연결한다.
- Boss-2 즉시 전투: `Trickal Fan Game > Debug > Open Boss-2 Test Room`을 선택하고 Play한다. 처음 실행하면 기존 ItemTestScene을 복사해 별도 `Assets/Scenes/Boss2TestScene.unity`를 만들고 새마음금고 한 마리만 시작 시 생성한다. 오른쪽 패널에서 플레이어와 살아 있는 보스의 현재/최대 HP를 함께 확인하며, `Heal / Reset HP`와 `Respawn Enemies`로 반복 테스트한다. 이후에는 해당 Scene을 직접 열어 Play해도 된다. `Debug > Verify Boss-2 Test Room`에서 참조·단일 보스 생성·HP 표시 대상·Scene GUID 유지 검사를 실행할 수 있다.
- Boss-2 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Boss2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 Prefab·사용자 Sprite GUID, 전용 런타임과 Tall Room 충돌 여유, 탄성 실루엣, 5회×5발 부채꼴 투척, 같은 seed의 1페이즈 3~5회·2페이즈 4~6회와 거리 4.8 점프, 착지 예고 정리·피해·넉백, 회복량 15와 1페이즈 무제한 재선택, 2페이즈 회복 제외·가속, 생성된 2층 보스방의 새마음금고 Prefab·Sprite 연결과 Boss-0~1·HUD-5 회귀를 검사한다.
- Boss-3 자동 구성·회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Boss3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 보스·4종 졸개 Prefab과 사용자 Sprite GUID, Large Room 충돌 여유, 가중 패턴 선택, 압박 공격 3회와 9/7.5초 절대 간격을 요구하는 내려찍기, 이동을 유지하는 2~3회 검격, 25/50/25%·25/30/45%의 1~3회 추적 대시와 연계 종료 뒤 0.3초 완전 정지, 원거리 2+근거리 2 소환과 최대 HP 15% 피해 재활성화, 황금 3방향 중첩 피해 치명 등급 3회(1층 기준 9칸), 3층 보스방 연결 및 Boss-0~2·HUD-5 회귀를 검사한다.
- Boss-4 구성·3층 Run 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week15Boss4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회와 Scene GUID 보존, 세 층 보스·Room Profile·Encounter 연결, 여러 seed의 3층 그래프, 1·2층 보상 단일 생성, 포털 층 이동, 최종 클리어 이벤트 1회와 재진입·재호출 중복 방지를 검사한다.
- Reward-1 선택 세션 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Reward1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Reward-0 후보 회귀와 함께 열린 후보의 재구성·원본 풀 순서 독립 복원, 선택 중 전투·방·층 이동 차단, 취소 시 차단 해제와 동일 후보 재개, Item 또는 회복의 단일 적용, 반복 입력·완료 보상 재방문 중복 방지와 Run 초기화를 검사한다.
- Reward-2 가디자인 구성: Unity 메뉴 `Trickal Fan Game > Week 16 > Setup Reward-2 Placeholder Cards`. `SampleScene`의 Game HUD에 `1920 × 1080` 기준 단색 Overlay와 교체 가능한 세로 카드 3장, 별도 `선택`·`취소` 버튼을 멱등 구성하고 종류·이름·등급·효과·스택 또는 회복 HP 필드와 마우스·키보드 입력을 연결한다.
- Reward-2 카드 UI 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Reward2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회와 Scene GUID, 활성 Item 전체·회복 카드 정보, 긴 문구 높이, 명시적 Navigation·포커스 범위·키보드 Submit·마우스 Pointer, 선택·취소와 동일 후보 재개, 색상 외 상태 표식, 확정 직후 Item HUD와 스탯 갱신을 검사한다.
- HP-1 반 칸 단위 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp1HealthUnitsVerification.VerifyRegressionBatch -logFile <로그 경로>`. 플레이어 시작 10단위(5칸), 적 피해 등급·층 단위표와 최소 1칸, 회복·방어막 단위 내림, 최대 HP 비례 최소 1단위, 풍선 갑옷 +1칸·방어막 3칸, 적 HP 연속값 유지를 검사하고 영향받는 적·보스·아이템·HUD-1·Reward 검증기 25개를 함께 실행해 실패 수를 비정상 종료로 반환한다. 단일 규칙 검사는 메뉴 `Trickal Fan Game > HP > Verify HP-1 Health Units`를 사용한다.
- HP-2 하트 HUD 구성·검증: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp2HeartHudVerification.SetupAndVerifyBatch -logFile <로그 경로>`. HUD-1 → HUD-2 Setup을 두 번 실행해 `Survival HUD`의 HP 게이지를 `HP Hearts` 줄로 교체하고, 반 칸 채움·최대 HP 증가·방어막 파란 하트·아이콘 축소와 HUD-1·2 검증을 실행한다. 하트 Sprite는 `Assets/Art/UI/HudHeart.png`에 없을 때만 생성하므로 최종 아트로 같은 경로를 교체하면 된다. 메뉴는 `Trickal Fan Game > HP > Setup HP-2 Heart HUD` / `Verify HP-2 Heart HUD`.
- HP-2 넓은 회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp2HeartHudVerification.VerifyBatch -logFile <로그 경로>`. HUD-1, HUD-2~5, HUD-7D, Setting-1C, Reward-2·3 SetupAndVerifyBatch와 HP-1·HP-4 검증을 함께 실행한다. 다른 Setup도 다시 실행하므로 Room Prefab·FrontendScene·ProjectSettings의 무관한 재직렬화 변경이 생길 수 있어, 실행 뒤 `git status`로 확인하고 되돌린다.
- HP-3 아이템 효과 구성·검증: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp3ItemEffectsVerification.SetupAndVerifyBatch -logFile <로그 경로>`. Phase G-1 아티팩트 계약 Setup과 아티팩트 설명 글리프 추가를 두 번 실행한 뒤, 코미의 베개(26: 2처치마다 반 칸, 스택당 -1)·광기의 가면(27: 공격력 20%)·풍선 갑옷(+1칸) 에셋과 설명, 기존 10·14번 계산식 유지, Phase G-1 계약, HP-4, HP-1 회귀 25개를 검사한다. 메뉴는 `Trickal Fan Game > HP > Setup HP-3 Item Effects` / `Verify HP-3 Item Effects`.
- HP-5 적 쪽 10배 검증: 메뉴 `Trickal Fan Game > HP > Verify HP-5 Enemy Scale` 또는 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp5EnemyScaleVerification.WideRegressionBatch -logFile <로그 경로>`. 적·보스·장애물 Prefab 15종 HP(×10), 새마음금고 회복 150, 코드 기본값과 플레이어 Scene 4개의 공격력 10·HP 10단위를 검사한 뒤, 적 HP·플레이어 피해·적 회복을 다루는 정적 `Verify()` 49개를 차례로 실행해 PASS/FAIL/SKIP을 기록하고 실패가 있으면 비정상 종료한다. 2026-09-28 기준 Scene Setup 선행이 필요한 `Week8FloorDifficulty`·`Week14Encounter1~3`·`Week14Room7~8`과 기존 실패 `Week6Item`·`Week15Enemy0`·`Week15Enemy2`·`Week15Enemy5`·`Week15Boss4`(실행 순서에 따른 Run seed 고정) 11개는 HP-5 이전 기준선에서도 실패한다.
- HP-4 생명의 보석 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Hp4LifeGemVerification.VerifyBatch -logFile <로그 경로>`. HP 30% 이하 진입 시 1회 발동, 최대 HP 45% 내림 단위를 3초에 걸쳐 반 칸씩 지급, 임계치 아래 획득 시 즉시 시작, 치명타 피격 미발동, 사망 시 중단, 2번째 스택 거부, 풍선 갑옷과의 최대 HP 연동을 검사하고 Content-0 계약과 HP-1 회귀를 함께 실행한다. 단일 검사는 메뉴 `Trickal Fan Game > HP > Verify HP-4 Life Gem`.
- Reward-3 방·보스 연결 구성: Unity 메뉴 `Trickal Fan Game > Week 16 > Setup Reward-3 Room Integration`. 모든 Room Prefab의 보물방 중앙에 상호작용 마커와 `[E] 보상 선택` 안내를 구성하고, 활성 아티팩트·스펠 통합 풀을 보물방 선택 보상과 1·2층 보스 단일 Pickup에 연결한다.
- Reward-3 통합 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Reward3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Reward-0~2 회귀, Setup 2회와 Scene GUID, 보물방 진입 시 자동 미표시·중앙 상호작용·취소 후 동일 후보 재개·선택 후 단일 지급·재구성 복원, 1·2층 보스의 통합 풀 Pickup 하나와 즉시 포털 해제·중복 방지, 최종 보스 무보상을 3층 연속 Run으로 검사한다.
- Spell-1 스펠 효과 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Spell1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 세 스펠의 공통 획득·스택 상한, 시작방·보물방·클리어한 방 비소비, 다음 미클리어 전투방 기본 공격 10% 단일 소비, 미클리어 보스방 공격속도·이동속도 조건과 클리어·재방문 해제, 새 Run 초기화 및 Phase G 단순·조건부 효과·HP-4 회귀를 검사한다.
- Artifact-1 아티팩트 효과 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Artifact1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 생명의 보석 층당 1회 반 칸 분할 회복, 30KG 케틀벨 2스택의 스킬 피해 증가·이동속도 감소, 날씨는 맑음 카드의 실제 기본 근접/투사체 9회·10회 경계와 비치명 공격력 150% 번개, 스킬·오라·번개 비집계, 번개 처치·새 Run 초기화 및 Phase G·Spell-1 회귀를 검사한다.
- Test-1 명확한 피드백 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Test1FeedbackVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 망원경 2~6m 거리 보정, 돌진몹 경량 접촉 피해와 강한 돌진 피해 분리, 생명의 보석 층당 1회, 고학년 Q 취소·80% 쿨타임, 광기의 가면 2.5m 범위와 Scene Gizmo 표시를 검사한다.
- Test-0 전체 자동 회귀: Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week16Test0Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Reward-0~3, Spell-1, Artifact-1, 방 생성 결정성·보상 취소와 재방문 상태·중복 입력 방지·스택 상한·획득 순서 Run 기록 및 HP-1~5 핵심 계약을 한 번에 검사한다. Backend의 전체 Jest와 `pnpm build`, Web의 `pnpm lint`와 `pnpm build`, HP-5 넓은 회귀 기준선 비교는 별도로 함께 수행한다.
- Resource-0 체력 회복 픽업: 메뉴 `Trickal Fan Game > Week 17 > Setup Resource-0 Health Pickup`이 `Assets/Prefabs/HealthPickup.prefab`을 GUID를 보존하며 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week17Resource0Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID·컴포넌트 단일성, `Pickup` 레이어가 Player·Environment·Pickup과만 충돌하는지, 1칸 회복·최대 HP/반 칸 부족 시 미획득·단일 지급·사망/플레이어 외 충돌 무시를 검사한다. 수동 확인은 `Trickal Fan Game > Debug > Open or Create Item Test Room`으로 테스트방을 열어 프리팹을 연결한 뒤 Play하고, 패널의 `Spawn Heart`로 하트를 만들어 최대 HP에서 밀리기만 하는지, `HP -1.5 Hearts` 후 1개만 획득되고 남은 반 칸 상태에서 다음 하트가 밀리는지, 적과 투사체가 하트를 통과하는지 확인한다.
- Resource-1 골드·열쇠·폭탄 픽업(Gold-0 이전 이름 엘리프, 프리팹 경로 유지): 메뉴 `Trickal Fan Game > Week 17 > Setup Resource-1 Run Resource Pickups`가 `Assets/Prefabs/ElifPickup.prefab`, `KeyPickup.prefab`, `BombPickup.prefab`을 GUID를 보존하며 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week17Resource1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID·컴포넌트 단일성, 픽업 1개 획득·99 상한과 초과분 버림·99일 때 미획득·단일 지급·플레이어 외 충돌 무시·Run 종료 후 미획득·`ResetProgress`와 새 Run의 0개 시작을 검사한다. 수동 확인은 `Trickal Fan Game > Debug > Open or Create Item Test Room`으로 프리팹을 연결한 뒤 Play하고, 패널의 `Spawn Gold/Key/Bomb`으로 1개씩 늘어나는지, `+98 All` 후 한 번 더 주워 99에서 멈추고 그다음 픽업은 밀리기만 하는지, 적과 투사체가 픽업을 통과하는지 확인한다.
- Resource-3 방 클리어 드롭: 메뉴 `Trickal Fan Game > Week 17 > Setup Resource-3 Room Clear Drops`가 `Assets/Items/Drops/room-clear-drop-table.asset`(33%, 하트 30·SP 30·골드 20·열쇠 12·폭탄 8, 드롭 ID는 `elif` 유지)을 멱등 구성하고 Game Scene의 `RoomGraphAssembler`에 연결하며, 삭제한 `PlayerSPDropper`가 남긴 Missing Script를 Game·Item Test·Boss Test·Boss-2 Test Scene의 플레이어에서 제거한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week17Resource3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 표 계약, 30000 seed의 드롭률·후보 비율과 seed 재현, 클리어 1회 추첨·재방문/드롭 없음 결과의 재추첨 방지, 실제 Game Scene assembler의 방별 드롭 seed와 층 재로드 후 재추첨 방지, 자원 드롭의 Run 연결, SP 드롭퍼 제거와 Phase C 회귀를 검사한다. 수동 확인은 Game Scene에서 전투방 여러 개를 클리어해 약 3번에 1번꼴로 방 중앙에 하트·SP·골드·열쇠·폭탄 중 하나가 떨어지는지, 적 처치로는 SP가 나오지 않는지, 클리어한 방을 다시 들어가도 추가 드롭이 없는지 확인한다.
- 주의: `Week14Encounter3Verification.SetupAndVerifyBatch`처럼 Week 14 Setup을 다시 실행하는 배치는 현재 Game Scene의 Room Template·Encounter 카탈로그(`roomContentVersion` 2, `encounterContentVersion` 6)와 Encounter/Room 에셋을 옛 구성으로 되돌린다. 검증만 필요하면 Setup이 없는 `Verify`를 쓰고, 실행했다면 변경된 에셋과 Scene을 되돌린다. Room-7/8 `Verify`는 HEAD 기준에서도 `roomContentVersion` 전제 조건으로 실패한다.
- Obstacle-0 적 장애물 우회: 메뉴 `Trickal Fan Game > Week 18 > Verify Obstacle-0 Enemy Obstacle Avoidance`. 기존 적 검증까지 함께 돌리는 배치는 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle0Verification.VerifyWithEnemyRegressionsBatch -logFile <로그 경로>`. 직선 우선 이동, 단일·컵 모양 장애물 우회 경로가 장애물 안으로 들어가지 않고 도착하는지, 경로 결정성, 갇힘 시 직선 복귀, 사선 판정, 원거리형·저격형의 사격 보류와 우회, 돌진형의 직선 확인 후 예고와 예고 종료 시 막힘 회복, 장애물 입사각 `80°` 경계의 접선 미끄러짐과 정면 충돌 정지, 진입·유지 충돌 콜백과 반복 스침의 접선 안정성을 검사하고, 빈 Scene에서 Phase E-1~4와 Week 15 Enemy-1·4 적 검증을 함께 돌린다. Week 15 Enemy-0·2·3·5 검증은 현재 프리팹·추적 기본값과 맞지 않아 실패하므로 제외했다. Enemy-0은 HEAD 코드에서도 기본 추적 1.1초 때문에 즉시 예고하지 않고, Enemy-2·3·5는 Obstacle-0이 건드리지 않는 프리팹 값(레거시 보스 컴포넌트, 추적 시간, 스프라이트 색) 검사에서 멈춘다. 수동 확인은 고정 기둥 방(`pillar-crossfire`)에서 기둥 뒤에 서서 추적형·돌진형이 기둥을 돌아오는지, 원거리형이 기둥 너머로 쏘지 않고 옆으로 돌아 나오는지 확인한다. 돌진형은 벽·장애물을 거의 평행하게 스칠 때만 남은 돌진 시간 동안 벽을 따라 이동하고, 이전 돌진 뒤 벽 접촉이 유지된 상태에서도 같은 판정을 반복하며, 정면·모서리·문 충돌에서는 즉시 회복해야 한다.
- Obstacle-1 파괴 가능한 장애물: 메뉴 `Trickal Fan Game > Week 18 > Setup Obstacle-1 Destructible Obstacle`이 `Assets/Prefabs/DestructibleObstacle.prefab`과 `Assets/Items/Drops/obstacle-basic-drop-table.asset`(3%, 하트 25·SP 25·열쇠 10·폭탄 8·골드 30·구덩이 2)을 GUID를 보존하며 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 프리팹 계약과 레이어 충돌, 적 이동·사선 차단, 공격력과 무관한 4타 파괴, 근접·투사체·저학년 스킬·궁극기 타격과 중복 방지, 적 투사체·돌진 미집계, 20만 seed 드롭률·후보 비율과 seed 재현, 구덩이 무드롭, 1회 드롭과 방 재구성 시 파괴 유지, Resource-3 회귀를 검사한다. 장애물을 방에 배치하는 수동 확인은 Obstacle-2 Layout 이후에 한다.
- Obstacle-2 장애물 Layout: 메뉴 `Trickal Fan Game > Week 18 > Setup Obstacle-2 Obstacle Layouts`가 Large 크기의 `large-cover-blocks`·`large-split-lanes`·`large-scattered-rubble` Template과 `room-large-*.prefab`을 GUID를 보존하며 멱등 구성하고, Obstacle-1 장애물 프리팹을 중첩 인스턴스로 배치한 뒤 기둥 방과 함께 Game Scene 카탈로그(Room 콘텐츠 버전 4)에 등록한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번 실행 후 GUID 보존, 빈 방·기둥 방과 같은 Large Profile, Layout 전용 SpawnPoint, 장애물 ID·위치·4타 프리팹 계약, 겹침·필수 통로·도달성·원거리 사선(35%) 검증, 실제 콜라이더에서 적 A\* 경로 교차 확인, 빈 방이 받는 Encounter를 모든 Layout이 받는지, 8종 위반(겹침, 문 통로 막힘, SpawnPoint 덮음, 진입점 고립, 시야 없는 SpawnPoint, 격자 이탈, 영역 이탈, ID 중복)의 명시적 실패와 Room 카탈로그·층 생성 실패, 1024 seed 안의 결정적 선택과 다섯 Large Template 노출을 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 Large 방에서 장애물이 문과 통로를 막지 않는지, 4타에 부서지는지, 추적형·원거리형이 장애물을 돌아오는지 본다. Room-7 검증은 현재 Encounter 카탈로그에 `pillar-crossfire`가 없어 Obstacle-2와 무관하게 실패한다(2026-09-29 확인). Room-8 검증도 Encounter 5종을 가정하므로 같은 원인으로 실패할 수 있다.
- Obstacle-3 장애물 방 적 끼임 Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle3PlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다(검증기가 성공 0/실패 1로 종료). 메뉴 `Trickal Fan Game > Week 18 > Play Verify Obstacle-3 Enemies Never Stall In Obstacle Rooms`는 끝나면 Play Mode만 종료한다. 먼저 Edit Mode에서 원거리형 후퇴(직선·45°·몰림 시 제자리 사격)를 확인하고, Game Scene 생성기로 seed를 늘려 가며 장애물 Layout 3종과 기둥 방마다 Encounter가 붙은 방 5개씩을 모은 뒤 빈 Scene의 Play Mode에서 실제 Room Prefab·적 Prefab·RoomController 웨이브를 4배속으로 돌린다. 방마다 플레이어가 연결된 문 입구와 SpawnPoint에서 가장 많이 가려지는 장애물 뒤 엄폐 위치에 각각 서 있고, 모든 적이 생성 후 20초 안에 공격(근접 예고·접촉, 사격, 돌진 예고)에 도달하고 중심이 장애물 안에 들어가지 않으며 모든 웨이브가 클리어되는지 검사한다. 실행별 가장 늦은 첫 공격 시간과 클리어 시간이 로그에 남는다. 수동 확인은 장애물 Layout 방에서 장애물 뒤에 붙어 서서 원거리형이 가까이 와도 등 뒤 장애물에 밀착한 채 멈추지 않고 옆으로 빠지거나 사격하는지, 추적형·돌진형이 돌아오는지 본다.
- Spawn-1 SpawnPoint 배치 역할: 메뉴 `Trickal Fan Game > Week 19 > Setup Spawn-1 Placement Roles`가 모든 Room Template의 SpawnPoint에 근접 압박·후방 사격·돌진 경로 역할을 멱등 저장하고 Encounter 콘텐츠 버전을 6으로 올린다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Spawn1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회와 Scene·Template GUID 보존, 전체 Normal Layout의 역할 계약, 추적·고속형/원거리·저격형/돌진형 역할 매핑, 그룹 및 명시 SpawnPoint의 부적합 배치 거부, 512 seed의 결정적 Encounter 선택과 모든 웨이브의 역할 적합성을 검사한다.
- Spawn-2 스폰 위치별 가중치 후보: 메뉴 `Trickal Fan Game > Week 19 > Setup Spawn-2 Weighted Spawn Candidates`가 프로필별 `pressure`·`crossfire` Encounter 10개, 일반 원거리형 임시 Prefab `QuickRangedFairy`, 역할 로스터와 Encounter 콘텐츠 버전 7을 멱등 구성한다. 일반 원거리형은 선호 거리 5.6(저격형의 70%)에서 이동 속도 3.25로 한 방향 선회하며, 플레이어 속도를 최대 0.25초까지 짧게 예측해 0.4초 간격 3발 60% / 4발 40% 묶음을 이동 사격한다. 마지막 탄 직후 정지해 1.5초 쉰 뒤 다음 묶음과 함께 이동을 재개한다. 피해 등급은 Tune-1 상수(약)를 따르며 전용 아트 전까지 저격형 Sprite를 다른 색으로 표시한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Spawn2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 짧은 이동 예측 사격, 3/4발 경계, 마지막 탄 직후 정지와 1.5초 휴식, 장애물 때만 선회 반전, 잘못된 후보 계약 거부, Small/Basic/Wide/Tall/Large 저격 비율 10/20/50/50/60%, 동일 seed 결정성, 모든 후보 노출, 생성 노드의 선택 결과 보존, 256 생성 그래프와 실제 웨이브 바인딩을 검사한다.
- Difficulty-1 방 난이도: 메뉴 `Trickal Fan Game > Week 19 > Setup Difficulty-1 Room Difficulty`가 `Assets/Encounters/room-difficulty-table.asset`(위협 점수 추적 2·고속 3·원거리 3·저격 4·돌진 4, 쉬움 ≤11·보통 12~15·어려움 ≥16, 층별 상한 18/20/22, 거리 곡선 40/45/15 → 10/40/50), `pressure` 10~14·`crossfire` 15~19 선언 위협 범위, 장애물 Layout 7종의 +1 보정과 Encounter 콘텐츠 버전 8을 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Difficulty1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 표 값과 잘못된 표 5종 거부, 선언 범위 위반·미선언 시 Encounter 검증과 층 생성 실패, Layout 보정, 거리 곡선 보간, 층 범위를 채울 후보가 없을 때의 명시적 실패, 512 seed의 방 점수·등급·거리 저장과 64 seed 결정성, 목표 등급 또는 가장 가까운 가능 등급 선택, 먼 방 어려움 비율 증가와 가까운 방 어려움 존재를 검사한다.
- Tune-1 원거리 적 밸런스: 메뉴 `Trickal Fan Game > Week 19 > Setup Tune-1 Ranged Enemy Balance`가 `QuickRangedFairy` 체력 20·투사체 피해 등급 약과 `HighBloodSugarFairy` 투사체 속도 11, `ChargingEnemy` 돌진 속도 9.5를 멱등 구성한다. 값은 `Week19Tune1Setup` 상수에 있으며 Spawn-2·Enemy-3·Enemy-4·E-4 Setup도 같은 상수를 쓰므로 재실행해도 되돌아가지 않는다. 저격형 탄속·돌진 속도를 조정할 때는 `SniperProjectileSpeed`·`ChargingDashSpeed`를 바꾸고 Setup을 다시 실행한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Tune1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 1층 공격력 10 기준 정확히 2타 처치, 1층 투사체 하트 1칸, 저격형 탄속이 이전 7과 일반 원거리형보다 빠르고 체력 30·강 등급 유지, 돌진 속도가 이전 8보다 빠르고 돌진 거리 6 이상 12 미만·체력 70·강 등급 유지를 검사한다.
- Door-1 문 통과 판정: 메뉴 `Trickal Fan Game > Week 19 > Setup Door-1 Doorway Passage`가 `Assets/Rooms/Prefabs`의 모든 Room Prefab 문 슬롯 전환 트리거를 문 방향 길이 `RoomLayout.TransitionLength`(1.6, 입구 2.4의 66.7%)로 멱등 구성한다. Room-F5 Setup(`Week8GridFloorSetup`)도 같은 상수를 쓰므로 재실행해도 되돌아가지 않는다. 트리거에 닿아도 플레이어 중심이 트리거 폭 안에 있어야 통과하며(`RoomDoorway.ContainsPassageCenter`), 통과 뒤 무적 0.75초와 들어온 문 재통과 금지 0.4초는 `RoomGraphController`의 `doorwayInvulnerabilityDuration`·`returnDoorwayBlockDuration` 직렬화 값이다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Door1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 모든 Room Prefab 트리거 크기·위치, 열린 문 장벽 면이 트리거 안에 있어 밀면 닿는지, 안전 진입점이 트리거 밖인지, 좌우·상하 문의 중심 폭 판정, 폭 밖 중심 접촉 거부, 통과 무적 0.75초, 들어온 문만 차단·다른 문 허용·만료 후 허용·층 초기화 시 해제를 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 문 옆 벽을 따라 미끄러질 때 넘어가지 않는지, 문 중앙으로 밀면 넘어가는지, 넘어간 직후 바로 되돌아가지 않는지, 적이 문 앞에 있을 때 진입 직후 피해를 받지 않는지 본다.
- Encounter-4 적 수 확장: 메뉴 `Trickal Fan Game > Week 19 > Setup Encounter-4 Expanded Encounters`가 Normal Room Template 13종의 SpawnPoint를 기존 1~3번 뒤에 추가(Small 4, Basic·Wide·Tall 5, Large 6)하고 방 Prefab의 `Spawn N` Transform과 RoomController 목록을 맞추며, 난이도 표에 방 전체 2~7·웨이브당 4·후방 사격 3 제한을 넣고, 프로필별 `swarm`·`elite-pair` Encounter 10개와 Encounter 콘텐츠 버전 9를 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Encounter4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID와 `Spawn N` 개수 유지, 기존 1~3번 좌표·역할 보존, Layout 검증, 선언 위협과 규모 제한, 잘못된 규모 4종의 명시적 실패, 15가지 출입구 조합의 `swarm`·`elite-pair` 배치, 512 seed의 적 수 2~7·웨이브 4 이하·지점 중복 없음과 네 조합 선택, 64 seed 결정성, 실제 웨이브 바인딩을 검사한다. Room-3·Room-6·Obstacle-2 Setup을 다시 실행하면 해당 방의 SpawnPoint가 3개로 돌아가므로 Encounter-4 Setup을 다시 실행한다(Spawn-1·Spawn-2 Setup은 추가 지점 역할과 추가 Encounter를 보존한다).
- Special-1 잠긴 보물방: 보물방은 독립 seed의 50% 후보로 잠기되, 보물방을 제외해도 시작방에서 보스방까지 갈 수 있는 선택 경로에서만 잠긴다. 잠긴 보물방 방향의 황금색 문에 닿으면 열쇠 1개를 자동 소비해 Run 동안 영구 해제하며, 열쇠가 없으면 통과하지 않는다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Special1Verification.Verify -logFile <로그 경로>`. 512 seed 결정성·잠김/열림 노출·필수 보스 경로 우회, 열쇠 부족·정확히 1개 소비·Run 종료 후 소비 차단, 중복 소비 방지와 층 재구성 뒤 해제 상태 복원을 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 열쇠 없이 황금색 보물방 문이 열리지 않는지, 열쇠를 얻고 다시 닿으면 1개만 줄며 진입하는지, 나갔다 다시 들어갈 때 추가 소비가 없는지 확인한다.
- Special-2 플레이어 폭탄: 메뉴 `Trickal Fan Game > Week 20 > Setup Special-2 Player Bomb`이 `Assets/Prefabs/PlacedBomb.prefab`과 Game Scene 플레이어의 단일 `PlayerBombController`를 GUID를 보존하며 멱등 구성한다. `F`를 누르면 폭탄 1개를 즉시 소비해 현재 위치에 설치하고, 0.75초 뒤 반경 2.5에서 적에게 고정 30 피해, 플레이어에게 1칸(2 단위) 자해, 파괴 가능한 장애물 즉시 파괴를 한 번 적용한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Special2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 Scene·Prefab GUID와 컴포넌트 단일성, 빈 자원·대기 중 중복·보상 선택·궁극기·사망·Run 종료 거부, 정확한 1개 소비·퓨즈 경계·범위 안팎·복수 콜라이더 단일 피해·자해·장애물 상태 보존을 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 폭탄을 획득해 `F`로 설치하고, 0.75초 뒤 주황색 반경 표시와 함께 근처 적·플레이어·장애물에만 효과가 적용되는지, 설치 중 연타해도 하나만 소비되는지 확인한다.
- Special-3 비밀방: 메뉴 `Trickal Fan Game > Week 20 > Setup Special-3 Secret Rooms`가 `Assets/Prefabs/SecretPit.prefab`(Pickup 레이어 트리거)을 GUID를 보존하며 멱등 구성하고, 기본 짱돌·마리 폭탄박스 드롭 표의 `pit` 후보를 이 Prefab에 연결하며, Room 콘텐츠 버전 6·Encounter 콘텐츠 버전 10으로 올린다. 층마다 독립 seed 50%로 비밀방(보물방과 같은 3택1 보상, Basic `16 × 9`)을 하나 추가하며, 시작방·보스방과 닿지 않는 빈 칸 중 인접 방이 가장 많은 칸에 두고 닿는 방 전부와 숨김 통로로 잇는다. 폭탄 폭발 반경이 닿은 벽의 통로만 열리고, 비밀방에 들어가면(문·구덩이 모두) 그 방의 모든 통로가 양쪽에서 열린다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Special3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 Prefab·Scene GUID, 드롭 표 가중치·구덩이 연결, 512 seed×3층의 결정성·최대 1개·약 50% 생성·인접 최다 칸·시작/보스 비인접·숨김 통로가 보스 거리를 줄이지 않음, 비밀방 없는 층의 구덩이 재가중, 폭탄 범위 밖 미개방·닿은 벽만 개방·양쪽 동시 개방, 문/구덩이 진입 시 전체 통로 개방, 미클리어 방·가장자리 접촉 구덩이 거부, 발견 후 구덩이 지름길, 층 재구성 뒤 통로·구덩이 복원을 검사한다. 수동 확인은 `FrontendScene`에서 여러 seed로 Play해 비밀방 인접 벽에 폭탄을 터뜨리면 갈색 통로가 열리고 미니맵에 비밀방(`H`)이 그때 나타나는지, 구덩이를 밟으면(방 클리어 후) 비밀방 중앙으로 떨어지는지, 비밀방 보상 3택1이 한 번만 지급되는지 확인한다. 비밀방 seed·폭탄·구덩이는 아래 Game Scene 개발 패널(`F1`)로 준비하되, 벽 위치 강조를 끈 상태로도 한 번 확인한다.
- Special-4 상점: 메뉴 `Trickal Fan Game > Week 20 > Setup Special-4 Shop`이 `Assets/Rooms/Definitions/shop-standard.asset`(RoomType Shop), `Assets/Items/Shop/shop-catalog.asset`(가격표), `Assets/Prefabs/ShopRoom.prefab`(상품대 4개, Pickup 레이어 트리거)을 GUID를 보존하며 멱등 구성하고, Basic 템플릿에 Shop 타입을 허용하며, Game Scene 생성기 정의 목록·Room 콘텐츠 버전 7·Encounter 콘텐츠 버전 11과 조립기의 상점 참조를 설정한다. 층마다 독립 seed 60%로 시작방·일반 전투방 옆에 열쇠 잠금 상점을 붙이고, 아티팩트/스펠 2개(일반 10·고급 15·희귀 20·영웅 25 골드)와 하트·열쇠·폭탄 중 2개(3·5·5 골드)를 판다. 자동 검증은 Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Special4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 Prefab·카탈로그·정의·Scene GUID, 가격표와 픽업 연결, 상품대 트리거 비중첩, 512 seed×3층의 결정성·최대 1개·약 60% 생성·마지막 번호의 막다른 잠금 방·상점 없는 생성기와 기존 방·통로 동일, 비밀방 비연결, 재고의 결정성·서로 다른 상품·등급 가격·부족 시 소모품 보충, 열쇠 없는 진입 거부·1개 소비, 골드 부족 거부, 아이템 1회 구매·재구매 거부, 소모품 바닥 드롭, 층 재구성 뒤 재고·판매 상태·열린 문·골드 유지, Run 종료 후 구매 거부를 검사한다. 수동 확인은 `FrontendScene`에서 Play하고 개발 패널(`F1`)의 `Find shop F1`·`Restart Run with this seed`·`+10 Key/Gold`·`Go to shop door`로 상점 문 앞에 가서, 미니맵 `$`와 황금색 문, 상품 이름·가격·`[E] 구매`/`골드 부족` 표시, 구매 후 상품이 사라지고 소모품이 앞에 떨어지는지, 다시 들어와도 상태가 같은지 확인한다.
- Game Scene 개발 패널: Editor Play 또는 Development Build의 Game Scene에서 `F1`로 연다(정식 빌드 제외, Setup 불필요). `Find secret F1`·`Find shop F1`이 입력한 seed(비어 있으면 현재 seed) 다음부터 1층에 비밀방·상점이 있는 seed를 찾고, `Restart Run with this seed`가 그 seed로 Run을 다시 시작한다(Frontend를 거치지 않고 Game Scene을 직접 Play한 경우 씬을 다시 로드). `+10 Bomb/Key/Elif`, `Full HP/SP`, `Invulnerable`, `Kill current wave`(방의 정상 사망 처리로 웨이브·클리어·보상 진행), `Show secret room and walls`(미니맵에 비밀방·숨김 통로 표시, 닫힌 비밀 벽을 분홍색으로 강조), `Next broken obstacle drops a pit`(비밀방이 있는 층에서 다음 첫 파괴 1회만 구덩이, 층 재구성 시에는 원래 드롭 결과로 복원), `Go to secret neighbor`/`Go to secret room`(전투 중이 아닐 때 방 이동, 비밀방 진입은 실제 발견으로 처리), `Go to shop door`(상점 문이 있는 방으로 이동)를 제공한다. 훅 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20DevPanelVerification.Verify -logFile <로그 경로>`.
- Obstacle-4 요정왕국 장애물·Layout: 메뉴 `Trickal Fan Game > Week 20 > Setup Obstacle-4 Fairy Kingdom Obstacles and Layouts`가 기존 Large 장애물 Layout 3종과 신규 Small·Basic·Wide·Tall Layout 4종의 짱돌을 특수 장애물 후보 슬롯으로 구성한다. 장애물 방마다 seed로 40%를 추첨해 최대 한 슬롯만 분홍색 `marie-bomb-box`로 바꾸며, 상자는 20% 확률로 폭탄 가중치 60인 전용 표를 사용한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Obstacle4Verification.SetupAndVerifyBatch -logFile <로그 경로>`, 기존 장애물·난이도·Encounter·폭탄까지 포함한 회귀는 `VerifyWithRegressionsBatch`를 사용한다. 수동 확인은 `FrontendScene`에서 여러 seed로 Play해 네 크기의 신규 Layout이 실제로 나오고, 문과 필수 통로가 열려 있으며, 한 방에 분홍색 마리 상자가 두 개 이상 나오지 않고 일반 짱돌보다 폭탄을 체감상 자주 주는지 확인한다.
- Move-1 벽 비빔 저항 제거: 메뉴 `Trickal Fan Game > Week 20 > Setup Move-1 Frictionless Actors and Player Collider`가 `Assets/Settings/FrictionlessActor.physicsMaterial2D`(마찰 0, 반발 0)를 Physics2D Default Material로 지정하고, 플레이어가 있는 4개 Scene의 플레이어 `CircleCollider2D` 반지름을 0.5로 맞춘다(0.38도 시험했으며 마찰 제거 단독 효과를 확인하는 중). 재질을 지정하지 않은 플레이어·적·장애물 콜라이더 전체에 적용된다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week20Move1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 재질 GUID 보존, Default Material 연결, 마찰 있는 재질로 덮어쓴 액터 Prefab 부재, 4개 Scene 플레이어 반지름, 벽 대각선 비빔 시뮬레이션에서 접선 속도 95% 이상 유지(기존 마찰 0.4 대조군 60%)를 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 벽·장애물에 대각선으로 붙어 이동해도 속도가 줄지 않는지, 대각선으로 엇갈린 장애물 사이와 벽-장애물 틈을 끼지 않고 지나가는지, 적도 장애물을 따라 끼지 않고 돌아 나오는지 확인한다.
- 보스 테스트방: `Trickal Fan Game > Debug > Open Boss Test Room`에서 Play Mode를 시작한다. 오른쪽 패널에서 보스 3종을 바꾸고, 활성 아이템·스펠 16종과 호환용 `item-06`을 각각 `+1`로 획득해 같은 보스전에서 효과를 비교할 수 있다. `HP 25% (Life Gem)`과 `Next Floor`는 생명의 보석의 층당 1회 재발동을 빠르게 확인한다. 크레용사용 선택 후 `Show Crayon Recognition Radius`를 켜면 약 `6.36`의 검격 인지 반경이 파란 원으로 표시된다. 보스는 원 안에서도 계속 접근하며 2~3회 검격해야 한다. 대시는 첫 `0.2초`와 연속 대시 사이 `0.1초` 동안 플레이어를 추적 조준한 뒤 고정 방향으로 출발하고, 전체 연계가 끝난 뒤에는 `0.3초` 동안 완전히 멈춰야 한다. 첫 소환 뒤 최대 HP 15% 피해를 줄 때마다 원거리 2+근거리 2 소환이 다시 후보가 되며, 황금 내려찍기는 세 예고선의 근거리 중첩에서 치명 등급 피해를 최대 3회(1층 기준 9칸) 줘야 한다. Edit Mode의 `Trickal Fan Game > Debug > Verify Boss Test Room`은 보스 3종 생성, 17개 개별 획득 항목과 크레용사용 인지 반경 표시 토글을 검사한다.
- Boss-2 수동 플레이: `ItemTestScene`에 `Assets/Prefabs/SaemaeumVaultBoss.prefab`을 임시 배치하고 다른 적을 비활성화한 뒤 Play한다. 작은 탄성 이동과 깊은 점프 웅크림이 구분되고, 계속 이동하면 연속 착지를 피할 수 있으며 가만히 있으면 맞아야 한다. 최종 착지와 회복 중 공격 기회가 있고, HP 50% 이하 전환 뒤에는 회복하지 않으며 접근·점프 리듬이 빨라져야 한다. 확인 뒤 임시 Scene 변경은 저장하지 않는다.
- 같은 프로젝트를 연 Unity가 있으면 배치 실행이 잠길 수 있다. 기존 Editor를 강제 종료하지 않고 수동 메뉴를 실행하거나 독립된 복사본에서 검증한다.
- Boss-2 점프 후속 확인: `Trickal Fan Game > Week 15 > Verify Boss-2 Saemaeum Vault`는 공중 Trigger, 포물선 높이, 근거리 착지 피해와 종료·취소 복원을 추가 검사한다. Play에서 착지 예고 안에 서면 공중 몸체에 먼저 밀려나지 않고 착지 순간 피해·넉백을 받아야 하며, 예고 밖으로 이동하면 피할 수 있어야 한다. 몸체는 도약 중 상승했다 착지점으로 내려와야 한다.

수동 확인: `FrontendScene`을 열고 Game View에 Fixed Resolution `1280 × 720`, `1920 × 1080`을 각각 추가하여 Play한다. 타이틀에서 저장 프로필의 홈 진입과 새 사용자의 닉네임 등록 진입을 각각 확인한다. 홈에서는 네 버튼의 마우스 호버, 위·아래 키 순환과 Enter/Space를 확인한다. 게임 시작에서는 에르핀 카드 선택 표시, 확인 활성화, 뒤로가기와 재진입 시 선택 초기화를 확인한다. 확인을 누르면 Game Scene으로 한 번 이동해 1층 첫 방에서 전투를 시작해야 하며 이전 캐릭터 선택 창이 다시 나타나지 않아야 한다. Run 종료 전에는 Backend 전적이 생성되지 않아야 한다.

저장소 공통 불변 규칙은 루트 `AGENTS.md`에 둔다. 기능 하나를 계획부터 검증과 체크리스트 갱신까지 진행할 때는
`.agents/skills/trickal-feature-cycle/SKILL.md`의 저장소 전용 스킬을 사용한다.

향후 Unity 콘텐츠 생성기·검증기, Unity ↔ Backend 계약 검사, 플레이테스트 텔레메트리와 조건부 Unity MCP는
[개발 생산성·검증 인프라 계획](./13-development-tooling-plan.md)에 따라 도입한다.

현재 구현된 Phase C·D·F Unity 도구:

- `Trickal Fan Game > Debug > Open or Create Item Test Room`: 랜덤 층 생성과 Backend Run 저장에서 분리된 `Assets/Scenes/ItemTestScene.unity`를 생성하거나 연다. Hierarchy의 `Item Test Room`을 선택하고 `Item Loadout`에서 시작 스택을, `Enemy Placements`에서 적 Prefab·활성 여부·로컬 좌표·회전을 설정한 뒤 Play Mode를 시작한다. 활성 아이템·스펠 16종과 호환성 확인용 비활성 `item-06`이 기본 목록에 포함되며, 기본 적 배치에는 불효자손·원거리 적·돌진형·산사모·고혈당 요정이 들어 있다.
- 기존 `ItemTestScene`에 산사모·고혈당 요정이 없다면 `Trickal Fan Game > Debug > Add Week 15 Enemy Color Samples`를 실행한다. 기존 배치는 유지한 채 두 디버그 샘플만 추가한다.
- Play Mode 오른쪽 `Item Test Room` 패널에서 현재 층·HP·방어막·공격력·공격속도·치명타·이동속도·투사체·관통 수치를 확인한다. 각 아이템·스펠의 `+1`로 즉시 한 스택을 획득하고, `Heal / Reset HP`와 `Respawn Enemies`로 같은 설정을 반복 검증한다. 생명의 보석은 `+1` 후 `HP 25% (Life Gem)`으로 1회 발동을 본 다음 같은 층에서 재발동하지 않는지 확인하고, `Next Floor` 뒤 다시 `HP 25%`를 눌러 새 층에서 한 번 더 발동하는지 확인한다. 광기의 가면 획득 뒤에는 Scene 뷰에서 2.5m 분홍색 Gizmo가 표시된다. 스택 감소와 완전 초기화는 Play Mode를 다시 시작한다.
- `Trickal Fan Game > Debug > Verify Item Test Room`: 전용 씬 격리, 16개 활성+1개 호환 항목의 목록·중복·최대 스택, 적 Prefab의 `Health`, Backend 저장 세션 부재를 검사한다. `ItemTestScene`을 연 상태에서 Edit Mode와 Play Mode에 각각 실행하며 성공 시 Console에 `Item Test Room verification passed`가 출력되고 오류가 없어야 한다.

- `Trickal Fan Game > Setup Phase C Lower Grade Skill`: Play Mode 밖에서 Player 컴포넌트와 SP 픽업·유도탄 프리팹을 생성 또는 갱신하고 씬을 저장한다.
- `Trickal Fan Game > Verify Phase C Lower Grade Skill`: SP 경계, 처치 드롭, 입력 방향 중심 36° 부채꼴과 `1→3→2→4` 슬롯 순서, 0.08초 간격 4발 연사, 다수 적 거리순 배분, 현재 공격력 100% 중첩 폭발, 타깃 없음과 SP 부족 경로를 검사한다. 성공 시 Console에 `Phase C verification passed`가 출력되고 불변조건 위반 시 예외로 실패한다.
- `Trickal Fan Game > Setup Phase D High Grade Skill`: Play Mode 밖에서 Player 행동 상태·고학년 스킬 컴포넌트와 적 넉백 수신기를 생성 또는 갱신하고 씬·적 프리팹을 저장한다. 재실행해도 중복 컴포넌트를 만들지 않는다.
- `Trickal Fan Game > Verify Phase D High Grade Skill`: Q 쿨타임, 조향·무적 돌진, 행동·전환 게이트, 200% 범위 피해, 일반/보스 넉백 후 경직, 충돌 후 플레이어 무적 경직, 시간 만료 감속과 사망 정리를 검사한다. 성공 시 Console에 `Phase D verification passed`가 출력되고 불변조건 위반 시 예외로 실패한다.
- `Trickal Fan Game > Setup Phase F-1 Random Room Definitions`: Play Mode 밖에서 `Assets/Rooms/Definitions`의 일반방 3종·보상방 1종·보스방 1종 정의 에셋과 씬의 `Phase F Floor Generator`를 생성 또는 갱신한다. 재실행해도 오브젝트나 에셋을 중복 생성하지 않는다.
- `Trickal Fan Game > Verify Phase F-1 Random Room Graph`: 고정 seed 재현성, 안정적인 `floor-XX-room-YY` ID, 중복 ID 거부, 시작→일반/보상→보스 연결, 전 층 도달 가능성과 잘못된 정의 실패를 검사한다. 성공 시 Console에 `Phase F-1 random room graph verification passed`가 출력된다.
- `Trickal Fan Game > Setup Phase F-2 Generated Room Graph Binding`: F-1 Setup을 갱신한 뒤 생성 결과를 Phase E에서 검증된 9개 방의 정의·몬스터 구성·보상방 활성 상태·출입구 순서에 적용하고 `RoomGraphController`를 연결한다. 완료된 Phase E-7 그래프가 선행되어야 하며 재실행해도 assembler나 출입구를 중복 생성하지 않는다.
- `Trickal Fan Game > Verify Phase F-2 Generated Room Graph Binding`: 생성 노드와 씬 방의 1:1 대응, 몬스터 패턴, 보상방 활성화, 정확한 출입구 순서와 graph 참조, 재적용 멱등성, 방 수 불일치 실패를 검사한다. 성공 시 Console에 `Phase F-2 generated room graph binding verification passed`가 출력된다.
- `Trickal Fan Game > Setup Phase H-5 Meta Progression`: Play Mode 밖에서 기존 `RunSession`에 `PlayerProgressClient`를 하나만 추가하거나 갱신하고 `test-player`, 저학년 스킬과 고학년 스킬 참조를 연결해 씬을 저장한다. Backend와 seed가 먼저 실행 중이어야 실제 온라인 진행을 조회할 수 있다.
- `Trickal Fan Game > Verify Phase H-5 Meta Progression`: 온라인·캐시·Lv.1 폴백, Lv.1/Lv.10 실제 스킬 효과, 새 스냅샷 덮어쓰기, 클리어·사망 Run DTO, 중복 종료 방지, 동일 `clientRunId` 재시도, 성공·네트워크 실패·충돌 결과 표시를 검사한다. Edit Mode와 Play Mode에서 각각 실행해 `Phase H-5 verification passed`와 Console 오류 0개를 확인한다.

- Floor-1 층 규모: 메뉴 `Trickal Fan Game > Week 20 > Setup Floor-1 Expanded Floors`가 Game Scene의
  생성기에 층별 설정 3개(총 방 수 8~12/8~12/12~18, 보스 최소 거리 4/5/5)와 Room 콘텐츠 버전 8·Encounter
  콘텐츠 버전 12를 GUID를 보존하며 구성한다. Special-4까지 구성된 Scene이 필요하다. 총 방 수에 시작·보스·
  보물·상점·비밀방이 모두 들어가며, 보스 거리는 숨김 통로를 제외한 최단 이동 횟수다. 예약한 비밀방·상점
  수를 먼저 빼고 보스 필수 경로와 가지를 생성한다. 새 Run마다 층별 범위에서 추첨하되 같은 seed로 재현한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 20 > Setup and Verify Floor-1 Expanded Floors`다.
  배치 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod
  TrickalFanGame.Editor.Week20Floor1Verification.VerifyWithRegressionsBatch -logFile <로그 경로>`를 사용한다.
  Setup 2회 Scene GUID, 1,024 seed×3층의 모든 총 방 수·연결성·보스 막다른 방과 층별 거리·열쇠 없는 필수 경로,
  특수방 최대 수량·50%/60% 등장률·콘텐츠 적합성, 64 seed의 동일 결과, 잘못된 설정 5종 실패를 검사한 뒤
  Special-1·3·4, Difficulty-1, Encounter-4, Room-0을 실행한다. 핵심 검증 실패는 예외와 배치 실패로 표시된다.
  수동 확인은 `Assets/Scenes/FrontendScene.unity`에서 Play하고 `F1` 개발 패널의 seed 변경·Run 재시작으로
  여러 맵을 비교한다. 1~2층보다 3층 탐색 범위가 커지고, 열쇠·폭탄 없이 보스에 도달하며, 보스 처치 후
  1→2→3층 전환과 3층 클리어 종료가 유지되는지 확인한다. 전체 Run 플레이 증거는 Play-1에 별도로 기록한다.
  예전 공통 층 생성 Setup을 다시 실행해 콘텐츠가 바뀌었다면 최신 선행 Setup과 Floor-1 Setup을 다시 적용한다.

- Play-1 기록과 통합 검증: Editor Play 또는 Development Build의 `RunSession`이 Run 시작부터 층별
  실시간(일시정지·메뉴 포함)을 누적한다. `F1` 개발 패널의 Play-1 영역에서 현재 초를 확인하고
  `Copy Play-1 record`로 복사한다. 클리어·사망·포기·재시작·Scene 종료 때 Console의 `[Play-1]`에
  seed·캐릭터·콘텐츠 버전·결과·진입한 층의 초·보조 도구 사용을 한 번 남긴다. 결과 화면에서는 `F1`의
  `Copy last Play-1 record`로 직전 종료 기록을 복사한다. 개발 패널은 스크롤로 작은 화면에서도 조작한다.
  측정은 정식 빌드에서 제외하며 기존 Run API·DB·Web 계약을 바꾸지 않는다. Console과 복사한 기록을
  [20-fifth-month-playtest.md](./20-fifth-month-playtest.md)의 실제 결과 표에 남긴다. 정상 Run 권장 seed는
  1·8·12이며, 특수방 기능 확인에 자원 추가·무적 등을 썼으면 별도 보조 Run으로 기록한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 20 > Verify Play-1 Preparation and Integration`이다.
  배치는 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod
  TrickalFanGame.Editor.Week20Play1Verification.VerifyBatch -logFile <로그 경로>`를 사용한다. 실제로
  전체 Run을 자동 플레이하지 않고 시간 기록·RunSession 종료 연결 및 기존 Room-8·Resource-1·3·
  Obstacle-4·Special-1~4·Reward-3를 저장된 Scene별로 분리 검증한다. 실패 시 예외로 배치를 중단한다.
  2026-10-01 Unity 배치 통과·종료 코드 0을 확인했으며, 실제 Run 완료와 시간 증거는 별도로 필요하다.

- Gold-0 Run 화폐 골드 전환: `RunResourceType.Elif = 0`을 `Gold = 0`으로 이름만 바꿨다. 값 0과
  `Assets/Prefabs/ElifPickup.prefab`의 경로·GUID, 드롭 표의 드롭 ID `elif`, 99 상한과 상점 가격은 그대로다.
  상점 가격·`골드 부족` 프롬프트·상품대 기본 문구, 개발 패널 `+10 Gold`, 아이템 테스트 패널 `GOLD`가 바뀐다.
  계약은 [23-item-classification-contract.md §6](./23-item-classification-contract.md)을 따른다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Gold-0 Run Currency`다. 배치는 Unity
  `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week21Gold0Verification.Verify
  -logFile <로그 경로>`를 사용한다. enum 값 0/1/2와 `Elif` 이름 부재, 픽업 프리팹 GUID·값, 상품대 기본 문구와
  바인딩된 가격·부족 프롬프트의 골드 표기를 검사한 뒤 Resource-1(99 상한)·Special-4(상점 구매) 회귀를 실행한다.
  2026-10-02 Unity 배치 통과·종료 코드 0을 확인했다. 수동 확인은 상점에서 가격과 `골드 부족`이 보이는지,
  `F1` 패널의 `+10 Gold`로 구매되는지 본다.

- Slot-0 스펠·짱셈스펠 공용 슬롯: 메뉴 `Trickal Fan Game > Week 21 > Setup Slot-0 Spell Slot`이
  `Assets/Prefabs/SingleUseItemPickup.prefab`(트리거 바닥 픽업)을 GUID를 보존하며 멱등 구성하고, Game Scene
  플레이어에 `PlayerSpellSlot`·`PlayerSingleUseEffects`를, HUD `ReferenceFrame`에 `Spell Slot HUD`(스킬 패널
  왼쪽, (480, -430))를 한 번만 추가한다. 입력은 **Left Shift**로 사용, 빈 슬롯은 닿으면 획득, 찬 슬롯은 닿으면
  자동 교체하며 기존 아이템을 그 자리에 배출한다. 배출된 아이템은 플레이어가 한 번 벗어난 뒤에 다시 주울 수 있다.
  E는 향후 액티브 키로 예약했다(D1). 자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Slot-0 Spell Slot`,
  배치는 Unity `-batchmode -quit -projectPath <game 경로> -executeMethod
  TrickalFanGame.Editor.Week21Slot0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다. Setup 2회
  Prefab·Scene GUID, `ItemKind` 값 0~3과 접두사 비중첩, 일회용 `maxStacks = 1`, 인스턴스당 1회 Run 기록,
  교체 시 유실·중복 없음과 재획득 미기록, 배출 Prefab이 없으면 교체 거부, `TryAcquire`의 일회용 거부,
  효과 미연결·조건 실패·일시정지·사망·Run 종료 시 미소비, 성공 시 1회 소비와 재진입 차단, HUD 표시와 입력
  비차단을 검사한다. 2026-10-02 Unity 배치 통과·종료 코드 0을 확인했고, Item-0·HUD-1·2·3A·3B·Reward-2·
  Setting-1C 회귀도 통과했다. 실제 일회용 아이템은 Spell-0부터 추가되므로 현재 수동 확인은 Game Scene HUD
  오른쪽 아래에 `비어 있음` 슬롯이 스킬 패널·보스 체력바와 겹치지 않는지 보는 것까지다. 획득·교체·Shift 사용의
  화면 확인은 첫 스펠 구현 뒤 진행한다.

- Spell-0 아로마 테라피·명상의 시간: 메뉴 `Trickal Fan Game > Week 21 > Setup Spell-0 SP Spells`가
  `Assets/Items/single-spell-aroma-therapy.asset`·`single-spell-meditation-time.asset`을 GUID를 보존하며 멱등
  구성한다. 선택 보상 풀에는 넣지 않는다. 자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Spell-0 SP Spells`,
  배치는 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod
  TrickalFanGame.Editor.Week21Spell0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다. Setup 2회 GUID,
  효과 타입 28/29와 에셋 계약, 선택 보상 풀 미포함, SP 반 칸·초과 충전 규칙, 아로마의 4/3 충전·초과 상태 거부·
  미소비, 명상의 첫 틱(1초)·12틱 상한·최대치 버림·일시정지 정지·방 이동 유지·재사용 갱신·사망/Run 종료 중단,
  HUD `SP 1.5 / 3`·초과 슬롯 표시를 검사한다. 2026-10-02 Unity 배치 통과·종료 코드 0을 확인했고, Slot-0·HUD-1·
  HUD-3B·Phase C·G-4·Item-0·Content-0·Reward-0·Reward-2·개발 패널 회귀도 통과했다.
  수동 확인: Game Scene Play → `F1` 패널 `— Spell slot —`의 `Drop 아로마 테라피`/`Drop 명상의 시간`으로 플레이어
  오른쪽에 픽업을 떨어뜨려 줍고 Shift로 사용한다. 아로마는 SP가 금색 초과 슬롯까지 차고(`SP 4 / 3`) 초과 상태에서는
  `[Shift] 사용 불가`로 남아야 한다. 명상은 SP를 비운 뒤 사용하면 1초마다 반 칸씩 차고(`SP 0.5 / 3`…), 패널의
  `SP regen` 남은 시간이 방 이동 중에도 줄고 Esc 일시정지 중에는 멈춰야 한다. Drop 버튼은 Editor 전용이며 Run을
  보조 Run으로 표시한다.

- Spell-1 저놈 잡아라 일회용 전환: 메뉴 `Trickal Fan Game > Week 21 > Setup Spell-1 Catch That One`이
  `Assets/Items/single-spell-catch-that-one.asset`을 GUID를 보존하며 멱등 구성하고, Game Scene 선택 보상 풀(상점 아이템
  상품 공유)에서 레거시 `spell-catch-that-one`을 뺀다. 은퇴 목록은 `Editor/LegacySpellRetirement.cs`이며 Reward-3
  Setup을 다시 실행해도 은퇴한 레거시 스펠은 풀에 돌아오지 않는다. 레거시 에셋·ID·Backend 카탈로그는 그대로 활성이다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Spell-1 Catch That One`, 배치는 Unity `-batchmode -nographics
  -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week21Spell1Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. Setup 2회 GUID, 효과 타입 30과 레거시 21 보존, 에셋·설명 계약, 레거시 계약 불변과 풀 제외,
  시작방·보물방·클리어한 방 사용 거부·미소비, 일시정지 거부, 미클리어 전투방/보스방 사용 시 기본 공격·투사체 ×1.1
  (스킬 ×1), 같은 방 재사용 +10% 누적, 클리어 후에도 방 안에서는 유지, 방·층 이탈 해제와 재방문 미발동, 레거시
  스펠과 합산(×1.2), Run 종료·사망 해제, 인스턴스별 획득 기록을 검사한다. 2026-10-02 Unity 배치 통과·종료 코드 0을
  확인했고 Reward-3·Week16 Spell-1(레거시)·Content-0·Spell-0·Slot-0·Special-4 상점·아이템 테스트 룸·개발 패널
  회귀(`Verify`)도 통과했다.
  수동 확인: Game Scene Play → `F1` 패널 `Drop 저놈 잡아라`로 픽업을 떨어뜨려 줍는다. 시작방에서 Shift를 누르면
  슬롯에 남아야 하고, 적이 있는 전투방에서 사용하면 패널에 `Room ATK +10%`가 표시되며 기본 공격 피해가 늘어야 한다.
  방을 나가면 표시가 사라지고 같은 방에 다시 들어가도 돌아오지 않아야 한다. 보물방 선택 보상·상점 아이템 상품에
  레거시 저놈 잡아라가 더 이상 나오지 않아야 한다.

- Spell-2 그건 내 잔상 일회용 전환: 메뉴 `Trickal Fan Game > Week 21 > Setup Spell-2 Afterimage`가
  `Assets/Items/single-spell-afterimage.asset`을 GUID를 보존하며 멱등 구성하고, Game Scene 플레이어의
  `PlayerSingleUseEffects.roomGraph`를 `RoomGraphController`로 연결하며, 선택 보상 풀(상점 아이템 상품 공유)에서
  레거시 `spell-afterimage`를 뺀다. 레거시 에셋·ID·Backend 카탈로그는 그대로 활성이다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Spell-2 Afterimage`, 배치는 Unity `-batchmode -nographics
  -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week21Spell2Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. 실제 Game Scene 층을 고정 seed로 조립해 Setup 2회 GUID, 효과 타입 31 추가와 기존 번호 보존,
  에셋·설명 계약, 레거시 계약 불변과 풀 제외, 씬 연결, 시작방·전투 시작 전·클리어한 방·일시정지·방 이동 차단 시
  사용 거부·미소비, 2웨이브 전투방의 2웨이브 도중 탈출 시 시작방 이동·남은 적 제거(처치 수 불변)·미클리어·웨이브
  0·문 열림·클리어 보상 미지급, 부순 장애물·바닥 픽업 유지, 시작방 재사용 거부, 재진입 시 같은 1웨이브 재시작과
  실제 클리어 1회 보상, 보스방 탈출 후 같은 보스 최대 HP 재시작을 검사한다. 관련 회귀를 함께 돌리려면
  `VerifyWithRegressionsBatch`(Encounter-4·Special-3·Spell-1 포함)를 사용한다. 2026-10-02 두 배치 모두 통과·종료
  코드 0을 확인했고 Slot-0·Spell-0·Reward-3 회귀(`Verify`)도 통과했다. `Week14Encounter3Verification.Verify`는 이후
  Encounter 목록 변경으로 이번 변경과 무관하게 실패한다(`Encounter-3 catalog or content version is not configured`).
  수동 확인: Game Scene Play → `F1` 패널 `Drop 그건 내 잔상`으로 픽업을 떨어뜨려 줍는다. 시작방과 적이 나오기 전
  방에서는 Shift를 눌러도 슬롯에 남아야 한다. 전투 중인 방에서 사용하면 시작방으로 이동하고 미니맵에서 그 방이
  미클리어로 남아야 하며, 다시 들어가면 문이 잠기고 첫 웨이브부터 시작해야 한다. 그 방에서 부순 장애물과 떨어진
  픽업은 그대로여야 한다. 보스방에서 보스 HP를 깎은 뒤 탈출하고 다시 들어가면 보스 HP가 가득 차 있어야 한다.

- Spell-3 막판 스퍼트 일회용 전환: 메뉴 `Trickal Fan Game > Week 21 > Setup Spell-3 Final Sprint`가
  `Assets/Items/single-spell-final-sprint.asset`을 GUID를 보존하며 멱등 구성하고, 선택 보상 풀(상점 아이템 상품 공유)에서
  마지막 레거시 스펠 `spell-final-sprint`를 뺀다. 이제 풀은 아티팩트만 담으므로 `Week16Reward3Setup`·검증은 레거시 스펠이
  없어야 통과한다. 레거시 에셋·ID·Backend 카탈로그는 그대로 활성이다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Spell-3 Final Sprint`, 배치는 Unity `-batchmode -nographics
  -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week21Spell3Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. Setup 2회 GUID, 효과 타입 32 추가와 레거시 22·23 보존, 에셋·설명·값 검증, 레거시 계약 불변,
  은퇴 목록 3종과 아티팩트 전용 풀, 시작방·전투방·보물방·클리어한 보스방 사용 거부·미소비, 일시정지 거부, 미클리어
  보스방 사용 시 공속 ×1.3·이속 ×1.05(피해 ×1), 같은 방 재사용 누적, 저놈 잡아라와 동시 적용, 보스 처치 후 방 안 유지,
  방·층 이탈 해제와 재방문 미발동, 레거시 스펠과 합산(공속 ×1.6), Run 종료·사망 해제, 인스턴스별 획득 기록을 검사한다.
  관련 회귀는 `VerifyWithRegressionsBatch`(Spell-1·Spell-2·Week16 Spell-1·Reward-3 포함)다. 2026-10-02 두 배치 모두
  통과·종료 코드 0을 확인했고 `Week16Reward3Verification.SetupAndVerifyBatch`, Special-4 상점, 개발 패널, Slot-0, Spell-0
  회귀도 통과했다. Special-4 검증기는 편집 모드에서 플레이어 인벤토리를 깨우지 않아 아티팩트 상품 구매 시 실패했으므로
  구매 전에 `PlayerInventory.Awake`를 호출하도록 고쳤다(이전에는 같은 seed의 첫 상품이 레거시 스펠이라 드러나지 않았다).
  수동 확인: Game Scene Play → `F1` 패널 `Drop 막판 스퍼트`로 픽업을 떨어뜨려 줍는다. 시작방·일반 전투방에서 Shift를
  누르면 슬롯에 남아야 하고, 보스방에서 사용하면 패널에 `Room ASPD +30% MS +5%`가 표시되며 공격·이동이 빨라져야 한다.
  보스방을 나가면 표시가 사라져야 한다. 보물방 선택 보상·상점 아이템 상품에 스펠이 더 이상 나오지 않아야 한다.

- Range-0 투사체 사거리(사거리 = 체공 시간 × 탄속): 메뉴 `Trickal Fan Game > Week 21 > Setup Range-0 Projectile Lifetimes`가
  원거리 적 Prefab 5종의 `projectileLifetime`만 멱등하게 맞춘다(원거리형 1.8초·빠른 원거리형 1.4초·저격형 1.3초·크레용 궁수
  1.7초·마법사 1.4초). 이전 Setup(Week7 원거리형·밸런스, Enemy-4, Spawn-2)도 같은 상수를 써서 다시 실행해도 값이 되돌아가지
  않는다. 플레이어 체공 시간은 `PlayerProjectileAttack.baseProjectileLifetime` 기본값 2/3초(사거리 약 5.3)다(Game Scene에서 덮어쓰지 않음).
  자동 검증 메뉴는 `Trickal Fan Game > Week 21 > Verify Range-0 Projectile Range`, 배치는 Unity `-batchmode -nographics -quit
  -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week21Range0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다.
  Setup 2회 GUID, 효과 타입 33·34 추가와 32 보존, 효과 검증·설명, 탄속 효과 미사용과 체공 시간 효과는 장난감 망원경만 사용
  (+30%, 세 효과 모두 설명에 표시, 획득 시 사거리 약 6.9), 적 5종 수명과 사거리(방 너비 16 미만),
  플레이어 기본 사거리 8과 탄속·체공 시간 가산 스택, 체공 시간 만료 시 즉시 소멸과 직렬화 수명 대체값, 적 투사체 만료를
  검사한다. 2026-10-02 배치 통과(종료 코드 0)를 확인했고, 투사체 회귀 `PhaseGProjectileEffectsVerification`,
  `Week7RangedEnemyVerification`, `Week7DamageContextVerification`, `Week15Enemy4Verification`, `Week19Spawn2Verification`의
  `Verify`도 통과했다. 망원경 추가 후 Range-0 배치, `PhaseGArtifactContractVerification`·`PhaseGProjectileEffectsVerification`·
  `Week13Hud3BVerification`의 `Verify`와 Backend 카탈로그 Jest도 통과했다. 아이템 설명은 이제 앞 2개가 아니라 모든 효과를 보여준다.
  수동 확인: Game Scene Play → 빈 방에서 한 방향으로 공격하면 탄이 방 너비의 1/3쯤(약 5.3)에서 페이드 없이 사라져야 하고,
  앞으로 이동하며 쏘면 조금 더, 물러나며 쏘면 조금 덜 날아가야 한다. 일반 원거리형 탄은 약 9, 저격형 탄은 약 14에서
  사라져야 하며 보스 탄막은 이전처럼 벽까지 날아가야 한다. 장난감 망원경을 얻으면 기본 공격이 약 6.9까지 날아가야 하고,
  획득 알림 설명 끝에 `사거리 +30%`가 보여야 한다.

- Chest-0 상자 3종·개봉 상태: 메뉴 `Trickal Fan Game > Week 22 > Setup Chest-0 Chest Prefab`이
  `Assets/Prefabs/TreasureChest.prefab`(Environment 레이어, 고체 `BoxCollider2D`와 full contacts Kinematic `Rigidbody2D`, `TreasureChest`, 0.8 크기 자리표시 스프라이트)을
  GUID를 보존하며 멱등 구성한다. 종류(`ChestKind` Normal 0·Golden 1·Diamond 2)와 방 내부 ID는 배치할 때 인스턴스마다
  정하고, 개봉·소멸 상태는 `RoomRunState`의 상자 기록에 남는다. 안전 위치는 `ChestPlacement.TryFindSafeLocalPosition`이
  고른다. 아직 Game Scene이나 클리어 보상에는 연결하지 않는다(Chest-1).
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Chest-0 Chests`, 배치는 Unity `-batchmode -nographics -quit
  -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week22Chest0Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. Setup 2회 GUID, enum 값, Prefab 고체 충돌체·Kinematic 바디·레이어·크기와 Environment 레이어 충돌(플레이어·적·플레이어 탄·픽업·벽),
  스크립트 시뮬레이션 1초 밀기(상자 약 1.2 vs 같은 조건 하트 약 4.25, 반대·옆 입력 미이동, 벽 앞 정지),
  플레이어 위치 회피, 모든 Template에서 내용물 7개 낙하 지점 확보,
  상자 기록의 멱등 등록·종류 고정·잘못된 ID 거부·1회 개봉·미개봉만 소멸, 일반상자 접촉 1회 개봉, 황금상자 열쇠 0개
  미개봉·열쇠 1개만 소비, 폭탄의 일반·황금 미개봉, 다이아몬드 접촉 미개봉·사거리 밖 미개봉·겹친 폭발 1회 개봉·자원
  미소비, 재구성 시 개봉 유지와 종류 불일치 거부, 소멸 상자 미복귀, Run 종료 후 미개봉, 모든 Room Template의 안전
  배치(장애물·문 통로·진입점·다른 상자 회피, 도달 가능, 같은 결과 재현, 중앙 기둥 회피)와 실제 조립 방에서
  Environment 충돌체 미중첩, 같은 층 이동 시 유지·실제 층 이동 시 미개봉 상자만 소멸을 검사한다. 관련 회귀는
  `VerifyWithRegressionsBatch`(Room-8·Obstacle-1·Special-3·Spell-2 포함)다. 2026-10-03 두 배치 모두 통과·종료 코드 0을
  확인했다. Room-8은 Special-3 뒤에 같은 프로세스에서 돌리면 남은 방 전환 쿨다운 때문에 경로 이동이 실패하므로
  회귀 순서에서 먼저 실행한다(단독 실행은 통과).
  수동 확인: 상자가 실제 Run에 나오는 Chest-1 이후 진행한다.

- Chest-1 클리어 상자·내용물: 메뉴 `Trickal Fan Game > Week 22 > Setup Chest-1 Clear Chests`가
  `Assets/Items/Drops/chest-content-table.asset`(상자 33%, 종류 75/20/5, 일반 1~3개 60/30/10·스펠 5%, 황금 2~4개,
  다이아몬드 4~6개, 구 클리어 드롭과 같은 픽업·가중치, 구현된 일회용 스펠 전부, Chest-0 상자 Prefab, Slot-0 픽업 Prefab)을
  GUID를 보존하며 멱등 구성하고 Game Scene `RoomGraphAssembler.chestContentTable`에 연결한다. 표가 있으면 전투방 클리어는
  구 드롭 표(`room-clear-drop-table`, 비교용으로 유지)를 쓰지 않는다. 개발 패널 `F1` → `— Chest —`의 `Chest Normal`·
  `Chest Golden`·`Chest Diamond`는 현재 방에 상자를 강제로 놓고 보조 Run으로 표시한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Chest-1 Clear Chests`, 배치는 Unity `-batchmode -nographics -quit
  -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week22Chest1Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. Setup 2회 GUID, 표 값과 구 드롭 가중치·픽업 일치, 스펠 목록(짱셈·레거시 제외), 잘못된 표 거부,
  20만 seed 상자 33%·종류 75/20/5, 종류별 6만 seed 개수 분포·소모품 비율·스펠 확률·seed 재현, 구 드롭 대비 자원별 기대
  지급량(로그에 표 출력, docs/21 Chest-1 비교표와 대조), 모든 층 전투방의 상자 전용 클리어 보상, 상자 없는 클리어의 1회
  추첨, 상자 1개 배치·재클리어 무시·구 드롭 없음, 같은 층 재구성 시 미개봉 상자 복원, 개봉 시 seed 내용물 1회·상자 주변
  배치, 개봉 후 재구성 시 내용물 미재지급, 층 이탈 시 미개봉 상자 소멸, 개발 상자 3종 ID·간격·개봉, 스펠 픽업 인스턴스 ID와
  벗어난 뒤 획득을 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Room-8·Resource-3·Chest-0·개발 패널 포함)다.
  Room-8·Resource-3의 클리어 드롭 검사는 상자 모드를 따르도록 갱신했다(Resource-3은 표·seed·스포너 단위 검사를 그대로
  두고 조립기 통합만 Chest-1 검사로 넘긴다). 2026-10-03 두 배치와 Chest-0 회귀 배치 모두 통과·종료 코드 0을 확인했다.
  Setup의 Game Scene 저장 때 Range-0에서 추가된 `baseProjectileLifetime` 기본값(2/3초)이 함께 직렬화되었다(값 변화 없음).
  수동 확인: Game Scene Play → 전투방 몇 개를 클리어하면 일부 방(약 1/3) 중앙 근처에 상자가 나와야 한다. 또는 `F1` 패널
  `Chest Normal`/`Chest Golden`/`Chest Diamond`로 현재 방에 상자를 놓는다. 일반상자는 닿으면 열리며 주변에 픽업 1~3개가
  떨어지고, 황금상자는 열쇠 없이 닿으면 그대로·열쇠가 있으면 1개 줄며 2~4개, 다이아몬드 상자는 닿아도 그대로이고 폭탄을
  옆에서 터뜨리면 4~6개가 떨어져야 한다. 연 상자는 어두워지고 다시 닿아도 아무것도 나오지 않아야 하며, 방을 나갔다
  들어와도 그대로여야 한다. 다음 층으로 가면 열지 않은 상자는 사라진다. 패널에 `Assisted run`이 표시되어야 한다.

- Terrain-0 구덩이 지형: 메뉴 `Trickal Fan Game > Week 22 > Setup Terrain-0 Pit Layouts`가 Physics 레이어 `Pit`(10번,
  Player·Enemy·Pickup과만 충돌)을 TagManager·2D 충돌 행렬에 등록하고, `Assets/Prefabs/RoomPit.prefab`(Pit 레이어, 고체
  `BoxCollider2D`, `RoomPit`, 테두리·구멍 자리표시 스프라이트, 정렬 -10/-9)과 새 Layout `basic-central-pit`·`large-pit-lanes`의
  Template·Prefab을 GUID를 보존하며 멱등 구성한다. Layout은 Game Scene 생성기 카탈로그에 등록하고(방 콘텐츠 버전 9),
  Encounter-4 SpawnPoint 확장과 요정마을 그림 재적용, Layout 난이도 +1을 함께 처리한다. 구덩이는 Layout 검증에서 도달성·문
  통로·SpawnPoint·상자 배치에는 장애물처럼 막히고 사선 검사에서는 제외된다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Terrain-0 Pit Layouts`, 배치는 Unity `-batchmode -quit -projectPath
  <game 경로> -executeMethod TrickalFanGame.Editor.Week22Terrain0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다.
  요정마을 그림을 다시 적용하므로 `-nographics`를 넣지 않는다. Setup 2회 GUID, `Pit` 레이어와 32개 레이어 충돌 행렬, 이동/사선
  마스크와 상자 밀기 마스크, Prefab 고체·레이어·정렬과 잘못된 트리거·레이어 거부, `SecretPit`과의 분리, 두 Layout의 프로필·
  Normal 전용·1~99층·난이도 +1·SpawnPoint 5/6개·구덩이 위치와 크기·장애물 후보 슬롯·구덩이의 사선 비차단, Layout 검증기의
  문 통로·SpawnPoint·격자·최소 크기·장애물 중첩·이동 범위·문 단절 거부, 스크립트 시뮬레이션의 Player·Enemy·Pickup 차단과
  플레이어 탄·적 탄 통과, 적 경로가 구덩이를 우회하고 사선은 통과, 상자와 내용물의 구덩이 회피, 512 seed 반복 재현과
  두 Layout의 1~3층 등장을 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Obstacle-2·Difficulty-1·Encounter-4·
  Obstacle-4·Chest-0·Chest-1 포함)다. 적 이동 마스크 변경은 `Week18Obstacle0Verification.VerifyWithEnemyRegressionsBatch`로,
  새 방 그림은 `FairyVillageArtworkVerification.Verify`(18개 Template)로 확인했다. 2026-10-03 위 배치 모두 통과·종료 코드 0.
  수동 확인: Game Scene Play → 전투방을 돌다 보면 일부 방(전투방 약 7개 중 1개)에 어두운 사각 구덩이가 있다. Basic 방은
  중앙 4×2, Large 방은 좌우 3×2 구덩이 4개와 가운데 바위 4개다. 플레이어는 구덩이 위로 걸어갈 수 없고, 구덩이 너머로 쏜
  기본 공격은 그대로 날아가야 한다. 근접 적은 구덩이를 돌아서 다가오고, 원거리 적은 구덩이 건너편에서도 사격해야 한다.
  상자는 구덩이 위에 생기지 않고, 밀어도 구덩이 안으로 들어가지 않아야 한다. 방 클리어가 멈추지 않아야 한다.

- Flight-0 시스트의 가짜 날개·비행: 메뉴 `Trickal Fan Game > Week 22 > Setup Flight-0 Fake Wings`가
  `Assets/Items/artifact-sist-fake-wings.asset`(Epic 아티팩트, 효과 35 `Flight`, 최대 1스택)을 GUID를 보존하며 멱등 구성하고,
  `chest-content-table`의 황금 전용 아티팩트 풀(`ItemPickup` Prefab 포함)에 넣는다. Game Scene 선택 보상 풀에 황금 전용
  아티팩트가 있으면 빼고(Reward-3 Setup도 `GoldenChestExclusivePool` 목록을 제외), 이름·설명 글리프를 추가한다. Chest-1을 먼저
  실행해야 한다. 개발 패널 `F1` → `— Chest —` 아래 `Golden exclusive`에 비행 여부와 `Drop 시스트의 가짜 날개`(보조 Run)가 있다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Flight-0 Fake Wings`, 배치는 Unity `-batchmode -quit -projectPath
  <game 경로> -executeMethod TrickalFanGame.Editor.Week22Flight0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다.
  Setup 2회 GUID, 효과 35 번호, 아이템 계약·ID 접두사·설명 한 줄·글리프, 황금 전용 풀 단독 소속과 잘못된 풀(스펠·중복·
  Prefab 없음) 거부, 선택 보상 풀 제외, 프로젝트 전체 직렬화 파일 중 날개 GUID 참조가 상자 표 하나뿐인지, Game Scene
  플레이어의 비행 미직렬화·`Pit` 미제외와 정적 비행 상태 없음(새 Run은 걸어서 시작), 이동 분류(장애물 2종만 낮은 장애물,
  벽·문·상자·비밀 통로 벽 제외), 스크립트 시뮬레이션에서 걷기는 구덩이·장애물 2종·벽·상자에 막히고 비행은 구덩이·장애물 2종만
  통과·벽·상자는 막힘, 적은 계속 구덩이에 막힘, 장애물 안에서 시작한 비행 플레이어 미밀림, 정렬 +2·그림자 1개, 인벤토리
  획득 시 비행 시작·Environment 전체 미제외·2번째 획득 거부·Run 기록 1회, 비행 중 적 탄·폭탄 자해 피해 유지, 폭탄의 구덩이
  위 거부·미소비와 장애물 2종 위 최근접 보행 지점(옆 구덩이도 회피), 적이 구덩이·장애물 위 플레이어 앞 가장자리까지 와서
  대기를 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Terrain-0·Chest-0·Chest-1·Special-2·Special-3·Reward-3·HUD-3B·
  Obstacle-0 포함)다. Obstacle-0의 "막힌 목표는 직선 대체" 검사는 D3 결정에 따라 "가장 가까운 도달 지점에서 대기"로 바꿨다.
  적 경로 변경은 `Week18Obstacle0Verification.VerifyWithEnemyRegressionsBatch`, 패널은 `Week20DevPanelVerification.Verify`로
  확인했다. 2026-10-03 위 배치 모두 통과·종료 코드 0, Backend Jest 132개·빌드 통과.
  수동 확인: Game Scene Play → `F1` → `Drop 시스트의 가짜 날개` → 오른쪽 픽업을 주우면 획득 알림이 뜨고 발밑에 그림자가
  생기며 패널에 `flying yes`가 보여야 한다. 구덩이 방에서 구덩이 위로, 바위 위로 지나갈 수 있어야 하고 벽·문·상자는 막혀야
  한다. 구덩이 위에서 `F`는 아무 일도 없고 폭탄 수가 그대로여야 하며, 바위 위에서 `F`를 누르면 폭탄이 바위 옆 바닥에 놓여야
  한다. 근접 적은 구덩이 가장자리까지 와서 멈추고, 원거리 적은 계속 쏴야 한다. 적 탄과 내 폭탄에 피해를 받아야 한다.
  비밀방 구덩이 위로 날아가면 바로 비밀방으로 떨어져야 한다. Run을 끝내고 새 Run을 시작하면 그림자 없이 걸어서 시작해야 한다.

- Chest-2 황금 특별 보상·다이아몬드 스펠: 메뉴 `Trickal Fan Game > Week 22 > Setup Chest-2 Special Rewards`가 Chest-1의 표
  구성(`Week22Chest1Setup.EnsureTable`)을 다시 실행해 `chest-content-table`의 종류 규칙에 황금 `specialRewardChance` 0.25,
  다이아몬드 `spellChance` 1·`jjangsemShare` 0.25를 넣고, 활성·유효한 짱셈스펠 에셋을 `jjangsemSpells`에 모은다(현재 0개).
  GUID와 Flight-0의 황금 전용 풀은 보존한다. 전용 풀이 비어 있으면 실패하므로 Chest-1 → Flight-0 → Chest-2 순서로 실행한다.
  Game Scene은 바꾸지 않는다. 짱셈스펠을 새로 구현하면 이 Setup을 다시 실행한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Chest-2 Special Rewards`, 배치는 Unity `-batchmode -nographics -quit
  -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week22Chest2Verification.SetupAndVerifyBatch
  -logFile <로그 경로>`다. Setup 2회 GUID, 종류별 규칙 값(황금 25%가 20~30% 안), 풀별 허용 분류(전용 풀은
  `GoldenChestExclusivePool`의 유효 아티팩트, 스펠 목록은 일회용 스펠, 짱셈 목록은 구현된 짱셈스펠)와 잘못된 표(일반·
  다이아몬드의 특별 보상, 황금의 짱셈 비율, 짱셈 목록의 스펠·중복) 거부, 종류별 6만 seed의 특별 보상 25%·일반/다이아몬드
  0%·다이아몬드 스펠 100%·seed 재현, 임시 짱셈스펠을 넣은 복사본의 다이아몬드 짱셈 비율 25%와 일반·황금 미등장·소모품과
  당첨 불변, 실제 클리어 경로의 황금상자 당첨 시 전용 아티팩트 픽업 1개 + seed 소모품·상자 주변 배치·재개봉과 층 재구성 시
  미재지급, 미당첨 상자의 소모품 전용, 최대 스택 보유 시 특별 보상 미지급·소모품 유지, 다이아몬드 상자의 스펠 픽업 1개
  (인스턴스 ID·벗어난 뒤 획득)와 소모품 4~6개를 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Chest-1·Chest-0·개발
  패널·Flight-0 포함, Flight-0 때문에 `-nographics` 없이 실행)다. Chest-1 검증의 다이아몬드 스펠 확률(0 → 1)과 클리어당 스펠
  기대값(0.0124 → 0.0289)을 함께 갱신했다. 2026-10-05 두 배치 모두 통과·종료 코드 0.
  수동 확인: Game Scene Play → `F1` → `+10 Key`·`+10 Bomb` → `Chest Golden`을 여러 번 눌러 상자를 열면 약 4개 중 1개에서
  소모품과 함께 `시스트의 가짜 날개` 픽업이 나와야 한다. 주우면 비행이 시작되고, 그 뒤에 여는 황금상자에서는 날개가 더
  나오지 않고 소모품만 나와야 한다. `Chest Diamond` 옆에서 폭탄을 터뜨리면 소모품 4~6개와 스펠 픽업 1개가 항상 나와야 하고,
  스펠은 한 번 벗어났다가 다시 닿으면 슬롯에 들어와야 한다. 연 상자는 다시 닿거나 방을 나갔다 와도 아무것도 나오지 않아야 한다.
  2026-10-05 사용자 화면 확인 완료.

- Jjangsem-0 빅우드의 열매: 메뉴 `Trickal Fan Game > Week 22 > Setup Jjangsem-0 Bigwood Fruit`가
  `Assets/Items/jjangsem-bigwood-fruit.asset`(Rare 짱셈스펠, 효과 36 `ReduceAndRecoverDamageTaken`: `magnitude` 1 = 피해
  감소 반 칸, `integerAmount` 2 = 추가 회복 1칸, `intervalSeconds` 2, `durationSeconds` 10)을 GUID를 보존하며 멱등 구성하고,
  이름·설명 글리프를 추가한 뒤 Chest-1의 표 구성을 다시 실행해 `chest-content-table.jjangsemSpells`에 넣는다. Game Scene은
  바꾸지 않는다. Backend `ITEM_CATALOG`에 같은 ID·등급·효과를 추가했으므로 로컬 DB는 `backend/`에서 seed를 다시 실행해야
  이 아이템을 가진 Run이 저장된다(없으면 `ITEM_NOT_FOUND`).
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Jjangsem-0 Bigwood Fruit`, 배치는 Unity `-batchmode -quit -projectPath
  <game 경로> -executeMethod TrickalFanGame.Editor.Week22Jjangsem0Verification.SetupAndVerifyBatch -logFile <로그 경로>`다
  (글리프 추가 때문에 `-nographics` 없이 실행). Setup 2회 GUID, 효과 36 번호와 필드 검증, 아이템 계약·ID 접두사·설명·글리프,
  표의 짱셈 목록 단독 소속과 다이아몬드 상자 등장·선택 보상 풀 제외, `Health`의 피해 감소(방어막보다 먼저, 해제·초기화),
  사용 시 10초 창과 소비, 1칸 피해 → 반 칸 피해 → 2초 뒤 1.5칸 회복, 일시정지 중 정지, 방·층 이동 유지, 방어막 전부 흡수 시
  미회복·방어막 관통분만 회복, 피해별 개별 회복과 최대 HP 상한, 10초 뒤 피해의 미감소·미회복과 예약 회복 도착, 재사용 시
  10초 갱신·감소 미중첩, Run 종료와 사망 시 효과·예약 회복 제거(사망 방지 없음), 스펠과의 슬롯 교체·일시정지 미사용·1회
  소비·인스턴스별 획득 기록을 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Slot-0·Spell-0~3·Chest-2·Chest-1·개발
  패널 포함)다. 2026-10-05 두 배치 통과·종료 코드 0, Backend Jest 133개 통과.
  수동 확인: Game Scene Play → `F1` → `— Spell slot —`의 `Drop 빅우드의 열매` → 픽업을 주우면 슬롯 HUD에 보라색 짱셈스펠로
  표시되어야 한다. 전투방에서 Left Shift로 사용하면 슬롯이 비고 패널에 `Fruit 10.0s`가 줄어들어야 한다. 그동안 적에게
  맞으면 하트가 평소보다 반 칸 덜 줄고, 약 2초 뒤 줄어든 양보다 1칸 더 회복되어야 한다. 10초가 지난 뒤 맞으면 평소대로
  줄고 회복되지 않아야 한다. `Chest Diamond`를 여러 번 열면 약 4개 중 1개에서 스펠 대신 빅우드의 열매가 나와야 한다.
  2026-10-05 사용자 화면 확인 완료.

- Jjangsem-1 멜룬카드: 메뉴 `Trickal Fan Game > Week 22 > Setup Jjangsem-1 Melune Card`가
  `Assets/Items/jjangsem-melune-card.asset`(Rare 짱셈스펠, 효과 41 `DuplicateRoomChestsAndPickups`, 수치 필드 없음)을 GUID를
  보존하며 멱등 구성하고, 이름·설명 글리프를 추가한 뒤 Chest-1의 표 구성을 다시 실행해 `chest-content-table.jjangsemSpells`에
  넣는다. Game Scene 플레이어의 `PlayerSingleUseEffects.chestContentTable`에 같은 표를 연결한다(이미 연결돼 있으면 Scene을
  저장하지 않음). Backend `ITEM_CATALOG`에도 추가했으므로 로컬 DB는 `backend/`에서 `pnpm prisma:seed`를 다시 실행한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Jjangsem-1 Melune Card`, 배치는 Unity `-batchmode -quit -projectPath
  <game 경로> -executeMethod TrickalFanGame.Editor.Week22Jjangsem1Verification.SetupAndVerifyBatch -logFile <로그 경로>`다
  (글리프 추가 때문에 `-nographics` 없이 실행). Setup 2회 아이템·표·Scene GUID, 효과 41 번호와 무수치 검증, 아이템 계약·ID
  접두사·설명·글리프, 표의 짱셈 목록 단독 소속과 다이아몬드 상자 등장·선택 보상 풀 제외·Scene 플레이어의 표 연결, 복제할
  것이 없는 방에서 미소비, 미개봉 3종 상자의 같은 종류 복제(`melune-chest-01~03`)·열린 상자와 아이템 픽업·지갑 제외, 바닥
  소모품(하트·SP·골드·열쇠·폭탄과 상자 드롭)의 같은 종류 복제, 상자 몸체 미겹침·도달 가능 배치, 복제 상자별 내용물 seed
  기록·원본과 다른 seed, 황금 복제의 열쇠 1개 소비·다이아몬드 복제의 폭탄 개봉과 원본 미개봉, 복제 상자의 seed 내용물 드롭,
  복제 골드의 개별 획득, 두 번째 사용에서 이전 복제물 포함·같은 사용의 복제물 제외(대상 고정)·다음 ID 번호, 재방문 유지,
  층 재구성 시 모든 상자의 위치·열림 상태 복원과 미재지급, 층 이탈 시 미개봉 복제 상자 소멸, 상자의 대각선 밀기·벽을 따라
  미끄러짐·스치는 입력 무시·벽에 붙은 상자의 벽을 따라/벽 반대 밀기·붙은 하트 밀어내기·벽에 막힌 하트 앞 정지를 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Chest-0·Chest-1·Chest-2·Jjangsem-0·Slot-0·
  개발 패널 포함)다. 2026-10-05 두 배치(Spell-5 단독 검증 포함) 통과·종료 코드 0, Backend Jest 134개 통과.
  수동 확인: Game Scene Play → `F1` → `+10 Key`·`+10 Bomb` → `Chest Normal`·`Chest Golden`·`Chest Diamond`로 상자를 놓고,
  `— Spell slot —`의 `Drop 멜룬카드`로 카드를 주워 Left Shift로 사용한다. 각 상자 옆에 같은 색의 상자가 하나씩 더 생겨야 하고,
  복제 황금상자는 열쇠 1개, 복제 다이아몬드 상자는 폭탄으로 열려야 하며 원본과 다른 내용물이 나올 수 있어야 한다. 바닥에
  하트·SP·골드 등이 있으면 옆에 같은 픽업이 하나씩 생겨야 한다. 상자와 픽업이 없는 방에서는 사용되지 않고 슬롯에 남아야
  한다. 상자를 비스듬히 밀면 4방향이 아니라 플레이어 반대쪽으로 밀려야 한다. 2026-10-05 사용자 화면 확인 완료. 이때 찾은
  벽에 붙은 상자·하트에 붙은 상자가 밀리지 않던 문제를 고쳤다. 벽에 붙인 상자를 벽을 따라 밀고, 하트를 상자 앞에 두고 밀어
  하트가 함께 밀리는지 다시 확인한다.

- Spell-5 소형 스펠 4종: 메뉴 `Trickal Fan Game > Week 22 > Setup Spell-5 Small Spells`가 `Assets/Items/`에
  `single-spell-armor-festival-invitation`(Rare, 효과 37 `GainShield` `magnitude` 4), `single-spell-amelia-love-letter`
  (Uncommon, 효과 38 `SpawnHealthPickups` `integerAmount` 2), `single-spell-random-coin`(Common, 효과 39 `GainRandomGold`
  `integerAmount` 2·`magnitude` 10), `single-spell-decisive-strike`(Uncommon, 효과 40 `CurrentRoomCriticalBonus`
  `magnitude` 0.5 = 치명타 피해·`secondaryMagnitude` 0.15 = 치명타 확률)를 GUID를 보존하며 멱등 구성한다. Game Scene 플레이어의 `PlayerSingleUseEffects.heartPickupPrefab`에
  `HealthPickup.prefab`을 연결하고(이미 연결돼 있으면 Scene을 저장하지 않음), 글리프를 추가한 뒤 Chest-1의 표 구성을 다시
  실행해 상자 스펠 풀에 넣는다. Backend `ITEM_CATALOG`에도 추가했으므로 로컬 DB는 `backend/`에서 `pnpm prisma:seed`를 다시
  실행한다.
  자동 검증 메뉴는 `Trickal Fan Game > Week 22 > Verify Spell-5 Small Spells`, 배치는 Unity `-batchmode -quit -projectPath
  <game 경로> -executeMethod TrickalFanGame.Editor.Week22Spell5Verification.SetupAndVerifyBatch -logFile <로그 경로>`다.
  Setup 2회 GUID, 효과 37~40 번호와 필드 검증, 4종의 계약·설명·글리프, Scene 플레이어의 하트 Prefab, 상자 스펠 목록 편입과
  선택 보상 풀 제외, 방어막 2칸 획득·기존 방어막 합산·일시정지 미사용, 하트 Prefab 미설정 시 미소비·가득 찬 HP에서 사용·
  1.1 거리의 서로 다른 빈 지점 2곳·막힌 지점 회피·HP 부족 시 1칸 회복, 골드 2~10 범위와 변동·범위 밖 추첨 보정·지갑 상한
  잘림·가득 찬 지갑에서 미소비, 치명타 확률 +15%p·피해 +50%p의 시작방·클리어방 거부·같은 방 합산·이탈/재방문/사망 해제·치명타 확률과
  기본 공격 피해 불변을 검사한다. 관련 회귀는 `VerifyWithRegressionsBatch`(Slot-0·Spell-0~3·Jjangsem-0·Chest-2·Chest-1·개발
  패널 포함)다. 2026-10-05 두 배치 통과·종료 코드 0, Backend Jest 133개 통과.
  수동 확인: Game Scene Play → `F1` → `— Spell slot —`의 `Drop ...` 버튼으로 각 스펠을 떨어뜨려 줍고 Left Shift로 사용한다.
  갑옷축제 초대장은 방어막이 2칸 늘어야 한다. 아멜리아의 러브레터는 캐릭터 옆에 하트 2개가 생기고, HP가 가득하면 주워지지
  않고 밀려야 한다. 랜덤코인은 골드가 2~10 늘어야 한다. 회심의 일격은 시작방에서는 사용되지 않고 슬롯에 남아야 하며,
  전투 중인 방에서 사용하면 패널에 `Room CRIT +15% DMG +50%`가 보이고 방을 나가면 사라져야 한다.
  2026-10-05 사용자 화면 확인 완료.

- 아직 구현되지 않은 도구의 명령과 경로는 이 문서에 확정된 사용법으로 기록하지 않는다.
- 도구가 구현되고 검증되면 실행 위치, 명령 또는 Unity 메뉴, 입력, 기대 결과와 대표 오류 해결 방법을 이 섹션에 추가한다.
- 개발 도구의 실행 실패가 게임 진행을 멈추는지 여부와 실패 종료 코드를 명확히 기록한다.

---

요정마을 타일·벽 적용: `Trickal Fan Game > Artwork > Apply Fairy Village Tiles and Walls`를 실행하고
새 Run을 시작한다. `Verify Fairy Village Tiles and Walls`는 모든 방 템플릿의 그림 참조와 문 상태·포탈
장벽을 검사한다. 반복 적용과 Collider·GUID 보존은 배치 진입점
`TrickalFanGame.Editor.FairyVillageArtworkVerification.SetupAndVerifyBatch`로 검사한다.
메뉴의 세부 범위·이전 배경과의 관계·Play 확인 항목은 [요정마을 Artwork 안내](./22-fairy-village-artwork.md)를 따른다.
2026-10-02 연결형 v3 에셋으로 교체했다. `ApplyVerifyAndRenderBatch`는 반복 적용과 기존 방 이동 검증 뒤
기본·작은·넓은·세로·큰 방 및 문 상태별 Unity 렌더를 `output/`에 저장한다. 그래픽 장치가 필요하므로
이 렌더 진입점에서는 `-nographics`를 사용하지 않는다.

### 일반 적 이동 모션

기존 4종 프리팹에 이동 모션이 연결되어 있다. 새 Run에서 불효자손은 통통 튀고,
산사모·저혈당 요정·고혈당 요정은 걷는다. 새 에셋을 적용할 때는 다음 메뉴를 사용한다.

- 적용: `Trickal Fan Game > Artwork > Setup Enemy Movement Animations`
- 자동 검증: `Trickal Fan Game > Artwork > Verify Enemy Movement Animations`
- 렌더 미리보기: `Trickal Fan Game > Artwork > Export Enemy Movement Preview`

배치 진입점은 `TrickalFanGame.Editor.EnemyMovementAnimationVerification.SetupAndVerifyBatch`이며,
반복 적용, 걷기 4프레임의 순서·루프·크기·기준점, 원본 Idle 복원, 물리 판정 보존,
벽·공격·넉백 억제와 재활성화를 검사한다. 걷기는 메시 변형 없이 기존 SpriteRenderer로 재생하며,
프레임 에셋은 `game/Assets/Art/Enemies/FairyKingdom/Walking/`에 있다.
`ExportPreview` 진입점은 같은 검증 후 `game/Logs/EnemyMovementPreviewV2/`에 16개 PNG를 저장한다.
렌더 미리보기에는 그래픽 장치가 필요하므로 `-nographics`를 사용하지 않는다.

Play Mode 수동 확인: 새 Run에서 각 적이 실제로 이동할 때만 모션을 재생하고, 정지·피격·공격 예고
중에는 원래 자세로 돌아오는지 확인한다. 방을 재방문해 그림이 사라지거나 두 번 표시되지 않는지도
확인한다. 움직임 크기와 보폭은 각 프리팹의 `EnemyMovementAnimator`에서 조정할 수 있다.

### 일반 적 공격 모션

불효자손은 엎어지며 공격하고, 산사모는 꽃줄기를 뒤로 감았다가 앞으로 휘두른다.
저혈당 요정은 웅크린 준비 후 프라이팬을 앞으로 내밀며 돌진하고, 고혈당 요정은 파를 들어
던진다. 기존 전투의 Telegraph·Active·Recovery에 실제 포즈 PNG를 연결했다. 공격 단계는 이동
모션보다 우선하며 목표/잠긴 돌진·발사 방향에 따라 좌우 반전한다. 루트 배율과 Collider는 고정이다.
고혈당 요정의 실제 발사는 별도 파 PNG를 사용하고, 파 색상을 유지하며 발사 방향으로 회전한다.
파는 200 PPU로 등록해 최초 적용보다 표시 크기를 2배 키웠다. 투사체 루트 배율 0.3과 원형 판정의
월드 반지름 0.15는 유지한다. 피해량·발사 속도·수명은 기존 값을 유지한다. 졸개에는 이 그림/파가 상속되지 않는다.

- 적용: `Trickal Fan Game > Artwork > Setup Enemy Attack Animations` (이동 모션 적용 이후)
- 검증: `Trickal Fan Game > Artwork > Verify Enemy Attack Animations`
- 미리보기: `Trickal Fan Game > Artwork > Export Enemy Attack Preview`
- 배치: `TrickalFanGame.Editor.EnemyAttackAnimationVerification.SetupAndVerifyBatch`
- 에셋: `Assets/Art/Enemies/FairyKingdom/Attacking/`; [프롬프트 기록](./art-prompts/enemy-attack.md)

Play Mode에서 새 Run의 네 적을 만나 준비→공격→회복→이동 연결과 좌우 방향을 확인한다.
특히 산사모의 꽃 끝과 실제 근접 판정 범위, 저혈당 요정의 긴 돌진 자세, 고혈당 요정의 손에서
파가 사라지는 시점과 실제 투사체 가독성을 확인한다. 프리팹 반영 전 이미 소환된 개체는 새 Run에서 확인한다.

### 보스 이동 모션

기존 부스러기·새마음금고는 띠용띠용 이동, 크레용사용은 망토·팔이 조금씩 움직이는 4포즈 걷기를 사용한다.
새마음금고의 바닥 보물은 본체의 작은 도약과 공격 점프 중 바닥에 남고 착지 때 압축·반동한다.

- 적용: `Trickal Fan Game > Artwork > Setup Boss Movement Animations`
- 자동 검증: `Trickal Fan Game > Artwork > Verify Boss Movement Animations`
- 렌더 미리보기: `Trickal Fan Game > Artwork > Export Boss Movement Preview`

배치 진입점은 `TrickalFanGame.Editor.BossMovementAnimationVerification.SetupAndVerifyBatch`다.
반복 Setup과 GUID 보존, 표시·물리 분리, 정지·패턴 억제, 재활성화, 실제 새마음금고 점프와 착지 반동,
소환 적 외형 보존, 일반 적 및 Boss-0~3·HUD-5 회귀를 확인한다. 회귀 전에 Game Scene을 열되 저장하지 않는다.
`ExportPreview`는 같은 검증 후 `game/Logs/BossMovementPreview/`에 이동 32장과 공격 점프 36장을 저장한다.
그래픽 장치가 필요하므로 렌더 진입점에는 `-nographics`를 사용하지 않는다.
에셋은 `game/Assets/Art/Bosses/FairyKingdom/Movement/`에 있다.

Play Mode 수동 확인: 새 Run의 보스방 또는 BossTestScene에서 본체가 이동할 때만 주기가 진행하고,
새마음금고의 보석은 체공 중 바닥에 남으며 착지 때 들썩여야 한다. 크레용사용의 대시·내려베기·황금 변신
예고와 소환 적 외형이 정상인지, 방 재방문 때 그림이 중복되지 않는지 확인한다.
움직임 크기와 보폭은 보스 프리팹의 `BossMovementAnimator`에서 조정한다.

금고 공격 점프는 바닥 준비 → 체공 → 바닥 착지 압축 → 복원/다음 준비 순서다. 체공 중에는 눌리지 않고,
스케일 변화에도 바닥 기준점을 유지한다. 착지 피해와 보석 반동은 표시상 바닥에 닿는 순간에 발생한다.
부스러기·금고의 작은 도약도 경계 속도를 부드럽게 연결하고 표시를 보간한다. 보스 이동 자동 검증은
곡선 경계의 연속성·준비와 착지 바닥 고정·체공 자세를 포함한다.

비교용 임시 상태(2026-10-03): 사용자 요청으로 `BossMovementAnimator`의 본체 가로·세로 변형식과
금고 패턴의 `ApplyScale` 배율 적용을 주석 처리했다. 본체는 원본 비율을 유지하며 점프 높이·이동·
방향 반전·금고 보석 반동은 그대로다. 따라서 위의 준비/착지 압축은 현재 표시되지 않는다.
주석 처리한 기존 식은 복원용으로 코드에 남아 있고 검증기는 현재 원본 비율 유지 상태를 확인한다.

2026-10-04 현재 상태: 부스러기·금고는 실제 생성한 4개 도약 포즈 에셋을 재생한다. 본체 런타임 스케일
변형은 계속 비활성화하며, 그림 안의 크림/팔/뚜껑/슬라임 변화가 준비·체공·착지를 표현한다.
`Setup Boss Movement Animations`가 `Buseureogi_Hop_0..3.png`·`Vault_Hop_0..3.png`를 등록한다.
금고 공격은 준비·체공·착지 상태에 맞는 프레임을 직접 선택하고, 실제 높이와 이동은 런타임이 처리한다.
`Export Boss Movement Preview`는 `game/Logs/BossDrawnHopPreview/`에 이동 120장(0.025초 간격,
부스러기·금고는 프리팹의 1페이즈 이동 속도)과 금고 공격 36장을 출력한다. 정규화된 카메라로 포즈를
비교하는 미리보기이며 실제 게임 카메라 화면은 아니다. 자동 검증에 고유 4포즈와 실제 공격 포즈 선택을
포함한다. 새 Run에서 띠용 이동·공격 착지·보석 반동·좌우 반전·히트박스 보존을 확인한다.

일반 적과 보스 모두 실제 좌우 이동 방향에 따라 그림을 반전한다. 정지·수직 이동·방 배치 이동은
마지막 방향을 유지하고, 일반 적의 넉백은 방향을 바꾸지 않는다. 새마음금고는 본체와 바닥 보물 레이어가
같은 방향으로 반전한다. Collider와 루트 Transform은 반전하지 않는다.

플레이어는 기본 공격 방향키를 누르는 동안 이동보다 공격 방향을 우선해 바라본다. 발사 간격 중에도
유지하며 제자리 공격에는 해당 방향의 첫 포즈를 사용한다. 공격 키를 놓으면 이동 방향을 다시 따른다.
궁극기 대시와 공격이 차단된 상태에서는 기존 행동 방향을 유지한다. 원거리·근접 기본 공격의 입력은
같은 방향 판독을 사용한다. `Tools > Trickal > Verify Erpin Walk Animation`에서 좌우 반대 이동·공격,
위쪽 공격 포즈, 제자리 공격, 공격 해제·비활성화, 근접 공격과 궁극기 대시를 자동 확인한다.
2026-10-03 배치 `TrickalFanGame.Editor.ErpinWalkAnimationVerification.Verify` 통과·종료 코드 0
(`game/Logs/player-attack-facing-verification.log`). 실제 Play Mode에서는 D+←, A+→를 누르고 이동과
탄 방향을 유지하면서 몸만 공격 방향으로 향하는지 확인한다.

### 크레용사용 소환 졸개 걷기

궁병·마법사·도끼병·방패병은 각자 4개의 실제 걷기 포즈를 사용한다. 에셋은
`game/Assets/Art/Bosses/FairyKingdom/Movement/Minions/`의 640×640 PNG, 400 PPU다.

- 적용: `Trickal Fan Game > Artwork > Setup Crayon Minion Movement Animations`
- 검증: `Trickal Fan Game > Artwork > Verify Enemy Movement Animations` (일반 적과 졸개 8종)
- 미리보기: `Trickal Fan Game > Artwork > Export Crayon Minion Movement Preview`

일반 적 Setup도 네 졸개의 전용 프레임 연결을 함께 적용한다. 각 졸개의 원본 Sprite와 Collider·AI·소환
구성은 보존한다. `EnemyMovementAnimationVerification.ExportMinionPreview` 배치는 일반 적·보스 Setup,
모션 검증과 Boss-0~3·HUD-5 회귀 후 `game/Logs/CrayonMinionMovementPreview/`에 16개의 포즈 PNG를
출력한다. 그래픽 장치가 필요하므로 `-nographics`를 넣지 않는다. 2026-10-04 배치 통과·종료 코드 0.
포즈 확인용 미리보기이며 실제 게임 카메라·이동 속도 영상은 아니다. Play Mode에서는 새 Run 또는
BossTestScene의 크레용사용 소환 후 네 졸개가 이동할 때만 걷고, 좌우로 반전하며 공격 예고·정지·넉백
중 원본 그림으로 돌아오는지 확인한다. 방 재활성화 때 그림이 중복되지 않아야 한다.

## 18. 참고 자료

- Node.js Releases: https://nodejs.org/en/about/previous-releases
- pnpm Installation: https://pnpm.io/installation
- Unity 6 Support: https://unity.com/releases/unity-6/support
- NestJS Modules: https://docs.nestjs.com/modules
- Prisma ORM: https://www.prisma.io/docs/orm
- Next.js App Router: https://nextjs.org/docs/app
- Supabase CLI: https://supabase.com/docs/reference/cli/getting-started

### 크레용사용 소환 졸개 공격

궁병은 몸을 굽히며 시위·화살을 당기고 발사, 마법사는 지팡이를 들어 시전, 도끼병은 높이 들고
앞으로 휘두르기, 방패병은 양손으로 방패를 올리고 내린 뒤 복귀한다. 기존 준비·공격·회복 단계에
전용 그림을 선택하며 원거리 두 졸개는 전용 화살·마법탄을 발사한다. 이동 프레임보다 공격 포즈가
우선한다. 본체 배율·공격 시간·피해·Collider와 투사체 원형 판정 반경 0.15는 유지한다.

- 적용: `Trickal Fan Game > Artwork > Setup Crayon Minion Attack Animations`
- 검증: `Trickal Fan Game > Artwork > Verify Enemy Attack Animations` (일반 적과 졸개 8종)
- 미리보기: `Trickal Fan Game > Artwork > Export Crayon Minion Attack Preview`
- 배치: `-executeMethod TrickalFanGame.Editor.EnemyAttackAnimationVerification.ExportMinionPreview`
- 에셋·프롬프트: [졸개 공격 기록](./art-prompts/crayon-minion-attack.md)

2026-10-05 배치 종료 코드 0 (`game/Logs/crayon-minion-attack-retry.log`). 반복 구성, 포즈 선택,
방향·이동 우선순위·넉백 복원, 실제 화살/마법탄 발사와 속도·판정 보존, 기존 전투 및 Boss-0~3·HUD-5
회귀를 통과했다. Unity 렌더는 `game/Logs/CrayonMinionAttackPreview/phase-0..3.png`에 저장한다.
GIF는 포즈 비교용이며 실제 게임의 공격 시간과 다르다. Play Mode에서 새 Run 또는 BossTestScene의
크레용사용 소환 뒤 준비→공격→회복→걷기 연결, 양손 방패 움직임과 좌우 발사 방향을 확인한다.
궁병 공격은 사용자 참고 영상에 맞춰 몸체 자체가 활처럼 휘는 방식으로 정정한다.
준비 때 몸이 C자로 휘고 몸 뒤의 시위·화살이 당겨진 뒤, 발사 때 몸·시위가 복원된다.
팔로 별도 활을 당기는 포즈는 사용하지 않는다.
### 에르핀 기본공격·저학년 구체

두 투사체에 노란색 원형 에너지 구체를 적용한다. 기본공격은 표시·루트 배율을 0.5→0.4,
히트박스 반경을 0.25→0.20으로 20% 줄였다. 저학년 그림은 기본공격보다 40% 크게 보이지만
접촉 반경 0.14와 폭발 반경 1.25는 유지한다. 저학년 초기 Setup도 새 그림을 유지한다.

- 적용: `Trickal Fan Game > Artwork > Setup Erpin Projectile Artwork`
- 검증: `Trickal Fan Game > Artwork > Verify Erpin Projectile Artwork`
- 배치: `-executeMethod TrickalFanGame.Editor.ErpinProjectileArtworkSetup.SetupAndVerifyBatch`
- [에셋·프롬프트·크기 기록](./art-prompts/erpin-projectiles.md)

2026-10-05 배치 종료 코드 0 (`game/Logs/erpin-projectile-artwork.log`): 반복 구성, 표시·판정 크기,
공통 투사체 판정·저학년·아이템 투사체 효과·사거리 회귀 통과. Play Mode에서 새 Run의 방향키
기본공격과 Space 저학년을 발사해 노란 구체의 크기 차이와 좁은 틈 통과를 확인한다.
### 에르핀 고학년 돌격·넘어짐 모션

Q 고학년 돌격에서 한 손으로 지팡이를 든 준비·달리기 그림을 재생한다. 적/보스와 충돌하면
기존 충돌 회복 시간 안에 뒤로 반동→넘어짐→누움→일어나기를 재생한다. Q 취소·시간 종료와
벽 충돌은 기존 전투 규칙을 따르며 충돌 넘어짐 포즈를 재생하지 않는다. 고학년이 걷기·저학년
그림보다 우선한다. 본체/Collider·넉백·피해·무적·속도·회복 시간은 변경하지 않는다.

- 적용: `Trickal Fan Game > Artwork > Setup Erpin High Grade Artwork` (Resources 가져오기, Scene 재구성 없음)
- 검증: `Trickal Fan Game > Artwork > Verify Erpin High Grade Artwork`
- 배치: `-executeMethod TrickalFanGame.Editor.ErpinHighGradeArtworkSetup.SetupAndVerifyBatch`
- [에셋·프롬프트·단계 기록](./art-prompts/erpin-one-hand-high-grade.md)

2026-10-05 고학년 그림 및 걷기·고학년·저학년 회귀 통과 (`game/Logs/erpin-one-hand-high-grade.log`, 종료 코드 0).
Play Mode에서 새 Run의 Q 돌격·WASD 방향 전환·적/보스 충돌과 회복, Q 취소·시간 종료를 확인한다.
`game/Logs/ErpinHighGradePreview/erpin-one-hand-complete.gif`는 포즈 비교용이다.