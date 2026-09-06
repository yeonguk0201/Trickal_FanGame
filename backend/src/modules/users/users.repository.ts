import { Injectable } from '@nestjs/common';
import {
  CHARACTER_MAX_LEVEL,
  EXPERIENCE_TO_NEXT_LEVEL,
  SKILL_MAX_LEVEL,
  type CharacterProgressSnapshot,
} from '../../contracts/meta-progression';
import { PrismaService } from '../../database/prisma.service';
import { Prisma } from '../../generated/prisma/client';

const MAX_TRANSACTION_ATTEMPTS = 3;

export type SkillType = 'LOW_GRADE' | 'HIGH_GRADE';

type UpgradeSkillInput = {
  nickname: string;
  characterId: string;
  skillType: SkillType;
  targetLevel: number;
};

export class UsersRepositoryError extends Error {
  constructor(
    public readonly code:
      | 'USER_NOT_FOUND'
      | 'CHARACTER_PROGRESS_NOT_FOUND'
      | 'SKILL_POINT_NOT_ENOUGH'
      | 'SKILL_LEVEL_MAX'
      | 'INVALID_SKILL_TARGET_LEVEL'
      | 'NICKNAME_ALREADY_EXISTS'
      | 'PROFILE_IDEMPOTENCY_CONFLICT',
    message: string,
  ) {
    super(message);
    this.name = 'UsersRepositoryError';
  }
}

const DEFAULT_CHARACTER_ID = 'erpin';

type CreateUserInput = {
  clientProfileId: string;
  nickname: string;
};

export type CreateUserResult = {
  id: string;
  clientProfileId: string;
  nickname: string;
  characterProgress: {
    characterId: string;
    level: number;
    experience: number;
    skillPoints: number;
    lowGradeSkillLevel: number;
    highGradeSkillLevel: number;
  }[];
  isNew: boolean;
};

type FindUserRunsOptions = {
  page: number;
  limit: number;
};

@Injectable()
export class UsersRepository {
  constructor(private readonly prisma: PrismaService) {}

  async createUser(input: CreateUserInput): Promise<CreateUserResult> {
    // Check for existing user with same clientProfileId (idempotency)
    const existingByProfileId = await this.prisma.user.findUnique({
      where: { clientProfileId: input.clientProfileId },
      select: {
        id: true,
        clientProfileId: true,
        nickname: true,
        characterProgress: {
          orderBy: { characterId: 'asc' },
          select: {
            characterId: true,
            level: true,
            experience: true,
            skillPoints: true,
            lowGradeSkillLevel: true,
            highGradeSkillLevel: true,
          },
        },
      },
    });

    if (existingByProfileId) {
      if (existingByProfileId.nickname !== input.nickname) {
        throw new UsersRepositoryError(
          'PROFILE_IDEMPOTENCY_CONFLICT',
          '이미 등록된 프로필 ID입니다. 다른 닉네임으로 등록되어 있습니다.',
        );
      }
      return { ...existingByProfileId, isNew: false };
    }

    // Check for nickname conflict
    const existingByNickname = await this.prisma.user.findUnique({
      where: { nickname: input.nickname },
      select: { id: true },
    });

    if (existingByNickname) {
      throw new UsersRepositoryError(
        'NICKNAME_ALREADY_EXISTS',
        '이미 사용 중인 닉네임입니다.',
      );
    }

    // Create user and initial character progress in transaction
    const user = await this.prisma.$transaction(async (tx) => {
      const newUser = await tx.user.create({
        data: {
          clientProfileId: input.clientProfileId,
          nickname: input.nickname,
        },
        select: { id: true, clientProfileId: true, nickname: true },
      });

      const progress = await tx.userCharacterProgress.create({
        data: {
          userId: newUser.id,
          characterId: DEFAULT_CHARACTER_ID,
        },
        select: {
          characterId: true,
          level: true,
          experience: true,
          skillPoints: true,
          lowGradeSkillLevel: true,
          highGradeSkillLevel: true,
        },
      });

      return {
        ...newUser,
        characterProgress: [progress],
      };
    });

    return { ...user, isNew: true };
  }

  findPublicByNickname(nickname: string) {
    return this.prisma.user.findUnique({
      where: { nickname },
      select: { nickname: true },
    });
  }

  findByNickname(nickname: string) {
    return this.prisma.user.findUnique({
      where: { nickname },
      select: { id: true },
    });
  }

  findByNicknameWithProgress(nickname: string) {
    return this.prisma.user.findUnique({
      where: { nickname },
      select: {
        id: true,
        nickname: true,
        characterProgress: {
          orderBy: { characterId: 'asc' },
          select: {
            characterId: true,
            level: true,
            experience: true,
            skillPoints: true,
            lowGradeSkillLevel: true,
            highGradeSkillLevel: true,
            character: { select: { name: true } },
          },
        },
      },
    });
  }

  async getRunStats(userId: string) {
    const [totalRuns, clears, aggregate] = await this.prisma.$transaction([
      this.prisma.run.count({ where: { userId } }),
      this.prisma.run.count({ where: { userId, isCleared: true } }),
      this.prisma.run.aggregate({
        where: { userId },
        _avg: { playTime: true, reachedFloor: true },
        _max: { reachedFloor: true },
      }),
    ]);

    return {
      totalRuns,
      clears,
      averagePlayTime: aggregate._avg.playTime,
      averageFloor: aggregate._avg.reachedFloor,
      highestFloor: aggregate._max.reachedFloor,
    };
  }

  async upgradeSkill(
    input: UpgradeSkillInput,
  ): Promise<CharacterProgressSnapshot> {
    for (let attempt = 1; attempt <= MAX_TRANSACTION_ATTEMPTS; attempt += 1) {
      try {
        return await this.upgradeSkillInTransaction(input);
      } catch (error: unknown) {
        if (
          !isRetryableTransactionError(error) ||
          attempt === MAX_TRANSACTION_ATTEMPTS
        ) {
          throw error;
        }
      }
    }

    throw new Error('Skill upgrade transaction retry loop ended unexpectedly.');
  }

  private upgradeSkillInTransaction(
    input: UpgradeSkillInput,
  ): Promise<CharacterProgressSnapshot> {
    return this.prisma.$transaction(
      async (transaction) => {
        const user = await transaction.user.findUnique({
          where: { nickname: input.nickname },
          select: { id: true },
        });
        if (!user) {
          throw new UsersRepositoryError(
            'USER_NOT_FOUND',
            '존재하지 않는 유저입니다.',
          );
        }

        const progress = await transaction.userCharacterProgress.findUnique({
          where: {
            userId_characterId: {
              userId: user.id,
              characterId: input.characterId,
            },
          },
          select: progressSelect,
        });
        if (!progress) {
          throw new UsersRepositoryError(
            'CHARACTER_PROGRESS_NOT_FOUND',
            '캐릭터 진행 데이터가 없습니다.',
          );
        }

        const currentLevel =
          input.skillType === 'LOW_GRADE'
            ? progress.lowGradeSkillLevel
            : progress.highGradeSkillLevel;

        if (input.targetLevel === currentLevel) {
          return toProgressSnapshot(input.characterId, progress);
        }
        if (input.targetLevel !== currentLevel + 1) {
          throw new UsersRepositoryError(
            'INVALID_SKILL_TARGET_LEVEL',
            '현재 스킬 레벨과 일치하지 않는 강화 목표입니다.',
          );
        }
        if (currentLevel >= SKILL_MAX_LEVEL) {
          throw new UsersRepositoryError(
            'SKILL_LEVEL_MAX',
            '대상 스킬이 최대 레벨입니다.',
          );
        }
        if (progress.skillPoints < 1) {
          throw new UsersRepositoryError(
            'SKILL_POINT_NOT_ENOUGH',
            '사용할 수 있는 스킬 포인트가 부족합니다.',
          );
        }

        const updated = await transaction.userCharacterProgress.update({
          where: { id: progress.id },
          data: {
            skillPoints: { decrement: 1 },
            ...(input.skillType === 'LOW_GRADE'
              ? { lowGradeSkillLevel: { increment: 1 } }
              : { highGradeSkillLevel: { increment: 1 } }),
          },
          select: progressSelect,
        });

        return toProgressSnapshot(input.characterId, updated);
      },
      { isolationLevel: Prisma.TransactionIsolationLevel.Serializable },
    );
  }

  async findRunsByUserId(userId: string, options: FindUserRunsOptions) {
    const { page, limit } = options;
    const where = { userId };
    const [runs, total] = await this.prisma.$transaction([
      this.prisma.run.findMany({
        where,
        orderBy: [{ endedAt: 'desc' }, { id: 'desc' }],
        skip: (page - 1) * limit,
        take: limit,
        select: {
          id: true,
          reachedFloor: true,
          playTime: true,
          isCleared: true,
          killCount: true,
          endedAt: true,
          character: {
            select: { id: true, name: true },
          },
        },
      }),
      this.prisma.run.count({ where }),
    ]);

    return { runs, total };
  }
}

const progressSelect = {
  id: true,
  level: true,
  experience: true,
  skillPoints: true,
  lowGradeSkillLevel: true,
  highGradeSkillLevel: true,
} as const;

function toProgressSnapshot(
  characterId: string,
  progress: {
    level: number;
    experience: number;
    skillPoints: number;
    lowGradeSkillLevel: number;
    highGradeSkillLevel: number;
  },
): CharacterProgressSnapshot {
  return {
    characterId,
    level: progress.level,
    experience: progress.experience,
    experienceToNextLevel:
      progress.level >= CHARACTER_MAX_LEVEL
        ? 0
        : EXPERIENCE_TO_NEXT_LEVEL[progress.level - 1],
    skillPoints: progress.skillPoints,
    lowGradeSkillLevel: progress.lowGradeSkillLevel,
    highGradeSkillLevel: progress.highGradeSkillLevel,
  };
}

function isRetryableTransactionError(error: unknown): boolean {
  if (!error || typeof error !== 'object' || !('code' in error)) {
    return false;
  }

  return (error as { code?: unknown }).code === 'P2034';
}
