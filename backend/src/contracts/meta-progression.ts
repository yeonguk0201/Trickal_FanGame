import { createHash } from 'node:crypto';

export const CHARACTER_MAX_LEVEL = 19;
export const SKILL_MAX_LEVEL = 10;
export const REACHED_FLOOR_EXPERIENCE = 100;
export const KILL_EXPERIENCE = 5;

// Index 0 is the XP required to advance from Lv.1 to Lv.2.
export const EXPERIENCE_TO_NEXT_LEVEL = [
  400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700,
  1800, 1900, 2000, 2100,
] as const;

export const ITEM_RARITY_EXPERIENCE = {
  COMMON: 10,
  UNCOMMON: 20,
  RARE: 30,
  EPIC: 50,
} as const;

export type SkillLevelContract = {
  level: number;
  damageMultiplierFromLevelOne: number;
  lowerGradeProjectileBonus: number;
  highGradeCooldownMultiplier: number;
};

export const SKILL_LEVEL_CONTRACTS: readonly SkillLevelContract[] = Array.from(
  { length: SKILL_MAX_LEVEL },
  (_, index) => {
    const level = index + 1;
    return {
      level,
      damageMultiplierFromLevelOne: 1 + index * 0.1,
      lowerGradeProjectileBonus: level >= 5 ? 1 : 0,
      highGradeCooldownMultiplier: level >= 5 ? 0.85 : 1,
    };
  },
);

export const RUN_FINGERPRINT_FIELDS = [
  'userId',
  'characterId',
  'gameVersion',
  'startedAt',
  'endedAt',
  'playTime',
  'reachedFloor',
  'isCleared',
  'killCount',
  'deathReason',
  'items',
] as const;

export const RUN_ITEM_FINGERPRINT_FIELDS = [
  'itemId',
  'floor',
  'order',
  'acquiredAt',
] as const;

export interface RunFingerprintItem {
  itemId: string;
  floor: number;
  order: number;
  acquiredAt: string;
}

export interface RunFingerprintInput {
  userId: string;
  characterId: string;
  gameVersion: string;
  startedAt: string;
  endedAt: string;
  playTime: number;
  reachedFloor: number;
  isCleared: boolean;
  killCount: number;
  deathReason?: string | null;
  items: RunFingerprintItem[];
}

export interface CharacterProgressSnapshot {
  characterId: string;
  level: number;
  experience: number;
  experienceToNextLevel: number;
  skillPoints: number;
  lowGradeSkillLevel: number;
  highGradeSkillLevel: number;
}

export interface CreateRunResultContract {
  runId: string;
  experienceGained: number;
  progress: CharacterProgressSnapshot;
}

export interface UpgradeSkillRequestContract {
  targetLevel: number;
}

export function createRunRequestFingerprint(
  input: RunFingerprintInput,
): string {
  const canonicalRequest = {
    userId: input.userId,
    characterId: input.characterId,
    gameVersion: input.gameVersion,
    startedAt: new Date(input.startedAt).toISOString(),
    endedAt: new Date(input.endedAt).toISOString(),
    playTime: input.playTime,
    reachedFloor: input.reachedFloor,
    isCleared: input.isCleared,
    killCount: input.killCount,
    deathReason: input.deathReason ?? null,
    items: [...input.items]
      .sort((left, right) => left.order - right.order)
      .map((item) => ({
        itemId: item.itemId,
        floor: item.floor,
        order: item.order,
        acquiredAt: new Date(item.acquiredAt).toISOString(),
      })),
  };

  return createHash('sha256')
    .update(JSON.stringify(canonicalRequest), 'utf8')
    .digest('hex');
}
