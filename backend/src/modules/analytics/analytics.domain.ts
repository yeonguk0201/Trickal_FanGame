import { RankingType } from './dto/get-rankings-query.dto';
import type { AnalyticsCatalog, AnalyticsRun } from './analytics.types';

const percent = (numerator: number, denominator: number) =>
  denominator === 0 ? 0 : (numerator / denominator) * 100;
const average = (values: number[]) =>
  values.length === 0
    ? 0
    : values.reduce((sum, value) => sum + value, 0) / values.length;

export function buildStatistics(catalog: AnalyticsCatalog) {
  const { runs } = catalog;
  const clears = runs.filter((run) => run.isCleared).length;

  return {
    overview: {
      totalUsers: catalog.users.length,
      totalRuns: runs.length,
      totalClears: clears,
      clearRate: percent(clears, runs.length),
      averagePlayTime: average(runs.map((run) => run.playTime)),
      averageReachedFloor: average(runs.map((run) => run.reachedFloor)),
      highestReachedFloor: Math.max(0, ...runs.map((run) => run.reachedFloor)),
    },
    characters: catalog.characters
      .map((character) => {
        const characterRuns = runs.filter(
          (run) => run.characterId === character.id,
        );
        const characterClears = characterRuns.filter(
          (run) => run.isCleared,
        ).length;
        return {
          characterId: character.id,
          characterName: character.name,
          totalRuns: characterRuns.length,
          clears: characterClears,
          clearRate: percent(characterClears, characterRuns.length),
          averageReachedFloor: average(
            characterRuns.map((run) => run.reachedFloor),
          ),
          averagePlayTime: average(characterRuns.map((run) => run.playTime)),
        };
      })
      .sort(
        (a, b) =>
          b.totalRuns - a.totalRuns ||
          a.characterId.localeCompare(b.characterId),
      ),
    items: catalog.items
      .map((item) => {
        const pickedRuns = runs.filter((run) => run.itemIds.includes(item.id));
        const clearsAfterPick = pickedRuns.filter(
          (run) => run.isCleared,
        ).length;
        return {
          itemId: item.id,
          itemName: item.name,
          acquiredRunCount: pickedRuns.length,
          selectionRate: percent(pickedRuns.length, runs.length),
          clearCountAfterAcquisition: clearsAfterPick,
          clearRateAfterAcquisition: percent(
            clearsAfterPick,
            pickedRuns.length,
          ),
        };
      })
      .sort(
        (a, b) =>
          b.acquiredRunCount - a.acquiredRunCount ||
          a.itemId.localeCompare(b.itemId),
      ),
    floors: Array.from(
      { length: Math.max(0, ...runs.map((run) => run.reachedFloor)) },
      (_, index) => index + 1,
    ).map((floor) => {
      const reached = runs.filter((run) => run.reachedFloor >= floor);
      const deaths = reached.filter(
        (run) => !run.isCleared && run.reachedFloor === floor,
      ).length;
      const cleared = reached.filter((run) => run.isCleared).length;
      return {
        floor,
        reachedRunCount: reached.length,
        reachRate: percent(reached.length, runs.length),
        deathCount: deaths,
        deathRate: percent(deaths, reached.length),
        clearCount: cleared,
        clearRate: percent(cleared, reached.length),
      };
    }),
  };
}

export function buildUserStatistics(catalog: AnalyticsCatalog, userId: string) {
  const runs = catalog.runs.filter((run) => run.userId === userId);
  const clears = runs.filter((run) => run.isCleared).length;
  return {
    totalRuns: runs.length,
    clears,
    clearRate: percent(clears, runs.length),
    averageReachedFloor: average(runs.map((run) => run.reachedFloor)),
    highestReachedFloor: Math.max(0, ...runs.map((run) => run.reachedFloor)),
    averagePlayTime: average(runs.map((run) => run.playTime)),
  };
}

export function buildRanking(runs: AnalyticsRun[], type: RankingType) {
  if (type === RankingType.MOST_CLEARS) {
    const byUser = new Map<
      string,
      { nickname: string; clears: number; totalRuns: number }
    >();
    for (const run of runs) {
      const current = byUser.get(run.userId) ?? {
        nickname: run.nickname,
        clears: 0,
        totalRuns: 0,
      };
      current.totalRuns += 1;
      current.clears += run.isCleared ? 1 : 0;
      byUser.set(run.userId, current);
    }
    return [...byUser.entries()]
      .map(([userId, value]) => ({ userId, ...value }))
      .sort(
        (a, b) =>
          b.clears - a.clears ||
          b.totalRuns - a.totalRuns ||
          a.nickname.localeCompare(b.nickname) ||
          a.userId.localeCompare(b.userId),
      )
      .map(({ nickname, clears, totalRuns }) => ({
        nickname,
        clears,
        totalRuns,
      }));
  }

  const candidates =
    type === RankingType.FASTEST_CLEAR
      ? runs.filter((run) => run.isCleared)
      : runs;
  const bestByUser = new Map<string, AnalyticsRun>();
  for (const run of candidates) {
    const current = bestByUser.get(run.userId);
    if (!current || compareRuns(run, current, type) < 0) {
      bestByUser.set(run.userId, run);
    }
  }
  return [...bestByUser.values()]
    .sort((a, b) => compareRuns(a, b, type))
    .map((run) => ({
      nickname: run.nickname,
      runId: run.id,
      character: { id: run.characterId, name: run.characterName },
      reachedFloor: run.reachedFloor,
      playTime: run.playTime,
      endedAt: run.endedAt,
    }));
}

function compareRuns(a: AnalyticsRun, b: AnalyticsRun, type: RankingType) {
  const primary =
    type === RankingType.FASTEST_CLEAR
      ? a.playTime - b.playTime
      : b.reachedFloor - a.reachedFloor || a.playTime - b.playTime;
  return (
    primary ||
    a.endedAt.getTime() - b.endedAt.getTime() ||
    a.nickname.localeCompare(b.nickname) ||
    a.id.localeCompare(b.id)
  );
}
