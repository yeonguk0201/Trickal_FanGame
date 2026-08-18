import { HttpStatus } from '@nestjs/common';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { ApiException } from '../../common/exceptions/api.exception';
import { UsersRepository } from './users.repository';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let repository: {
    findByNickname: jest.Mock;
    findRunsByUserId: jest.Mock;
  };

  beforeEach(() => {
    repository = {
      findByNickname: jest.fn().mockResolvedValue({ id: 'user-id' }),
      findRunsByUserId: jest.fn().mockResolvedValue({
        runs: [
          {
            id: 'run-id',
            character: { id: 'character-a', name: 'Character A' },
            reachedFloor: 3,
            playTime: 600,
            isCleared: true,
            killCount: 100,
            endedAt: new Date('2026-08-18T00:00:00.000Z'),
          },
        ],
        total: 1,
      }),
    };
    service = new UsersService(repository as unknown as UsersRepository);
  });

  it('returns the default first page of run history', async () => {
    await expect(service.getRunHistory('test-player', {})).resolves.toEqual({
      data: [
        {
          runId: 'run-id',
          character: { id: 'character-a', name: 'Character A' },
          reachedFloor: 3,
          playTime: 600,
          isCleared: true,
          killCount: 100,
          endedAt: new Date('2026-08-18T00:00:00.000Z'),
        },
      ],
      meta: { page: 1, limit: 20, total: 1 },
    });
    expect(repository.findRunsByUserId).toHaveBeenCalledWith('user-id', {
      page: 1,
      limit: 20,
    });
  });

  it('returns an empty history for a user without runs', async () => {
    repository.findRunsByUserId.mockResolvedValue({ runs: [], total: 0 });

    await expect(service.getRunHistory('test-player', {})).resolves.toEqual({
      data: [],
      meta: { page: 1, limit: 20, total: 0 },
    });
  });

  it('rejects an unknown user', async () => {
    repository.findByNickname.mockResolvedValue(null);

    await expect(service.getRunHistory('missing', {})).rejects.toMatchObject({
      status: HttpStatus.NOT_FOUND,
      response: {
        success: false,
        error: { code: 'USER_NOT_FOUND' },
      },
    } as ApiException);
    expect(repository.findRunsByUserId).not.toHaveBeenCalled();
  });
});
