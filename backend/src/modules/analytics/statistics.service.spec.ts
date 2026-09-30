import { ApiException } from '../../common/exceptions/api.exception';
import { StatisticsService } from './statistics.service';

describe('StatisticsService', () => {
  it('distinguishes a missing user from a user with no Runs', async () => {
    const repository = {
      loadCatalog: jest.fn().mockResolvedValue({
        users: [{ id: 'user-a', nickname: 'empty' }],
        characters: [],
        items: [],
        runs: [],
      }),
    };
    const service = new StatisticsService(repository as never);

    await expect(service.getUser('empty')).resolves.toEqual({
      nickname: 'empty',
      totalRuns: 0,
      clears: 0,
      clearRate: 0,
      averageReachedFloor: 0,
      highestReachedFloor: 0,
      averagePlayTime: 0,
    });
    try {
      await service.getUser('missing');
      throw new Error('Expected USER_NOT_FOUND.');
    } catch (error: unknown) {
      expect(error).toBeInstanceOf(ApiException);
      expect((error as ApiException).getResponse()).toEqual({
        success: false,
        error: {
          code: 'USER_NOT_FOUND',
          message: '존재하지 않는 유저입니다.',
        },
      });
    }
  });
});
