import 'reflect-metadata';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { createRunRequestFingerprint } from '../../contracts/meta-progression';
import { Prisma } from '../../generated/prisma/client';
import { CreateRunDto, DeathReason } from './dto/create-run.dto';
import {
  RunsRepository,
  RunsRepositoryError,
  type PersistRunResult,
} from './runs.repository';

describe('RunsRepository H-3 transaction and idempotency', () => {
  type Transaction = ReturnType<typeof createTransactionMock>;
  type TransactionCallback = (
    transaction: Transaction,
  ) => Promise<PersistRunResult>;
  type TransactionRunner = (
    callback: TransactionCallback,
    options?: { isolationLevel?: string },
  ) => Promise<PersistRunResult>;

  let repository: RunsRepository;
  let prisma: {
    $transaction: jest.MockedFunction<TransactionRunner>;
    run: { findUnique: jest.Mock };
  };
  let transaction: Transaction;

  const validDto: CreateRunDto = {
    clientRunId: '10000000-0000-4000-8000-000000000001',
    userId: '00000000-0000-4000-8000-000000000001',
    characterId: 'erpin',
    gameVersion: '0.1.0',
    startedAt: '2026-08-17T10:00:00.000Z',
    endedAt: '2026-08-17T10:10:00.000Z',
    playTime: 600,
    reachedFloor: 3,
    isCleared: true,
    killCount: 100,
    deathReason: null,
    items: [
      {
        itemId: 'item-01',
        floor: 1,
        order: 1,
        acquiredAt: '2026-08-17T10:02:00.000Z',
      },
      {
        itemId: 'item-02',
        floor: 2,
        order: 2,
        acquiredAt: '2026-08-17T10:06:00.000Z',
      },
    ],
  };
  const expectedProgress = {
    characterId: 'erpin',
    level: 2,
    experience: 440,
    experienceToNextLevel: 500,
    skillPoints: 1,
    lowGradeSkillLevel: 1,
    highGradeSkillLevel: 1,
  };
  const emptyElifMeta = {
    elifBalance: 0,
    elifGained: 0,
    elifSpent: 0,
    shopUpgrades: [],
  };
  const expectedResult: PersistRunResult = {
    runId: '20000000-0000-4000-8000-000000000001',
    experienceGained: 840,
    progress: expectedProgress,
    meta: emptyElifMeta,
    created: true,
  };
  const elifDto: CreateRunDto = {
    ...validDto,
    elif: {
      gained: 3,
      spent: 5,
      upgradePurchases: [
        { upgradeId: 'shop-upgrade-discount', level: 1, floor: 1, order: 1 },
        { upgradeId: 'shop-upgrade-extra-slot', level: 1, floor: 2, order: 2 },
      ],
    },
  };

  beforeEach(() => {
    transaction = createTransactionMock();
    const transactionRunner = jest.fn<TransactionRunner>();
    transactionRunner.mockImplementation(
      (callback: TransactionCallback): Promise<PersistRunResult> =>
        callback(transaction),
    );
    prisma = {
      $transaction: transactionRunner,
      run: { findUnique: jest.fn() },
    };
    repository = new RunsRepository(prisma as never);
  });

  it.each([
    ['cleared', validDto],
    ['death', { ...validDto, isCleared: false, deathReason: DeathReason.BOSS }],
  ])(
    'atomically stores a %s run, items, XP, and progress',
    async (_label, dto) => {
      await expect(repository.create(dto)).resolves.toEqual(expectedResult);

      expect(prisma.$transaction).toHaveBeenCalledWith(expect.any(Function), {
        isolationLevel: Prisma.TransactionIsolationLevel.Serializable,
      });
      expect(transaction.run.create).toHaveBeenCalledWith(
        expect.objectContaining({
          // Jest asymmetric matchers are intentionally untyped test values.
          // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
          data: expect.objectContaining({
            experienceGained: 840,
            progressSnapshot: expectedProgress,
          }),
        }),
      );
      expect(transaction.runItem.createMany).toHaveBeenCalledTimes(1);
      expect(transaction.userCharacterProgress.update).toHaveBeenCalledWith(
        expect.objectContaining({
          data: { level: 2, experience: 440, skillPoints: 1 },
        }),
      );
    },
  );

  it('keeps the balance and writes no Elif rows for a Run without Elif', async () => {
    await repository.create(validDto);

    expect(transaction.user.update).not.toHaveBeenCalled();
    expect(transaction.userShopUpgrade.upsert).not.toHaveBeenCalled();
    expect(transaction.runUpgradePurchase.createMany).not.toHaveBeenCalled();
    expect(transaction.run.create).toHaveBeenCalledWith(
      expect.objectContaining({
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        data: expect.objectContaining({
          elifGained: 0,
          elifSpent: 0,
          elifShortfall: 0,
          elifSnapshot: emptyElifMeta,
        }),
      }),
    );
  });

  it('applies Elif gain, spending, and upgrades in the Run transaction', async () => {
    transaction.user.findUnique.mockResolvedValue({
      id: validDto.userId,
      elifBalance: 6,
    });
    const meta = {
      elifBalance: 4,
      elifGained: 3,
      elifSpent: 5,
      shopUpgrades: [
        { upgradeId: 'shop-upgrade-discount', level: 1 },
        { upgradeId: 'shop-upgrade-extra-slot', level: 1 },
      ],
    };

    await expect(repository.create(elifDto)).resolves.toEqual({
      ...expectedResult,
      meta,
    });
    expect(transaction.run.create).toHaveBeenCalledWith(
      expect.objectContaining({
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        data: expect.objectContaining({
          elifGained: 3,
          elifSpent: 5,
          elifShortfall: 0,
          elifSnapshot: meta,
        }),
      }),
    );
    expect(transaction.runUpgradePurchase.createMany).toHaveBeenCalledWith({
      data: [
        {
          runId: expectedResult.runId,
          upgradeId: 'shop-upgrade-discount',
          level: 1,
          floor: 1,
          purchaseOrder: 1,
        },
        {
          runId: expectedResult.runId,
          upgradeId: 'shop-upgrade-extra-slot',
          level: 1,
          floor: 2,
          purchaseOrder: 2,
        },
      ],
    });
    expect(transaction.userShopUpgrade.upsert).toHaveBeenCalledTimes(2);
    expect(transaction.user.update).toHaveBeenCalledWith({
      where: { id: validDto.userId },
      data: { elifBalance: 4 },
    });
  });

  it('skips and refunds a level another Run already stored', async () => {
    transaction.user.findUnique.mockResolvedValue({
      id: validDto.userId,
      elifBalance: 6,
    });
    transaction.userShopUpgrade.findMany.mockResolvedValue([
      { upgradeId: 'shop-upgrade-discount', level: 1 },
    ]);

    const result = await repository.create(elifDto);

    // spent 5 − refunded discount Lv.1 cost 2 = 3; balance 6 + 3 − 3 = 6.
    expect(result.meta).toEqual({
      elifBalance: 6,
      elifGained: 3,
      elifSpent: 3,
      shopUpgrades: [
        { upgradeId: 'shop-upgrade-discount', level: 1 },
        { upgradeId: 'shop-upgrade-extra-slot', level: 1 },
      ],
    });
    expect(transaction.userShopUpgrade.upsert).toHaveBeenCalledTimes(1);
    expect(transaction.userShopUpgrade.upsert).toHaveBeenCalledWith(
      expect.objectContaining({
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        create: expect.objectContaining({
          upgradeId: 'shop-upgrade-extra-slot',
          level: 1,
        }),
      }),
    );
    expect(transaction.user.update).not.toHaveBeenCalled();
  });

  it('saves the Run and records the shortfall when the balance would go negative', async () => {
    transaction.user.findUnique.mockResolvedValue({
      id: validDto.userId,
      elifBalance: 1,
    });

    const result = await repository.create(elifDto);

    expect(result.meta.elifBalance).toBe(0);
    expect(transaction.run.create).toHaveBeenCalledWith(
      expect.objectContaining({
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        data: expect.objectContaining({ elifShortfall: 1 }),
      }),
    );
    expect(transaction.user.update).toHaveBeenCalledWith({
      where: { id: validDto.userId },
      data: { elifBalance: 0 },
    });
  });

  it('caps the stored balance at the server maximum', async () => {
    transaction.user.findUnique.mockResolvedValue({
      id: validDto.userId,
      elifBalance: 9998,
    });

    const result = await repository.create({
      ...validDto,
      elif: { gained: 5, spent: 0, upgradePurchases: [] },
    });

    expect(result.meta.elifBalance).toBe(9999);
  });

  it('rejects an upgrade whose previous level is not stored', async () => {
    await expectRepositoryError(
      repository.create({
        ...validDto,
        elif: {
          gained: 0,
          spent: 3,
          upgradePurchases: [
            {
              upgradeId: 'shop-upgrade-discount',
              level: 2,
              floor: 1,
              order: 1,
            },
          ],
        },
      }),
      'ELIF_UPGRADE_LEVEL_GAP',
    );
    expect(transaction.run.create).not.toHaveBeenCalled();
    expect(transaction.user.update).not.toHaveBeenCalled();
  });

  it('returns the stored Elif snapshot once for an identical Elif retransmission', async () => {
    const meta = {
      elifBalance: 4,
      elifGained: 3,
      elifSpent: 5,
      shopUpgrades: [{ upgradeId: 'shop-upgrade-discount', level: 1 }],
    };
    transaction.run.findUnique.mockResolvedValue({
      ...createExistingRun(elifDto),
      elifSnapshot: meta,
    });

    await expect(repository.create(elifDto)).resolves.toEqual({
      ...expectedResult,
      meta,
      created: false,
    });
    expect(transaction.user.update).not.toHaveBeenCalled();
    expect(transaction.userShopUpgrade.upsert).not.toHaveBeenCalled();
    expect(transaction.runUpgradePurchase.createMany).not.toHaveBeenCalled();
  });

  it('rejects changed Elif content for the same clientRunId', async () => {
    transaction.run.findUnique.mockResolvedValue(createExistingRun(elifDto));

    await expectRepositoryError(
      repository.create({
        ...elifDto,
        elif: { ...elifDto.elif!, gained: 4 },
      }),
      'RUN_IDEMPOTENCY_CONFLICT',
    );
  });

  it('fails a retransmission whose stored Elif snapshot is missing', async () => {
    transaction.run.findUnique.mockResolvedValue({
      ...createExistingRun(validDto),
      elifSnapshot: null,
    });

    await expectRepositoryError(
      repository.create(validDto),
      'RUN_RESULT_UNAVAILABLE',
    );
  });

  it('loads a Run detail with items ordered by acquisition order', async () => {
    prisma.run.findUnique.mockResolvedValue({ id: 'run-id', runItems: [] });

    await expect(repository.findDetailById('run-id')).resolves.toEqual({
      id: 'run-id',
      runItems: [],
    });
    // Jest stores mock call arguments as untyped test values.
    // eslint-disable-next-line @typescript-eslint/no-unsafe-member-access
    const query = prisma.run.findUnique.mock.calls[0]?.[0] as unknown as {
      where: { id: string };
      select: { runItems: { orderBy: { itemOrder: string } } };
    };
    expect(query.where).toEqual({ id: 'run-id' });
    expect(query.select.runItems.orderBy).toEqual({ itemOrder: 'asc' });
  });

  it('returns the stored result for an identical retransmission without writing', async () => {
    transaction.run.findUnique.mockResolvedValue(createExistingRun(validDto));

    await expect(repository.create(validDto)).resolves.toEqual({
      ...expectedResult,
      created: false,
    });
    expect(transaction.run.create).not.toHaveBeenCalled();
    expect(transaction.runItem.createMany).not.toHaveBeenCalled();
    expect(transaction.userCharacterProgress.update).not.toHaveBeenCalled();
  });

  it('rejects the same clientRunId with changed content before writing', async () => {
    transaction.run.findUnique.mockResolvedValue(createExistingRun(validDto));

    await expectRepositoryError(
      repository.create({ ...validDto, killCount: 101 }),
      'RUN_IDEMPOTENCY_CONFLICT',
    );
    expect(transaction.run.create).not.toHaveBeenCalled();
    expect(transaction.userCharacterProgress.upsert).not.toHaveBeenCalled();
  });

  it.each([
    [
      'user',
      'USER_NOT_FOUND',
      (tx: typeof transaction) => tx.user.findUnique.mockResolvedValue(null),
    ],
    [
      'character',
      'CHARACTER_NOT_FOUND',
      (tx: typeof transaction) =>
        tx.character.findFirst.mockResolvedValue(null),
    ],
    [
      'item',
      'ITEM_NOT_FOUND',
      (tx: typeof transaction) => tx.item.findMany.mockResolvedValue([]),
    ],
  ])(
    'rejects an invalid %s reference without creating a run',
    async (_label, code, arrange) => {
      arrange(transaction);

      await expectRepositoryError(repository.create(validDto), code);
      expect(transaction.run.create).not.toHaveBeenCalled();
      expect(transaction.runItem.createMany).not.toHaveBeenCalled();
      expect(transaction.userCharacterProgress.update).not.toHaveBeenCalled();
    },
  );

  it('rejects the whole transaction when RunItem persistence fails', async () => {
    transaction.runItem.createMany.mockRejectedValue(
      new Error('simulated RunItem failure'),
    );

    await expect(repository.create(validDto)).rejects.toThrow(
      'simulated RunItem failure',
    );
    expect(prisma.$transaction).toHaveBeenCalledTimes(1);
    expect(transaction.userCharacterProgress.update).not.toHaveBeenCalled();
  });

  it('returns one stored result and awards XP once for concurrent retransmission', async () => {
    let finishFirstTransaction: () => void = () => undefined;
    const firstTransactionFinished = new Promise<void>((resolve) => {
      finishFirstTransaction = resolve;
    });
    let transactionCall = 0;

    prisma.$transaction.mockImplementation(async (callback) => {
      transactionCall += 1;
      if (transactionCall === 1) {
        const result = await callback(transaction);
        transaction.run.findUnique.mockResolvedValue(
          createExistingRun(validDto),
        );
        finishFirstTransaction();
        return result;
      }
      if (transactionCall === 2) {
        await firstTransactionFinished;
        const uniqueConflict = new Error(
          'simulated unique conflict',
        ) as Error & {
          code: string;
        };
        uniqueConflict.code = 'P2002';
        throw uniqueConflict;
      }
      return callback(transaction);
    });

    const [first, second] = await Promise.all([
      repository.create(validDto),
      repository.create(validDto),
    ]);

    expect(first).toEqual(expectedResult);
    expect(second).toEqual({ ...expectedResult, created: false });
    expect(transaction.run.create).toHaveBeenCalledTimes(1);
    expect(transaction.runItem.createMany).toHaveBeenCalledTimes(1);
    expect(transaction.userCharacterProgress.update).toHaveBeenCalledTimes(1);
    expect(prisma.$transaction).toHaveBeenCalledTimes(3);
  });
});

function createTransactionMock() {
  return {
    run: {
      findUnique: jest.fn().mockResolvedValue(null),
      create: jest.fn().mockResolvedValue({
        id: '20000000-0000-4000-8000-000000000001',
      }),
    },
    runItem: {
      createMany: jest.fn().mockResolvedValue({ count: 2 }),
    },
    user: {
      findUnique: jest.fn().mockResolvedValue({
        id: '00000000-0000-4000-8000-000000000001',
        elifBalance: 0,
      }),
      update: jest.fn().mockResolvedValue({ id: 'user-id' }),
    },
    userShopUpgrade: {
      findMany: jest.fn().mockResolvedValue([]),
      upsert: jest.fn().mockResolvedValue({ id: 'upgrade-id' }),
    },
    runUpgradePurchase: {
      createMany: jest.fn().mockResolvedValue({ count: 0 }),
    },
    character: {
      findFirst: jest.fn().mockResolvedValue({ id: 'erpin' }),
    },
    item: {
      findMany: jest.fn().mockResolvedValue([
        { id: 'item-01', rarity: 'COMMON', isActive: true },
        { id: 'item-02', rarity: 'RARE', isActive: true },
      ]),
    },
    userCharacterProgress: {
      upsert: jest.fn().mockResolvedValue({
        level: 1,
        experience: 0,
        skillPoints: 0,
        lowGradeSkillLevel: 1,
        highGradeSkillLevel: 1,
      }),
      update: jest.fn().mockResolvedValue({ id: 'progress-id' }),
    },
  };
}

function createExistingRun(dto: CreateRunDto) {
  return {
    id: '20000000-0000-4000-8000-000000000001',
    requestFingerprint: createRunRequestFingerprint(dto),
    experienceGained: 840,
    progressSnapshot: {
      characterId: 'erpin',
      level: 2,
      experience: 440,
      experienceToNextLevel: 500,
      skillPoints: 1,
      lowGradeSkillLevel: 1,
      highGradeSkillLevel: 1,
    },
    elifSnapshot: {
      elifBalance: 0,
      elifGained: 0,
      elifSpent: 0,
      shopUpgrades: [],
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
    expect(error).toBeInstanceOf(RunsRepositoryError);
    expect((error as RunsRepositoryError).code).toBe(code);
  }
}
