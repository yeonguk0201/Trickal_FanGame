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
    description:
      '기본 공격이 적을 맞히면 그 적의 위치에서 상하좌우 4방향으로 소형 투사체가 분열합니다. 분열탄은 피해가 50%, 크기가 기본 탄의 50%이고 사거리가 짧으며 다시 분열하지 않습니다. 다른 아이템의 관통이 있으면 본탄은 계속 날아가며 맞힐 때마다 분열하고, 분열탄도 관통을 물려받아 방금 맞힌 적을 다시 맞힙니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      {
        type: 'SplitOnHit',
        secondaryMagnitude: 0.5,
        integerAmount: 4,
        maximumDistance: 3,
        scaleMultiplier: 0.5,
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
    id: 'single-spell-armor-festival-invitation',
    name: '갑옷축제 초대장',
    description:
      '사용하면 방어막 2칸을 얻습니다. 이미 있는 방어막에 더해집니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'GainShield', magnitude: 4 }],
  },
  {
    id: 'single-spell-amelia-love-letter',
    name: '아멜리아의 러브레터',
    description:
      '사용하면 캐릭터 주변에 HP 1칸을 회복하는 하트 2개가 떨어집니다. HP가 가득 차 있으면 하트는 바닥에 남습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'SpawnHealthPickups', integerAmount: 2 }],
  },
  {
    id: 'single-spell-random-coin',
    name: '랜덤코인',
    description:
      '사용하면 골드를 2~10 사이에서 무작위로 얻습니다. 골드가 이미 가득 차 있으면 사용할 수 없습니다.',
    rarity: 'COMMON',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'GainRandomGold', integerAmount: 2, magnitude: 10 }],
  },
  {
    id: 'single-spell-decisive-strike',
    name: '회심의 일격',
    description:
      '전투 중인 방에서 사용하면 그 방에 있는 동안 치명타 확률이 15%p, 치명타 피해가 50%p 증가합니다. 방을 떠나면 해제되며, 시작방·보상방이나 이미 클리어한 방에서는 사용할 수 없습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [
      {
        type: 'CurrentRoomCriticalBonus',
        magnitude: 0.5,
        secondaryMagnitude: 0.15,
      },
    ],
  },
  {
    id: 'single-spell-membership-card',
    name: '멤버십카드',
    description:
      '상점에서 사용하면 그 상점에 남아 있는 상품이 모두 무료가 됩니다. 다시 방문해도 유지되며, 다른 상점에는 적용되지 않습니다. 상점 밖에서는 사용할 수 없습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'FreeCurrentShopOffers' }],
  },
  {
    id: 'artifact-sist-fake-wings',
    name: '시스트의 가짜 날개',
    description:
      'Run이 끝날 때까지 날아다니며 구덩이와 장애물 위를 지나갈 수 있습니다. 벽·닫힌 문·상자는 통과하지 못하고 피해는 그대로 받습니다. 황금상자에서만 얻을 수 있습니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'Flight' }],
  },
  {
    id: 'jjangsem-bigwood-fruit',
    name: '빅우드의 열매',
    description:
      '사용하면 10초 동안 받는 피해가 하트 반 칸씩 줄고, HP 피해를 받을 때마다 2초 뒤 그 피해에 하트 1칸을 더해 회복합니다. 방어막이 막은 피해는 회복하지 않으며, 사망하면 효과가 끝납니다. 다이아몬드 상자에서만 얻을 수 있습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [
      {
        type: 'ReduceAndRecoverDamageTaken',
        magnitude: 1,
        integerAmount: 2,
        intervalSeconds: 2,
        durationSeconds: 10,
      },
    ],
  },
  {
    id: 'jjangsem-melune-card',
    name: '멜룬카드',
    description:
      '사용하면 현재 방의 열지 않은 상자와 바닥의 하트·SP·골드·열쇠·폭탄을 하나씩 복제합니다. 복제한 상자는 내용물을 따로 추첨하며 같은 방법(열쇠·폭탄)으로 엽니다. 복제할 것이 없는 방에서는 사용할 수 없습니다. 다이아몬드 상자에서만 얻을 수 있습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'DuplicateRoomChestsAndPickups' }],
  },
  {
    id: 'artifact-kanna-cannon',
    name: '칸나의 대포',
    description:
      '기본 공격 투사체의 크기가 스택당 50% 커집니다. 피해·탄속·사거리는 그대로입니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [{ type: 'ProjectileSizePercent', magnitude: 0.5 }],
  },
  {
    id: 'artifact-bibi-snot',
    name: '비비의 콧물',
    description:
      '기본 공격이 적중하면 스택당 15% 확률로 적을 중독시킵니다. 중독은 4초 동안 1초마다 공격력의 15%씩 피해를 주며 3번까지 중첩되고, 다시 걸리면 지속 시간이 갱신됩니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [
      {
        type: 'BasicAttackPoison',
        magnitude: 0.15,
        secondaryMagnitude: 0.15,
        integerAmount: 3,
        intervalSeconds: 1,
        durationSeconds: 4,
      },
    ],
  },
  {
    id: 'artifact-giant-potion',
    name: '거대화 물약',
    description:
      '몸이 스택당 30% 커지고 기본 공격 피해가 20%, 최대 HP가 3칸 증가하지만 이동속도가 20% 감소합니다. 스킬 피해는 그대로입니다. 피격 판정은 기본의 2배까지만 커지며, 벽·문·장애물에 닿는 발밑 판정은 그대로입니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'PlayerSizePercent', magnitude: 0.3 },
      { type: 'BasicAttackDamagePercent', magnitude: 0.2 },
      { type: 'MaxHealthFlat', magnitude: 6 },
      { type: 'MoveSpeedPenaltyPercent', magnitude: 0.2 },
    ],
  },
  {
    id: 'artifact-ner-eldr-flag',
    name: '네르의 엘드르 깃발',
    description:
      '최대 HP가 스택당 1칸 증가합니다. 저학년 스킬을 3회 시전할 때마다 HP를 반 칸 회복하며, 스택마다 필요한 시전 횟수가 1회 줄어듭니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'MaxHealthFlat', magnitude: 2 },
      { type: 'HealOnLowerGradeSkillEveryN', magnitude: 1, integerAmount: 3 },
    ],
  },
  {
    id: 'artifact-emergency-protection-belt',
    name: '긴급 보호 벨트',
    description:
      '새 전투가 시작되는 방에 들어갈 때마다 방어막을 반 칸 얻습니다. 방어막은 방마다 누적되며 현재 HP와 방어막의 합이 15칸이 되면 더 얻지 않습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'ShieldOnCombatRoomEntry', magnitude: 1 }],
  },
  {
    id: 'artifact-pork-cutlet-hairpin',
    name: '돈까스 모양 머리핀',
    description: '최대 HP가 스택당 1칸 증가하고 그 1칸을 회복합니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 3,
    effects: [{ type: 'MaxHealthFlat', magnitude: 2 }],
  },
  {
    id: 'artifact-rim-scythe',
    name: '림의 낫',
    description:
      '공격력이 8% 증가합니다. 피해를 준 직후 적의 남은 HP가 최대 HP의 20% 이하면 즉시 처치합니다. 보스에게는 적용되지 않습니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.08 },
      { type: 'ExecuteBelowHealth', magnitude: 0.2 },
    ],
  },
  {
    id: 'artifact-explosive-muffin',
    name: '폭발 머핀',
    description:
      '치명타 확률이 5%p 증가합니다. 기본 공격이 8회 적중할 때마다 적중한 적의 위치에서 반경 1.5m에 공격력의 100% 피해를 주는 폭발이 일어납니다. 폭발은 적중 횟수에 포함되지 않습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'CriticalChance', magnitude: 0.05 },
      {
        type: 'BasicAttackHitExplosion',
        magnitude: 1,
        integerAmount: 8,
        radius: 1.5,
      },
    ],
  },
  {
    id: 'artifact-blazing-bow',
    name: '활활 불타활',
    description:
      '공격력이 스택당 3% 증가합니다. 기본 공격이 적중하면 스택당 20% 확률로 적에게 화상을 입힙니다. 화상은 3초 동안 0.5초마다 공격력의 20%씩 피해를 주며 중첩되지 않고 지속 시간만 갱신됩니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      {
        type: 'BasicAttackBurn',
        magnitude: 0.2,
        secondaryMagnitude: 0.2,
        intervalSeconds: 0.5,
        durationSeconds: 3,
      },
    ],
  },
  {
    id: 'artifact-attatta-gun',
    name: '앗땃따건',
    description:
      '공격력이 스택당 3% 증가합니다. 기본 공격이 적중하면 스택당 20% 확률로 적을 감전시킵니다. 감전은 2.5초 동안 이동속도를 10%씩 낮추며 4번까지 중첩되고, 보스에게는 절반만 적용됩니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      {
        type: 'BasicAttackShock',
        magnitude: 0.2,
        secondaryMagnitude: 0.1,
        integerAmount: 4,
        durationSeconds: 2.5,
      },
    ],
  },
  {
    id: 'artifact-atta-sword',
    name: '앗따검',
    description:
      '상태이상(중독·화상)의 틱 피해가 스택당 10%, 공격력이 스택당 5% 증가합니다. 스스로 상태이상을 부여하지는 않습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'StatusTickDamagePercent', magnitude: 0.1 },
      { type: 'AttackDamagePercent', magnitude: 0.05 },
    ],
  },
  {
    id: 'artifact-burning-branch',
    name: '불타는 가지',
    description:
      '공격력이 3% 증가합니다. 화상에 걸린 적에게 주는 직접 피해가 25% 증가하고, 기본 공격이 적중하면 10% 확률로 화상(3초, 0.5초마다 공격력의 20%)을 입힙니다. 화상의 틱 피해에는 적용되지 않습니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      { type: 'DirectDamagePercentVsBurning', magnitude: 0.25 },
      {
        type: 'BasicAttackBurn',
        magnitude: 0.1,
        secondaryMagnitude: 0.2,
        intervalSeconds: 0.5,
        durationSeconds: 3,
      },
    ],
  },
  {
    id: 'artifact-amelia-epad-classic',
    name: '아멜리아의 E-Pad 클래식',
    description:
      '스킬 피해가 8% 증가합니다. 감전된 적에게는 스킬 피해가 60% 더 증가하고, 기본 공격과 스킬의 치명타 확률과 치명타 피해가 33.61%p씩 증가합니다. 스스로 감전을 부여하지는 않습니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'SkillDamagePercent', magnitude: 0.08 },
      { type: 'SkillDamagePercentVsShocked', magnitude: 0.6 },
      {
        type: 'CriticalBonusVsShocked',
        magnitude: 0.3361,
        secondaryMagnitude: 0.3361,
      },
    ],
  },
  {
    id: 'artifact-greed-ring',
    name: '탐욕의 반지',
    description:
      '치명타 피해가 스택당 30%p, 상태이상(중독·화상)의 틱 피해가 스택당 30% 증가합니다.',
    rarity: 'UNCOMMON',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'CriticalDamage', magnitude: 0.3 },
      { type: 'StatusTickDamagePercent', magnitude: 0.3 },
    ],
  },
  {
    id: 'artifact-sylla-wind-arrow',
    name: '실라의 바람살',
    description:
      '공격속도가 스택당 15%, 이동속도가 5%, 기본 공격 탄속이 15% 증가합니다. 탄속이 늘면 사거리와 넉백도 함께 늘어납니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 2,
    effects: [
      { type: 'AttackSpeedPercent', magnitude: 0.15 },
      { type: 'MoveSpeedPercent', magnitude: 0.05 },
      { type: 'ProjectileSpeedPercent', magnitude: 0.15 },
    ],
  },
  {
    id: 'artifact-levi-dagger',
    name: '레비의 단도',
    description:
      '공격력이 8%, 이동속도가 3% 증가합니다. Run당 1회, 사망에 이르는 피해를 무효화하고 5초 동안 무적이 됩니다. HP와 방어막은 피격 전 그대로이며, 효과를 쓴 뒤에도 능력치는 유지됩니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.08 },
      { type: 'MoveSpeedPercent', magnitude: 0.03 },
      { type: 'NegateLethalDamageOnce', durationSeconds: 5 },
    ],
  },
  {
    id: 'artifact-shushushushuk-glove',
    name: '슈슈슈슉 글러브',
    description:
      '공격력이 8% 증가합니다. 적을 처치하면 5초 동안 중첩당 기본 공격 피해 5%, 공격속도 8%, 넉백 15%가 증가하며 3번까지 중첩되고 처치할 때마다 5초로 갱신됩니다. 5초가 지나면 모든 중첩이 사라지고 10초 동안은 다시 발동하지 않습니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'AttackDamagePercent', magnitude: 0.08 },
      {
        type: 'KillFrenzy',
        magnitude: 0.05,
        secondaryMagnitude: 0.08,
        integerAmount: 3,
        intervalSeconds: 10,
        durationSeconds: 5,
      },
      { type: 'KillFrenzyKnockbackPercent', magnitude: 0.15 },
    ],
  },
  {
    id: 'artifact-aisia-wallet',
    name: '아이시아의 지갑',
    description:
      '획득하면 골드를 100 얻습니다. 보유 한도 99를 넘는 골드는 버려집니다. 황금상자에서만 얻을 수 있습니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 1,
    effects: [{ type: 'GainGoldOnAcquire', magnitude: 100 }],
  },
  {
    id: 'artifact-kanta-top',
    name: '칸타의 팽이',
    description:
      '기본 공격 투사체가 적을 맞히면 반경 3.5m 안의 가장 가까운 다른 적에게 2회 튕깁니다. 스택마다 튕김이 1회 늘어납니다. 다른 적이 없으면 0.15초 뒤 같은 적을 다시 맞히며, 같은 적을 다시 맞힐 때마다 피해가 절반이 됩니다.',
    rarity: 'RARE',
    isActive: true,
    maxStacks: 2,
    effects: [
      {
        type: 'BounceBetweenEnemies',
        secondaryMagnitude: 0.5,
        integerAmount: 2,
        radius: 3.5,
        intervalSeconds: 0.15,
      },
    ],
  },
  {
    id: 'artifact-shasha-jar',
    name: '샤샤의 항아리',
    description:
      '기본 공격이 투사체 대신 물줄기가 됩니다. 물줄기는 공격 키를 누르고 있으면 공격 쿨다운마다 한 번씩 발사되어 벽이나 문까지 일직선으로 뻗고, 장애물을 타격하며 넘어가고, 줄기 위의 모든 적을 맞힙니다. 넉백은 투사체의 30%이며 사거리와 탄속 효과는 적용되지 않습니다.',
    rarity: 'EPIC',
    isActive: true,
    maxStacks: 1,
    effects: [
      { type: 'WaterStreamAttack', magnitude: 0.2, secondaryMagnitude: 0.3 },
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
