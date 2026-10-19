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

        // Q: one footfall while the dash runs. The dash lasts until it hits
        // something, up to ten seconds, so no single clip can cover it; the
        // two feet alternate at a fixed cadence for as long as it lasts.
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
    }
}
