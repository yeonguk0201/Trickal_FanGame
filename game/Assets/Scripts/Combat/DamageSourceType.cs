namespace TrickalFanGame.Combat
{
    public enum DamageSourceType
    {
        Unknown,
        PlayerProjectile,
        PlayerAttack,
        PlayerSkillExplosion,
        PlayerUltimateImpact,
        EnemyContact,
        EnemyProjectile,
        PlayerDamageAura,
        EnemyMelee,
        PlayerItemLightning,
        PlayerBomb,
        // Passive-0 §4.7: a status effect tick (poison). Player damage, so its kills count for the player.
        PlayerStatusEffect
    }
}
