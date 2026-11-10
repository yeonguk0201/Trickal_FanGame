# 베타 공개 전 작업 계획

> 기준일: 2026-11-06  
> 이전 계획: [26-seventh-month-plan.md](./26-seventh-month-plan.md), [28-difficulty-enemies-drops-plan.md](./28-difficulty-enemies-drops-plan.md)  
> 장기 기준: [02-roadmap.md](./02-roadmap.md) Phase 7(통합 테스트·폴리싱·배포)  
> 목표: **이 문서의 필수 항목을 끝내면 베타 테스트를 공개한다. 공개 뒤에는 원화가를 구해 임시 그림을 바꾸고 테마 확장으로 넘어간다.**

## 1. 출발 상태

- 콘텐츠: 에르핀 1명, 요정왕국 3층. 신규 적 4종(자폭형·대왕쥬비·곡사형·잠복형), 엘리트·챔피언, 함정 7종, 스펠·짱셈스펠·권능,
  아이템 출처별 풀과 드롭률(28번 Curve-0 ~ Drop-1)까지 끝났다.
- 26번에서 남은 필수 조각: Verify-0·Balance-0·Play-2·Build-0(모두 미착수). 그 선행인 Layout 추가는 범위가 미정이다.
- 폴리싱: 메뉴 정식 디자인(Menu-0~5), 효과음 보강(Sound-1~5) 모두 미착수. Effect-0 목록 일부가 남았다.
- 배포: 로드맵 Phase 7의 배포 체크리스트가 전부 비어 있다.
- 테마 구조(28번 §3.1): 요정왕국 → 정령산 → 수인마을 → 용족동굴 → 마녀왕국, 3층마다 바뀐다(2026-11-06 사용자 결정).
  베타에는 요정왕국만 들어가므로 Depth-0(깊이와 테마 안 층수 분리)은 베타 뒤로 미룬다(§8).

## 2. 착수 전 결정

| 게이트 | 정할 것 | 막는 조각 |
| ------ | ------- | --------- |
| B1 공개 형태 | **결정(2026-11-08)**: 서버를 운영해 웹 전적을 열되, 게임 플레이는 서버에 의존하지 않는다. 세부는 §3.1 | Auth-0, Offline-0·1, Deploy-0 |
| B2 배포 채널 | 빌드를 어디에 올릴지(직접 배포·itch.io 등), 대상 OS, 테스터 모집 범위 | Beta-0 |
| B3 Layout 범위 | **결정(2026-11-09)**: 직사각형 Layout 8개를 Small +3·Tall +3·Wide +2로 추가하고, 그에 따른 크기별 등장 비중 변화는 그대로 받는다. 세부는 §3.2 | Layout-0 |
| B4 피드백 수집 | 버그·의견을 받을 경로(설문·디스코드 등), 게임 안 링크 여부, 로그 첨부 방식 | Beta-0 |
| B5 보스 BGM 음원 | 기존 BGM과 같은 출처인지, 새로 구할지(크레딧 범위) | Sound-2 |
| B6 방 클리어음 | 지금처럼 `DoorOpen`이 대신할지, 따로 둘지 | Sound-4 |

## 3. 필수 조각(베타 공개 조건)

이 조각들은 줄이지 않는다. 서버가 없거나 끊겨도 게임은 정상적으로 플레이돼야 한다(서버는 동기화만 맡는다).

| 조각 | 작업 | 선행 | 완료 기준 | 상태 |
| ---- | ---- | ---- | --------- | ---- |
| Auth-0 | 기기 키 인증, 웹 스킬 강화 제거 | B1 | §3.1 S1·S2. 쓰기 요청(Run 저장·스킬 강화)은 기기 키가 맞아야 받고, 다른 사용자의 키나 키 없는 요청은 거부. Web은 조회만 | 구현(2026-11-08, Backend Jest·build, Web lint·tsc·vitest·build, Unity `BetaAuth0Verification.Verify` 통과, 로컬 서버로 게임 스킬 강화 사용자 확인). 완료(2026-11-10, Run 저장·재전송과 헤더 없는 요청 401을 사용자가 확인, Deploy-0 운영 확인과 같은 결과) |
| Offline-0 | 서버 없이 신규 등록·플레이 | Auth-0, Elif-3 | 26번 §3.1 완료 기준 그대로. 서버 등록 실패 시 로컬 프로필로 바로 시작, 서버가 살아나면 등록과 쌓인 Run 저장을 이어 함. 닉네임 충돌은 §3.1 S3 | 완료(2026-11-09, Unity `BetaOffline0Verification.Verify`와 Week13 Flow-3 Play 검증 통과, 사용자 Play로 수동 확인 3종) |
| Offline-1 | 서버 없이 성장·스킬 강화 | Offline-0 | §3.1 S4. 서버 없이도 경험치·레벨·스킬 포인트가 오르고 스킬을 강화하며, 서버가 살아나면 Run과 강화를 순서대로 보내 서버 값과 같아짐 | 완료(2026-11-09, Unity `BetaOffline1Verification.Verify`(Backend 계약 파일 직접 대조 포함), `Week13SkillUpgradeVerification.Verify`, `BetaOffline0Verification.Verify`, `Week27Elif3Verification.Verify`, Flow-5 Play 검증 통과). 사용자 Play 수동 확인 (1) 미등록 오프라인 성장·강화, (2) 등록 후 순서 전송·Web 값 일치, (3) 등록된 프로필 오프라인 → 재연결 값 일치 확인(2026-11-09) |
| Deploy-0 | Backend·DB·Web 운영 배포 | Auth-0 | §3.1 S6. 운영 DB 설정, 환경 변수 분리, CORS, 게임·Web의 API 주소 설정, 운영 환경에서 프로필 → Run 저장 → 웹 전적 조회 | 완료(2026-11-09, Backend Jest·build, Web lint·build, Unity `BetaDeploy0Verification.Verify` 통과. Render·Neon·Vercel 배포 후 운영 API의 health·DB·헤더 없는 Run 저장 401·CORS 확인, 사용자 Play로 운영 서버 등록 → Run 저장 → 운영 Web 전적 확인) |
| Layout-0 | Layout 추가 | B3 | 텍스트 Layout과 가져오기 도구(T7)로 작성, seed 재현·문 사이 경로·스폰·상자 위치 검증 | 구현(2026-11-09, Unity `RoomLayoutImporterVerification.ImportAndVerifyBatch`·`VerifyWithRegressionsBatch`, `Week25Trap1Verification.Verify` 통과, 사용자 Play 확인 — 고칠 방 없음) |
| Sound-1 | 전투 피드백 효과음(적·사망·권능) | Sound-0 | 26번 §3.6 그대로. Play-2 전에 넣는다 | 예정 |
| Verify-0 | 현재 계약 기준 회귀와 Setup 보존 | 위 변경 | 유효 검증의 통과/실패 목록, Setup 재실행 시 콘텐츠·GUID 보존, 계약 차이 출력 | 구현(2026-11-10, 검증기 195개 통과, Setup 149개 중 유지 85·은퇴 62·내부 ID만 2·내용 변경 0, Unity↔Backend 계약 차이 0, Backend Jest 191 통과, §3.3). 완료(2026-11-10, 일시정지 화면 겹침·버튼 한 줄·스크롤 사용자 Play 확인) |
| Balance-0 | 경제 측정 | Layout-0 | 26번 §7 "실제 플레이와 경제 측정" 그대로 | 예정 |
| Play-2 | 여러 seed 실제 Run·저장·웹 확인 | Balance-0 | 무자원 필수 경로·3층 종료, 중복 보상·재추첨 없음, 아이템 전적 정상 | 예정 |
| Build-0 | 실제 게임 빌드 | Play-2 | 정상 빌드에 개발 패널 미포함, 프로필 → Run → 저장 → 웹 확인, 저장 실패 재시도 | 예정 |
| Beta-0 | 베타 공개 준비 | Build-0, B2, B4 | 빌드에 버전 표시, 피드백 경로 안내, 조작 안내(키 설명), 알려진 문제 목록, 크레딧(CC BY 음원 포함) 확인, 베타 전적 초기화 안내(§3.1 S5), 배포 채널 업로드 | 예정 |

### 3.1 서버·오프라인 설계(B1, 2026-11-08 사용자 결정)

서버는 운영하고 웹 전적도 열지만, 게임 플레이는 서버에 의존하지 않는다. 서버는 저장과 웹 조회를 맡는다.

- **S1 신원(로그인 없음)**: 이메일·비밀번호·소셜 로그인은 두지 않는다. 게임이 처음 만든 `clientProfileId`(PlayerPrefs)를
  기기 키로 쓰고, 쓰기 요청마다 보내 서버가 소유자를 확인한다. 기기 키는 공개 조회 응답에 넣지 않는다. PC를 바꾸거나
  저장을 지우면 계정을 잃는다(복구 코드는 베타 뒤에 검토). 개인정보를 받지 않으므로 별도 개인정보 처리 안내는 두지 않는다.
- **S2 Web은 조회만**: Web의 스킬 강화 요청(`api-client.ts`의 `PUT .../skills/...`)과 화면 버튼을 뺀다. 스킬 강화는 게임에서만 한다.
- **S3 닉네임 충돌**: 서버 없이 등록한 닉네임이 나중에 서버에서 이미 쓰이고 있으면, 플레이는 막지 않고 서버에 연결된 때
  타이틀에서 새 닉네임을 받는다. 새 닉네임으로 등록되면 쌓인 Run을 보낸다. 성장·엘리프 캐시는 서버가 확인한 값만
  닉네임 기준으로 저장하므로 등록 전 프로필에는 옮길 캐시가 없다. Offline-1의 로컬 성장 캐시는 기기 키 기준으로 둔다.
- **S4 오프라인 성장**: 서버가 없어도 게임이 경험치·레벨·스킬 포인트를 Backend `meta-progression` 계약과 같은 규칙으로
  계산하고 스킬을 강화한다. 서버가 살아나면 Run과 스킬 강화를 일어난 순서대로 한 대기열에서 보낸다. 스킬 강화는 목표 단계를
  보내므로 다시 보내도 한 번만 반영된다. 서버가 거부하면(계산 차이) 그 강화는 버리고 서버 값을 따른다. 서버 값이 기준이고
  로컬 값은 캐시다(Elif-3 §4.3과 같은 방식). 계약 값은 contract-drift-check로 대조한다.
- **S5 베타 데이터**: 베타 계정·전적은 정식 공개 때 초기화한다. 베타 안내에 적는다.
- **Auth-0 구현 기록**: 헤더 `X-Client-Profile-Id`(Backend `common/auth/client-profile-key.ts`, Unity
  `ApiClient.ProfileKeyHeader`). Run 저장은 키로 사용자를 찾고 `userId`는 선택이 됐다(보내면 키의 사용자와 같아야 함). 그래서
  Offline-0의 등록 전 Run도 `userId` 없이 보낼 수 있고, Auth-0 전에 쌓인 대기 Run도 그대로 보내진다. 오류는 키 없음
  `PROFILE_KEY_REQUIRED`(401), 다른 사용자 `PROFILE_KEY_MISMATCH`(403). Web의 스킬 강화 버튼·요청을 지우고 단계만 보인다.
  수동 확인: 게임 스킬 강화(2026-11-08 사용자), Run 저장·재전송과 헤더 없는 `POST /api/runs` 401(2026-11-10 사용자 확인) 완료.
- **Offline-0 구현 기록**: `LocalProfile`은 닉네임만 있으면 플레이할 수 있고(`IsRegistered`), 서버 사용자 ID가 있으면
  `HasServerAccount`다. 등록 전 Run의 소유자(`OwnerId`)는 기기 키이고, 등록되면 `ProfileRegistration`이 대기 Run을 서버
  사용자 ID로 옮긴다(`LocalPendingRunStorage.ReassignOwner`). 등록 요청이 네트워크 오류·5xx면 로컬 프로필로 바로 시작하고,
  타이틀이 실행마다 다시 등록한 뒤 대기 Run을 보낸다. 닉네임 충돌이면 `NeedsNewNickname`을 켜고 타이틀 시작 때 닉네임 화면에
  안내를 띄운다(그때도 서버가 없으면 로컬에서 이름만 바꾼다). 등록 전 닉네임은 다른 사람 것일 수 있어 서버 조회
  (`GetUser`)를 하지 않고 캐시·기본 성장으로 시작하며, 스킬 강화 화면은 "서버에 등록되면 강화" 안내를 띄운다(Offline-1에서
  오프라인 강화). 게임이 Backend 닉네임 충돌 코드(`NICKNAME_ALREADY_EXISTS`)를 몰라 "프로필 충돌"로 보이던 문제도 고쳤다.
  Week13 Flow-3 Play 검증의 재시도 시나리오는 네트워크 실패가 이제 오프라인 시작이므로 409 거부로 바꿨다.
  수동 확인: (1) 서버를 끈 채 새 닉네임 → 바로 홈·Run 시작, 결과는 오프라인 저장.
  (1)·(3) 사용자 확인 완료(2026-11-09). (2)는 사용자가 `두두`로 확인했고 Backend에 Run 2판이 저장됐다. 이때 Web 사용자
  페이지가 한글 닉네임을 두 번 인코딩해 "존재하지 않는 사용자"로 보이던 기존 버그를 같이 고쳤다(`users/[nickname]/page.tsx`). 같은 PC에서는 Frontend 왼쪽 위 `DEV: RESET LOCAL PROFILE` → `NEW PLAYER`(새 기기 키)로 새 사용자를 만든다.
  `RESET`은 기기 키를 유지하므로 서버에 이미 그 키로 등록된 닉네임만 다시 받는다(다른 닉네임은 `PROFILE_IDEMPOTENCY_CONFLICT`).
  (2) 서버를 끈 채 `NEW PLAYER` → 새 닉네임 → Run 1판 → 서버를 켜고 재실행 → 타이틀에서 등록되고 그 Run이 웹 전적에 1번
  올라감. (3) 서버를 끈 채 `NEW PLAYER` → 서버에 이미 있는 닉네임 → 서버를 켜고 재실행 → 시작 버튼에서 새 닉네임 안내, 새
  닉네임 등록 후 Run 전송.
- **Offline-1 구현 기록**: 게임의 `MetaProgressionRules`가 Backend `meta-progression` 계약과 같은 규칙(층 100·처치 5·등급
  10/20/30/50, 레벨별 필요 경험치, Lv.19 상한, 레벨당 스킬 포인트 1, 스킬 Lv.10 상한)으로 계산한다. 성장 캐시는 서버가
  확인한 값만 기기 키 기준으로 두고(`LocalProgression`), 게임이 보여 주고 Run에 적용하는 성장은 그 캐시에 아직 안 보낸 Run과
  스킬 강화를 대기열 순서대로 더한 값이다(Elif-3 §4.3과 같은 방식). 스킬 강화는 `PendingRunQueue.v1`에 Run과 함께 들어가고
  (`PendingSkillUpgrade`), 등록되면 Run과 함께 서버 사용자 ID로 옮겨진다. `PendingSync`가 타이틀(과 스킬 화면의 오프라인 강화
  직후)에서 오래된 것부터 보내며, 규칙 거부 강화는 버리고 네트워크·5xx는 멈춘 뒤 다음 실행에 다시 보낸다. 대기 중인 강화 뒤의
  새 Run도 대기열을 거쳐 보낸다(`RunSubmission`). Run 경험치는 종료 때 `RunSession`이 아이템 등급으로 계산해 두고
  (`PendingRunExperience.v1`), 결과 화면은 저장 실패 때도 경험치·레벨 업·스킬 포인트를 보여 주며 `오프라인 기록` 안내를 띄운다.
  스킬 화면의 "서버에 등록되면 강화" 안내는 오프라인 진행 표시로 바뀌었다. 계약 대조는 `BetaOffline1Verification`이
  `backend/src/contracts/meta-progression.ts`를 직접 읽어 한다. 수동 확인 3종은 [05번](./05-development-setup.md)
  "서버 없이 성장·스킬 강화 Offline-1"에 있다.
- **S6 운영 환경**: 무료 요금제 조합으로 시작한다. Web은 Vercel, Backend는 무료 요금제 호스팅(Render 등), DB는 무료 Postgres
- **Deploy-0 구현 기록**: Backend는 `config/runtime-config.ts`가 시작 때 환경 변수를 읽고, `NODE_ENV=production`에서
  `DATABASE_URL`·`CORS_ORIGIN`이 없으면 시작하지 않는다(로컬 기본값으로 조용히 넘어가지 않음). CORS는 쉼표로 여러 출처, 끝 `/`
  제거. 종료 신호에 DB 연결을 닫는다(`enableShutdownHooks`). seed는 운영에서 `test-player`를 만들지 않는다(`SEED_TEST_USER`로
  바꿀 수 있음). `render.yaml`(Render Blueprint, 무료·싱가포르, `main` 브랜치)이 빌드 때 migrate deploy와 카탈로그 seed를
  실행한다(무료 요금제는 배포 전 단계가 없음). Web은 Vercel 운영 빌드에 `NEXT_PUBLIC_API_URL`이 없으면 빌드를 멈춘다. Unity는
  `ApiEndpoint`가 주소를 정한다: `-apiUrl` → Editor는 로컬(메뉴 `Trickal Fan Game/Beta/Use Production API in Editor`로 운영)
  → 빌드는 운영. `ApiClient`는 시작 때 `/api/health`로 잠든 서버를 깨운다. 운영 DB는 Neon(싱가포르, pooled 연결)이다. Supabase 무료
  슬롯이 다른 프로젝트에 쓰이고 있고 무료 프로젝트가 일주일 무요청이면 일시정지되기 때문이다(2026-11-09 사용자 결정). 배포 절차와 운영 확인 항목은 [05번](./05-development-setup.md) §11.4.
  운영 주소: API `https://trickal-fan-game-api.onrender.com/api`, Web `https://trickal-fan-game.vercel.app`, DB Neon
  `trickal-fan-game-prod`(싱가포르). 운영 확인(2026-11-09): `/api/health/ready` DB up, 헤더 없는 `POST /api/runs` 401
  `PROFILE_KEY_REQUIRED`, CORS 허용, Editor 운영 토글로 `NEW PLAYER` 등록(201) → Run 저장 → 운영 Web 전적 표시(사용자 확인).
  (Neon·Supabase 등). Backend가 잠들어 첫 요청이 늦어도 게임은 대기열로 처리한다. 운영 기간은 베타 기간(정식 공개 전까지).

### 3.2 Layout-0(B3, 2026-11-09 사용자 결정)

- **범위**: 방 수가 적은 크기에 직사각형 Layout 8개를 더한다. Small 3 → 6, Tall 4 → 7, Wide 5 → 7. Basic·Large(각 9)는
  그대로다. Small·Tall에 처음으로 함정 방이 생긴다. 후보를 더 만들어 고르지 않고 정한 수만 만들어 사용자 Play로 고친다.
- **등장 비중**: 생성기는 프로필을 먼저 고르지 않고 그 방에 맞는 모든 템플릿을 가중치로 한 번에 추첨한다
  (`RoomTemplateSelector`). 그래서 Layout을 더한 크기는 더 자주 나온다. 가중치 합 기준(겹침으로 실제와 조금 다름)으로 Small
  11 → 16%, Basic 27 → 21%, Wide 17 → 20%, Tall 13 → 19%, Large 32 → 25%가 된다. 사용자가 이 변화를 그대로 받기로 했고,
  Balance-0이 이 분포로 기준선을 잡는다.
- **추가한 방**(`game/RoomLayouts/`, 가중치는 따로 적은 것만 100이 아님, 난이도 보정은 모두 +1):

  | ID | 성격 |
  | --- | --- |
  | `small-fire-gate` | 함정: 왼쪽 불 기둥 3칸, 가운데 가시 돌 2개 |
  | `small-twin-pits` | 아래 양쪽 구덩이, 가운데 짱돌 1개 |
  | `small-gold-vein` | 위쪽 양옆 황금돌 4개(가중치 50) |
  | `tall-spike-ladder` | 함정: 네 구석 바닥 가시와 주기 가시 A·B, 가운데 바닥 가시 2개 |
  | `tall-chasm` | 네 구석과 가운데의 구덩이 |
  | `tall-rock-maze` | 구석 짱돌 선반과 가운데 짱돌 기둥(짱돌 16개) |
  | `wide-web-thicket` | 함정: 양쪽 거미줄 덤불과 그 앞의 독버섯 2개 |
  | `wide-rubble-yard` | 짱돌 무더기가 흩어진 앞마당(짱돌 13개) |

- **콘텐츠 버전**: Room 콘텐츠 버전을 15 → 16으로 올렸다(`RoomLayoutImporter.RoomContentVersion`). 같은 seed라도 이전과 다른
  방이 나온다. Trap-1 검증기가 임포터 버전을 13으로 고정해 비교하던 것은 "13 이상"으로 바꿨다.
- **검증**: `ImportAndVerifyBatch`가 22개 방 파일과 Prefab·템플릿 일치, 문 사이 경로, 모든 문 조합의 Encounter 배치, 400 seed
  안에서 모든 방 등장과 결정성을 확인했다(2026-11-09). 이 검증기의 강제 재임포트는 기존 방 Prefab 14개의 내부 fileID만 바꾸므로
  (내용 같음) 커밋 전에 되돌린다. 회귀 묶음(Encounter-4·Spawn-2·Difficulty-1·Terrain-0·Chest-1·Jjangsem-1·Obstacle-1~7·Special-2·3·
  Flight-0)도 통과했다. Trap-1 검증기는 함정이 Trap-1 방 3개에만 있다고 가정해 새 함정 방에서 실패했으므로, 함정이 있는 다른
  방은 방 파일에서 들여온 것인지 확인하고 같은 상자 위치·런타임 검사를 하도록 바꿨다.
- **수동 확인**(완료, 2026-11-09 사용자 확인, 고칠 방 없음): Game Scene Play → `F1` → `— Layout —`의 `Show layouts`에서 새 방마다 `Force <방 ID>`를 눌러 본다.
  (1) 어느 문으로 들어와도 서 있는 자리에 함정·장애물이 없다. (2) 함정을 밟지 않고 모든 문을 오갈 수 있다. (3) 적이
  끼이지 않고 방이 클리어되고, 원거리형이 엄폐물 뒤에서 멈추지 않는다. (4) 클리어 상자가 함정·구덩이 위에 놓이지 않는다.
  (5) 방마다 느낌이 다른지, 고칠 방이 있는지. 끝나면 `Clear forced layout and restart`.
- **ㄱ·ㄴ자 방(Shape-0, 후보)**: 사용자가 원함(2026-11-09). 직사각형이 아닌 방은 새 프로필이고, 생성기 겹침 판정·걸을 수
  있는 바닥과 벽 그림·카메라 구역·미니맵 모양·문 위치·Encounter가 모두 직사각형을 전제로 하므로 Layout-0과 분리한다.
  베타 뒤로 미룬다(2026-11-09 사용자 결정, §8).

### 3.3 Verify-0(2026-11-10)

세 가지 도구를 만들었다(실행법은 [05번](./05-development-setup.md) "Verify-0 전체 검증·Setup 재실행 검사").

- **전체 검증** `BetaVerify0Runner`: `Verify()`가 있는 검증기를 모두 빈 Scene에서 하나씩 돌리고 결과를
  `game/Logs/Verify0/verify-all.md`에 쓴다. 처음 194개 중 11개가 실패했고, 원인별로 고쳐 195개(계약 검사 추가) 모두 통과했다.
  - 순서에 따라 실패하던 2개: `UserArtwork`(스프라이트·재질)와 `CombatEffectArtwork`(프레임)의 static 캐시가 Unity가 파괴한
    객체를 그대로 돌려줬다. 파괴된 항목은 다시 만들도록 고쳤다(런타임 코드, 값 변경 없음).
  - 이후 조각에서 바뀐 계약을 따라가지 못한 4개: 스파이크 경고 색(Trap-0에서 제거), 일시정지 버튼 크기(UX-0), 보상 풀
    종류(Power-1 권능 추가), 상세 스탯 기본값. 지금 계약에 맞게 단언을 고쳤다.
  - 열린 Scene이 필요한 3개(`Week13Frontend`, `Week7HighGradeSkill`, `Week7ChaserMonster`)와 일부러 오류 로그를 남기는 4개
    (Slot-0·Power-1 거부 시험, Reward-2·HUD-7B의 Edit Mode EventSystem)는 실행기에 Scene과 예상 로그를 적었다.
- **계약 차이** `BetaVerify0ContractVerification`: Unity 아이템 83개를 `backend/src/contracts/item-catalog.ts`와 ID·등급·isActive·
  maxStacks·효과 종류와 수치까지 비교하고, 캐릭터를 seed와 비교한다. 결과는 `contract-diff.md`.
  아이시아의 지갑의 엘리프 추첨 효과(`GainPermanentElifRollOnAcquire`, Elif-2)가 Backend 카탈로그에 빠져 있어 추가했다(설명 문구
  포함, Backend Jest 191 통과). 운영 DB는 다음 배포의 seed로 반영된다. Backend에만 있는 `item-05·07·09·10`은 과거 전적 보존용
  레거시라 참고로만 표시한다.
- **Setup 재실행 보존** `BetaVerify0SetupRunner`: Setup을 하나씩 다시 실행해 바뀐 파일을 기록하고 git으로 되돌린다. 시작 때의
  미커밋 파일은 백업에서 복원한다. 결과는 `setup-rerun.md`와 Setup별 diff(`setup-diffs/`).
  - 처음 결과: 149개 중 78개 유지, 71개가 내용을 바꿨다.
  - **일시정지 창 회귀**: UX-1 커밋(`decce5d`) 때 HUD-3C Setup이 다시 실행돼 일시정지 창이 예전 배치(목록 1024×600, 상세
    스탯과 겹침)로 돌아가 있었다. HUD-3C가 HUD-7B 배치를, HUD-7B·7C·7D가 UX-0 버튼 줄을 다시 적용하도록 고치고 Scene을
    복구했다(HUD-7B 배치: 목록 700×520, 제목 "일시정지", 안내 문구 위). Boss-Intro-0 오버레이는 일시정지 창 바로 아래에 둔다.
  - **은퇴 62개**(`RetiredSetup`): 지금 값을 이후 조각이나 Layout 가져오기가 관리하는 Setup은 실행하면 안내만 하고 멈춘다.
    아이템 18(풀 소속·충전 값), 방·Encounter·장애물 18, 적·보스 16, 예전 Game HUD 5, Frontend 5. 은퇴한 Setup을 부르는
    유지 Setup은 없다. 각 검증기의 `Verify()`는 그대로 쓰고, 은퇴한 Setup을 부르는 `SetupAndVerifyBatch`만 멈춘다.
  - **처음 분석에서 남았던 6개**는 모두 값이 같았다(diff를 오브젝트 단위로 다시 비교).
    - `EnemyAttackAnimationSetup`·`EnemyMovementAnimationSetup`·`BossMovementAnimationSetup`: 나중에 생긴 필드를 기본값으로 적고
      (`knockbackImmune: 0` 등), 보스 스프라이트 `.meta`에 생략돼 있던 기본 Import 설정을 모두 적는다. 한 번 실행한 결과를
      커밋해 다시 실행해도 바뀌지 않게 했다.
    - `FairyVillageArtworkSetup`(방 Prefab 41개)·`Week25Ux0Setup`(게임 안 설정 창 복사본): 오브젝트를 지우고 다시 만들어 내부
      fileID와 순서만 바뀐다. 실행기가 IDONLY로 분류한다. 커밋할 필요가 없다.
  - 최종(2026-11-10): 유지 85, 은퇴 62, IDONLY 2, 내용 변경 0. 전체 검증 195개 통과.
- **수동 확인**: Game Scene Play → `Esc` → 일시정지 창에서 상세 스탯(왼쪽)과 보유 아티팩트 목록(오른쪽)이 겹치지 않고, 아래
  버튼 네 개(돌아가기·설정·처음부터·홈으로)가 한 줄로 보이는지 본다. 아티팩트가 많을 때 목록이 스크롤되는지 `F1` 개발 패널로
  아티팩트를 여러 개 얻어 확인한다.

## 4. 연출(Effect-0 남은 것)

28번 §7 Effect-0 목록에서 남은 항목이다. 피해 숫자는 베타 전에 한다(2026-11-06 사용자 요청).

| 조각 | 작업 | 완료 기준 | 상태 |
| ---- | ---- | --------- | ---- |
| Number-0 | 피해 숫자를 TextMeshPro 프리팹으로, 치명타와 구분 | 지금의 적 전용 월드 숫자(HUD-6: 체력바 위에서 시작, 상승·페이드, 동시 타격 흩어짐, 풀 재사용) 유지. 글꼴은 `Week13FrontendSetup.FontPath`. 치명타는 색·크기·모양 중 둘 이상으로 구분. 틱 피해(중독·화상)도 일반 타격과 구분할지 착수 때 정함 | 완료(2026-11-09, `Number0Verification` 회귀 배치 통과, 사용자 Play 확인, §4.1) |
| Boss-Intro-0 | 보스 등장 연출 | 보스방 진입 → 짧은 등장 연출 → 전투 시작. 연출 중 플레이어·보스 피해 없음, 일시정지 대응, 건너뛰기 여부 착수 때 정함. 보스 2종(부스러기·새마음금고)과 3층 보스 | 완료(2026-11-09, `BossIntro0Verification` 배치 통과, 사용자 Play 확인, §4.2). 원화 교체는 §7 2번 |
| Defeat-4 | 남은 쓰러짐 | 누루링·자폭형·부스러기 쫄따구·크레용 용사. 그림이 없으면 원화 조달(§7) 뒤로 미룬다 | 원화 대기 |
| Effect-0 | 새마음금고 투사체를 돈·보석 그림으로 | 판정·탄속 불변 | 예정 |
| Motion-0 | 걷기·공격 모션이 없는 적의 모션 | 대상 목록은 착수 때 정리. 원화가 필요한 것은 §7로 | 일부 진행 |

### 4.1 Number-0(2026-11-09)

- **공통 모양**: 모든 피해 숫자는 주황빛 흰 글자, 진한 주황 외곽선(0.4), 오른쪽 아래 옅은 갈색 그림자(월드 0.04·-0.03,
  1080p에서 약 4px·3px), 자간 -6을 쓴다. "무적" 글자는 자간 -18로 거의 맞닿게 한다(2026-11-09 사용자 결정: 일반 공격도 치명타와 같은 색·외곽선).
- **치명타**: 공통 모양에 1.35배 크기(처음 0.12초 튀어나옴)와 뒤의 뾰족 별만 더한다. 별은 테두리 진한 주황(약 6px),
  안쪽 연한 주황이고, 끝 개수·길이가 다른 6가지 모양을 런타임에 한 번 그려 두고(`CriticalBurstSprites`) 치명타마다
  모양·회전·가로세로 비율을 다르게 골라 천천히 돈다(사용자 요청: 참고 게임처럼 뾰족한 도형, 모양이 조금씩 다르게).
- **무적**: 무적이 막은 직접 타격은 `Health.AnyDamageBlocked`를 보내고, 적·보스 위에 "무적"(안쪽 RGB 255·247·138, 외곽선
  RGB 194·143·38)과 그 아래 0을 띄운다(사용자 요청). 같은 대상은 0.35초에 한 번만 띄우고, 틱이 막힌 것과 플레이어는
  띄우지 않는다. 지금 적이 무적이 되는 때는 크레용 용사 단계 전환과 잠복형이 땅속에 있을 때다.
- **틱 피해**: 구분한다. `Periodic` 피해(화상·중독·감전·오라·스펠 지속 피해)는 같은 모양에 0.72배 크기로 직접 타격을
  가리지 않게 한다.
- **외곽선 재질**: TMP 모바일 SDF 셰이더는 `OUTLINE_ON` 키워드가 있어야 외곽선을 그리고, 빌드는 저장된 재질이 쓰는 변형만
  남긴다. 그래서 코드에서 `outlineWidth`만 바꾸던 처음 구현은 외곽선이 보이지 않았다(2026-11-09 사용자 확인).
  `Number0Setup`(메뉴 `Trickal Fan Game > Beta > Setup Number-0 Damage Numbers`)이 `Assets/Fonts/`의 `Combat Number.mat`
  (숫자), `Combat Number Shadow.mat`(그림자), `Combat Number Blocked Label.mat`(무적 글자)을 만들어 풀에 연결한다.
  외곽선 색·두께는 이 재질에서 바꾼다.
- **검증**: `Number0Verification.Verify`(메뉴 `Trickal Fan Game > Beta > Verify Number-0 Damage Numbers`)가 치명타 크기·색·별,
  틱 크기, 무적 0 표시와 반복 제한, 플레이어 제외, 풀 재사용 시 이전 종류의 별·라벨·크기 초기화를 검사한다.
  `VerifyWithRegressionsBatch`(HUD-6·개발 패널 포함)도 통과했다(2026-11-09).
- **수동 확인**(완료, 2026-11-09 사용자 Play 확인): Game Scene Play → `F1` → `— Player —`의 `Damage number samples`로 일반·치명타·틱·무적을 나란히 본다.
  `CRIT 100%`를 켜고 실제 적을 때려 별 모양이 매번 다른지, 숫자가 겹쳐도 읽히는지, 크레용 용사 단계 전환 중 때리면 "무적 0"이
  뜨는지 본다.

### 4.2 Boss-Intro-0(2026-11-09)

착수 때 정한 것(사용자 결정):

- 보스방에 들어가 보스가 나타나면 게임 시간을 0으로 둔다(`Time.timeScale = 0`). 캐릭터·적·투사체가 모두 멈추고 피해도 없다. 연출은 실제 시간으로 돈다.
- 흐름(약 2.9초): 왼쪽 위에서 보스 패널, 오른쪽 아래에서 플레이어 패널이 사선 빨간 띠와 함께 들어온다(0.35초) → 가운데에 노란 VS가 크게 나타났다 줄어들며 붙는다. 흰 번쩍임과 흔들림 → 1.6초부터 패널이 양옆으로 빠진다 → 보스 체력바가 0에서 끝까지 찬다(1초) → 정지가 풀리고 전투가 시작된다.
- 건너뛰기: Space·Enter·마우스 왼쪽·게임패드 A/Start. 패널 중에는 체력바 채우기로, 채우는 중에는 바로 전투로 넘어간다. 처음 0.25초는 문으로 걸어 들어오며 누른 키를 무시한다. Escape는 일시정지와 겹쳐서 쓰지 않는다.
- 같은 보스를 다시 만날 때 생략하는 기능은 넣지 않는다. 필요하면 나중에 추가한다.
- 그림: 보스는 기본 몸 스프라이트(`BossMovementAnimator.BodySprite`), 플레이어는 `CharacterDefinition.Portrait`를 크게 확대해 쓴다. VS는 프론트엔드 글꼴과 외곽선 재질(`Assets/Fonts/Boss Intro VS.mat`)로 그린다. 원화(§7 2번)는 VS·패널·보스 체력바 정도이고, 받으면 이미지만 바꾼다.

구성: `Trickal Fan Game/Beta/Setup Boss-Intro-0 Boss Intro`가 Game HUD `ReferenceFrame` 아래에 `Boss Intro` 오버레이를 만든다(재실행 시 같은 오브젝트·재질 재사용). 런타임은 `GameBossIntroView`, 체력바 채우기는 `GameBossHudView.SetIntroPresentation`.

검증: `Trickal Fan Game/Beta/Verify Boss-Intro-0 Boss Intro`(배치 `BossIntro0Verification.SetupAndVerifyBatch` 통과, 2026-11-09). 회귀 묶음은 `VerifyWithRegressionsBatch`(HUD-5, 개발 패널).

수동 확인(2026-11-09 사용자 확인, 문제 없음): Game Scene Play → `F1` 개발 패널 `— Boss room —`의 `Boss F1`·`Boss F2`·`Boss F3`로 보스방 이동 → 연출 중 모두 멈추는지, 패널·VS·체력바 채우기가 보기 좋은지, Space로 건너뛰기가 되는지, 끝나면 전투가 바로 시작되는지 본다.

## 5. 소리

### 5.1 이미 계획된 것(26번 §3.6)

Sound-1(적·사망·권능, 필수로 올림) → Sound-2(보스 효과음·보스 BGM) 순서는 고정이고, Sound-3(보상·줍기)은 독립이다.
Sound-4(방 흐름·환경)와 Sound-5(상점·보물방 BGM)는 일정이 밀리면 줄인다.

### 5.2 새로 찾은 빈 곳(2026-11-06 코드 확인)

지금 연결된 것은 효과음 21종(`CombatSfxCue`)과 BGM 2곡(`BGM_Home`·`BGM_Combat`), UI 클릭음 1개다. Sound-1~5에 없는 후보를
아래에 모았다. 이벤트가 이미 있는 것은 소리만 붙이면 되고, 없는 것은 이벤트부터 만든다. 착수 때 넣을 것을 고른다.

| 조각 | 후보 | 연결할 곳(확인한 이벤트) |
| ---- | ---- | ------------------------ |
| Sound-6 | 함정: 불타는 나무, 가시 올라옴, 버섯 피격, 독가스 분출 | 함정 런타임(이벤트 확인 필요) |
| Sound-6 | 챔피언·엘리트 등장, 챔피언 자폭 예고(1초 경고), 불사 챔피언 부활 | `EnemySpawned`, `BlastSpawned`, 변형 상태 |
| Sound-6 | 적 특수: 분열(`SplitSpawned`), 잠복 등장(`Emerged`), 소환(`Summoned`), 기절(`Stunned`) | Sound-4와 겹치는 부분은 Sound-4로 |
| Sound-7 | 보호막 생성·깨짐(`ShieldChanged`), 레비의 단도 발동(`ArtifactSpent`) | 플레이어 이벤트라 `PlayerCombatAudio` |
| Sound-7 | 체력 낮음 경고(하트 1칸 이하일 때 반복음 또는 1회) | 체력 변화 이벤트 |
| Sound-7 | 열쇠 사용(잠긴 문·상자), 열쇠·골드 부족으로 실패, 상점 구매 실패 | 문·상자·상점(실패 이벤트 확인 필요) |
| Sound-7 | 비밀방·비밀 벽 발견, 층 이동, 보스 처치 팡파레(`FinalBossCleared`) | 방·층 흐름 |
| Sound-8 | 메뉴: 버튼 호버·확정·뒤로·실패, 캐릭터 선택, 스킬 강화 성공 | Frontend 버튼(`Clicked`·`Focused`), 강화 응답 |
| Sound-8 | 결과 화면: 경험치 바 차오름, LEVEL UP, 클리어·GAME OVER 짧은 음악 | 결과 연출(16번 §4.3) |
| Sound-8 | 일시정지 열기·닫기, 3택1 넘기기 | 설정·보상 창 |
| Sound-9 | BGM: 타이틀(지금은 홈과 같음), 보스(Sound-2), 상점·보물방(Sound-5), 결과 | `ResolveBgmForScene` 확장 |

- Sound-6·7은 플레이 체감에 바로 닿으므로 Sound-2 다음에 둔다. Sound-8은 Menu 조각과 함께 하면 화면 확인이 한 번에 끝난다.
- 음원·검증 규칙은 26번 §3.6을 따른다(cue는 끝에만 추가, CC BY는 같은 조각에서 크레딧, `CombatSfxVerification`에 cue 추가).

## 6. UI

Menu-0~5(26번 §3.5): 분위기·색·부품 결정과 HTML 목업 → 공통 부품 → 타이틀·닉네임·홈 → 캐릭터 선택·스킬 강화 → Run 결과 →
설정·크레딧. 베타 첫인상에 닿으므로 Menu-0~2는 베타 전에 하고, Menu-3~5는 일정이 밀리면 임시 UI로 공개한다.
Beta-0의 버전 표시·피드백 안내·조작 안내는 Menu 디자인과 같은 부품을 쓴다.

## 7. 원화 조달(원화가를 구하면 맡길 것)

베타는 임시 그림으로 공개할 수 있다. 원화가를 구하면 아래 순서로 맡긴다. 그 전까지는 지금 그림을 유지한다.

1. 쓰러짐 그림: 누루링·자폭형·부스러기 쫄따구·크레용 용사
2. 보스 등장 연출용 그림(Boss-Intro-0)
3. 임시 그림 교체: 저혈당 요정-활발의 빵 탄, 잠복형 매복·공격 상태(임시 색), 다중 투사체(`item-06`) 아이콘, 새마음금고 투사체
4. 걷기·공격 모션이 없는 적의 프레임(Motion-0)
5. 메뉴 UI 부품·배경(Menu-1)
6. 다음 테마(정령산) 적·보스·바닥 원화. 테마당 쉬운 적 3, 중간 2, 어려운 2, 보스 정도(28번 §3.5)

불타는 나무 길이 수정 원화는 이미 있고 연결만 남았다(26번 §3.1 Trap-Fire-Art).

## 8. 베타 뒤로 미루는 것

- Depth-0(깊이와 테마 안 층수 분리)과 테마 확장(정령산 이후). 테마를 추가할 때 함께 한다.
- Shape-0 ㄱ·ㄴ자 방(§3.2). 새 프로필로 생성기 겹침 판정·바닥과 벽 그림·카메라 구역·미니맵 모양·문 위치·Encounter를 함께 설계한다.
- 적 전체의 깊이별 패턴 강화(28번 §3.3), 산사모-순수 늦은 테마 4발·약한 보정 포함.
- 캐릭터 추가(지금은 에르핀만).
- Sound-5, Sound-9 중 결과 BGM, Menu-3~5(일정이 밀릴 때).

## 9. 순서와 축소

권장 순서:

1. B3 결정, Auth-0 → Offline-0 → Offline-1, Deploy-0은 Auth-0 뒤 병렬(다른 작업과 겹치지 않고 일정 위험이 가장 크다)
2. Layout-0 → Sound-1 → Number-0
3. Verify-0 → Balance-0 → Play-2 → Build-0 → Beta-0
4. 병렬: Menu-0~2, Boss-Intro-0, Sound-2·3·6·7, Effect-0 남은 것

일정이 밀리면 (0) Sound-5·8·9와 Menu-3~5, (1) Sound-6·7 중 일부, (2) Motion-0 대상 수, (3) Boss-Intro-0 순으로 줄인다.
§3 필수 조각과 Number-0은 줄이지 않는다.

## 10. 완료 체크리스트

- [ ] B1~B4를 정했다. (B1 2026-11-08, B3 2026-11-09 결정)
- [x] 쓰기 요청이 기기 키로 확인되고 Web은 조회만 한다. (Auth-0)
- [x] 운영 환경에서 프로필 → Run 저장 → 웹 전적 조회가 된다. (Deploy-0)
- [x] 서버 없이 신규 등록과 플레이가 되고, 서버가 살아나면 쌓인 저장을 보낸다. (Offline-0)
- [x] 서버 없이 성장·스킬 강화가 되고, 서버가 살아나면 서버 값과 같아진다. (Offline-1)
- [x] Layout을 추가하고 배치 검증을 통과했다. (Layout-0)
- [ ] 적·사망·권능 효과음을 사용자 Play로 확인했다. (Sound-1)
- [x] 피해 숫자가 TextMeshPro로 바뀌고 치명타가 구분된다. (Number-0)
- [x] 회귀 목록과 Setup 재실행 보존을 확인했다. (Verify-0)
- [ ] 여러 seed 실제 Run에서 경제·시간을 기록하고 저장·웹 전적을 확인했다. (Balance-0, Play-2)
- [ ] 실제 빌드에서 개발 패널 없이 전체 흐름이 된다. (Build-0)
- [ ] 버전 표시·피드백 경로·조작 안내·크레딧을 넣고 배포 채널에 올렸다. (Beta-0)
