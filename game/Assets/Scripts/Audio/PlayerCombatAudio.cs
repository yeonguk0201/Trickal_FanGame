using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Audio
{
    // Sound-0: turns the player's combat events into sound cues. It sits on
    // the player so every subscription dies with the player, and it is the
    // only place that knows which moment maps to which cue; the gameplay
    // scripts only raise events and never name a sound.
    [DisallowMultipleComponent]
    public sealed class PlayerCombatAudio : MonoBehaviour
    {
        private static readonly int CueCount = Enum.GetValues(typeof(CombatSfxCue)).Length;

        // Seconds between two footfalls while the dash runs. A sprint, not a
        // walk; the two step clips alternate at this cadence.
        [SerializeField, Min(0.02f)] private float dashStepInterval = 0.17f;

        private readonly int[] requestCounts = new int[CueCount];
        private readonly List<HomingSkillProjectile> trackedShots = new();
        private float nextDashStepTime;
        private bool nextDashStepIsLeft;

        public float DashStepInterval => dashStepInterval;

        public void ConfigureDashSteps(float interval)
        {
            dashStepInterval = Mathf.Max(0.02f, interval);
        }

        private Health health;
        private PlayerCombatEvents combatEvents;
        private PlayerProjectileAttack projectileAttack;
        private PlayerSkill lowerGradeSkill;
        private PlayerUltimate highGradeSkill;
        private PlayerSP sp;
        private PlayerSpellSlot spellSlot;
        private bool cached;
        // Set while resolving a hit that rolled a critical. Health raises
        // AnyDamageResolved — which carries the critical flag — just before
        // the basic attack hit event, so the flag is still true when the hit
        // arrives and picks the louder cue.
        private bool lastHitWasCritical;

        // How many times each cue has been asked for, whether or not a clip
        // was configured or the retrigger guard let it through. The verifier
        // asserts on this, so routing can be checked without audio hardware.
        public int GetRequestCount(CombatSfxCue cue) => requestCounts[(int)cue];

        private void Awake() => CacheComponents();

        private void OnEnable()
        {
            CacheComponents();
            if (health != null) health.DamageApplied += OnDamageApplied;
            if (combatEvents != null) combatEvents.BasicAttackHit += OnBasicAttackHit;
            Health.AnyDamageResolved -= OnAnyDamageResolved;
            Health.AnyDamageResolved += OnAnyDamageResolved;
            if (projectileAttack != null) projectileAttack.Fired += OnBasicAttackFired;
            if (lowerGradeSkill != null)
            {
                lowerGradeSkill.SalvoStarted += OnSalvoStarted;
                lowerGradeSkill.ProjectileLaunched += OnProjectileLaunched;
            }

            if (highGradeSkill != null)
            {
                highGradeSkill.DashStarted += OnDashStarted;
                highGradeSkill.DashEnded += OnDashEnded;
            }

            if (health != null) health.Healed += OnHealed;
            if (sp != null) sp.Restored += OnSpRestored;
            if (spellSlot != null) spellSlot.ItemUsed += OnSpellUsed;
        }

        private void OnDisable() => Unsubscribe();

        // Health.AnyDamageResolved is static, so a subscription left behind
        // outlives this component and the next damage anywhere in the game
        // would reach a destroyed object. OnDisable covers the normal path;
        // this covers a destroy that never disabled it first.
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (health != null) health.DamageApplied -= OnDamageApplied;
            if (combatEvents != null) combatEvents.BasicAttackHit -= OnBasicAttackHit;
            Health.AnyDamageResolved -= OnAnyDamageResolved;
            if (projectileAttack != null) projectileAttack.Fired -= OnBasicAttackFired;
            if (lowerGradeSkill != null)
            {
                lowerGradeSkill.SalvoStarted -= OnSalvoStarted;
                lowerGradeSkill.ProjectileLaunched -= OnProjectileLaunched;
            }

            if (highGradeSkill != null)
            {
                highGradeSkill.DashStarted -= OnDashStarted;
                highGradeSkill.DashEnded -= OnDashEnded;
            }

            if (health != null) health.Healed -= OnHealed;
            if (sp != null) sp.Restored -= OnSpRestored;
            if (spellSlot != null) spellSlot.ItemUsed -= OnSpellUsed;

            foreach (HomingSkillProjectile shot in trackedShots)
                if (shot != null) shot.Exploded -= OnShotExploded;
            trackedShots.Clear();
            nextDashStepTime = 0f;
        }

        private void CacheComponents()
        {
            if (cached) return;
            cached = true;
            health = GetComponent<Health>();
            combatEvents = GetComponent<PlayerCombatEvents>();
            projectileAttack = GetComponent<PlayerProjectileAttack>();
            lowerGradeSkill = GetComponent<PlayerSkill>();
            highGradeSkill = GetComponent<PlayerUltimate>();
            sp = GetComponent<PlayerSP>();
            spellSlot = GetComponent<PlayerSpellSlot>();
        }

        private void OnBasicAttackFired() => Play(CombatSfxCue.BasicAttack);

        // A critical replaces the ordinary hit rather than stacking on it.
        private void OnBasicAttackHit(PlayerBasicAttackHitEvent hit)
        {
            Play(lastHitWasCritical ? CombatSfxCue.HitCritical : CombatSfxCue.HitEnemy);
            lastHitWasCritical = false;
        }

        private void OnHealed(float amount)
        {
            if (amount <= 0f) return;
            Play(CombatSfxCue.Heal);
        }

        private void OnSpRestored() => Play(CombatSfxCue.SpGain);

        private void OnSpellUsed(ItemDefinition definition) => Play(CombatSfxCue.SpellCast);

        // Damage over time on an enemy: poison, burn, the damage aura, a
        // spell's pulse. They all arrive as Periodic delivery, which is what
        // separates a tick from a hit; the basic attack has its own event
        // above and never reaches here.
        public void OnAnyDamageResolved(Health target, DamageContext context, DamageResult result)
        {
            if (target == null || result.FinalDamage <= 0f) return;
            // Remember a critical basic attack for the hit event that follows.
            if (context.Source == gameObject && result.IsCritical &&
                context.SourceType is DamageSourceType.PlayerProjectile or DamageSourceType.PlayerAttack)
            {
                lastHitWasCritical = true;
            }

            if (context.DeliveryType != DamageDeliveryType.Periodic) return;
            if (!PlayerCombatEvents.IsPlayerDamage(context.SourceType)) return;
            // The player's own ticks are the hurt sound, not this one.
            if (target.gameObject == gameObject || target.GetComponent<PlayerCombatEvents>() != null) return;
            Play(CombatSfxCue.EnemyTickDamage);
        }

        // Every damage the player takes, traps and status ticks included. A
        // blocked hit reports no damage, so it stays silent.
        private void OnDamageApplied(DamageContext context, float damage, float remaining)
        {
            if (damage <= 0f) return;
            Play(CombatSfxCue.PlayerHurt);
        }

        private void OnSalvoStarted() => Play(CombatSfxCue.SkillLowerCast);

        private void OnProjectileLaunched(HomingSkillProjectile shot)
        {
            Play(CombatSfxCue.SkillLowerShot);
            // A shot destroys itself once it explodes or expires, so drop the
            // dead entries here instead of letting a long run pile them up.
            trackedShots.RemoveAll(tracked => tracked == null);
            if (shot == null) return;
            shot.Exploded += OnShotExploded;
            trackedShots.Add(shot);
        }

        private void OnShotExploded() => Play(CombatSfxCue.SkillLowerBurst);

        private void OnDashStarted()
        {
            Play(CombatSfxCue.SkillUpperDash);
            BeginDashSteps(Time.unscaledTime);
        }

        // The first footfall comes one cadence after the launch, so the
        // whoosh and a step do not land on the same frame.
        public void BeginDashSteps(float currentTime)
        {
            nextDashStepTime = currentTime + dashStepInterval;
            nextDashStepIsLeft = true;
        }

        // The dash has no fixed length — it runs until it hits something, up
        // to ten seconds — so the run is kept going here rather than baked
        // into a clip of its own.
        public void TickDashSteps(float currentTime)
        {
            if (highGradeSkill == null || !highGradeSkill.IsDashing) return;
            PumpDashSteps(currentTime);
        }

        // Plays every footfall due by now. Kept apart from the dash check
        // above so the cadence can be verified without a dashing player.
        public void PumpDashSteps(float currentTime)
        {
            // Never started, or the game stalled for a while: resync rather
            // than firing off every footfall that was missed.
            if (nextDashStepTime <= 0f || currentTime - nextDashStepTime > 1f)
                nextDashStepTime = currentTime;
            while (currentTime >= nextDashStepTime)
            {
                Play(nextDashStepIsLeft ? CombatSfxCue.SkillUpperStepLeft : CombatSfxCue.SkillUpperStepRight);
                nextDashStepIsLeft = !nextDashStepIsLeft;
                nextDashStepTime += dashStepInterval;
            }
        }

        private void Update() => TickDashSteps(Time.unscaledTime);

        // Only a dash that lands on an enemy makes the impact sound; running
        // out of time or cancelling it does not.
        private void OnDashEnded(UltimateEndReason reason)
        {
            if (reason != UltimateEndReason.Impact) return;
            Play(CombatSfxCue.SkillUpperImpact);
        }

        private void Play(CombatSfxCue cue)
        {
            requestCounts[(int)cue]++;
            GameAudioController.Instance?.PlayCombatCue(cue);
        }
    }
}
