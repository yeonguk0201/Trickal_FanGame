import {
  CHARACTER_MAX_LEVEL,
  createRunRequestFingerprint,
  EXPERIENCE_TO_NEXT_LEVEL,
  ITEM_RARITY_EXPERIENCE,
  RUN_ELIF_FINGERPRINT_FIELDS,
  RUN_FINGERPRINT_FIELDS,
  RUN_ITEM_FINGERPRINT_FIELDS,
  SKILL_LEVEL_CONTRACTS,
  SKILL_MAX_LEVEL,
  type CharacterProgressSnapshot,
  type CreateRunResultContract,
  type UpgradeSkillRequestContract,
} from './meta-progression';

const PRE_ELIF_FINGERPRINT =
  '96ce94869ccd770bb5dc33c620e8af5b25d120376d7de947e8bf6b732b0acd44';

describe('Phase H-1 meta progression contract', () => {
  it('uses a steadily increasing Lv.1-Lv.19 XP curve and stops at max level', () => {
    expect(CHARACTER_MAX_LEVEL).toBe(19);
    expect(EXPERIENCE_TO_NEXT_LEVEL).toHaveLength(18);
    expect(EXPERIENCE_TO_NEXT_LEVEL[0]).toBe(400);
    expect(EXPERIENCE_TO_NEXT_LEVEL.at(-1)).toBe(2100);
    expect(
      EXPERIENCE_TO_NEXT_LEVEL.every(
        (value, index) =>
          index === 0 || value > EXPERIENCE_TO_NEXT_LEVEL[index - 1],
      ),
    ).toBe(true);
  });

  it('grants more item XP for every higher rarity', () => {
    expect(ITEM_RARITY_EXPERIENCE).toEqual({
      COMMON: 10,
      UNCOMMON: 20,
      RARE: 30,
      EPIC: 50,
    });
  });

  it('adds damage every skill level and the Lv.5 milestone effects', () => {
    expect(SKILL_MAX_LEVEL).toBe(10);
    expect(SKILL_LEVEL_CONTRACTS).toHaveLength(10);
    expect(SKILL_LEVEL_CONTRACTS[0]).toEqual({
      level: 1,
      damageMultiplierFromLevelOne: 1,
      lowerGradeProjectileBonus: 0,
      highGradeCooldownMultiplier: 1,
    });
    expect(SKILL_LEVEL_CONTRACTS[4]).toEqual({
      level: 5,
      damageMultiplierFromLevelOne: 1.4,
      lowerGradeProjectileBonus: 1,
      highGradeCooldownMultiplier: 0.85,
    });
    expect(SKILL_LEVEL_CONTRACTS[9]).toEqual({
      level: 10,
      damageMultiplierFromLevelOne: 1.9,
      lowerGradeProjectileBonus: 1,
      highGradeCooldownMultiplier: 0.85,
    });
  });

  it('fixes the request fingerprint and response field names', () => {
    expect(RUN_FINGERPRINT_FIELDS).toEqual([
      'userId',
      'characterId',
      'gameVersion',
      'startedAt',
      'endedAt',
      'playTime',
      'reachedFloor',
      'isCleared',
      'killCount',
      'deathReason',
      'items',
      'elif',
    ]);
    expect(RUN_ELIF_FINGERPRINT_FIELDS).toEqual([
      'gained',
      'spent',
      'upgradePurchases',
    ]);
    expect(RUN_ITEM_FINGERPRINT_FIELDS).toEqual([
      'itemId',
      'floor',
      'order',
      'acquiredAt',
    ]);

    const progress: CharacterProgressSnapshot = {
      characterId: 'erpin',
      level: 2,
      experience: 25,
      experienceToNextLevel: 500,
      skillPoints: 1,
      lowGradeSkillLevel: 1,
      highGradeSkillLevel: 1,
    };
    const result: CreateRunResultContract = {
      runId: '20000000-0000-4000-8000-000000000001',
      experienceGained: 425,
      progress,
      meta: {
        elifBalance: 0,
        elifGained: 0,
        elifSpent: 0,
        shopUpgrades: [],
      },
    };
    const upgrade: UpgradeSkillRequestContract = { targetLevel: 2 };

    expect(Object.keys(result)).toEqual([
      'runId',
      'experienceGained',
      'progress',
      'meta',
    ]);
    expect(Object.keys(result.meta)).toEqual([
      'elifBalance',
      'elifGained',
      'elifSpent',
      'shopUpgrades',
    ]);
    expect(Object.keys(progress)).toEqual([
      'characterId',
      'level',
      'experience',
      'experienceToNextLevel',
      'skillPoints',
      'lowGradeSkillLevel',
      'highGradeSkillLevel',
    ]);
    expect(Object.keys(upgrade)).toEqual(['targetLevel']);
  });

  it('creates a stable hash independent of item array order but detects changed content', () => {
    const input = {
      userId: '00000000-0000-4000-8000-000000000001',
      characterId: 'erpin',
      gameVersion: '0.1.0',
      startedAt: '2026-09-02T10:00:00.000Z',
      endedAt: '2026-09-02T10:10:00.000Z',
      playTime: 600,
      reachedFloor: 2,
      isCleared: false,
      killCount: 30,
      deathReason: 'BOSS',
      items: [
        {
          itemId: 'item-01',
          floor: 1,
          order: 1,
          acquiredAt: '2026-09-02T10:02:00.000Z',
        },
        {
          itemId: 'item-04',
          floor: 1,
          order: 2,
          acquiredAt: '2026-09-02T10:04:00.000Z',
        },
      ],
    };

    const fingerprint = createRunRequestFingerprint(input);
    expect(fingerprint).toHaveLength(64);
    expect(
      createRunRequestFingerprint({
        ...input,
        items: [...input.items].reverse(),
      }),
    ).toBe(fingerprint);
    expect(
      createRunRequestFingerprint({
        ...input,
        startedAt: '2026-09-02T10:00:00Z',
        endedAt: '2026-09-02T10:10:00Z',
      }),
    ).toBe(fingerprint);
    expect(createRunRequestFingerprint({ ...input, killCount: 31 })).not.toBe(
      fingerprint,
    );
  });

  describe('Elif-1 fingerprint compatibility', () => {
    const input = {
      userId: '00000000-0000-4000-8000-000000000001',
      characterId: 'erpin',
      gameVersion: '0.1.0',
      startedAt: '2026-09-02T10:00:00.000Z',
      endedAt: '2026-09-02T10:10:00.000Z',
      playTime: 600,
      reachedFloor: 2,
      isCleared: false,
      killCount: 30,
      deathReason: 'BOSS',
      items: [],
    };
    const purchases = [
      { upgradeId: 'shop-upgrade-discount', level: 1, floor: 1, order: 1 },
      { upgradeId: 'shop-upgrade-extra-slot', level: 1, floor: 2, order: 2 },
    ];

    it('pins the pre-Elif hash so pending and stored requests never turn into 409', () => {
      // Hash produced before Elif-1 for this exact input. Changing it breaks
      // retransmission of every Run saved or pending before Elif.
      expect(createRunRequestFingerprint(input)).toBe(PRE_ELIF_FINGERPRINT);
    });

    it.each([
      ['missing', undefined],
      ['null', null],
      ['all zero', { gained: 0, spent: 0, upgradePurchases: [] }],
    ])('keeps the pre-Elif hash when elif is %s', (_label, elif) => {
      expect(createRunRequestFingerprint({ ...input, elif })).toBe(
        PRE_ELIF_FINGERPRINT,
      );
    });

    it('changes the hash for any Elif content and ignores purchase array order', () => {
      const elif = { gained: 2, spent: 4, upgradePurchases: purchases };
      const fingerprint = createRunRequestFingerprint({ ...input, elif });

      expect(fingerprint).not.toBe(PRE_ELIF_FINGERPRINT);
      expect(
        createRunRequestFingerprint({
          ...input,
          elif: { ...elif, upgradePurchases: [...purchases].reverse() },
        }),
      ).toBe(fingerprint);
      expect(
        createRunRequestFingerprint({ ...input, elif: { ...elif, spent: 5 } }),
      ).not.toBe(fingerprint);
      expect(
        createRunRequestFingerprint({
          ...input,
          elif: { gained: 1, spent: 0, upgradePurchases: [] },
        }),
      ).not.toBe(PRE_ELIF_FINGERPRINT);
    });
  });
});
