import {
  ELIF_BALANCE_MAX,
  ELIF_RUN_GAIN_MAX,
  getShopUpgradeCost,
  SHOP_UPGRADE_CATALOG,
  settleRunElif,
  toShopUpgradeLevels,
  validateRunElif,
  type RunElifUpgradePurchase,
} from './permanent-elif';

function purchase(
  upgradeId: string,
  level: number,
  order: number,
  floor = 1,
): RunElifUpgradePurchase {
  return { upgradeId, level, floor, order };
}

describe('Elif-1 permanent Elif contract', () => {
  it('fixes the shop upgrade IDs, levels, and costs (27 §4.2)', () => {
    expect(SHOP_UPGRADE_CATALOG).toEqual([
      { upgradeId: 'shop-upgrade-extra-slot', levelCosts: [2, 3, 5] },
      { upgradeId: 'shop-upgrade-discount', levelCosts: [2, 3, 4] },
    ]);
    expect(ELIF_BALANCE_MAX).toBe(9999);
    expect(ELIF_RUN_GAIN_MAX).toBe(50);
  });

  it('returns a cost only for known IDs and levels inside the catalog', () => {
    expect(getShopUpgradeCost('shop-upgrade-extra-slot', 3)).toBe(5);
    expect(getShopUpgradeCost('shop-upgrade-discount', 0)).toBeUndefined();
    expect(getShopUpgradeCost('shop-upgrade-discount', 4)).toBeUndefined();
    expect(getShopUpgradeCost('shop-upgrade-missing', 1)).toBeUndefined();
  });

  describe('validateRunElif', () => {
    it('accepts consecutive levels and spending at least the upgrade cost', () => {
      expect(
        validateRunElif(
          {
            gained: 0,
            spent: 7,
            upgradePurchases: [
              purchase('shop-upgrade-discount', 1, 1),
              purchase('shop-upgrade-extra-slot', 1, 2),
              purchase('shop-upgrade-discount', 2, 3, 3),
            ],
          },
          3,
        ),
      ).toBeNull();
    });

    it.each([
      ['NON_SEQUENTIAL_ORDER', [purchase('shop-upgrade-discount', 1, 2)], 2],
      ['UNKNOWN_UPGRADE', [purchase('shop-upgrade-missing', 1, 1)], 2],
      ['LEVEL_OUT_OF_RANGE', [purchase('shop-upgrade-discount', 4, 1)], 9],
      [
        'LEVEL_SKIPPED',
        [
          purchase('shop-upgrade-discount', 1, 1),
          purchase('shop-upgrade-discount', 3, 2),
        ],
        9,
      ],
      ['FLOOR_OUT_OF_RANGE', [purchase('shop-upgrade-discount', 1, 1, 4)], 2],
      [
        'SPENT_BELOW_UPGRADE_COST',
        [purchase('shop-upgrade-extra-slot', 1, 1)],
        1,
      ],
    ])('reports %s', (code, upgradePurchases, spent) => {
      expect(validateRunElif({ gained: 0, spent, upgradePurchases }, 3)).toBe(
        code,
      );
    });
  });

  describe('settleRunElif', () => {
    it('lets a Run start from an upgrade level already stored', () => {
      const settlement = settleRunElif({
        balance: 5,
        ownedLevels: new Map([['shop-upgrade-discount', 1]]),
        elif: {
          gained: 0,
          spent: 3,
          upgradePurchases: [purchase('shop-upgrade-discount', 2, 1)],
        },
      });

      expect(settlement).toMatchObject({
        ok: true,
        effectiveSpent: 3,
        balance: 2,
        shortfall: 0,
      });
    });

    it('fails a level gap against the stored state', () => {
      expect(
        settleRunElif({
          balance: 10,
          ownedLevels: new Map(),
          elif: {
            gained: 0,
            spent: 3,
            upgradePurchases: [purchase('shop-upgrade-discount', 2, 1)],
          },
        }),
      ).toEqual({ ok: false, upgradeId: 'shop-upgrade-discount', level: 2 });
    });
  });

  it('lists upgrade levels sorted by ID without empty levels', () => {
    expect(
      toShopUpgradeLevels(
        new Map([
          ['shop-upgrade-extra-slot', 2],
          ['shop-upgrade-discount', 1],
          ['shop-upgrade-retired', 0],
        ]),
      ),
    ).toEqual([
      { upgradeId: 'shop-upgrade-discount', level: 1 },
      { upgradeId: 'shop-upgrade-extra-slot', level: 2 },
    ]);
  });
});
