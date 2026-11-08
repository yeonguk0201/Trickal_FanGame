import 'dotenv/config';
import { PrismaPg } from '@prisma/adapter-pg';
import { PrismaClient } from '../src/generated/prisma/client';
import { ITEM_CATALOG } from '../src/contracts/item-catalog';

const connectionString = process.env.DATABASE_URL;

if (!connectionString) {
  throw new Error('DATABASE_URL 환경 변수가 필요합니다.');
}

const prisma = new PrismaClient({
  adapter: new PrismaPg({ connectionString }),
});

const character = {
  id: 'erpin',
  name: '에르핀',
  description: 'MVP 기본 플레이 캐릭터',
  isActive: true,
};

const testUserId = '00000000-0000-4000-8000-000000000001';
const testClientProfileId = '10000000-0000-4000-8000-000000000001';

// The local test player is for development only; the beta server starts empty.
const seedTestUser =
  process.env.SEED_TEST_USER !== undefined
    ? process.env.SEED_TEST_USER === 'true'
    : process.env.NODE_ENV !== 'production';

async function main() {
  const testUserWrites = seedTestUser
    ? [
        prisma.user.upsert({
          where: { nickname: 'test-player' },
          update: {},
          create: {
            id: testUserId,
            clientProfileId: testClientProfileId,
            nickname: 'test-player',
          },
        }),
        prisma.userCharacterProgress.upsert({
          where: {
            userId_characterId: {
              userId: testUserId,
              characterId: character.id,
            },
          },
          update: {},
          create: {
            userId: testUserId,
            characterId: character.id,
          },
        }),
      ]
    : [];

  await prisma.$transaction(
    [
      prisma.character.upsert({
        where: { id: character.id },
        update: character,
        create: character,
      }),
      ...testUserWrites,
      ...ITEM_CATALOG.map((item) =>
        prisma.item.upsert({
          where: { id: item.id },
          update: {
            name: item.name,
            description: item.description,
            rarity: item.rarity,
            isActive: item.isActive,
            maxStacks: item.maxStacks,
            effectData: [...item.effects],
          },
          create: {
            id: item.id,
            name: item.name,
            description: item.description,
            rarity: item.rarity,
            isActive: item.isActive,
            maxStacks: item.maxStacks,
            effectData: [...item.effects],
          },
        }),
      ),
    ],
    {
      maxWait: 10000,
      timeout: 30000,
    },
  );
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
