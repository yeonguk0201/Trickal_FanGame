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
