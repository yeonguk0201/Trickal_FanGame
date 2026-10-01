import { ITEM_CATALOG } from './item-catalog';

describe('Item catalog', () => {
  it('preserves the Phase G IDs and adds the first six Week 16 stable IDs', () => {
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
      'spell-catch-that-one',
      'spell-final-sprint',
      'spell-afterimage',
      'artifact-life-gem',
      'artifact-30kg-kettlebell',
      'artifact-clear-weather-card',
      'single-spell-aroma-therapy',
      'single-spell-meditation-time',
      'single-spell-catch-that-one',
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
      'UNCOMMON',
      'RARE',
      'UNCOMMON',
      'RARE',
      'RARE',
      'EPIC',
      'UNCOMMON',
      'UNCOMMON',
      'UNCOMMON',
    ]);
    expect(new Set(active.map((item) => item.id)).size).toBe(19);
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

  it('uses half-heart units and HP-3 effects for balloon armor, pillow, and mask', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-02')?.effects[0],
    ).toEqual({ type: 'MaxHealthFlat', magnitude: 2 });
    expect(ITEM_CATALOG.find((item) => item.id === 'item-08')).toMatchObject({
      maxStacks: 2,
      effects: [{ type: 'HealOnKillEveryN', magnitude: 1, integerAmount: 2 }],
    });
    expect(ITEM_CATALOG.find((item) => item.id === 'item-14')).toMatchObject({
      maxStacks: 3,
      effects: [
        {
          type: 'AttackDamageAura',
          magnitude: 0.2,
          radius: 2.5,
          intervalSeconds: 1,
        },
      ],
    });
  });

  it('matches the first Week 16 spell and artifact contracts', () => {
    expect(
      ITEM_CATALOG.filter(
        (item) =>
          item.id.startsWith('spell-') || item.id.startsWith('artifact-'),
      ),
    ).toMatchObject([
      {
        id: 'spell-catch-that-one',
        rarity: 'UNCOMMON',
        maxStacks: 1,
        effects: [
          { type: 'NextCombatRoomAttackDamagePercent', magnitude: 0.1 },
        ],
      },
      {
        id: 'spell-final-sprint',
        rarity: 'RARE',
        maxStacks: 1,
        effects: [
          { type: 'BossRoomAttackSpeedPercent', magnitude: 0.3 },
          { type: 'BossRoomMoveSpeedPercent', magnitude: 0.05 },
        ],
      },
      {
        id: 'spell-afterimage',
        rarity: 'UNCOMMON',
        maxStacks: 3,
        effects: [
          { type: 'MoveSpeedPercent', magnitude: 0.1 },
          { type: 'AttackSpeedPercent', magnitude: 0.05 },
        ],
      },
      {
        id: 'artifact-life-gem',
        rarity: 'RARE',
        maxStacks: 1,
      },
      {
        id: 'artifact-30kg-kettlebell',
        rarity: 'RARE',
        maxStacks: 2,
      },
      {
        id: 'artifact-clear-weather-card',
        rarity: 'EPIC',
        maxStacks: 1,
      },
    ]);
  });

  it('matches the single-use spell contracts', () => {
    expect(
      ITEM_CATALOG.filter((item) => item.id.startsWith('single-spell-')),
    ).toMatchObject([
      {
        id: 'single-spell-aroma-therapy',
        name: '아로마 테라피',
        rarity: 'UNCOMMON',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'RestoreAllSPWithOvercharge', integerAmount: 1 }],
      },
      {
        id: 'single-spell-meditation-time',
        name: '명상의 시간',
        rarity: 'UNCOMMON',
        isActive: true,
        maxStacks: 1,
        effects: [
          {
            type: 'RegenerateSPHalvesOverTime',
            integerAmount: 1,
            intervalSeconds: 1,
            durationSeconds: 12,
          },
        ],
      },
      {
        id: 'single-spell-catch-that-one',
        name: '저놈 잡아라',
        rarity: 'UNCOMMON',
        isActive: true,
        maxStacks: 1,
        effects: [
          { type: 'CurrentRoomBasicAttackDamagePercent', magnitude: 0.1 },
        ],
      },
    ]);
  });

  it('keeps the legacy spell-catch-that-one active for past Run records', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'spell-catch-that-one'),
    ).toMatchObject({
      name: '저놈 잡아라',
      rarity: 'UNCOMMON',
      isActive: true,
      effects: [{ type: 'NextCombatRoomAttackDamagePercent', magnitude: 0.1 }],
    });
  });
});
