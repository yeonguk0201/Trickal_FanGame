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
        PlayerStatusEffect,
        // Artifact-2 (폭발 머핀): the area damage an artifact adds to basic attack hits. Player damage.
        PlayerItemExplosion
    }
}
