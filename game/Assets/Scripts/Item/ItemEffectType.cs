namespace TrickalFanGame.Item
{
    public enum ItemEffectType
    {
        // Legacy numeric values are retained so existing serialized assets keep their meaning.
        AttackDamage = 0,
        MaxHealth = 1,
        MoveSpeed = 2,
        MultiShot = 3,
        Pierce = 4,
        HealOnKill = 5,
        AttackDamagePercent = 6,
        SkillDamagePercent = 7,
        CriticalChance = 8,
        AttackSpeedPercent = 9,
        MaxHealthDamageAura = 10,
        MaxHealthFlat = 11,
        ShieldOnAcquireMaxHealthPercent = 12,
        MoveSpeedPercentBelowHealth = 13,
        HealOnKillMaxHealthPercent = 14,
        DistanceDamage = 15,
        SplitAfterPierce = 16,
        MaxSP = 17,
        SkillProjectileBonusAtSP = 18,
        MoveSpeedPercent = 19,
        MoveSpeedPenaltyPercent = 20,
        NextCombatRoomAttackDamagePercent = 21,
        BossRoomAttackSpeedPercent = 22,
        BossRoomMoveSpeedPercent = 23,
        HealOverTimeBelowHealthOnce = 24,
        BasicAttackHitLightning = 25,
        HealOnKillEveryN = 26,
        AttackDamageAura = 27,
        // Single-use effects (Spell-0). They run only when the slot item is used, never while held.
        // IntegerAmount = SP allowed above the maximum after filling.
        RestoreAllSPWithOvercharge = 28,
        // IntegerAmount = half-SP units per tick, every IntervalSeconds for DurationSeconds.
        RegenerateSPHalvesOverTime = 29,
        // Magnitude = basic attack damage bonus in the room where the item is used (Spell-1). Leaving the room ends it.
        CurrentRoomBasicAttackDamagePercent = 30,
        // Spell-2: moves the player from a room in combat to the current floor's start room. The escaped room drops
        // its remaining enemies and restarts its Encounter on the next entry. No value fields are used.
        EscapeToFloorStartRoom = 31,
        // Spell-3: Magnitude = attack speed and SecondaryMagnitude = move speed bonus in the boss room where the item
        // is used. Leaving the room ends both.
        CurrentBossRoomSpeedPercent = 32,
        // Range-0: basic attack travel distance = flight time × shot speed, so both effects lengthen the range.
        // Magnitude = basic attack shot speed bonus. Opened as a contract; no item uses it yet.
        ProjectileSpeedPercent = 33,
        // Magnitude = basic attack flight time bonus (the telescope candidate). Opened as a contract; no item uses it yet.
        ProjectileLifetimePercent = 34,
        // Flight-0 (시스트의 가짜 날개): the player flies over pits and low obstacles until the Run ends. Walls, doors
        // and chests still stop the player and damage is unchanged. No value fields are used.
        Flight = 35,
        // Jjangsem-0 (빅우드의 열매): for DurationSeconds every hit on the player is reduced by Magnitude health units,
        // and each hit that still costs HP is healed IntervalSeconds later by that HP damage plus IntegerAmount units.
        ReduceAndRecoverDamageTaken = 36,
        // Spell-5 single-use effects.
        // 갑옷축제 초대장: Magnitude = shield gained in health units, added to the current shield.
        GainShield = 37,
        // 아멜리아의 러브레터: IntegerAmount = heart pickups dropped around the player.
        SpawnHealthPickups = 38,
        // 랜덤코인: gains a random amount of gold from IntegerAmount to Magnitude, both inclusive.
        GainRandomGold = 39,
        // 회심의 일격: Magnitude = critical damage and SecondaryMagnitude = critical chance bonus in the combat room
        // where the item is used. Leaving the room ends both.
        CurrentRoomCriticalBonus = 40,
        // Jjangsem-1 (멜룬카드): copies each unopened chest and each floor consumable (heart, SP, gold, key, bomb) in
        // the current room once. A copied chest rolls its own contents. No value fields are used.
        DuplicateRoomChestsAndPickups = 41,
        // Spell-4 (멤버십카드): every offer still on sale in the shop the player stands in becomes free. The shop keeps
        // that across revisits; other shops are unaffected. No value fields are used.
        FreeCurrentShopOffers = 42,
        // Passive-1 (칸나의 대포): Magnitude = basic attack shot size bonus. The look and the collision radius grow
        // together; damage, shot speed and range do not change.
        ProjectileSizePercent = 43,
        // Passive-1 (비비의 콧물): a basic attack hit may poison the enemy. Magnitude = chance per hit,
        // SecondaryMagnitude = share of attack damage each poison stack deals per tick, DurationSeconds,
        // IntervalSeconds = tick interval, IntegerAmount = maximum poison stacks on one enemy.
        BasicAttackPoison = 44,
        // Passive-2 (거대화 물약): Magnitude = body size bonus. The look follows it without a limit, the hurtbox
        // stops at twice the base size and the feet (terrain collision) do not change (Hitbox-1).
        PlayerSizePercent = 45,
        // Passive-2 (거대화 물약): Magnitude = basic attack damage bonus, kept for the whole Run. Only basic attack
        // hits and shots gain it; skills, auras and the attack stat itself do not change.
        BasicAttackDamagePercent = 46,
        // Artifact-2: the 14 confirmed artifacts (docs/12 "확정 상세 효과").
        // 네르의 엘드르 깃발: every IntegerAmount successful lower-grade skill casts heal Magnitude health units.
        // Each later stack lowers the cast count by one.
        HealOnLowerGradeSkillEveryN = 47,
        // 긴급 보호 벨트: entering a room that starts a new combat Encounter adds Magnitude shield units, while
        // current health + shield stays within 15 hearts.
        ShieldOnCombatRoomEntry = 48,
        // 림의 낫: right after player damage, a non-boss enemy at or below Magnitude of its maximum health dies.
        ExecuteBelowHealth = 49,
        // 폭발 머핀: every IntegerAmount basic attack hits explode at the hit enemy for Magnitude of the attack
        // damage within Radius. The explosion is not a basic attack hit.
        BasicAttackHitExplosion = 50,
        // 활활 불타활·불타는 가지: a basic attack hit may burn the enemy. Magnitude = chance per hit,
        // SecondaryMagnitude = share of attack damage per tick, DurationSeconds, IntervalSeconds = tick interval.
        BasicAttackBurn = 51,
        // 앗땃따건: a basic attack hit may shock the enemy. Magnitude = chance per hit, SecondaryMagnitude = move
        // speed lost per shock stack, DurationSeconds, IntegerAmount = maximum shock stacks on one enemy.
        BasicAttackShock = 52,
        // 앗따검·탐욕의 반지: Magnitude = tick damage bonus of every status effect the player applies.
        StatusTickDamagePercent = 53,
        // 불타는 가지: Magnitude = direct damage bonus against a burning enemy. Status ticks do not gain it.
        DirectDamagePercentVsBurning = 54,
        // 아멜리아의 E-Pad 클래식: Magnitude = skill damage bonus against a shocked enemy.
        SkillDamagePercentVsShocked = 55,
        // 아멜리아의 E-Pad 클래식: Magnitude = critical damage and SecondaryMagnitude = critical chance bonus of
        // basic attacks and skills against a shocked enemy.
        CriticalBonusVsShocked = 56,
        // 탐욕의 반지: Magnitude = critical damage multiplier bonus, kept for the whole Run.
        CriticalDamage = 57,
        // 레비의 단도: once per Run a hit that would kill is cancelled and the player is invulnerable for
        // DurationSeconds.
        NegateLethalDamageOnce = 58,
        // 슈슈슈슉 글러브: a kill adds a stack (up to IntegerAmount) for DurationSeconds, renewed by each kill.
        // Each stack gives Magnitude basic attack damage and SecondaryMagnitude attack speed. When the time runs
        // out every stack ends and kills do not count for IntervalSeconds.
        KillFrenzy = 59,
        // 슈슈슈슉 글러브: Magnitude = basic attack knockback bonus per KillFrenzy stack.
        KillFrenzyKnockbackPercent = 60,
        // Artifact-3 (아이시아의 지갑): Magnitude = gold gained when the item is acquired. The part beyond the
        // wallet limit of 99 is dropped.
        GainGoldOnAcquire = 61,
        // Passive-3 (칸타의 팽이, Passive-0 §4.4): a basic attack shot that hit an enemy turns toward the nearest
        // other enemy within Radius. IntegerAmount = bounces of the first stack (each later stack adds one),
        // SecondaryMagnitude = damage kept per earlier hit of the same shot on the same enemy,
        // IntervalSeconds = delay before the shot hits the same enemy again when no other enemy is near.
        BounceBetweenEnemies = 62,
        // Passive-4 (샤샤의 항아리, Passive-0 §4.5): the basic attack becomes a straight water stream that reaches
        // the room's wall or door and hits every enemy on it once per attack cooldown. Magnitude = stream width,
        // SecondaryMagnitude = share of the basic attack knockback each hit applies.
        WaterStreamAttack = 63,
        // Passive-5 (다야의 다이아몬드 커터): every basic attack hit sends IntegerAmount small shots from the enemy
        // toward the screen's up, down, left and right. SecondaryMagnitude = their share of the shot's damage,
        // MaximumDistance = their range, ScaleMultiplier = their size as a share of the base shot. They inherit
        // pierces from other items and never split again. Replaces the legacy SplitAfterPierce (16) on item-11.
        SplitOnHit = 64,
        // Power-1 (교주의 권능, contract 23 §9). Only the 권능 slot runs these, on use; holding one does nothing.
        // 팝핀 스타: every enemy in the room takes Magnitude x attack damage (ScaleMultiplier x that for non-bosses).
        // Each enemy hit adds SecondaryMagnitude player damage for DurationSeconds, up to IntegerAmount stacks.
        PowerRoomBlast = 65,
        // 애착 담요: for DurationSeconds every hit is reduced by Magnitude HP units (never below half a heart), and
        // IntegerAmount HP units are healed every IntervalSeconds.
        PowerProtectiveBlanket = 66,
        // 찐짜천벌: the enemy with the most health (bosses first) takes Magnitude x attack damage and IntegerAmount
        // shock stacks (SecondaryMagnitude slow per stack for DurationSeconds).
        PowerSmite = 67,
        // 명치 꽂기: every enemy in the room, bosses included, is stunned for DurationSeconds.
        PowerStunRoomEnemies = 68,
        // 명치 꽂기: every enemy in the room takes Magnitude more damage for DurationSeconds.
        PowerRoomEnemyVulnerability = 69,
        // 날먹보호막: Magnitude HP units of shield, within the 15 heart health + shield limit.
        PowerGainShield = 70,
        // 급발진: for DurationSeconds attack speed +Magnitude, move speed +SecondaryMagnitude and the high-grade
        // skill cooldown runs as if ScaleMultiplier shorter.
        PowerOverdrive = 71,
        // 급발진: IntegerAmount full SP at once.
        PowerRestoreSP = 72,
        // Jjangsem-2 (짱셈스펠 4종, plan 26 §4.2). Room-bound effects only start in an uncleared combat or boss room.
        // 파스텔빛: every IntervalSeconds for DurationSeconds each enemy in the room takes Magnitude x attack damage
        // (no critical, no knockback, credited to the player). Clearing or leaving the room ends it.
        CurrentRoomPulseDamage = 73,
        // 파스텔빛: every IntervalSeconds for DurationSeconds IntegerAmount HP units are healed. Ends with the pulse.
        CurrentRoomPulseHeal = 74,
        // 수상한 물약: for DurationSeconds every enemy hit taken in the room it was used in loses Magnitude of its
        // damage, rounded up to half hearts and never below half a heart.
        CurrentRoomEnemyDamageReduction = 75,
        // 이성의 끈: player basic attack, skill and artifact damage x Magnitude in the room it was used in.
        // 권능 damage, status ticks and bombs are excluded. Leaving the room ends it.
        CurrentRoomDamageMultiplier = 76,
        // 깜짝상자: IntegerAmount random enemies of the room (bosses included) are stunned for DurationSeconds and
        // lose Magnitude of their maximum health, which never kills. A boss loses SecondaryMagnitude of its maximum
        // health instead (0 = the same Magnitude).
        StunAndCutRandomEnemies = 77,
        // 파스텔빛: every IntervalSeconds for DurationSeconds IntegerAmount half SP slots are gained. Ends with the
        // pulse.
        CurrentRoomPulseSP = 78,
        // 수상한 물약: every enemy in the room at use gets IntegerAmount poison stacks (up to IntegerAmount), each
        // dealing Magnitude x attack damage every IntervalSeconds for DurationSeconds.
        CurrentRoomEnemyPoison = 79,
        // Spell-6 (스펠 5종, plan 26 §4.3).
        // 네티의 지도: the minimap shows every room of the current floor, secret rooms included (their position
        // only: hidden passages stay sealed and unknown). No value fields are used.
        RevealCurrentFloorMap = 80,
        // 네티의 지도: IntegerAmount gold, usable with a full wallet (the part beyond the limit is dropped).
        GainGold = 81,
        // 에슈르의 마도서: basic attack shots fired in the room it was used in steer toward an enemy (contract 24 §4.2).
        // Magnitude = homing coefficient (x 360 degrees per second), Radius = detection range. Only in an uncleared
        // combat or boss room; another use in the same room adds the coefficient, leaving the room ends it.
        CurrentRoomBasicAttackHoming = 82,
        // 티그의 도전장: moves the player to the current floor's boss room at its door entry and starts the boss fight.
        // A room left mid-fight resets like 그건 내 잔상. Refused inside the boss room. No value fields are used.
        TeleportToBossRoom = 83,
        // 골디의 초대장: turns the current floor's secret room into the 골디 shop (if it is not one already) and moves
        // the player inside. Refused during combat, on a floor without a secret room or when its reward was taken.
        // No value fields.
        TeleportToGoldiShop = 84,
        // 엘리스의 타로카드: raises one or two random stats and sometimes lowers others for the rest of the Run
        // (TarotCardDraw holds the odds and amounts). The draw comes from the Run seed and the use count.
        // No value fields are used.
        RandomStatShift = 85,
        // Elif-2 (아이시아의 지갑): on acquire, rolls 0-4 permanent elif once from the Run seed and the item ID
        // (PermanentElif.WalletWeights) and adds them to the Run's elif gain. No value fields are used.
        GainPermanentElifRollOnAcquire = 86,
    }
}
