import { HttpStatus } from '@nestjs/common';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { ApiException } from '../../common/exceptions/api.exception';
import { UsersRepository, UsersRepositoryError } from './users.repository';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let repository: {
    findPublicByNickname: jest.Mock;
    findByNickname: jest.Mock;
    findByNicknameWithProgress: jest.Mock;
    getRunStats: jest.Mock;
    findRunsByUserId: jest.Mock;
    upgradeSkill: jest.Mock;
  };

  beforeEach(() => {
    repository = {
      findPublicByNickname: jest
        .fn()
        .mockResolvedValue({ nickname: 'test-player' }),
      findByNickname: jest.fn().mockResolvedValue({ id: 'user-id' }),
      findByNicknameWithProgress: jest.fn().mockResolvedValue({
        id: 'user-id',
        nickname: 'test-player',
        characterProgress: [
          {
            characterId: 'erpin',
            character: { name: '에르핀' },
            level: 3,
            experience: 120,
            skillPoints: 2,
            lowGradeSkillLevel: 1,
            highGradeSkillLevel: 1,
          },
        ],
      }),
      getRunStats: jest.fn().mockResolvedValue({
        totalRuns: 4,
        clears: 1,
        averagePlayTime: 582,
        averageFloor: 2.5,
        highestFloor: 3,
      }),
      findRunsByUserId: jest.fn().mockResolvedValue({
        runs: [
          {
            id: 'run-id',
            character: { id: 'erpin', name: '에르핀' },
            reachedFloor: 3,
            playTime: 600,
            isCleared: true,
            killCount: 100,
            endedAt: new Date('2026-08-18T00:00:00.000Z'),
          },
        ],
        total: 1,
      }),
      upgradeSkill: jest.fn().mockResolvedValue({
        characterId: 'erpin',
        level: 3,
        experience: 120,
        experienceToNextLevel: 600,
        skillPoints: 1,
        lowGradeSkillLevel: 2,
        highGradeSkillLevel: 1,
      }),
    };
    service = new UsersService(repository as unknown as UsersRepository);
  });

  it('returns one exact public nickname search result', async () => {
    await expect(service.searchUsers('test-player')).resolves.toEqual([
      { nickname: 'test-player' },
    ]);
    expect(repository.findPublicByNickname).toHaveBeenCalledWith('test-player');
  });

  it('returns an empty search result when the exact nickname does not exist', async () => {
    repository.findPublicByNickname.mockResolvedValue(null);

    await expect(service.searchUsers('missing')).resolves.toEqual([]);
  });

  it('returns user stats and character progression contract fields', async () => {
    await expect(service.getUser('test-player')).resolves.toEqual({
      id: 'user-id',
      nickname: 'test-player',
      stats: {
        totalRuns: 4,
        clears: 1,
        winRate: 25,
        averagePlayTime: 582,
        averageFloor: 2.5,
        highestFloor: 3,
      },
      characterProgress: [
        {
          characterId: 'erpin',
          characterName: '에르핀',
          level: 3,
          maxLevel: 19,
          experience: 120,
          experienceToNextLevel: 600,
          skillPoints: 2,
          lowGradeSkillLevel: 1,
          highGradeSkillLevel: 1,
          maxSkillLevel: 10,
        },
      ],
    });
  });

  it('returns zero stats and no progress for a new user', async () => {
    repository.findByNicknameWithProgress.mockResolvedValue({
      id: 'user-id',
      nickname: 'new-player',
      characterProgress: [],
    });
    repository.getRunStats.mockResolvedValue({
      totalRuns: 0,
      clears: 0,
      averagePlayTime: null,
      averageFloor: null,
      highestFloor: null,
    });

    await expect(service.getUser('new-player')).resolves.toMatchObject({
      stats: {
        totalRuns: 0,
        clears: 0,
        winRate: 0,
        averagePlayTime: 0,
        averageFloor: 0,
        highestFloor: 0,
      },
      characterProgress: [],
    });
  });

  it('rejects a missing user profile before querying stats', async () => {
    repository.findByNicknameWithProgress.mockResolvedValue(null);

    await expect(service.getUser('missing')).rejects.toMatchObject({
      status: HttpStatus.NOT_FOUND,
      response: {
        success: false,
        error: { code: 'USER_NOT_FOUND' },
      },
    } as ApiException);
    expect(repository.getRunStats).not.toHaveBeenCalled();
  });

  it('upgrades a valid skill and returns its progress snapshot', async () => {
    await expect(
      service.upgradeSkill('test-player', 'erpin', 'LOW_GRADE', 2),
    ).resolves.toMatchObject({ skillPoints: 1, lowGradeSkillLevel: 2 });
    expect(repository.upgradeSkill).toHaveBeenCalledWith({
      nickname: 'test-player',
      characterId: 'erpin',
      skillType: 'LOW_GRADE',
      targetLevel: 2,
    });
  });

  it('rejects an invalid skill type before opening a transaction', async () => {
    await expect(
      service.upgradeSkill('test-player', 'erpin', 'ULTIMATE', 2),
    ).rejects.toMatchObject({
      status: HttpStatus.UNPROCESSABLE_ENTITY,
      response: {
        success: false,
        error: { code: 'INVALID_SKILL_TYPE' },
      },
    } as ApiException);
    expect(repository.upgradeSkill).not.toHaveBeenCalled();
  });

  it.each([
    ['USER_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['CHARACTER_PROGRESS_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['SKILL_POINT_NOT_ENOUGH', HttpStatus.UNPROCESSABLE_ENTITY],
    ['SKILL_LEVEL_MAX', HttpStatus.UNPROCESSABLE_ENTITY],
    ['INVALID_SKILL_TARGET_LEVEL', HttpStatus.UNPROCESSABLE_ENTITY],
  ] as const)(
    'maps repository error %s to the API contract',
    async (code, status) => {
      repository.upgradeSkill.mockRejectedValue(
        new UsersRepositoryError(code, '강화 실패'),
      );

      await expect(
        service.upgradeSkill('test-player', 'erpin', 'LOW_GRADE', 2),
      ).rejects.toMatchObject({
        status,
        response: { success: false, error: { code } },
      } as ApiException);
    },
  );

  it('returns the default first page of run history', async () => {
    await expect(service.getRunHistory('test-player', {})).resolves.toEqual({
      data: [
        {
          runId: 'run-id',
          character: { id: 'erpin', name: '에르핀' },
          reachedFloor: 3,
          playTime: 600,
          isCleared: true,
          killCount: 100,
          endedAt: new Date('2026-08-18T00:00:00.000Z'),
        },
      ],
      meta: { page: 1, limit: 20, total: 1, totalPages: 1 },
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
      meta: { page: 1, limit: 20, total: 0, totalPages: 1 },
    });
  });

  it('rejects a page beyond the last available page', async () => {
    repository.findRunsByUserId.mockResolvedValue({ runs: [], total: 21 });

    await expect(
      service.getRunHistory('test-player', { page: 4, limit: 10 }),
    ).rejects.toMatchObject({
      status: HttpStatus.UNPROCESSABLE_ENTITY,
      response: {
        success: false,
        error: { code: 'RUN_PAGE_OUT_OF_RANGE' },
      },
    } as ApiException);
  });

  it('accepts page one for a user without runs', async () => {
    repository.findRunsByUserId.mockResolvedValue({ runs: [], total: 0 });

    await expect(
      service.getRunHistory('test-player', { page: 1, limit: 10 }),
    ).resolves.toMatchObject({
      data: [],
      meta: { page: 1, limit: 10, total: 0, totalPages: 1 },
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
