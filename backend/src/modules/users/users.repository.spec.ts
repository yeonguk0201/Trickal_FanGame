import 'reflect-metadata';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { Prisma } from '../../generated/prisma/client';
import { UsersRepository, UsersRepositoryError } from './users.repository';

describe('UsersRepository H-4 skill upgrade transaction', () => {
  type Progress = ReturnType<typeof createProgress>;
  type Transaction = ReturnType<typeof createTransactionMock>;
  type TransactionCallback = (transaction: Transaction) => Promise<unknown>;
  type TransactionRunner = (
    callbackOrQueries: TransactionCallback | unknown[],
    options?: { isolationLevel?: string },
  ) => Promise<unknown>;

  let repository: UsersRepository;
  let transaction: Transaction;
  let transactionRunner: jest.MockedFunction<TransactionRunner>;

  beforeEach(() => {
    transaction = createTransactionMock();
    transactionRunner = jest.fn<TransactionRunner>();
    transactionRunner.mockImplementation((callbackOrQueries) => {
      if (typeof callbackOrQueries !== 'function') {
        return Promise.resolve([]);
      }
      return callbackOrQueries(transaction);
    });
    repository = new UsersRepository({
      $transaction: transactionRunner,
    } as never);
  });

  it.each([
    ['LOW_GRADE', 'lowGradeSkillLevel'],
    ['HIGH_GRADE', 'highGradeSkillLevel'],
  ] as const)(
    'atomically spends one point and upgrades %s',
    async (skillType, levelField) => {
      await expect(
        repository.upgradeSkill({
          nickname: 'test-player',
          characterId: 'erpin',
          skillType,
          targetLevel: 2,
        }),
      ).resolves.toMatchObject({
        characterId: 'erpin',
        skillPoints: 1,
        [levelField]: 2,
      });

      expect(transactionRunner).toHaveBeenCalledWith(expect.any(Function), {
        isolationLevel: Prisma.TransactionIsolationLevel.Serializable,
      });
      expect(transaction.userCharacterProgress.update).toHaveBeenCalledWith({
        where: { id: 'progress-id' },
        data: {
          skillPoints: { decrement: 1 },
          [levelField]: { increment: 1 },
        },
        select: {
          id: true,
          level: true,
          experience: true,
          skillPoints: true,
          lowGradeSkillLevel: true,
          highGradeSkillLevel: true,
        },
      });
    },
  );

  it('returns the current result for the same target level without spending a point', async () => {
    const progress = createProgress({
      skillPoints: 1,
      lowGradeSkillLevel: 2,
    });
    transaction.userCharacterProgress.findUnique.mockResolvedValue(progress);

    await expect(
      repository.upgradeSkill({
        nickname: 'test-player',
        characterId: 'erpin',
        skillType: 'LOW_GRADE',
        targetLevel: 2,
      }),
    ).resolves.toMatchObject({ skillPoints: 1, lowGradeSkillLevel: 2 });
    expect(transaction.userCharacterProgress.update).not.toHaveBeenCalled();
  });

  it.each([
    [
      'missing progress',
      createProgress(),
      'CHARACTER_PROGRESS_NOT_FOUND',
      (tx: Transaction) =>
        tx.userCharacterProgress.findUnique.mockResolvedValue(null),
      2,
    ],
    [
      'insufficient points',
      createProgress({ skillPoints: 0 }),
      'SKILL_POINT_NOT_ENOUGH',
      (tx: Transaction, progress: Progress) =>
        tx.userCharacterProgress.findUnique.mockResolvedValue(progress),
      2,
    ],
    [
      'maximum level',
      createProgress({ lowGradeSkillLevel: 10 }),
      'SKILL_LEVEL_MAX',
      (tx: Transaction, progress: Progress) =>
        tx.userCharacterProgress.findUnique.mockResolvedValue(progress),
      11,
    ],
    [
      'skipped target level',
      createProgress(),
      'INVALID_SKILL_TARGET_LEVEL',
      (tx: Transaction, progress: Progress) =>
        tx.userCharacterProgress.findUnique.mockResolvedValue(progress),
      3,
    ],
  ] as const)(
    'rejects %s without updating',
    async (_label, progress, code, arrange, targetLevel) => {
      arrange(transaction, progress);

      await expectRepositoryError(
        repository.upgradeSkill({
          nickname: 'test-player',
          characterId: 'erpin',
          skillType: 'LOW_GRADE',
          targetLevel,
        }),
        code,
      );
      expect(transaction.userCharacterProgress.update).not.toHaveBeenCalled();
    },
  );

  it('serializes concurrent upgrades and spends a point only once', async () => {
    let storedProgress = createProgress();
    transaction.userCharacterProgress.findUnique.mockImplementation(() =>
      Promise.resolve(storedProgress),
    );
    transaction.userCharacterProgress.update.mockImplementation(() => {
      storedProgress = createProgress({
        skillPoints: 1,
        lowGradeSkillLevel: 2,
      });
      return Promise.resolve(storedProgress);
    });
    let transactionCall = 0;
    transactionRunner.mockImplementation(async (callbackOrQueries) => {
      if (typeof callbackOrQueries !== 'function') {
        return [];
      }
      transactionCall += 1;
      if (transactionCall === 2) {
        const serializationFailure = new Error(
          'serialization conflict',
        ) as Error & {
          code: string;
        };
        serializationFailure.code = 'P2034';
        throw serializationFailure;
      }
      return callbackOrQueries(transaction);
    });

    const [first, second] = await Promise.all([
      repository.upgradeSkill({
        nickname: 'test-player',
        characterId: 'erpin',
        skillType: 'LOW_GRADE',
        targetLevel: 2,
      }),
      repository.upgradeSkill({
        nickname: 'test-player',
        characterId: 'erpin',
        skillType: 'LOW_GRADE',
        targetLevel: 2,
      }),
    ]);

    expect(first).toMatchObject({ skillPoints: 1, lowGradeSkillLevel: 2 });
    expect(second).toEqual(first);
    expect(transaction.userCharacterProgress.update).toHaveBeenCalledTimes(1);
    expect(transactionRunner).toHaveBeenCalledTimes(3);
  });
});

function createProgress(
  overrides: Partial<{
    id: string;
    level: number;
    experience: number;
    skillPoints: number;
    lowGradeSkillLevel: number;
    highGradeSkillLevel: number;
  }> = {},
) {
  return {
    id: 'progress-id',
    level: 2,
    experience: 120,
    skillPoints: 2,
    lowGradeSkillLevel: 1,
    highGradeSkillLevel: 1,
    ...overrides,
  };
}

function createTransactionMock() {
  return {
    user: {
      findUnique: jest.fn().mockResolvedValue({ id: 'user-id' }),
    },
    userCharacterProgress: {
      findUnique: jest.fn().mockResolvedValue(createProgress()),
      update: jest.fn().mockImplementation(
        ({
          data,
        }: {
          data: {
            lowGradeSkillLevel?: unknown;
            highGradeSkillLevel?: unknown;
          };
        }) =>
          Promise.resolve(
            createProgress({
              skillPoints: 1,
              lowGradeSkillLevel: data.lowGradeSkillLevel ? 2 : 1,
              highGradeSkillLevel: data.highGradeSkillLevel ? 2 : 1,
            }),
          ),
      ),
    },
  };
}

async function expectRepositoryError(
  promise: Promise<unknown>,
  code: string,
): Promise<void> {
  try {
    await promise;
    throw new Error(`Expected repository error ${code}.`);
  } catch (error: unknown) {
    expect(error).toBeInstanceOf(UsersRepositoryError);
    expect((error as UsersRepositoryError).code).toBe(code);
  }
}
