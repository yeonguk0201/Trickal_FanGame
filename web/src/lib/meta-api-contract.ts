export type SkillType = "LOW_GRADE" | "HIGH_GRADE";

export interface ApiSuccess<T> {
  success: true;
  data: T;
}

export interface ApiFailure {
  success: false;
  error: { code: string; message: string };
}

export interface CharacterSummaryDto {
  id: string;
  name: string;
}

export interface CharacterProgressDto {
  characterId: string;
  characterName?: string;
  level: number;
  maxLevel?: number;
  experience: number;
  experienceToNextLevel: number;
  skillPoints: number;
  lowGradeSkillLevel: number;
  highGradeSkillLevel: number;
  maxSkillLevel?: number;
}

export interface CreateRunResultDto {
  runId: string;
  experienceGained: number;
  progress: CharacterProgressDto;
}

export interface UpgradeSkillRequestDto {
  targetLevel: number;
}

export interface UserProfileDto {
  id: string;
  nickname: string;
  stats: {
    totalRuns: number;
    clears: number;
    winRate: number;
    averagePlayTime: number;
    averageFloor: number;
    highestFloor: number;
  };
  characterProgress: CharacterProgressDto[];
}

export interface RunSummaryDto {
  runId: string;
  character: CharacterSummaryDto;
  reachedFloor: number;
  playTime: number;
  isCleared: boolean;
  killCount: number;
  endedAt: string;
}

export interface RunHistoryDto {
  runs: RunSummaryDto[];
  page: number;
  limit: number;
  total: number;
}

export interface RunItemDto {
  itemId: string;
  name: string;
  rarity: string;
  floor: number;
  order: number;
  acquiredAt: string;
}

export interface RunDetailDto {
  id: string;
  user: { id: string; nickname: string };
  character: CharacterSummaryDto;
  gameVersion: string;
  startedAt: string;
  endedAt: string;
  playTime: number;
  reachedFloor: number;
  isCleared: boolean;
  killCount: number;
  deathReason: string | null;
  items: RunItemDto[];
}
