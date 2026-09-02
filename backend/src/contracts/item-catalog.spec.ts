import { ITEM_CATALOG } from './item-catalog';

describe('Phase G item catalog', () => {
  it('contains the exact 10 active stable IDs and rarity distribution', () => {
    const active = ITEM_CATALOG.filter((item) => item.isActive);

    expect(active.map((item) => item.id)).toEqual([
      'item-01',
      'item-12',
      'item-04',
      'item-14',
      'item-02',
      'item-03',
      'item-08',
      'item-15',
      'item-11',
      'item-13',
    ]);
    expect(active.map((item) => item.rarity)).toEqual([
      'COMMON',
      'COMMON',
      'UNCOMMON',
      'UNCOMMON',
      'RARE',
      'RARE',
      'RARE',
      'EPIC',
      'EPIC',
      'EPIC',
    ]);
    expect(new Set(active.map((item) => item.id)).size).toBe(10);
    expect(
      active.every((item) => item.maxStacks > 0 && item.effects.length > 0),
    ).toBe(true);
  });

  it('preserves item-06 as inactive multi-shot data and uses item-15 for the telescope', () => {
    expect(ITEM_CATALOG.find((item) => item.id === 'item-06')).toMatchObject({
      name: '다중 투사체',
      isActive: false,
      effects: [{ type: 'MultiShot', integerAmount: 1 }],
    });
    expect(ITEM_CATALOG.find((item) => item.id === 'item-15')).toMatchObject({
      name: '장난감 망원경',
      isActive: true,
      maxStacks: 1,
    });
  });

  it('stores every required compound and conditional parameter explicitly', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-02')?.effects,
    ).toHaveLength(2);
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-11')?.effects[1],
    ).toMatchObject({
      type: 'SplitAfterPierce',
      secondaryMagnitude: 0.3,
      integerAmount: 3,
      maximumDistance: 3,
      spreadAngleDegrees: 15,
      scaleMultiplier: 0.6,
    });
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-13')?.effects[1],
    ).toMatchObject({
      type: 'SkillProjectileBonusAtSP',
      healthThreshold: 1,
      integerAmount: 2,
    });
  });
});
