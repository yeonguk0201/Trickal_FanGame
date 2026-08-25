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

저장소 공통 불변 규칙은 루트 `AGENTS.md`에 둔다. 기능 하나를 계획부터 검증과 체크리스트 갱신까지 진행할 때는
`.agents/skills/trickal-feature-cycle/SKILL.md`의 저장소 전용 스킬을 사용한다.

향후 Unity 콘텐츠 생성기·검증기, Unity ↔ Backend 계약 검사, 플레이테스트 텔레메트리와 조건부 Unity MCP는
[개발 생산성·검증 인프라 계획](./13-development-tooling-plan.md)에 따라 도입한다.

현재 구현된 Phase C·D Unity 도구:

- `Trickal Fan Game > Setup Phase C Lower Grade Skill`: Play Mode 밖에서 Player 컴포넌트와 SP 픽업·유도탄 프리팹을 생성 또는 갱신하고 씬을 저장한다.
- `Trickal Fan Game > Verify Phase C Lower Grade Skill`: SP 경계, 처치 드롭, 입력 방향 중심 36° 부채꼴과 `1→3→2→4` 슬롯 순서, 0.08초 간격 4발 연사, 다수 적 거리순 배분, 현재 공격력 100% 중첩 폭발, 타깃 없음과 SP 부족 경로를 검사한다. 성공 시 Console에 `Phase C verification passed`가 출력되고 불변조건 위반 시 예외로 실패한다.
- `Trickal Fan Game > Setup Phase D High Grade Skill`: Play Mode 밖에서 Player 행동 상태·고학년 스킬 컴포넌트와 적 넉백 수신기를 생성 또는 갱신하고 씬·적 프리팹을 저장한다. 재실행해도 중복 컴포넌트를 만들지 않는다.
- `Trickal Fan Game > Verify Phase D High Grade Skill`: Q 쿨타임, 조향·무적 돌진, 행동·전환 게이트, 200% 범위 피해, 일반/보스 넉백 후 경직, 충돌 후 플레이어 무적 경직, 시간 만료 감속과 사망 정리를 검사한다. 성공 시 Console에 `Phase D verification passed`가 출력되고 불변조건 위반 시 예외로 실패한다.

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
