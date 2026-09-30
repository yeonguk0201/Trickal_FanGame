import 'reflect-metadata';
import {
  buildRanking,
  buildStatistics,
  buildUserStatistics,
} from './analytics.domain';
import type { AnalyticsCatalog } from './analytics.types';
import { RankingType } from './dto/get-rankings-query.dto';

describe('analytics calculations with a hand-calculated data set', () => {
  const catalog: AnalyticsCatalog = {
    users: [
      { id: 'user-a', nickname: 'alpha' },
      { id: 'user-b', nickname: 'beta' },
      { id: 'user-c', nickname: 'empty' },
    ],
    characters: [
      { id: 'erpin', name: '에르핀' },
      { id: 'komi', name: '코미' },
    ],
    items: [
      { id: 'item-01', name: '첫 번째' },
      { id: 'item-02', name: '두 번째' },
      { id: 'item-03', name: '미획득' },
    ],
    runs: [
      run(
        'run-a1',
        'user-a',
        'alpha',
        'erpin',
        600,
        3,
        true,
        ['item-01', 'item-01', 'item-02'],
        1,
      ),
      run('run-a2', 'user-a', 'alpha', 'erpin', 400, 2, false, ['item-01'], 2),
      run('run-b1', 'user-b', 'beta', 'erpin', 550, 3, true, ['item-02'], 3),
      run('run-b2', 'user-b', 'beta', 'komi', 300, 1, false, [], 4),
    ],
  };

  it('calculates overview, character, unique-Run item, and floor statistics', () => {
    const result = buildStatistics(catalog);

    expect(result.overview).toEqual({
      totalUsers: 3,
      totalRuns: 4,
      totalClears: 2,
      clearRate: 50,
      averagePlayTime: 462.5,
      averageReachedFloor: 2.25,
      highestReachedFloor: 3,
    });
    expect(result.characters[0]).toEqual({
      characterId: 'erpin',
      characterName: '에르핀',
      totalRuns: 3,
      clears: 2,
      clearRate: (2 / 3) * 100,
      averageReachedFloor: 8 / 3,
      averagePlayTime: 1550 / 3,
    });
    expect(result.items).toEqual([
      {
        itemId: 'item-01',
        itemName: '첫 번째',
        acquiredRunCount: 2,
        selectionRate: 50,
        clearCountAfterAcquisition: 1,
        clearRateAfterAcquisition: 50,
      },
      {
        itemId: 'item-02',
        itemName: '두 번째',
        acquiredRunCount: 2,
        selectionRate: 50,
        clearCountAfterAcquisition: 2,
        clearRateAfterAcquisition: 100,
      },
      {
        itemId: 'item-03',
        itemName: '미획득',
        acquiredRunCount: 0,
        selectionRate: 0,
        clearCountAfterAcquisition: 0,
        clearRateAfterAcquisition: 0,
      },
    ]);
    expect(result.floors).toEqual([
      {
        floor: 1,
        reachedRunCount: 4,
        reachRate: 100,
        deathCount: 1,
        deathRate: 25,
        clearCount: 2,
        clearRate: 50,
      },
      {
        floor: 2,
        reachedRunCount: 3,
        reachRate: 75,
        deathCount: 1,
        deathRate: (1 / 3) * 100,
        clearCount: 2,
        clearRate: (2 / 3) * 100,
      },
      {
        floor: 3,
        reachedRunCount: 2,
        reachRate: 50,
        deathCount: 0,
        deathRate: 0,
        clearCount: 2,
        clearRate: 100,
      },
    ]);
  });

  it('returns zeroes for a user with no Runs and empty arrays for an empty DB', () => {
    expect(buildUserStatistics(catalog, 'user-c')).toEqual({
      totalRuns: 0,
      clears: 0,
      clearRate: 0,
      averageReachedFloor: 0,
      highestReachedFloor: 0,
      averagePlayTime: 0,
    });
    expect(
      buildStatistics({ users: [], characters: [], items: [], runs: [] }),
    ).toEqual({
      overview: {
        totalUsers: 0,
        totalRuns: 0,
        totalClears: 0,
        clearRate: 0,
        averagePlayTime: 0,
        averageReachedFloor: 0,
        highestReachedFloor: 0,
      },
      characters: [],
      items: [],
      floors: [],
    });
  });

  it('selects one best Run per user and excludes deaths from fastest clear', () => {
    expect(
      buildRanking(catalog.runs, RankingType.HIGHEST_FLOOR).map(
        (entry) => 'runId' in entry && entry.runId,
      ),
    ).toEqual(['run-b1', 'run-a1']);
    expect(
      buildRanking(catalog.runs, RankingType.FASTEST_CLEAR).map(
        (entry) => 'runId' in entry && entry.runId,
      ),
    ).toEqual(['run-b1', 'run-a1']);
    expect(buildRanking(catalog.runs, RankingType.MOST_CLEARS)).toEqual([
      { nickname: 'alpha', clears: 1, totalRuns: 2 },
      { nickname: 'beta', clears: 1, totalRuns: 2 },
    ]);
  });

  it('handles a single death Run without producing a fastest-clear entry', () => {
    expect(buildRanking([catalog.runs[3]], RankingType.FASTEST_CLEAR)).toEqual(
      [],
    );
  });
});

function run(
  id: string,
  userId: string,
  nickname: string,
  characterId: string,
  playTime: number,
  reachedFloor: number,
  isCleared: boolean,
  itemIds: string[],
  day: number,
) {
  return {
    id,
    userId,
    nickname,
    characterId,
    characterName: characterId,
    playTime,
    reachedFloor,
    isCleared,
    endedAt: new Date(`2026-09-0${day}T00:00:00.000Z`),
    itemIds: [...new Set(itemIds)],
  };
}
