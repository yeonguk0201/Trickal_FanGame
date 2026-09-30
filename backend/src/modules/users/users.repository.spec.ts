import 'reflect-metadata';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { Prisma } from '../../generated/prisma/client';
import { UsersRepository, UsersRepositoryError } from './users.repository';

describe('UsersRepository user search', () => {
  it('uses the unique nickname index and selects only the public nickname', async () => {
    const findUnique = jest.fn().mockResolvedValue({ nickname: 'test-player' });
    const repository = new UsersRepository({ user: { findUnique } } as never);

    await expect(
      repository.findPublicByNickname('test-player'),
    ).resolves.toEqual({ nickname: 'test-player' });
    expect(findUnique).toHaveBeenCalledWith({
      where: { nickname: 'test-player' },
      select: { nickname: true },
    });
  });

  it('propagates database failures for the common exception filter', async () => {
    const databaseError = new Error('database unavailable');
    const repository = new UsersRepository({
      user: { findUnique: jest.fn().mockRejectedValue(databaseError) },
    } as never);

    await expect(repository.findPublicByNickname('test-player')).rejects.toBe(
      databaseError,
    );
  });
});

describe('UsersRepository local profile registration', () => {
  const clientProfileId = '10000000-0000-4000-8000-000000000001';
  const progress = {
    characterId: 'erpin',
    level: 1,
    experience: 0,
    skillPoints: 0,
    lowGradeSkillLevel: 1,
    highGradeSkillLevel: 1,
  };

  it('creates the user and initial Erpin progress in one transaction', async () => {
    const findUnique = jest
      .fn()
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null);
    const createUser = jest.fn().mockResolvedValue({
      id: 'user-id',
      clientProfileId,
      nickname: 'Yeonguk',
    });
    const createProgress = jest.fn().mockResolvedValue(progress);
    const transaction = jest.fn().mockImplementation(async (callback) =>
      callback({
        user: { create: createUser },
        userCharacterProgress: { create: createProgress },
      }),
    );
    const repository = new UsersRepository({
      user: { findUnique },
      $transaction: transaction,
    } as never);

    await expect(
      repository.createUser({ clientProfileId, nickname: 'Yeonguk' }),
    ).resolves.toEqual({
      id: 'user-id',
      clientProfileId,
      nickname: 'Yeonguk',
      characterProgress: [progress],
      isNew: true,
    });
    expect(findUnique).toHaveBeenNthCalledWith(2, {
      where: { nickname: 'Yeonguk' },
      select: { id: true },
    });
    expect(transaction).toHaveBeenCalledTimes(1);
    expect(createUser).toHaveBeenCalledWith({
      data: { clientProfileId, nickname: 'Yeonguk' },
      select: { id: true, clientProfileId: true, nickname: true },
    });
    expect(createProgress).toHaveBeenCalledWith({
      data: { userId: 'user-id', characterId: 'erpin' },
      select: {
        characterId: true,
        level: true,
        experience: true,
        skillPoints: true,
        lowGradeSkillLevel: true,
        highGradeSkillLevel: true,
      },
    });
  });

  it('returns the existing result for the same profile id and exact nickname', async () => {
    const existing = {
      id: 'user-id',
      clientProfileId,
      nickname: 'yeonguk',
      characterProgress: [progress],
    };
    const findUnique = jest.fn().mockResolvedValue(existing);
    const transaction = jest.fn();
    const repository = new UsersRepository({
      user: { findUnique },
      $transaction: transaction,
    } as never);

    await expect(
      repository.createUser({ clientProfileId, nickname: 'yeonguk' }),
    ).resolves.toEqual({ ...existing, isNew: false });
    expect(findUnique).toHaveBeenCalledTimes(1);
    expect(transaction).not.toHaveBeenCalled();
  });

  it('rejects a different nickname for the same profile id', async () => {
    const repository = new UsersRepository({
      user: {
        findUnique: jest.fn().mockResolvedValue({
          id: 'user-id',
          clientProfileId,
          nickname: 'yeonguk',
          characterProgress: [progress],
        }),
      },
    } as never);

    await expectRepositoryError(
      repository.createUser({ clientProfileId, nickname: 'Yeonguk' }),
      'PROFILE_IDEMPOTENCY_CONFLICT',
    );
  });

  it('rejects an exact nickname owned by another profile', async () => {
    const findUnique = jest
      .fn()
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce({ id: 'other-user-id' });
    const repository = new UsersRepository({ user: { findUnique } } as never);

    await expectRepositoryError(
      repository.createUser({ clientProfileId, nickname: 'yeonguk' }),
      'NICKNAME_ALREADY_EXISTS',
    );
    expect(findUnique).toHaveBeenNthCalledWith(2, {
      where: { nickname: 'yeonguk' },
      select: { id: true },
    });
  });

  it('returns the concurrent winner for the same idempotent request', async () => {
    const existing = {
      id: 'user-id',
      clientProfileId,
      nickname: 'yeonguk',
      characterProgress: [progress],
    };
    const findUnique = jest
      .fn()
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(existing);
    const uniqueConflict = Object.assign(new Error('unique conflict'), {
      code: 'P2002',
    });
    const repository = new UsersRepository({
      user: { findUnique },
      $transaction: jest.fn().mockRejectedValue(uniqueConflict),
    } as never);

    await expect(
      repository.createUser({ clientProfileId, nickname: 'yeonguk' }),
    ).resolves.toEqual({ ...existing, isNew: false });
  });

  it('classifies a concurrent exact nickname conflict from the database constraint', async () => {
    const findUnique = jest
      .fn()
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce({ id: 'other-user-id' });
    const uniqueConflict = Object.assign(new Error('unique conflict'), {
      code: 'P2002',
    });
    const repository = new UsersRepository({
      user: { findUnique },
      $transaction: jest.fn().mockRejectedValue(uniqueConflict),
    } as never);

    await expectRepositoryError(
      repository.createUser({ clientProfileId, nickname: 'yeonguk' }),
      'NICKNAME_ALREADY_EXISTS',
    );
  });
});

describe('UsersRepository run history pagination', () => {
  it('uses a stable endedAt and id order with exact page boundaries', async () => {
    const findMany = jest
      .fn()
      .mockResolvedValue([{ id: 'run-04' }, { id: 'run-03' }]);
    const count = jest.fn().mockResolvedValue(5);
    const transaction = jest
      .fn()
      .mockImplementation((queries: Promise<unknown>[]) =>
        Promise.all(queries),
      );
    const repository = new UsersRepository({
      run: { findMany, count },
      $transaction: transaction,
    } as never);

    await expect(
      repository.findRunsByUserId('user-id', { page: 2, limit: 2 }),
    ).resolves.toEqual({
      runs: [{ id: 'run-04' }, { id: 'run-03' }],
      total: 5,
    });
    expect(findMany).toHaveBeenCalledWith(
      expect.objectContaining({
        where: { userId: 'user-id' },
        orderBy: [{ endedAt: 'desc' }, { id: 'desc' }],
        skip: 2,
        take: 2,
      }),
    );
    expect(count).toHaveBeenCalledWith({ where: { userId: 'user-id' } });
  });
});

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
