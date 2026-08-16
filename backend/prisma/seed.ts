import 'dotenv/config';
import { PrismaPg } from '@prisma/adapter-pg';
import { PrismaClient } from '../src/generated/prisma/client';

const connectionString = process.env.DATABASE_URL;

if (!connectionString) {
  throw new Error('DATABASE_URL 환경 변수가 필요합니다.');
}

const prisma = new PrismaClient({
  adapter: new PrismaPg({ connectionString }),
});

const character = {
  id: 'character-a',
  name: 'Character A',
  description: 'MVP 기본 플레이 캐릭터',
  isActive: true,
};

const items = [
  ['item-01', '공격력 강화', '공격력이 증가합니다.', 'COMMON'],
  ['item-02', '최대 체력 강화', '최대 체력이 증가합니다.', 'COMMON'],
  ['item-03', '이동 속도 강화', '이동 속도가 증가합니다.', 'COMMON'],
  ['item-04', '공격 속도 강화', '공격 속도가 증가합니다.', 'COMMON'],
  ['item-05', '투사체 크기 강화', '투사체 크기가 증가합니다.', 'COMMON'],
  ['item-06', '다중 투사체', '발사되는 투사체 수가 증가합니다.', 'RARE'],
  ['item-07', '공격 범위 강화', '공격 범위가 증가합니다.', 'COMMON'],
  ['item-08', '처치 회복', '적 처치 시 체력을 회복합니다.', 'RARE'],
  ['item-09', '피격 반격', '피격 시 반격 효과가 발생합니다.', 'RARE'],
  ['item-10', '추가 공격', '특정 조건에서 추가 공격이 발생합니다.', 'EPIC'],
] as const;

async function main() {
  await prisma.$transaction([
    prisma.user.upsert({
      where: { nickname: 'test-player' },
      update: {},
      create: {
        id: '00000000-0000-4000-8000-000000000001',
        nickname: 'test-player',
      },
    }),
    prisma.character.upsert({
      where: { id: character.id },
      update: character,
      create: character,
    }),
    ...items.map(([id, name, description, rarity]) =>
      prisma.item.upsert({
        where: { id },
        update: { name, description, rarity, isActive: true },
        create: { id, name, description, rarity, isActive: true },
      }),
    ),
  ]);
}

main()
  .then(async () => {
    await prisma.$disconnect();
  })
  .catch(async (error: unknown) => {
    console.error(error);
    await prisma.$disconnect();
    process.exitCode = 1;
  });
