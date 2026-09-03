import { RankingsService } from './rankings.service';
import { RankingType } from './dto/get-rankings-query.dto';

describe('RankingsService', () => {
  it('paginates after globally sorting and preserves global rank numbers', async () => {
    const repository = {
      loadCatalog: jest.fn().mockResolvedValue({
        users: [],
        characters: [],
        items: [],
        runs: [
          makeRun('run-a', 'user-a', 'alpha', 3, 600),
          makeRun('run-b', 'user-b', 'beta', 2, 500),
        ],
      }),
    };
    const service = new RankingsService(repository as never);

    await expect(
      service.getRankings({
        type: RankingType.HIGHEST_FLOOR,
        page: 2,
        limit: 1,
      }),
    ).resolves.toEqual({
      data: [expect.objectContaining({ rank: 2, runId: 'run-b' })],
      meta: {
        type: RankingType.HIGHEST_FLOOR,
        page: 2,
        limit: 1,
        total: 2,
        totalPages: 2,
      },
    });
  });
});

function makeRun(
  id: string,
  userId: string,
  nickname: string,
  reachedFloor: number,
  playTime: number,
) {
  return {
    id,
    userId,
    nickname,
    characterId: 'erpin',
    characterName: '에르핀',
    playTime,
    reachedFloor,
    isCleared: true,
    endedAt: new Date('2026-09-01T00:00:00.000Z'),
    itemIds: [],
  };
}
