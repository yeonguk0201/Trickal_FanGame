export type AnalyticsRun = {
  id: string;
  userId: string;
  nickname: string;
  characterId: string;
  characterName: string;
  playTime: number;
  reachedFloor: number;
  isCleared: boolean;
  endedAt: Date;
  itemIds: string[];
};

export type AnalyticsCatalog = {
  users: { id: string; nickname: string }[];
  characters: { id: string; name: string }[];
  items: { id: string; name: string }[];
  runs: AnalyticsRun[];
};
