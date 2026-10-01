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
    }
}
