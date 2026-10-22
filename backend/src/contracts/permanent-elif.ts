// Permanent Elif contract (docs/27-permanent-elif-contract.md).
// Upgrade IDs are stable keys like Item IDs: never rename, reuse, or delete them.

export const ELIF_BALANCE_MAX = 9999;
export const ELIF_RUN_GAIN_MAX = 50;
export const ELIF_UPGRADE_PURCHASES_MAX = 50;

export type ShopUpgradeId = 'shop-upgrade-extra-slot' | 'shop-upgrade-discount';

export interface ShopUpgradeDefinition {
  upgradeId: ShopUpgradeId;
  // Index 0 is the cost of level 1.
  levelCosts: readonly number[];
}

export const SHOP_UPGRADE_CATALOG: readonly ShopUpgradeDefinition[] = [
  { upgradeId: 'shop-upgrade-extra-slot', levelCosts: [2, 3, 5] },
  { upgradeId: 'shop-upgrade-discount', levelCosts: [2, 3, 4] },
];

export interface ShopUpgradeLevel {
  upgradeId: string;
  level: number;
}

export interface RunElifUpgradePurchase {
  upgradeId: string;
  level: number;
  floor: number;
  order: number;
}

export interface RunElifInput {
  gained: number;
  spent: number;
  upgradePurchases: RunElifUpgradePurchase[];
}

export interface ElifResultMeta {
  elifBalance: number;
  elifGained: number;
  elifSpent: number;
  shopUpgrades: ShopUpgradeLevel[];
}

export function findShopUpgrade(
  upgradeId: string,
): ShopUpgradeDefinition | undefined {
  return SHOP_UPGRADE_CATALOG.find(
    (definition) => definition.upgradeId === upgradeId,
  );
}

// Returns undefined for an unknown ID or a level outside 1..max.
export function getShopUpgradeCost(
  upgradeId: string,
  level: number,
): number | undefined {
  const definition = findShopUpgrade(upgradeId);
  if (!definition || level < 1 || level > definition.levelCosts.length) {
    return undefined;
  }
  return definition.levelCosts[level - 1];
}

// A missing elif block and an all-zero block are the same request.
export function isEmptyRunElif(elif: RunElifInput | null | undefined): boolean {
  return (
    !elif ||
    (elif.gained === 0 &&
      elif.spent === 0 &&
      (elif.upgradePurchases?.length ?? 0) === 0)
  );
}

export function normalizeRunElif(
  elif: Partial<RunElifInput> | null | undefined,
): RunElifInput {
  return {
    gained: elif?.gained ?? 0,
    spent: elif?.spent ?? 0,
    upgradePurchases: [...(elif?.upgradePurchases ?? [])].sort(
      (left, right) => left.order - right.order,
    ),
  };
}

export type ElifPurchaseValidationError =
  | 'UNKNOWN_UPGRADE'
  | 'LEVEL_OUT_OF_RANGE'
  | 'NON_SEQUENTIAL_ORDER'
  | 'LEVEL_SKIPPED'
  | 'FLOOR_OUT_OF_RANGE'
  | 'SPENT_BELOW_UPGRADE_COST';

// Request-only checks. Server-state checks (already-owned levels) run in the
// Run transaction.
export function validateRunElif(
  elif: RunElifInput,
  reachedFloor: number,
): ElifPurchaseValidationError | null {
  const purchases = elif.upgradePurchases;
  if (!purchases.every((purchase, index) => purchase.order === index + 1)) {
    return 'NON_SEQUENTIAL_ORDER';
  }

  const lastLevelById = new Map<string, number>();
  let upgradeCost = 0;
  for (const purchase of purchases) {
    if (!findShopUpgrade(purchase.upgradeId)) {
      return 'UNKNOWN_UPGRADE';
    }
    const cost = getShopUpgradeCost(purchase.upgradeId, purchase.level);
    if (cost === undefined) {
      return 'LEVEL_OUT_OF_RANGE';
    }
    const previousLevel = lastLevelById.get(purchase.upgradeId);
    if (previousLevel !== undefined && purchase.level !== previousLevel + 1) {
      return 'LEVEL_SKIPPED';
    }
    if (purchase.floor > reachedFloor) {
      return 'FLOOR_OUT_OF_RANGE';
    }
    lastLevelById.set(purchase.upgradeId, purchase.level);
    upgradeCost += cost;
  }

  return elif.spent < upgradeCost ? 'SPENT_BELOW_UPGRADE_COST' : null;
}

export interface ElifSettlementInput {
  balance: number;
  ownedLevels: ReadonlyMap<string, number>;
  elif: RunElifInput;
}

export type ElifSettlement =
  | {
      ok: true;
      appliedPurchases: RunElifUpgradePurchase[];
      effectiveSpent: number;
      shortfall: number;
      balance: number;
      levels: Map<string, number>;
    }
  | { ok: false; upgradeId: string; level: number };

// Applies one validated Run's Elif result to the stored balance and levels.
// Levels another Run already bought are skipped and refunded. A level gap
// against the stored state fails. A negative balance never fails the Run.
export function settleRunElif(input: ElifSettlementInput): ElifSettlement {
  const levels = new Map(input.ownedLevels);
  const appliedPurchases: RunElifUpgradePurchase[] = [];
  let refunded = 0;

  for (const purchase of input.elif.upgradePurchases) {
    const current = levels.get(purchase.upgradeId) ?? 0;
    if (purchase.level <= current) {
      refunded += getShopUpgradeCost(purchase.upgradeId, purchase.level) ?? 0;
      continue;
    }
    if (purchase.level !== current + 1) {
      return {
        ok: false,
        upgradeId: purchase.upgradeId,
        level: purchase.level,
      };
    }
    levels.set(purchase.upgradeId, purchase.level);
    appliedPurchases.push(purchase);
  }

  const effectiveSpent = input.elif.spent - refunded;
  const rawBalance = input.balance + input.elif.gained - effectiveSpent;

  return {
    ok: true,
    appliedPurchases,
    effectiveSpent,
    shortfall: rawBalance < 0 ? -rawBalance : 0,
    balance: Math.min(ELIF_BALANCE_MAX, Math.max(0, rawBalance)),
    levels,
  };
}

export function toShopUpgradeLevels(
  levels: ReadonlyMap<string, number>,
): ShopUpgradeLevel[] {
  return [...levels.entries()]
    .filter(([, level]) => level > 0)
    .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0))
    .map(([upgradeId, level]) => ({ upgradeId, level }));
}
