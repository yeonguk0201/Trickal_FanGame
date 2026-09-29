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
- Resource-1 엘리프·열쇠·폭탄 픽업: 메뉴 `Trickal Fan Game > Week 17 > Setup Resource-1 Run Resource Pickups`가 `Assets/Prefabs/ElifPickup.prefab`, `KeyPickup.prefab`, `BombPickup.prefab`을 GUID를 보존하며 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week17Resource1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID·컴포넌트 단일성, 픽업 1개 획득·99 상한과 초과분 버림·99일 때 미획득·단일 지급·플레이어 외 충돌 무시·Run 종료 후 미획득·`ResetProgress`와 새 Run의 0개 시작을 검사한다. 수동 확인은 `Trickal Fan Game > Debug > Open or Create Item Test Room`으로 프리팹을 연결한 뒤 Play하고, 패널의 `Spawn Elif/Key/Bomb`으로 1개씩 늘어나는지, `+98 All` 후 한 번 더 주워 99에서 멈추고 그다음 픽업은 밀리기만 하는지, 적과 투사체가 픽업을 통과하는지 확인한다.
- Resource-3 방 클리어 드롭: 메뉴 `Trickal Fan Game > Week 17 > Setup Resource-3 Room Clear Drops`가 `Assets/Items/Drops/room-clear-drop-table.asset`(33%, 하트 30·SP 30·엘리프 20·열쇠 12·폭탄 8)을 멱등 구성하고 Game Scene의 `RoomGraphAssembler`에 연결하며, 삭제한 `PlayerSPDropper`가 남긴 Missing Script를 Game·Item Test·Boss Test·Boss-2 Test Scene의 플레이어에서 제거한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week17Resource3Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 표 계약, 30000 seed의 드롭률·후보 비율과 seed 재현, 클리어 1회 추첨·재방문/드롭 없음 결과의 재추첨 방지, 실제 Game Scene assembler의 방별 드롭 seed와 층 재로드 후 재추첨 방지, 자원 드롭의 Run 연결, SP 드롭퍼 제거와 Phase C 회귀를 검사한다. 수동 확인은 Game Scene에서 전투방 여러 개를 클리어해 약 3번에 1번꼴로 방 중앙에 하트·SP·엘리프·열쇠·폭탄 중 하나가 떨어지는지, 적 처치로는 SP가 나오지 않는지, 클리어한 방을 다시 들어가도 추가 드롭이 없는지 확인한다.
- 주의: `Week14Encounter3Verification.SetupAndVerifyBatch`처럼 Week 14 Setup을 다시 실행하는 배치는 현재 Game Scene의 Room Template·Encounter 카탈로그(`roomContentVersion` 2, `encounterContentVersion` 6)와 Encounter/Room 에셋을 옛 구성으로 되돌린다. 검증만 필요하면 Setup이 없는 `Verify`를 쓰고, 실행했다면 변경된 에셋과 Scene을 되돌린다. Room-7/8 `Verify`는 HEAD 기준에서도 `roomContentVersion` 전제 조건으로 실패한다.
- Obstacle-0 적 장애물 우회: 메뉴 `Trickal Fan Game > Week 18 > Verify Obstacle-0 Enemy Obstacle Avoidance`. 기존 적 검증까지 함께 돌리는 배치는 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle0Verification.VerifyWithEnemyRegressionsBatch -logFile <로그 경로>`. 직선 우선 이동, 단일·컵 모양 장애물 우회 경로가 장애물 안으로 들어가지 않고 도착하는지, 경로 결정성, 갇힘 시 직선 복귀, 사선 판정, 원거리형·저격형의 사격 보류와 우회, 돌진형의 직선 확인 후 예고와 예고 종료 시 막힘 회복, 장애물 입사각 `80°` 경계의 접선 미끄러짐과 정면 충돌 정지, 진입·유지 충돌 콜백과 반복 스침의 접선 안정성을 검사하고, 빈 Scene에서 Phase E-1~4와 Week 15 Enemy-1·4 적 검증을 함께 돌린다. Week 15 Enemy-0·2·3·5 검증은 현재 프리팹·추적 기본값과 맞지 않아 실패하므로 제외했다. Enemy-0은 HEAD 코드에서도 기본 추적 1.1초 때문에 즉시 예고하지 않고, Enemy-2·3·5는 Obstacle-0이 건드리지 않는 프리팹 값(레거시 보스 컴포넌트, 추적 시간, 스프라이트 색) 검사에서 멈춘다. 수동 확인은 고정 기둥 방(`pillar-crossfire`)에서 기둥 뒤에 서서 추적형·돌진형이 기둥을 돌아오는지, 원거리형이 기둥 너머로 쏘지 않고 옆으로 돌아 나오는지 확인한다. 돌진형은 벽·장애물을 거의 평행하게 스칠 때만 남은 돌진 시간 동안 벽을 따라 이동하고, 이전 돌진 뒤 벽 접촉이 유지된 상태에서도 같은 판정을 반복하며, 정면·모서리·문 충돌에서는 즉시 회복해야 한다.
- Obstacle-1 파괴 가능한 장애물: 메뉴 `Trickal Fan Game > Week 18 > Setup Obstacle-1 Destructible Obstacle`이 `Assets/Prefabs/DestructibleObstacle.prefab`과 `Assets/Items/Drops/obstacle-basic-drop-table.asset`(3%, 하트 25·SP 25·열쇠 10·폭탄 8·엘리프 30·구덩이 2)을 GUID를 보존하며 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. 프리팹 계약과 레이어 충돌, 적 이동·사선 차단, 공격력과 무관한 5타 파괴, 근접·투사체·저학년 스킬·궁극기 타격과 중복 방지, 적 투사체·돌진 미집계, 20만 seed 드롭률·후보 비율과 seed 재현, 구덩이 무드롭, 1회 드롭과 방 재구성 시 파괴 유지, Resource-3 회귀를 검사한다. 장애물을 방에 배치하는 수동 확인은 Obstacle-2 Layout 이후에 한다.
- Obstacle-2 장애물 Layout: 메뉴 `Trickal Fan Game > Week 18 > Setup Obstacle-2 Obstacle Layouts`가 Large 크기의 `large-cover-blocks`·`large-split-lanes`·`large-scattered-rubble` Template과 `room-large-*.prefab`을 GUID를 보존하며 멱등 구성하고, Obstacle-1 장애물 프리팹을 중첩 인스턴스로 배치한 뒤 기둥 방과 함께 Game Scene 카탈로그(Room 콘텐츠 버전 4)에 등록한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번 실행 후 GUID 보존, 빈 방·기둥 방과 같은 Large Profile, Layout 전용 SpawnPoint, 장애물 ID·위치·5타 프리팹 계약, 겹침·필수 통로·도달성·원거리 사선(35%) 검증, 실제 콜라이더에서 적 A\* 경로 교차 확인, 빈 방이 받는 Encounter를 모든 Layout이 받는지, 8종 위반(겹침, 문 통로 막힘, SpawnPoint 덮음, 진입점 고립, 시야 없는 SpawnPoint, 격자 이탈, 영역 이탈, ID 중복)의 명시적 실패와 Room 카탈로그·층 생성 실패, 1024 seed 안의 결정적 선택과 다섯 Large Template 노출을 검사한다. 수동 확인은 `FrontendScene`에서 Play 후 Large 방에서 장애물이 문과 통로를 막지 않는지, 5타에 부서지는지, 추적형·원거리형이 장애물을 돌아오는지 본다. Room-7 검증은 현재 Encounter 카탈로그에 `pillar-crossfire`가 없어 Obstacle-2와 무관하게 실패한다(2026-09-29 확인). Room-8 검증도 Encounter 5종을 가정하므로 같은 원인으로 실패할 수 있다.
- Obstacle-3 장애물 방 적 끼임 Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week18Obstacle3PlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다(검증기가 성공 0/실패 1로 종료). 메뉴 `Trickal Fan Game > Week 18 > Play Verify Obstacle-3 Enemies Never Stall In Obstacle Rooms`는 끝나면 Play Mode만 종료한다. 먼저 Edit Mode에서 원거리형 후퇴(직선·45°·몰림 시 제자리 사격)를 확인하고, Game Scene 생성기로 seed를 늘려 가며 장애물 Layout 3종과 기둥 방마다 Encounter가 붙은 방 5개씩을 모은 뒤 빈 Scene의 Play Mode에서 실제 Room Prefab·적 Prefab·RoomController 웨이브를 4배속으로 돌린다. 방마다 플레이어가 연결된 문 입구와 SpawnPoint에서 가장 많이 가려지는 장애물 뒤 엄폐 위치에 각각 서 있고, 모든 적이 생성 후 20초 안에 공격(근접 예고·접촉, 사격, 돌진 예고)에 도달하고 중심이 장애물 안에 들어가지 않으며 모든 웨이브가 클리어되는지 검사한다. 실행별 가장 늦은 첫 공격 시간과 클리어 시간이 로그에 남는다. 수동 확인은 장애물 Layout 방에서 장애물 뒤에 붙어 서서 원거리형이 가까이 와도 등 뒤 장애물에 밀착한 채 멈추지 않고 옆으로 빠지거나 사격하는지, 추적형·돌진형이 돌아오는지 본다.
- Spawn-1 SpawnPoint 배치 역할: 메뉴 `Trickal Fan Game > Week 19 > Setup Spawn-1 Placement Roles`가 모든 Room Template의 SpawnPoint에 근접 압박·후방 사격·돌진 경로 역할을 멱등 저장하고 Encounter 콘텐츠 버전을 6으로 올린다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Spawn1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회와 Scene·Template GUID 보존, 전체 Normal Layout의 역할 계약, 추적·고속형/원거리·저격형/돌진형 역할 매핑, 그룹 및 명시 SpawnPoint의 부적합 배치 거부, 512 seed의 결정적 Encounter 선택과 모든 웨이브의 역할 적합성을 검사한다.
- Spawn-2 스폰 위치별 가중치 후보: 메뉴 `Trickal Fan Game > Week 19 > Setup Spawn-2 Weighted Spawn Candidates`가 프로필별 `pressure`·`crossfire` Encounter 10개, 일반 원거리형 임시 Prefab `QuickRangedFairy`, 역할 로스터와 Encounter 콘텐츠 버전 7을 멱등 구성한다. 일반 원거리형은 선호 거리 5.6(저격형의 70%)에서 이동 속도 3.25로 한 방향 선회하며, 플레이어 속도를 최대 0.25초까지 짧게 예측해 0.4초 간격 3발 60% / 4발 40% 묶음을 이동 사격한다. 마지막 탄 직후 정지해 1.5초 쉰 뒤 다음 묶음과 함께 이동을 재개한다. 피해 등급은 중이며 전용 아트 전까지 저격형 Sprite를 다른 색으로 표시한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Spawn2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 짧은 이동 예측 사격, 3/4발 경계, 마지막 탄 직후 정지와 1.5초 휴식, 장애물 때만 선회 반전, 잘못된 후보 계약 거부, Small/Basic/Wide/Tall/Large 저격 비율 10/20/50/50/60%, 동일 seed 결정성, 모든 후보 노출, 생성 노드의 선택 결과 보존, 256 생성 그래프와 실제 웨이브 바인딩을 검사한다.
- Difficulty-1 방 난이도: 메뉴 `Trickal Fan Game > Week 19 > Setup Difficulty-1 Room Difficulty`가 `Assets/Encounters/room-difficulty-table.asset`(위협 점수 추적 2·고속 3·원거리 3·저격 4·돌진 4, 쉬움 ≤11·보통 12~15·어려움 ≥16, 층별 상한 18/20/22, 거리 곡선 40/45/15 → 10/40/50), `pressure` 10~14·`crossfire` 15~19 선언 위협 범위, 장애물 Layout 3종의 +1 보정과 Encounter 콘텐츠 버전 8을 멱등 구성한다. 자동 검증은 Unity `-batchmode -nographics -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week19Difficulty1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 2회 GUID, 표 값과 잘못된 표 5종 거부, 선언 범위 위반·미선언 시 Encounter 검증과 층 생성 실패, Layout 보정, 거리 곡선 보간, 층 범위를 채울 후보가 없을 때의 명시적 실패, 512 seed의 방 점수·등급·거리 저장과 64 seed 결정성, 목표 등급 또는 가장 가까운 가능 등급 선택, 먼 방 어려움 비율 증가와 가까운 방 어려움 존재를 검사한다.
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

- 아직 구현되지 않은 도구의 명령과 경로는 이 문서에 확정된 사용법으로 기록하지 않는다.
- 도구가 구현되고 검증되면 실행 위치, 명령 또는 Unity 메뉴, 입력, 기대 결과와 대표 오류 해결 방법을 이 섹션에 추가한다.
- 개발 도구의 실행 실패가 게임 진행을 멈추는지 여부와 실패 종료 코드를 명확히 기록한다.

---

## 18. 참고 자료

- Node.js Releases: https://nodejs.org/en/about/previous-releases
- pnpm Installation: https://pnpm.io/installation
- Unity 6 Support: https://unity.com/releases/unity-6/support
- NestJS Modules: https://docs.nestjs.com/modules
- Prisma ORM: https://www.prisma.io/docs/orm
- Next.js App Router: https://nextjs.org/docs/app
- Supabase CLI: https://supabase.com/docs/reference/cli/getting-started
