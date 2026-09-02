import type { ItemRarity } from '../contracts/item-catalog';
import {
  CHARACTER_MAX_LEVEL,
  EXPERIENCE_TO_NEXT_LEVEL,
  ITEM_RARITY_EXPERIENCE,
  KILL_EXPERIENCE,
  REACHED_FLOOR_EXPERIENCE,
  type CharacterProgressSnapshot,
} from '../contracts/meta-progression';

export interface RunExperienceInput {
  reachedFloor: number;
  killCount: number;
  itemIds: readonly string[];
}

export interface RunExperienceItem {
  id: string;
  rarity: string;
  isActive: boolean;
}

export class MetaProgressionDomainError extends Error {
  constructor(
    public readonly code:
      | 'INVALID_EXPERIENCE_INPUT'
      | 'INVALID_PROGRESS_STATE'
      | 'RUN_ITEM_NOT_FOUND'
      | 'RUN_ITEM_INACTIVE'
      | 'RUN_ITEM_RARITY_INVALID',
    message: string,
  ) {
    super(message);
    this.name = 'MetaProgressionDomainError';
  }
}

export function calculateRunExperience(
  input: RunExperienceInput,
  itemDefinitions: readonly RunExperienceItem[],
): number {
  assertNonNegativeSafeInteger(input.reachedFloor, 'reachedFloor');
  assertNonNegativeSafeInteger(input.killCount, 'killCount');

  const itemById = new Map(itemDefinitions.map((item) => [item.id, item]));
  let itemExperience = 0;

  for (const itemId of input.itemIds) {
    const item = itemById.get(itemId);
    if (!item) {
      throw new MetaProgressionDomainError(
        'RUN_ITEM_NOT_FOUND',
        `Run item does not exist: ${itemId}`,
      );
    }
    if (!item.isActive) {
      throw new MetaProgressionDomainError(
        'RUN_ITEM_INACTIVE',
        `Run item is inactive: ${itemId}`,
      );
    }
    if (!isItemRarity(item.rarity)) {
      throw new MetaProgressionDomainError(
        'RUN_ITEM_RARITY_INVALID',
        `Run item has an invalid rarity: ${itemId}`,
      );
    }

    itemExperience += ITEM_RARITY_EXPERIENCE[item.rarity];
  }

  const experience =
    input.reachedFloor * REACHED_FLOOR_EXPERIENCE +
    input.killCount * KILL_EXPERIENCE +
    itemExperience;
  assertNonNegativeSafeInteger(experience, 'calculated experience');
  return experience;
}

export function applyRunExperience(
  progress: CharacterProgressSnapshot,
  experienceGained: number,
): CharacterProgressSnapshot {
  assertProgressState(progress);
  assertNonNegativeSafeInteger(experienceGained, 'experienceGained');

  if (progress.level === CHARACTER_MAX_LEVEL) {
    return {
      ...progress,
      experience: 0,
      experienceToNextLevel: 0,
    };
  }

  let level = progress.level;
  let experience = progress.experience + experienceGained;
  if (!Number.isSafeInteger(experience)) {
    throw new MetaProgressionDomainError(
      'INVALID_EXPERIENCE_INPUT',
      'Total experience must be a safe integer.',
    );
  }

  let levelsGained = 0;
  while (level < CHARACTER_MAX_LEVEL) {
    const requiredExperience = EXPERIENCE_TO_NEXT_LEVEL[level - 1];
    if (experience < requiredExperience) {
      break;
    }

    experience -= requiredExperience;
    level += 1;
    levelsGained += 1;
  }

  if (level === CHARACTER_MAX_LEVEL) {
    experience = 0;
  }

  return {
    ...progress,
    level,
    experience,
    experienceToNextLevel:
      level === CHARACTER_MAX_LEVEL ? 0 : EXPERIENCE_TO_NEXT_LEVEL[level - 1],
    skillPoints: progress.skillPoints + levelsGained,
  };
}

function assertProgressState(progress: CharacterProgressSnapshot): void {
  if (
    !Number.isInteger(progress.level) ||
    progress.level < 1 ||
    progress.level > CHARACTER_MAX_LEVEL ||
    !Number.isSafeInteger(progress.experience) ||
    progress.experience < 0 ||
    !Number.isSafeInteger(progress.skillPoints) ||
    progress.skillPoints < 0
  ) {
    throw new MetaProgressionDomainError(
      'INVALID_PROGRESS_STATE',
      'Character progress contains an invalid level, experience, or skill point value.',
    );
  }
}

function assertNonNegativeSafeInteger(value: number, field: string): void {
  if (!Number.isSafeInteger(value) || value < 0) {
    throw new MetaProgressionDomainError(
      'INVALID_EXPERIENCE_INPUT',
      `${field} must be a non-negative safe integer.`,
    );
  }
}

function isItemRarity(rarity: string): rarity is ItemRarity {
  return Object.hasOwn(ITEM_RARITY_EXPERIENCE, rarity);
}
