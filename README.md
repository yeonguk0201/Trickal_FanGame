# Trickal Fan Game

Unity 기반 로그라이크 팬게임과 전적 검색 웹 서비스를 연결하는 프로젝트입니다.

## Quick Start

```bash
pnpm install        # 최초 1회
pnpm dev            # backend + web 동시 실행
```

| 명령어           | 설명                    |
| ---------------- | ----------------------- |
| `pnpm dev`       | backend + web 동시 실행 |
| `pnpm db:push`   | Prisma 스키마 DB 반영   |
| `pnpm db:studio` | Prisma Studio (DB GUI)  |
| `pnpm test`      | 전체 테스트 실행        |
| `pnpm lint`      | 전체 lint 실행          |
| `pnpm build`     | 전체 build 실행         |

자세한 환경 구성은 [개발 환경 설정 가이드](docs/05-development-setup.md)를 참고합니다. 구현 중 실제
Build에서 해결한 문제와 재발 방지 기준은 [트러블슈팅 기록](docs/troubleshooting/17-troubleshooting.md)에 정리합니다.
