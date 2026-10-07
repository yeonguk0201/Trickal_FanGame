using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 레비의 단도: once per Run a hit that would kill the player is cancelled whole (health and shield stay as
    // they were) and the player is invulnerable for a while. The artifact's stats stay after it is spent.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDeathWard : MonoBehaviour
    {
        private Health health;
        private float invulnerabilityDuration;

        public bool IsConfigured => invulnerabilityDuration > 0f;
        public bool IsSpent { get; private set; }
        public float InvulnerableUntil { get; private set; } = float.NegativeInfinity;

        private void OnDestroy()
        {
            if (health != null && IsConfigured) health.SetLethalDamageGuard(null);
        }

        public void Configure(float configuredInvulnerabilityDuration)
        {
            if (IsConfigured || configuredInvulnerabilityDuration <= 0f)
            {
                return;
            }

            invulnerabilityDuration = configuredInvulnerabilityDuration;
            health = GetComponent<Health>();
            health.SetLethalDamageGuard(TryNegate);
        }

        private bool TryNegate()
        {
            if (IsSpent || health == null || health.IsDead)
            {
                return false;
            }

            IsSpent = true;
            InvulnerableUntil = Time.time + invulnerabilityDuration;
            DamageInvulnerability invulnerability = GetComponent<DamageInvulnerability>();
            if (invulnerability == null)
            {
                invulnerability = gameObject.AddComponent<DamageInvulnerability>();
            }

            invulnerability.BeginWindow(Time.time, invulnerabilityDuration);
            Debug.Log($"[PlayerDeathWard] Cancelled a lethal hit; invulnerable for {invulnerabilityDuration}s.", this);
            return true;
        }
    }
}
