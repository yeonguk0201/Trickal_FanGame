export type ItemRarity = 'COMMON' | 'UNCOMMON' | 'RARE' | 'EPIC';

// Player HP values (MaxHealthFlat, HealOnKillEveryN magnitude) are half-heart units: 2 = 1 heart.
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
  durationSeconds?: number;
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
    description: '1초마다 반경 2.5m의 주변 적에게 공격력의 20% 피해를 줍니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 3,
    effects: [
      {
        type: 'AttackDamageAura',
        magnitude: 0.2,
        radius: 2.5,
        intervalSeconds: 1,
      },
    ],
  },
  {
    id: 'item-02',
    name: '풍선 갑옷',
    description:
      '최대 HP가 1칸 증가하고 획득 시 최대 HP의 50%만큼 방어막을 얻습니다.',
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
    description:
      '적 2마리 처치마다 HP를 반 칸 회복합니다. 스택마다 필요한 처치 수가 1 줄어듭니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [{ type: 'HealOnKillEveryN', magnitude: 1, integerAmount: 2 }],
  },
  {
    id: 'item-15',
    name: '장난감 망원경',
    description:
      '공격력이 15% 증가하고 2~6m 거리에서 추가 피해가 최대 40% 증가하며 사거리가 30% 증가합니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.15 },
      {
        type: 'DistanceDamage',
        magnitude: 0.4,
        minimumDistance: 2,
        maximumDistance: 6,
      },
      { type: 'ProjectileLifetimePercent', magnitude: 0.3 },
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
    id: 'spell-catch-that-one',
    name: '저놈 잡아라',
    description: '다음에 입장한 전투방에서 기본 공격 피해가 10% 증가합니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'NextCombatRoomAttackDamagePercent', magnitude: 0.1 }],
  },
  {
    id: 'spell-final-sprint',
    name: '막판 스퍼트',
    description: '보스방에서 공격속도가 30%, 이동속도가 5% 증가합니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'BossRoomAttackSpeedPercent', magnitude: 0.3 },
      { type: 'BossRoomMoveSpeedPercent', magnitude: 0.05 },
    ],
  },
  {
    id: 'spell-afterimage',
    name: '그건 내 잔상',
    description: '스택당 이동속도가 10%, 공격속도가 5% 증가합니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 3,
    effects: [
      { type: 'MoveSpeedPercent', magnitude: 0.1 },
      { type: 'AttackSpeedPercent', magnitude: 0.05 },
    ],
  },
  {
    id: 'artifact-life-gem',
    name: '생명의 보석',
    description:
      'HP가 30% 이하가 되면 3초간 최대 HP의 45%를 회복합니다. 층마다 한 번 발동합니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [
      {
        type: 'HealOverTimeBelowHealthOnce',
        magnitude: 0.45,
        healthThreshold: 0.3,
        durationSeconds: 3,
      },
    ],
  },
  {
    id: 'artifact-30kg-kettlebell',
    name: '30KG 케틀벨',
    description:
      '스택당 저학년·고학년 스킬 피해가 25% 증가하고 이동속도가 10% 감소합니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'SkillDamagePercent', magnitude: 0.25 },
      { type: 'MoveSpeedPenaltyPercent', magnitude: 0.1 },
    ],
  },
  {
    id: 'artifact-clear-weather-card',
    name: '날씨는 맑음 카드',
    description: '기본 공격 10회 적중마다 공격력의 150% 번개 피해를 줍니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'BasicAttackHitLightning', magnitude: 1.5, integerAmount: 10 },
    ],
  },
  {
    id: 'single-spell-aroma-therapy',
    name: '아로마 테라피',
    description:
      '사용하면 SP를 최대치보다 1 많게 즉시 회복합니다. 초과분은 SP를 사용해 최대치 이하가 되면 사라지며, 이미 초과 충전 상태면 사용할 수 없습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'RestoreAllSPWithOvercharge', integerAmount: 1 }],
  },
  {
    id: 'single-spell-meditation-time',
    name: '명상의 시간',
    description:
      '사용하면 12초 동안 1초마다 SP를 반 칸씩 최대치까지 회복합니다. 방과 층을 이동해도 유지되며, 다시 사용하면 지속 시간이 처음부터 갱신됩니다.',
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
    description:
      '전투 중인 방에서 사용하면 그 방에 있는 동안 기본 공격 피해가 10% 증가합니다. 방을 떠나면 해제되며, 시작방·보상방이나 이미 클리어한 방에서는 사용할 수 없습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'CurrentRoomBasicAttackDamagePercent', magnitude: 0.1 }],
  },
  {
    id: 'single-spell-afterimage',
    name: '그건 내 잔상',
    description:
      '전투 중인 방에서 사용하면 현재 층 시작방으로 탈출합니다. 탈출한 방은 다시 들어가면 처음부터 전투가 시작되며, 부순 장애물과 바닥의 보상은 그대로 남습니다. 시작방이나 전투 중이 아닌 방에서는 사용할 수 없습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'EscapeToFloorStartRoom' }],
  },
  {
    id: 'single-spell-final-sprint',
    name: '막판 스퍼트',
    description:
      '아직 클리어하지 않은 보스방에서 사용하면 그 방에 있는 동안 공격속도가 30%, 이동속도가 5% 증가합니다. 방을 떠나면 해제되며, 보스방 밖이나 클리어한 보스방에서는 사용할 수 없습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [
      {
        type: 'CurrentBossRoomSpeedPercent',
        magnitude: 0.3,
        secondaryMagnitude: 0.05,
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
