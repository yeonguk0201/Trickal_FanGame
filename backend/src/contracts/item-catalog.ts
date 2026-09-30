export type ItemRarity = 'COMMON' | 'UNCOMMON' | 'RARE' | 'EPIC';

export interface ItemEffectData {
  type: string;
  magnitude?: number;
  healthThreshold?: number;
  secondaryMagnitude?: number;
  integerAmount?: number;
  minimumDistance?: number;
  maximumDistance?: number;
  radius?: number;
  intervalSeconds?: number;
  spreadAngleDegrees?: number;
  scaleMultiplier?: number;
}

export interface ItemCatalogEntry {
  id: string;
  name: string;
  description: string;
  rarity: ItemRarity;
  isActive: boolean;
  maxStacks: number;
  effects: readonly ItemEffectData[];
}

export const ITEM_CATALOG = [
  {
    id: 'item-01',
    name: '급조한 목검',
    description: '공격력이 스택당 5% 증가합니다.',
    rarity: 'COMMON',
    isActive: true,
    maxStacks: 5,
    effects: [{ type: 'AttackDamagePercent', magnitude: 0.05 }],
  },
  {
    id: 'item-12',
    name: '녹슨 송곳',
    description: '치명타 확률이 스택당 3%p 증가합니다.',
    rarity: 'COMMON',
    isActive: true,
    maxStacks: 5,
    effects: [{ type: 'CriticalChance', magnitude: 0.03 }],
  },
  {
    id: 'item-04',
    name: '낡은 화살',
    description: '공격속도가 스택당 10% 증가합니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 3,
    effects: [{ type: 'AttackSpeedPercent', magnitude: 0.1 }],
  },
  {
    id: 'item-14',
    name: '광기의 가면',
    description: '1초마다 주변 적에게 최대 HP의 1% 피해를 줍니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 3,
    effects: [
      {
        type: 'MaxHealthDamageAura',
        magnitude: 0.01,
        radius: 1.5,
        intervalSeconds: 1,
      },
    ],
  },
  {
    id: 'item-02',
    name: '풍선 갑옷',
    description:
      '최대 HP가 2 증가하고 획득 시 최대 HP의 50%만큼 방어막을 얻습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'MaxHealthFlat', magnitude: 2 },
      { type: 'ShieldOnAcquireMaxHealthPercent', magnitude: 0.5 },
    ],
  },
  {
    id: 'item-03',
    name: '도깨비 감투',
    description: 'HP가 30% 이하일 때 이동속도가 스택당 25% 증가합니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [
      {
        type: 'MoveSpeedPercentBelowHealth',
        magnitude: 0.25,
        healthThreshold: 0.3,
      },
    ],
  },
  {
    id: 'item-08',
    name: '코미의 베개',
    description: '적 처치 시 최대 HP의 5%를 회복합니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [{ type: 'HealOnKillMaxHealthPercent', magnitude: 0.05 }],
  },
  {
    id: 'item-15',
    name: '장난감 망원경',
    description:
      '공격력이 15% 증가하고 거리에 따라 추가 피해가 최대 40% 증가합니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.15 },
      {
        type: 'DistanceDamage',
        magnitude: 0.4,
        minimumDistance: 3,
        maximumDistance: 8,
      },
    ],
  },
  {
    id: 'item-11',
    name: '다야의 다이아몬드 커터',
    description: '관통이 1 증가하고 첫 관통 후 소형 투사체 3개로 분열합니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'Pierce', integerAmount: 1 },
      {
        type: 'SplitAfterPierce',
        secondaryMagnitude: 0.3,
        integerAmount: 3,
        maximumDistance: 3,
        spreadAngleDegrees: 15,
        scaleMultiplier: 0.6,
      },
    ],
  },
  {
    id: 'item-13',
    name: '에르핀의 지팡이',
    description: '최대 SP가 1 증가하고 저학년 스킬 투사체가 2개 추가됩니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'MaxSP', integerAmount: 1 },
      {
        type: 'SkillProjectileBonusAtSP',
        healthThreshold: 1,
        integerAmount: 2,
      },
    ],
  },
  {
    id: 'item-06',
    name: '다중 투사체',
    description:
      '발사되는 투사체 수가 증가합니다. Phase G 보상 풀에서는 제외됩니다.',
    rarity: 'RARE',
    isActive: false,
    maxStacks: 2,
    effects: [{ type: 'MultiShot', integerAmount: 1 }],
  },
  ...[
    ['item-05', '투사체 크기 강화', '투사체 크기가 증가합니다.'],
    ['item-07', '공격 범위 강화', '공격 범위가 증가합니다.'],
    ['item-09', '피격 반격', '피격 시 반격 효과가 발생합니다.'],
    ['item-10', '추가 공격', '특정 조건에서 추가 공격이 발생합니다.'],
  ].map(([id, name, description]) => ({
    id,
    name,
    description,
    rarity: id === 'item-10' ? ('EPIC' as const) : ('COMMON' as const),
    isActive: false,
    maxStacks: 1,
    effects: [],
  })),
] satisfies readonly ItemCatalogEntry[];
