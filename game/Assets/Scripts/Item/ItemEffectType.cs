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
    }
}
