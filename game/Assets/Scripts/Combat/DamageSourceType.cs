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
        PlayerItemExplosion,
        // Trap-0: a room trap (가시, 장판, 불, 가시 돌, 독가스). Not player damage, so its kills are not credited.
        Environment,
        // Power-1: damage a 교주의 권능 deals (팝핀 스타, 찐짜천벌). Player damage.
        PlayerPower,
        // Jjangsem-2 (파스텔빛): a jjangsem spell's pulse over the room. Player damage, but periodic like a status tick.
        PlayerSpell
    }
}
