using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 레비의 단도: once per Run a hit that would kill the player is cancelled whole (health and shield stay as
    // they were) and the player is invulnerable for a while. The artifact's stats stay after it is spent.
    // The cancelled hit still starts the usual post-hit window (blink, passing through enemy bodies, 1 s or 2 s
    // for a heavy hit); the rest of the invulnerability keeps the body collisions. DeathWardGlow shows it.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDeathWard : MonoBehaviour
    {
        private Health health;
        private float invulnerabilityDuration;
        private string itemId;

        public bool IsConfigured => invulnerabilityDuration > 0f;
        public bool IsSpent { get; private set; }
        public float InvulnerabilityDuration => invulnerabilityDuration;
        public float SpentAt { get; private set; } = float.NegativeInfinity;
        public float InvulnerableUntil { get; private set; } = float.NegativeInfinity;
        public DeathWardGlow Glow { get; private set; }

        private void OnDestroy()
        {
            if (health != null && IsConfigured) health.SetLethalDamageGuard(null);
        }

        public void Configure(float configuredInvulnerabilityDuration, string configuredItemId = null)
        {
            if (IsConfigured || configuredInvulnerabilityDuration <= 0f)
            {
                return;
            }

            invulnerabilityDuration = configuredInvulnerabilityDuration;
            itemId = configuredItemId;
            health = GetComponent<Health>();
            health.SetLethalDamageGuard(TryNegate);
        }

        private bool TryNegate(float damage)
        {
            if (IsSpent || health == null || health.IsDead)
            {
                return false;
            }

            IsSpent = true;
            SpentAt = Time.time;
            InvulnerableUntil = Time.time + invulnerabilityDuration;
            DamageInvulnerability invulnerability = GetComponent<DamageInvulnerability>();
            if (invulnerability == null)
            {
                invulnerability = gameObject.AddComponent<DamageInvulnerability>();
            }

            // The hit's own window first (it alone lets the body pass through enemies), then the longer one.
            invulnerability.BeginHitWindowForDamage(Time.time, damage);
            invulnerability.BeginWindow(Time.time, invulnerabilityDuration);
            Glow = DeathWardGlow.Play(GetComponent<SpriteRenderer>(), Time.time, invulnerabilityDuration);
            if (!string.IsNullOrEmpty(itemId)) GetComponent<PlayerInventory>()?.MarkArtifactSpent(itemId);
            Debug.Log($"[PlayerDeathWard] Cancelled a lethal hit; invulnerable for {invulnerabilityDuration}s.", this);
            return true;
        }
    }
}
