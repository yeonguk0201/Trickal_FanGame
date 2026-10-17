using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Player;
using TrickalFanGame.Run;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Trap-0: the gas a broken 독버섯 leaves for 3 seconds. The player takes half a heart from one cloud once.
    // Every enemy inside gains a poison stack each second with the 비비의 콧물 values, and because the player broke
    // the mushroom the poison is the player's damage and its kills are credited. Flying bodies are hit too.
    [DisallowMultipleComponent]
    public sealed class PoisonGasCloud : MonoBehaviour
    {
        public const float Radius = 1.5f;
        public const float Duration = 3f;
        public const float EnemyTickInterval = 1f;
        public const string ArtworkId = "effect-poison-gas";
        private const float FadeSeconds = 0.5f;
        private const float VisualAlpha = 0.7f;
        // Over actors and pickups, under a tree canopy.
        private const int SortingOrder = 11;

        // 24번 §4.8: 4 seconds, 1 second ticks, 15% of the attack damage per stack, up to 3 stacks.
        public static readonly PoisonSettings Poison = new(1f, 0.15f, 4f, 1f, 3);

        private readonly HashSet<Health> tickedEnemies = new();
        private PlayerMovement player;
        private Health playerHealth;
        private PlayerStats playerStats;
        private SpriteRenderer visual;
        private float nextEnemyTickTime;

        public float EndTime { get; private set; }
        public bool HasDamagedPlayer { get; private set; }
        public bool HasEnded { get; private set; }

        public static PoisonGasCloud Spawn(Vector3 position, Transform parent, PlayerMovement configuredPlayer,
            float currentTime)
        {
            GameObject owner = new("Poison Gas Cloud");
            owner.transform.SetParent(parent, false);
            owner.transform.position = position;
            PoisonGasCloud cloud = owner.AddComponent<PoisonGasCloud>();
            cloud.player = configuredPlayer;
            cloud.playerHealth = configuredPlayer != null ? configuredPlayer.GetComponent<Health>() : null;
            cloud.playerStats = configuredPlayer != null ? configuredPlayer.GetComponent<PlayerStats>() : null;
            cloud.EndTime = currentTime + Duration;
            cloud.nextEnemyTickTime = currentTime;
            Sprite sprite = EnvironmentHazardArtwork.Fit(EnvironmentHazardArtwork.Gas, 0f, Radius * 2f)
                ?? FairyKingdomArtwork.Fit(ArtworkId, Radius * 2f);
            if (sprite != null)
            {
                cloud.visual = owner.AddComponent<SpriteRenderer>();
                cloud.visual.sprite = sprite;
                cloud.visual.color = new Color(1f, 1f, 1f, VisualAlpha);
                cloud.visual.sortingOrder = SortingOrder;
            }

            return cloud;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Tick(Time.time);
        }

        public void Tick(float currentTime)
        {
            if (HasEnded) return;
            if (currentTime >= EndTime)
            {
                HasEnded = true;
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Vector2 center = transform.position;
            // An invulnerable player keeps the one hit for later, as with the other traps.
            if (!HasDamagedPlayer && player != null && playerHealth != null && !playerHealth.IsDead &&
                !playerHealth.IsInvulnerable &&
                (player.FeetPosition - center).sqrMagnitude <= Radius * Radius)
            {
                HasDamagedPlayer = true;
                playerHealth.GetComponent<PlayerDeathReason>()?.SetReason(RoomTrap.DeathReason);
                playerHealth.TakeDamage(RoomTrap.CreatePlayerDamage(gameObject));
            }

            while (currentTime >= nextEnemyTickTime && nextEnemyTickTime < EndTime)
            {
                nextEnemyTickTime += EnemyTickInterval;
                PoisonEnemies(center);
            }

            if (visual != null)
            {
                Sprite frame = EnvironmentHazardArtwork.Fit(EnvironmentHazardArtwork.Gas,
                    currentTime - (EndTime - Duration), Radius * 2f);
                if (frame != null) visual.sprite = frame;
                Color color = visual.color;
                color.a = VisualAlpha * Mathf.Clamp01((EndTime - currentTime) / FadeSeconds);
                visual.color = color;
            }
        }

        private void PoisonEnemies(Vector2 center)
        {
            if (playerStats == null) return;
            tickedEnemies.Clear();
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, Radius,
                         LayerMask.GetMask(RoomTrap.EnemyLayerName)))
            {
                Health enemy = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (enemy == null || enemy == playerHealth || !tickedEnemies.Add(enemy)) continue;
                EnemyStatusEffects.TryApplyPoison(enemy, playerStats.gameObject, playerStats.AttackDamage, Poison, 0f);
            }
        }
    }
}
