import { ITEM_CATALOG } from '../contracts/item-catalog';
import type { CharacterProgressSnapshot } from '../contracts/meta-progression';
import {
  applyRunExperience,
  calculateRunExperience,
  MetaProgressionDomainError,
} from './meta-progression';

describe('Phase H-2 meta progression domain', () => {
  describe('calculateRunExperience', () => {
    it('calculates floor, kill, and acquisition-time rarity experience', () => {
      expect(
        calculateRunExperience(
          {
            reachedFloor: 2,
            killCount: 30,
            itemIds: ['item-01', 'item-01', 'item-02'],
          },
          ITEM_CATALOG,
        ),
      ).toBe(400);
    });

    it('counts every valid duplicate acquisition and every rarity tier', () => {
      expect(
        calculateRunExperience(
          {
            reachedFloor: 0,
            killCount: 0,
            itemIds: ['item-01', 'item-01', 'item-04', 'item-02', 'item-15'],
          },
          ITEM_CATALOG,
        ),
      ).toBe(120);
    });

    it.each([
      ['missing item', 'missing-item', 'RUN_ITEM_NOT_FOUND'],
      ['inactive item', 'item-06', 'RUN_ITEM_INACTIVE'],
    ])('rejects a %s', (_label, itemId, expectedCode) => {
      expectDomainError(
        () =>
          calculateRunExperience(
            { reachedFloor: 1, killCount: 0, itemIds: [itemId] },
            ITEM_CATALOG,
          ),
        expectedCode,
      );
    });

    it('rejects an item rarity outside the stable contract', () => {
      expectDomainError(
        () =>
          calculateRunExperience(
            { reachedFloor: 1, killCount: 0, itemIds: ['item-invalid'] },
            [
              {
                id: 'item-invalid',
                rarity: 'LEGENDARY',
                isActive: true,
              },
            ],
          ),
        'RUN_ITEM_RARITY_INVALID',
      );
    });
  });

  describe('applyRunExperience', () => {
    it('keeps progress below the exact level boundary', () => {
      expect(
        applyRunExperience(createProgress({ experience: 398 }), 1),
      ).toEqual(createProgress({ experience: 399 }));
    });

    it('carries excess experience and grants one point at the boundary', () => {
      expect(
        applyRunExperience(createProgress({ experience: 399 }), 26),
      ).toEqual(
        createProgress({
          level: 2,
          experience: 25,
          experienceToNextLevel: 500,
          skillPoints: 1,
        }),
      );
    });

    it('levels up repeatedly and grants one point per gained level', () => {
      expect(
        applyRunExperience(
          createProgress({ experience: 350, skillPoints: 2 }),
          1100,
        ),
      ).toEqual(
        createProgress({
          level: 3,
          experience: 550,
          experienceToNextLevel: 600,
          skillPoints: 4,
        }),
      );
    });

    it('discards excess experience at Lv.19 and preserves skill levels', () => {
      expect(
        applyRunExperience(
          createProgress({
            level: 18,
            experience: 2050,
            experienceToNextLevel: 2100,
            skillPoints: 7,
            lowGradeSkillLevel: 4,
            highGradeSkillLevel: 6,
          }),
          500,
        ),
      ).toEqual(
        createProgress({
          level: 19,
          experience: 0,
          experienceToNextLevel: 0,
          skillPoints: 8,
          lowGradeSkillLevel: 4,
          highGradeSkillLevel: 6,
        }),
      );
    });

    it('does not store more experience or grant points at max level', () => {
      expect(
        applyRunExperience(
          createProgress({
            level: 19,
            experience: 0,
            experienceToNextLevel: 0,
            skillPoints: 18,
          }),
          10000,
        ),
      ).toEqual(
        createProgress({
          level: 19,
          experience: 0,
          experienceToNextLevel: 0,
          skillPoints: 18,
        }),
      );
    });
  });
});

function createProgress(
  overrides: Partial<CharacterProgressSnapshot> = {},
): CharacterProgressSnapshot {
  return {
    characterId: 'erpin',
    level: 1,
    experience: 0,
    experienceToNextLevel: 400,
    skillPoints: 0,
    lowGradeSkillLevel: 1,
    highGradeSkillLevel: 1,
    ...overrides,
  };
}

function expectDomainError(action: () => unknown, expectedCode: string): void {
  try {
    action();
    throw new Error(`Expected domain error ${expectedCode} to be thrown.`);
  } catch (error: unknown) {
    expect(error).toBeInstanceOf(MetaProgressionDomainError);
    expect((error as MetaProgressionDomainError).code).toBe(expectedCode);
  }
}
