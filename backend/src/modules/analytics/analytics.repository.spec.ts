import 'reflect-metadata';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { AnalyticsRepository } from './analytics.repository';

describe('AnalyticsRepository', () => {
  it('loads only analytics fields in one transaction and deduplicates items per Run', async () => {
    const transaction = {
      user: {
        findMany: jest
          .fn()
          .mockResolvedValue([{ id: 'user-a', nickname: 'alpha' }]),
      },
      character: {
        findMany: jest
          .fn()
          .mockResolvedValue([{ id: 'erpin', name: '에르핀' }]),
      },
      item: {
        findMany: jest
          .fn()
          .mockResolvedValue([{ id: 'item-01', name: '아이템' }]),
      },
      run: {
        findMany: jest.fn().mockResolvedValue([
          {
            id: 'run-a',
            userId: 'user-a',
            characterId: 'erpin',
            playTime: 600,
            reachedFloor: 3,
            isCleared: true,
            endedAt: new Date('2026-09-01T00:00:00.000Z'),
            user: { nickname: 'alpha' },
            character: { name: '에르핀' },
            runItems: [{ itemId: 'item-01' }, { itemId: 'item-01' }],
          },
        ]),
      },
    };
    const prisma = {
      $transaction: jest
        .fn()
        .mockImplementation(
          (callback: (client: typeof transaction) => Promise<unknown>) =>
            callback(transaction),
        ),
    };
    const repository = new AnalyticsRepository(prisma as never);

    const catalog = await repository.loadCatalog();

    expect(prisma.$transaction).toHaveBeenCalledTimes(1);
    expect(catalog.runs[0].itemIds).toEqual(['item-01']);
    expect(transaction.run.findMany).toHaveBeenCalledWith(
      expect.objectContaining({
        select: expect.objectContaining({
          playTime: true,
          reachedFloor: true,
          isCleared: true,
          runItems: { select: { itemId: true } },
        }) as unknown,
      }),
    );
  });
});
