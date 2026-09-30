import { Injectable } from '@nestjs/common';
import type { CreateRunResultContract } from '../../contracts/meta-progression';
import {
  CHARACTER_MAX_LEVEL,
  createRunRequestFingerprint,
  EXPERIENCE_TO_NEXT_LEVEL,
} from '../../contracts/meta-progression';
import { PrismaService } from '../../database/prisma.service';
import {
  applyRunExperience,
  calculateRunExperience,
} from '../../domain/meta-progression';
import { Prisma } from '../../generated/prisma/client';
import type { CreateRunDto } from './dto/create-run.dto';

const MAX_TRANSACTION_ATTEMPTS = 3;

export type PersistRunResult = CreateRunResultContract & {
  created: boolean;
};

export class RunsRepositoryError extends Error {
  constructor(
    public readonly code:
      | 'USER_NOT_FOUND'
      | 'CHARACTER_NOT_FOUND'
      | 'ITEM_NOT_FOUND'
      | 'RUN_IDEMPOTENCY_CONFLICT'
      | 'RUN_RESULT_UNAVAILABLE',
    message: string,
  ) {
    super(message);
    this.name = 'RunsRepositoryError';
  }
}

@Injectable()
export class RunsRepository {
  constructor(private readonly prisma: PrismaService) {}

  findDetailById(runId: string) {
    return this.prisma.run.findUnique({
      where: { id: runId },
      select: {
        id: true,
        gameVersion: true,
        startedAt: true,
        endedAt: true,
        playTime: true,
        reachedFloor: true,
        isCleared: true,
        killCount: true,
        deathReason: true,
        user: {
          select: { id: true, nickname: true },
        },
        character: {
          select: { id: true, name: true },
        },
        runItems: {
          orderBy: { itemOrder: 'asc' },
          select: {
            itemId: true,
            floor: true,
            itemOrder: true,
            acquiredAt: true,
            item: {
              select: { name: true, rarity: true },
            },
          },
        },
      },
    });
  }

  async create(dto: CreateRunDto): Promise<PersistRunResult> {
    const requestFingerprint = createRunRequestFingerprint(dto);

    for (let attempt = 1; attempt <= MAX_TRANSACTION_ATTEMPTS; attempt += 1) {
      try {
        return await this.createInTransaction(dto, requestFingerprint);
      } catch (error: unknown) {
        if (
          !isRetryableTransactionError(error) ||
          attempt === MAX_TRANSACTION_ATTEMPTS
        ) {
          throw error;
        }
      }
    }

    throw new Error('Run transaction retry loop ended unexpectedly.');
  }

  private createInTransaction(
    dto: CreateRunDto,
    requestFingerprint: string,
  ): Promise<PersistRunResult> {
    return this.prisma.$transaction(
      async (transaction) => {
        const existingRun = await transaction.run.findUnique({
          where: { clientRunId: dto.clientRunId },
          select: {
            id: true,
            requestFingerprint: true,
            experienceGained: true,
            progressSnapshot: true,
          },
        });

        if (existingRun) {
          return restoreExistingRun(existingRun, requestFingerprint);
        }

        const user = await transaction.user.findUnique({
          where: { id: dto.userId },
          select: { id: true },
        });
        if (!user) {
          throw new RunsRepositoryError(
            'USER_NOT_FOUND',
            '존재하지 않는 유저입니다.',
          );
        }

        const character = await transaction.character.findFirst({
          where: { id: dto.characterId, isActive: true },
          select: { id: true },
        });
        if (!character) {
          throw new RunsRepositoryError(
            'CHARACTER_NOT_FOUND',
            '존재하지 않는 캐릭터입니다.',
          );
        }

        const requestedItemIds = [
          ...new Set(dto.items.map((item) => item.itemId)),
        ];
        const items = await transaction.item.findMany({
          where: { id: { in: requestedItemIds } },
          select: { id: true, rarity: true, isActive: true },
        });
        if (
          items.length !== requestedItemIds.length ||
          items.some((item) => !item.isActive)
        ) {
          throw new RunsRepositoryError(
            'ITEM_NOT_FOUND',
            '존재하지 않는 아이템입니다.',
          );
        }

        const experienceGained = calculateRunExperience(
          {
            reachedFloor: dto.reachedFloor,
            killCount: dto.killCount,
            itemIds: dto.items.map((item) => item.itemId),
          },
          items,
        );
        const storedProgress = await transaction.userCharacterProgress.upsert({
          where: {
            userId_characterId: {
              userId: dto.userId,
              characterId: dto.characterId,
            },
          },
          create: {
            userId: dto.userId,
            characterId: dto.characterId,
          },
          update: {},
          select: {
            level: true,
            experience: true,
            skillPoints: true,
            lowGradeSkillLevel: true,
            highGradeSkillLevel: true,
          },
        });
        const progress = applyRunExperience(
          {
            characterId: dto.characterId,
            ...storedProgress,
            experienceToNextLevel:
              storedProgress.level === CHARACTER_MAX_LEVEL
                ? 0
                : EXPERIENCE_TO_NEXT_LEVEL[storedProgress.level - 1],
          },
          experienceGained,
        );
        const progressSnapshot = { ...progress };

        const run = await transaction.run.create({
          data: {
            clientRunId: dto.clientRunId,
            requestFingerprint,
            userId: dto.userId,
            characterId: dto.characterId,
            gameVersion: dto.gameVersion,
            startedAt: new Date(dto.startedAt),
            endedAt: new Date(dto.endedAt),
            playTime: dto.playTime,
            reachedFloor: dto.reachedFloor,
            isCleared: dto.isCleared,
            killCount: dto.killCount,
            deathReason: dto.deathReason ?? null,
            experienceGained,
            progressSnapshot,
          },
          select: { id: true },
        });

        if (dto.items.length > 0) {
          await transaction.runItem.createMany({
            data: dto.items.map((item) => ({
              runId: run.id,
              itemId: item.itemId,
              floor: item.floor,
              acquiredAt: new Date(item.acquiredAt),
              itemOrder: item.order,
            })),
          });
        }

        await transaction.userCharacterProgress.update({
          where: {
            userId_characterId: {
              userId: dto.userId,
              characterId: dto.characterId,
            },
          },
          data: {
            level: progress.level,
            experience: progress.experience,
            skillPoints: progress.skillPoints,
          },
        });

        return {
          runId: run.id,
          experienceGained,
          progress,
          created: true,
        };
      },
      { isolationLevel: Prisma.TransactionIsolationLevel.Serializable },
    );
  }
}

function restoreExistingRun(
  existingRun: {
    id: string;
    requestFingerprint: string;
    experienceGained: number;
    progressSnapshot: Prisma.JsonValue | null;
  },
  requestFingerprint: string,
): PersistRunResult {
  if (existingRun.requestFingerprint !== requestFingerprint) {
    throw new RunsRepositoryError(
      'RUN_IDEMPOTENCY_CONFLICT',
      '같은 clientRunId에 다른 플레이 데이터가 전송되었습니다.',
    );
  }
  if (!isProgressSnapshot(existingRun.progressSnapshot)) {
    throw new RunsRepositoryError(
      'RUN_RESULT_UNAVAILABLE',
      '저장된 Run 결과를 복원할 수 없습니다.',
    );
  }

  return {
    runId: existingRun.id,
    experienceGained: existingRun.experienceGained,
    progress: existingRun.progressSnapshot,
    created: false,
  };
}

function isProgressSnapshot(
  value: Prisma.JsonValue | null,
): value is Prisma.JsonObject & CreateRunResultContract['progress'] {
  if (!value || Array.isArray(value) || typeof value !== 'object') {
    return false;
  }

  return (
    typeof value.characterId === 'string' &&
    isNumber(value.level) &&
    isNumber(value.experience) &&
    isNumber(value.experienceToNextLevel) &&
    isNumber(value.skillPoints) &&
    isNumber(value.lowGradeSkillLevel) &&
    isNumber(value.highGradeSkillLevel)
  );
}

function isNumber(value: Prisma.JsonValue | undefined): value is number {
  return typeof value === 'number' && Number.isSafeInteger(value);
}

function isRetryableTransactionError(error: unknown): boolean {
  if (!error || typeof error !== 'object' || !('code' in error)) {
    return false;
  }

  const code = (error as { code?: unknown }).code;
  return code === 'P2002' || code === 'P2034';
}
