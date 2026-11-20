namespace TrickalFanGame.Audio
{
    // Sound-0: the combat sounds, named by the moment they play at rather than
    // by the candidate that currently fills them, so swapping a sound is an
    // asset change and never a code change.
    public enum CombatSfxCue
    {
        // Erpin firing her basic attack, once per volley.
        BasicAttack,

        // A basic attack landing on an enemy. Erpin's attack lands as a
        // small explosion, so this is a blast rather than a knock.
        HitEnemy,

        // The player taking damage from anything.
        PlayerHurt,

        // Space: the lower-grade salvo starting.
        SkillLowerCast,

        // Space: each of the four homing shots leaving the staff.
        SkillLowerShot,

        // Space: a homing shot exploding.
        SkillLowerBurst,

        // Q: the upper-grade dash starting. The launch only — the running
        // itself is the two step cues below.
        SkillUpperDash,

        // Retired in the 2026-11-21 sound pass (replaced by SkillUpperRun); kept for numbering.
        // Q: one footfall while the dash ran, alternating feet.
        SkillUpperStepLeft,
        SkillUpperStepRight,

        // Q: the dash ending on an enemy.
        SkillUpperImpact,

        // A tick of damage over time on an enemy — poison, burn, the damage
        // aura, a spell's pulse. Several enemies tick on the same frame, so
        // this cue leans on its retrigger guard more than the others.
        EnemyTickDamage,

        // A basic attack hit that rolled a critical. It replaces HitEnemy
        // for that hit rather than sounding on top of it.
        HitCritical,

        // Taking the portal to the next floor.
        BossPortal,

        // A bomb or an exploding box going off. Both end up as a PlacedBomb,
        // so both share this cue.
        BombExplosion,

        // The 폭발 머핀 artifact's blast on a basic attack hit.
        MuffinExplosion,

        // Using an ordinary spell from the spell slot.
        SpellCast,

        // Picking up SP.
        SpGain,

        // The player being healed.
        Heal,

        // A locked door opening.
        DoorOpen,

        // An item landing on the floor.
        ItemDrop,

        // Using a 짱셈 spell. It shares the spell slot with an ordinary
        // spell, so the two are told apart by the item's kind.
        // New members go after this one: the scenes store a cue as its
        // number, so inserting above would repoint every later cue.
        JjangsemSpellCast,

        // Sound-1: the 크레용사 boss's sword. One slash-and-swing covers an
        // approach swing landing and every dash of the dash chain starting;
        // the map slash comes down as a slam with a metallic impact.
        BossSwordSwing,
        BossSwordSlam,

        // A player attack hitting a destructible obstacle, once per hit.
        ObstacleHit,

        // An ordinary enemy attacking: a melee strike starting, or a shot or
        // lob leaving it. Bosses carry their own cues and are left out.
        EnemyAttack,

        // An enemy dying, whatever killed it.
        EnemyDefeated,

        // Using the 교주의 권능.
        PowerCast,

        // The VS artwork slamming down in the boss intro.
        BossIntroVs,

        // Sound-2: the 새마음금고 boss. A gold pillar bursting out of the
        // floor, one sound per pillar; the whistle while the vault is in the
        // air, cut by its landing thud; a meteor's wind while it falls, cut
        // by its explosion.
        VaultPillarEmerge,
        VaultJumpWhistle,
        VaultJumpLanding,
        VaultMeteorFall,
        VaultMeteorImpact,

        // The 부스러기 boss's thrown obstacles touching down together.
        BossObstacleLanding,

        // The floor portal opening after the boss.
        PortalAppear,

        // The character levelling up on the run result screen.
        LevelUp,

        // A spell that moves the player to another room. It replaces the
        // ordinary spell sound for that spell.
        SpellTeleport,

        // Backing out of a screen: a back, cancel, close or resume button, or Esc.
        UiCancel,

        // Enemies appearing: a new wave, a boss summon, an obstacle's ambush.
        EnemyAppear,

        // Sound-3: the shop's focus moving to another offer, invest row,
        // action button or popup choice, by keyboard or mouse.
        UiFocusMove,

        // Picking up a dropped item other than gold: an artifact, a spell, a
        // 권능, a key, a bomb or elif. Gold, SP and hearts keep their own cues.
        ItemPickup,

        // Sound-3: picking up gold; a door taking the player to the next
        // room; a chest opening; one of the player's footfalls while walking.
        GoldPickup,
        RoomTransition,
        ChestOpen,
        PlayerStep,

        // Sound-3: the 새마음금고 chewing while it heals (cut when the heal
        // ends), throwing its slime, and each hop of its walk.
        VaultEat,
        VaultSlimeThrow,
        VaultMove,

        // Sound pass 2026-11-21: Q's running, one looping voice from the launch until the
        // dash ends. It replaces the two footfall cues above, which are kept
        // only so the numbers after them do not move (CombatSfxSetup.RetiredCues).
        SkillUpperRun,

        // Sound pass 2026-11-21: a gold or elif purchase (or an investment) going through in the shop.
        ShopPurchase,

        // Sound pass 2026-11-21: one wingbeat per walk cycle while flying on 시스트의 가짜 날개.
        PlayerWingFlap,

        // Sound pass 2026-11-21: the 크레용사 drawing its sword to call its minions; the
        // sword charging before the map slash (cut at the slash); the two
        // layers that land after BossSwordSlam's swing; and its phase-two
        // transformation, faded out early.
        BossCrayonSummon,
        BossCrayonCharge,
        BossSwordSlamMagic,
        BossSwordSlamClash,
        BossCrayonTransform,
    }
}
