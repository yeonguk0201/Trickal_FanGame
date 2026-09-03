jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { UsersController } from './users.controller';
import type { UsersService } from './users.service';

describe('UsersController H-4 progression API', () => {
  const progress = {
    characterId: 'erpin',
    level: 2,
    experience: 120,
    experienceToNextLevel: 500,
    skillPoints: 1,
    lowGradeSkillLevel: 2,
    highGradeSkillLevel: 1,
  };

  it('wraps exact nickname search results without exposing user ids', async () => {
    const results = [{ nickname: 'test-player' }];
    const service = { searchUsers: jest.fn().mockResolvedValue(results) };
    const controller = new UsersController(service as unknown as UsersService);

    await expect(controller.searchUsers({ q: 'test-player' })).resolves.toEqual(
      {
        success: true,
        data: results,
      },
    );
    expect(service.searchUsers).toHaveBeenCalledWith('test-player');
  });

  it('wraps the user profile in the success response contract', async () => {
    const profile = {
      id: 'user-id',
      nickname: 'test-player',
      stats: { totalRuns: 0 },
      characterProgress: [progress],
    };
    const service = { getUser: jest.fn().mockResolvedValue(profile) };
    const controller = new UsersController(service as unknown as UsersService);

    await expect(controller.getUser('test-player')).resolves.toEqual({
      success: true,
      data: profile,
    });
    expect(service.getUser).toHaveBeenCalledWith('test-player');
  });

  it('passes pagination fields and wraps Run history metadata', async () => {
    const history = {
      data: [{ runId: 'run-id' }],
      meta: { page: 2, limit: 10, total: 21, totalPages: 3 },
    };
    const service = { getRunHistory: jest.fn().mockResolvedValue(history) };
    const controller = new UsersController(service as unknown as UsersService);

    await expect(
      controller.getRunHistory('test-player', { page: 2, limit: 10 }),
    ).resolves.toEqual({ success: true, ...history });
    expect(service.getRunHistory).toHaveBeenCalledWith('test-player', {
      page: 2,
      limit: 10,
    });
  });

  it('passes the stable skill target fields and wraps the result', async () => {
    const service = { upgradeSkill: jest.fn().mockResolvedValue(progress) };
    const controller = new UsersController(service as unknown as UsersService);

    await expect(
      controller.upgradeSkill('test-player', 'erpin', 'LOW_GRADE', {
        targetLevel: 2,
      }),
    ).resolves.toEqual({ success: true, data: progress });
    expect(service.upgradeSkill).toHaveBeenCalledWith(
      'test-player',
      'erpin',
      'LOW_GRADE',
      2,
    );
  });
});
