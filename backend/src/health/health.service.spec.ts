jest.mock('../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { HealthService } from './health.service';
import { PrismaService } from '../database/prisma.service';

describe('HealthService', () => {
  it('reports the database as ready', async () => {
    const prisma = {
      $queryRaw: jest.fn().mockResolvedValue([{ '?column?': 1 }]),
    };
    const service = new HealthService(prisma as unknown as PrismaService);

    await expect(service.getReadiness()).resolves.toEqual({
      status: 'ok',
      database: 'up',
    });
  });

  it('returns a service-unavailable API error when the database fails', async () => {
    const prisma = {
      $queryRaw: jest.fn().mockRejectedValue(new Error('connection failed')),
    };
    const service = new HealthService(prisma as unknown as PrismaService);

    await expect(service.getReadiness()).rejects.toMatchObject({
      status: 503,
      response: {
        success: false,
        error: { code: 'DATABASE_UNAVAILABLE' },
      },
    });
  });
});
