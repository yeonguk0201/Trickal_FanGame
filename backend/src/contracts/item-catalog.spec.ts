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
      'single-spell-afterimage',
      'single-spell-final-sprint',
      'single-spell-armor-festival-invitation',
      'single-spell-amelia-love-letter',
      'single-spell-random-coin',
      'single-spell-decisive-strike',
      'single-spell-membership-card',
      'artifact-sist-fake-wings',
      'jjangsem-bigwood-fruit',
      'jjangsem-melune-card',
      'artifact-kanna-cannon',
      'artifact-bibi-snot',
      'artifact-giant-potion',
      'artifact-ner-eldr-flag',
      'artifact-emergency-protection-belt',
      'artifact-pork-cutlet-hairpin',
      'artifact-rim-scythe',
      'artifact-explosive-muffin',
      'artifact-blazing-bow',
      'artifact-attatta-gun',
      'artifact-atta-sword',
      'artifact-burning-branch',
      'artifact-amelia-epad-classic',
      'artifact-greed-ring',
      'artifact-sylla-wind-arrow',
      'artifact-levi-dagger',
      'artifact-shushushushuk-glove',
      'artifact-aisia-wallet',
      'artifact-kanta-top',
      'artifact-shasha-jar',
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
      'UNCOMMON',
      'RARE',
      'RARE',
      'UNCOMMON',
      'COMMON',
      'UNCOMMON',
      'RARE',
      'EPIC',
      'RARE',
      'RARE',
      'RARE',
      'RARE',
      'EPIC',
      'EPIC',
      'RARE',
      'UNCOMMON',
      'EPIC',
      'RARE',
      'UNCOMMON',
      'UNCOMMON',
      'UNCOMMON',
      'UNCOMMON',
      'EPIC',
      'UNCOMMON',
      'EPIC',
      'EPIC',
      'EPIC',
      'RARE',
      'RARE',
      'EPIC',
    ]);
    expect(new Set(active.map((item) => item.id)).size).toBe(49);
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
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-15')?.effects[2],
    ).toEqual({ type: 'ProjectileLifetimePercent', magnitude: 0.3 });
  });

  it('stores every required compound and conditional parameter explicitly', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'item-02')?.effects,
    ).toHaveLength(2);
    expect(
      // Passive-5: the pierce and the three-way SplitAfterPierce became one four-way SplitOnHit.
      ITEM_CATALOG.find((item) => item.id === 'item-11')?.effects,
    ).toEqual([
      {
        type: 'SplitOnHit',
        secondaryMagnitude: 0.5,
        integerAmount: 4,
        maximumDistance: 3,
        scaleMultiplier: 0.5,
      },
    ]);
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
      {
        id: 'artifact-sist-fake-wings',
        rarity: 'EPIC',
        maxStacks: 1,
      },
      {
        id: 'artifact-kanna-cannon',
        rarity: 'RARE',
        maxStacks: 2,
      },
      {
        id: 'artifact-bibi-snot',
        rarity: 'RARE',
        maxStacks: 2,
      },
      {
        id: 'artifact-giant-potion',
        rarity: 'EPIC',
        maxStacks: 2,
      },
      {
        id: 'artifact-ner-eldr-flag',
        rarity: 'EPIC',
        maxStacks: 2,
      },
      {
        id: 'artifact-emergency-protection-belt',
        rarity: 'RARE',
        maxStacks: 1,
      },
      {
        id: 'artifact-pork-cutlet-hairpin',
        rarity: 'UNCOMMON',
        maxStacks: 3,
      },
      {
        id: 'artifact-rim-scythe',
        rarity: 'EPIC',
        maxStacks: 1,
      },
      {
        id: 'artifact-explosive-muffin',
        rarity: 'RARE',
        maxStacks: 1,
      },
      {
        id: 'artifact-blazing-bow',
        rarity: 'UNCOMMON',
        maxStacks: 2,
      },
      {
        id: 'artifact-attatta-gun',
        rarity: 'UNCOMMON',
        maxStacks: 2,
      },
      {
        id: 'artifact-atta-sword',
        rarity: 'UNCOMMON',
        maxStacks: 2,
      },
      {
        id: 'artifact-burning-branch',
        rarity: 'UNCOMMON',
        maxStacks: 1,
      },
      {
        id: 'artifact-amelia-epad-classic',
        rarity: 'EPIC',
        maxStacks: 1,
      },
      {
        id: 'artifact-greed-ring',
        rarity: 'UNCOMMON',
        maxStacks: 2,
      },
      {
        id: 'artifact-sylla-wind-arrow',
        rarity: 'EPIC',
        maxStacks: 2,
      },
      {
        id: 'artifact-levi-dagger',
        rarity: 'EPIC',
        maxStacks: 1,
      },
      {
        id: 'artifact-shushushushuk-glove',
        rarity: 'EPIC',
        maxStacks: 1,
      },
      {
        id: 'artifact-aisia-wallet',
        rarity: 'RARE',
        maxStacks: 1,
      },
      {
        id: 'artifact-kanta-top',
        rarity: 'RARE',
        maxStacks: 2,
      },
      {
        id: 'artifact-shasha-jar',
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
      {
        id: 'single-spell-afterimage',
        name: '그건 내 잔상',
        rarity: 'UNCOMMON',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'EscapeToFloorStartRoom' }],
      },
      {
        id: 'single-spell-final-sprint',
        name: '막판 스퍼트',
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
        rarity: 'RARE',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'GainShield', magnitude: 4 }],
      },
      {
        id: 'single-spell-amelia-love-letter',
        name: '아멜리아의 러브레터',
        rarity: 'UNCOMMON',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'SpawnHealthPickups', integerAmount: 2 }],
      },
      {
        id: 'single-spell-random-coin',
        name: '랜덤코인',
        rarity: 'COMMON',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'GainRandomGold', integerAmount: 2, magnitude: 10 }],
      },
      {
        id: 'single-spell-decisive-strike',
        name: '회심의 일격',
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
        rarity: 'RARE',
        isActive: true,
        maxStacks: 1,
        effects: [{ type: 'FreeCurrentShopOffers' }],
      },
    ]);
  });

  it('adds the golden chest exclusive fake wings as a flight artifact (Flight-0)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-sist-fake-wings'),
    ).toMatchObject({
      name: '시스트의 가짜 날개',
      rarity: 'EPIC',
      isActive: true,
      maxStacks: 1,
      effects: [{ type: 'Flight' }],
    });
  });

  it('adds the first jjangsem spell, the bigwood fruit (Jjangsem-0)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'jjangsem-bigwood-fruit'),
    ).toMatchObject({
      name: '빅우드의 열매',
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
    });
  });

  it('adds the melune card jjangsem spell (Jjangsem-1)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'jjangsem-melune-card'),
    ).toMatchObject({
      name: '멜룬카드',
      rarity: 'RARE',
      isActive: true,
      maxStacks: 1,
      effects: [{ type: 'DuplicateRoomChestsAndPickups' }],
    });
  });

  it('adds the cannon and the snot as attack modifier artifacts (Passive-1)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-kanna-cannon'),
    ).toMatchObject({
      name: '칸나의 대포',
      rarity: 'RARE',
      isActive: true,
      maxStacks: 2,
      effects: [{ type: 'ProjectileSizePercent', magnitude: 0.5 }],
    });
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-bibi-snot'),
    ).toMatchObject({
      name: '비비의 콧물',
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
    });
    // The legacy inactive item-05 (shot size) keeps its ID; the cannon does not reuse it.
    expect(ITEM_CATALOG.find((item) => item.id === 'item-05')).toMatchObject({
      isActive: false,
    });
  });

  it('adds the giant potion as a body size artifact (Passive-2)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-giant-potion'),
    ).toMatchObject({
      name: '거대화 물약',
      rarity: 'EPIC',
      isActive: true,
      maxStacks: 2,
      effects: [
        { type: 'PlayerSizePercent', magnitude: 0.3 },
        { type: 'BasicAttackDamagePercent', magnitude: 0.2 },
        { type: 'MaxHealthFlat', magnitude: 6 },
        { type: 'MoveSpeedPenaltyPercent', magnitude: 0.2 },
      ],
    });
  });

  it('adds the 14 confirmed artifacts with their status and trigger effects (Artifact-2)', () => {
    const effectsOf = (id: string) =>
      ITEM_CATALOG.find((item) => item.id === id)?.effects;

    expect(effectsOf('artifact-ner-eldr-flag')).toEqual([
      { type: 'MaxHealthFlat', magnitude: 2 },
      { type: 'HealOnLowerGradeSkillEveryN', magnitude: 1, integerAmount: 3 },
    ]);
    expect(effectsOf('artifact-emergency-protection-belt')).toEqual([
      { type: 'ShieldOnCombatRoomEntry', magnitude: 1 },
    ]);
    expect(effectsOf('artifact-pork-cutlet-hairpin')).toEqual([
      { type: 'MaxHealthFlat', magnitude: 2 },
    ]);
    expect(effectsOf('artifact-rim-scythe')).toEqual([
      { type: 'AttackDamagePercent', magnitude: 0.08 },
      { type: 'ExecuteBelowHealth', magnitude: 0.2 },
    ]);
    expect(effectsOf('artifact-explosive-muffin')).toEqual([
      { type: 'CriticalChance', magnitude: 0.05 },
      {
        type: 'BasicAttackHitExplosion',
        magnitude: 1,
        integerAmount: 8,
        radius: 1.5,
      },
    ]);
    const burn = {
      type: 'BasicAttackBurn',
      secondaryMagnitude: 0.2,
      intervalSeconds: 0.5,
      durationSeconds: 3,
    };
    expect(effectsOf('artifact-blazing-bow')).toEqual([
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      { ...burn, magnitude: 0.2 },
    ]);
    expect(effectsOf('artifact-attatta-gun')).toEqual([
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      {
        type: 'BasicAttackShock',
        magnitude: 0.2,
        secondaryMagnitude: 0.1,
        integerAmount: 4,
        durationSeconds: 2.5,
      },
    ]);
    expect(effectsOf('artifact-atta-sword')).toEqual([
      { type: 'StatusTickDamagePercent', magnitude: 0.1 },
      { type: 'AttackDamagePercent', magnitude: 0.05 },
    ]);
    expect(effectsOf('artifact-burning-branch')).toEqual([
      { type: 'AttackDamagePercent', magnitude: 0.03 },
      { type: 'DirectDamagePercentVsBurning', magnitude: 0.25 },
      { ...burn, magnitude: 0.1 },
    ]);
    expect(effectsOf('artifact-amelia-epad-classic')).toEqual([
      { type: 'SkillDamagePercent', magnitude: 0.08 },
      { type: 'SkillDamagePercentVsShocked', magnitude: 0.6 },
      {
        type: 'CriticalBonusVsShocked',
        magnitude: 0.3361,
        secondaryMagnitude: 0.3361,
      },
    ]);
    expect(effectsOf('artifact-greed-ring')).toEqual([
      { type: 'CriticalDamage', magnitude: 0.3 },
      { type: 'StatusTickDamagePercent', magnitude: 0.3 },
    ]);
    expect(effectsOf('artifact-sylla-wind-arrow')).toEqual([
      { type: 'AttackSpeedPercent', magnitude: 0.15 },
      { type: 'MoveSpeedPercent', magnitude: 0.05 },
      { type: 'ProjectileSpeedPercent', magnitude: 0.15 },
    ]);
    expect(effectsOf('artifact-levi-dagger')).toEqual([
      { type: 'AttackDamagePercent', magnitude: 0.08 },
      { type: 'MoveSpeedPercent', magnitude: 0.03 },
      { type: 'NegateLethalDamageOnce', durationSeconds: 5 },
    ]);
    expect(effectsOf('artifact-shushushushuk-glove')).toEqual([
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
    ]);
  });

  it('adds the wallet, the top and the jar (Artifact-3, Passive-3, Passive-4)', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-aisia-wallet'),
    ).toMatchObject({
      name: '아이시아의 지갑',
      effects: [{ type: 'GainGoldOnAcquire', magnitude: 100 }],
    });
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-kanta-top'),
    ).toMatchObject({
      name: '칸타의 팽이',
      effects: [
        {
          type: 'BounceBetweenEnemies',
          secondaryMagnitude: 0.5,
          integerAmount: 2,
          radius: 3.5,
          intervalSeconds: 0.15,
        },
      ],
    });
    expect(
      ITEM_CATALOG.find((item) => item.id === 'artifact-shasha-jar'),
    ).toMatchObject({
      name: '샤샤의 항아리',
      effects: [
        { type: 'WaterStreamAttack', magnitude: 0.2, secondaryMagnitude: 0.3 },
      ],
    });
  });

  it('keeps the legacy spell-final-sprint active for past Run records', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'spell-final-sprint'),
    ).toMatchObject({
      name: '막판 스퍼트',
      rarity: 'RARE',
      isActive: true,
      effects: [
        { type: 'BossRoomAttackSpeedPercent', magnitude: 0.3 },
        { type: 'BossRoomMoveSpeedPercent', magnitude: 0.05 },
      ],
    });
  });

  it('keeps the legacy spell-afterimage active for past Run records', () => {
    expect(
      ITEM_CATALOG.find((item) => item.id === 'spell-afterimage'),
    ).toMatchObject({
      name: '그건 내 잔상',
      rarity: 'UNCOMMON',
      isActive: true,
      maxStacks: 3,
      effects: [
        { type: 'MoveSpeedPercent', magnitude: 0.1 },
        { type: 'AttackSpeedPercent', magnitude: 0.05 },
      ],
    });
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
