# 개발 환경 설정 가이드

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
- Play Mode 자동 검증: 구성 후 별도 Unity 프로세스에서 `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13FrontendPlayVerification.RunBatch -logFile <로그 경로>`를 사용한다. `-quit`는 넣지 않는다. 타이틀 선행, 프로필 재시도·저장, 홈 진입, 데이터 기반 에르핀 카드, 선택 전 확인 차단, 단일 확정 이벤트, 뒤로가기·재진입 상태 초기화와 Flow-4 전 Run 미생성을 검사한 뒤 성공 0/실패 1로 종료한다.
- Flow-4 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow4Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Flow-4 Setup 두 번, 두 Scene GUID와 Bootstrap 단일성, Game 참조 및 전체 Frontend 레이아웃 회귀를 검사한다.
- Flow-4 Play Mode: Unity `-batchmode -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Flow4PlayVerification.RunBatch -logFile <로그 경로>`. `-quit`는 넣지 않는다. Frontend 확정부터 Game Scene까지 이동하여 로컬 사용자·캐릭터 전달, 단일 Run과 UUID `clientRunId`, 새 seed·첫 방·빈 인벤토리, 이전 선택 UI 비활성화와 종료 전 `POST /runs` 미호출을 검사한다.
- HUD-1 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-1 HP and SP`. Game Scene에 HP 바, 반복 SP 슬롯과 저학년 스킬 사용 가능 표시를 구성하고 실제 플레이어 상태에 연결한다.
- HUD-1 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud1Verification.SetupAndVerifyBatch -logFile <로그 경로>`. Setup 두 번, Game Scene GUID·계층 단일성, HP 피해·회복, SP 획득·소비·최대치 증가, 저학년 스킬 사용 가능 조건과 Flow-4 회귀를 검사한다.
- HUD-2 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-2 Skill States`. Game Scene 우하단에 저학년·고학년 임시 아이콘, 실제 입력 키, 사용 가능 상태와 고학년 쿨타임 표시를 구성한다.
- HUD-2 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud2Verification.SetupAndVerifyBatch -logFile <로그 경로>`. HUD-1 구성을 보존하며 Setup 두 번, Game Scene GUID·계층 단일성, 두 스킬의 실제 입력 가능 조건, 고학년 사용 중·쿨타임 중간값·종료 경계, `Time.timeScale = 0` 정지와 Flow-4 회귀를 검사한다.
- HUD-3A 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3A Artifact List`. Game Scene 좌하단에 실제 인벤토리와 연결된 48×48 임시 아티팩트 아이콘, 스택 배지와 10종 초과 `+N` 표시를 구성한다.
- HUD-3A 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3AVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 빈 상태, 중복 획득 스택, 11종 획득 순서·10개 제한·`+1`, 재구성 상태 유지, Setup 두 번과 HUD-2·Flow-4 회귀를 검사한다.
- HUD-3B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3B Artifact Acquisition Toast`. Game Scene 중앙의 단일 TMP 메시로 28px 이름과 그 아래 보조색 20px 효과 설명을 구성하고 동적 설명에 필요한 TMP 글리프를 Build용 폰트 아틀라스에 추가한다.
- HUD-3B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 이름·설명의 실제 2줄 TMP 메시와 선호 높이, 폰트 글리프, 1.5초 unscaled 시간 경계, 연속 획득 큐, 최대 스택 거부, 입력·시간 불변, Setup 두 번과 HUD-3A·HUD-2·Flow-4 회귀를 검사한다.
- HUD-3B Build에서 이름만 보이고 효과 설명이 사라지는 문제의 조사·해결 과정은 [트러블슈팅 §2](./17-troubleshooting.md#2-unity-build에서-tmp-두-번째-줄이-표시되지-않는-문제)에 기록한다.
- HUD-3C 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-3C Pause Artifact List`. Game Scene에 `Esc` 일시정지·복귀와 10종 초과분을 포함한 전체 아티팩트 이름·스택·설명 스크롤 목록을 구성한다.
- HUD-3C 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud3CVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 빈 목록, 12종 전체 획득 순서, 초과 항목 스택·설명, timeScale·전투 입력 루프 정지와 상태 보존, 중첩 거부, Setup 두 번과 HUD-3B·HUD-3A·HUD-2·Flow-4 회귀를 검사한다.
- HUD-4A 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-4A Minimap Foundation`. Game Scene 우상단 220×180 영역에 실제 생성 그래프의 격자 방향대로 방과 연결선을 구성하고 현재 방을 `P`와 외곽선으로 강조한다.
- HUD-4A 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud4AVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 고정 seed 생성 그래프에서 시작방과 인접방 이동 후의 현재 방, 상·하·좌·우 상대 위치와 연결선 수를 검사하고 Setup 두 번, Scene GUID·계층 단일성과 HUD-3C·HUD-7A·Flow-4 회귀를 확인한다.
- HUD-4B 구성: Unity 메뉴 `Trickal Fan Game > Week 13 > Setup HUD-4B Explored Minimap`. 방문한 방과 그 인접 미방문방을 층 그래프에 누적하고, `?` 미확인·`P` 현재·`V` 클리어·`S/T/B` 특수 문 표식을 구성한다. 인접한 보물방·보스방은 문 종류와 함께 `T`·`B`를 공개하지만 실제 진입 전에는 `V`를 표시하지 않는다.
- HUD-4B 자동 구성·회귀: Unity `-batchmode -quit -projectPath <game 경로> -executeMethod TrickalFanGame.Editor.Week13Hud4BVerification.SetupAndVerifyBatch -logFile <로그 경로>`. 보물방까지 실제 그래프 경로를 이동하며 누적 공개, 이전 방 유지, 특수 문 공개, 보물방 선클리어와 실제 방문 분리, 진입 전 `V` 은닉, 패널 자동 맞춤과 HUD-4A·HUD-3C·HUD-7A·Flow-4 회귀를 검사한다.
- 같은 프로젝트를 연 Unity가 있으면 배치 실행이 잠길 수 있다. 기존 Editor를 강제 종료하지 않고 수동 메뉴를 실행하거나 독립된 복사본에서 검증한다.

수동 확인: `FrontendScene`을 열고 Game View에 Fixed Resolution `1280 × 720`, `1920 × 1080`을 각각 추가하여 Play한다. 타이틀에서 저장 프로필의 홈 진입과 새 사용자의 닉네임 등록 진입을 각각 확인한다. 홈에서는 네 버튼의 마우스 호버, 위·아래 키 순환과 Enter/Space를 확인한다. 게임 시작에서는 에르핀 카드 선택 표시, 확인 활성화, 뒤로가기와 재진입 시 선택 초기화를 확인한다. 확인을 누르면 Game Scene으로 한 번 이동해 1층 첫 방에서 전투를 시작해야 하며 이전 캐릭터 선택 창이 다시 나타나지 않아야 한다. Run 종료 전에는 Backend 전적이 생성되지 않아야 한다.

저장소 공통 불변 규칙은 루트 `AGENTS.md`에 둔다. 기능 하나를 계획부터 검증과 체크리스트 갱신까지 진행할 때는
`.agents/skills/trickal-feature-cycle/SKILL.md`의 저장소 전용 스킬을 사용한다.

향후 Unity 콘텐츠 생성기·검증기, Unity ↔ Backend 계약 검사, 플레이테스트 텔레메트리와 조건부 Unity MCP는
[개발 생산성·검증 인프라 계획](./13-development-tooling-plan.md)에 따라 도입한다.

현재 구현된 Phase C·D·F Unity 도구:

- `Trickal Fan Game > Debug > Open or Create Item Test Room`: 랜덤 층 생성과 Backend Run 저장에서 분리된 `Assets/Scenes/ItemTestScene.unity`를 생성하거나 연다. Hierarchy의 `Item Test Room`을 선택하고 `Item Loadout`에서 시작 아티팩트 스택을, `Enemy Placements`에서 적 Prefab·활성 여부·로컬 좌표·회전을 설정한 뒤 Play Mode를 시작한다. 활성 아티팩트 10종과 호환성 확인용 비활성 `item-06`이 기본 목록에 포함된다.
- Play Mode 오른쪽 `Item Test Room` 패널에서 현재 HP·방어막·공격력·공격속도·치명타·이동속도·투사체·관통 수치를 확인한다. 각 아이템의 `+1`로 즉시 한 스택을 획득하고, `Heal / Reset HP`와 `Respawn Enemies`로 같은 설정을 반복 검증한다. 스택 감소와 완전 초기화는 Play Mode를 다시 시작한다.
- `Trickal Fan Game > Debug > Verify Item Test Room`: 전용 씬 격리, 아이템 목록·중복·최대 스택, 적 Prefab의 `Health`, Backend 저장 세션 부재를 검사한다. `ItemTestScene`을 연 상태에서 Edit Mode와 Play Mode에 각각 실행하며 성공 시 Console에 `Item Test Room verification passed`가 출력되고 오류가 없어야 한다.

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
