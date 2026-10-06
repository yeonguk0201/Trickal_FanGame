using System;
using System.Collections.Generic;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // A 1x1 room obstacle on the Environment layer: it blocks players, enemies, projectiles and charges like a wall,
    // but player attacks and skills break it after a fixed number of hits. Hits are counted, not damage, so attack
    // power never lowers the count. Breaking rolls the obstacle's drop table once from the room seed, and the room
    // state keeps it broken across revisits and floor reloads.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DestructibleObstacle : MonoBehaviour
    {
        public const int DefaultRequiredHits = 4;
        public const uint DropSeedSalt = 0x27D4EB2Fu;

        [SerializeField] private string obstacleId = "obstacle-01";
        [SerializeField] private string variantId = "rock";
        [SerializeField, Min(1)] private int requiredHits = DefaultRequiredHits;
        [SerializeField] private ResourceDropTable dropTable;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color intactColor = new(0.62f, 0.45f, 0.3f);
        [SerializeField] private Color crackedColor = new(0.3f, 0.2f, 0.14f);

        private RoomRunState runState;
        private RunProgress runProgress;
        private SecretRoomLink secretLink;
        private RoomNode sourceNode;
        private RoomController sourceRoom;
        private Transform dropParent;
        private int dropSeed;
        private int hitsTaken;
        private bool isBroken;

        public string ObstacleId => obstacleId;
        public string VariantId => variantId;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int HitsTaken => hitsTaken;
        public bool IsBroken => isBroken;
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public GameObject LastDrop { get; private set; }
        public event Action<DestructibleObstacle> Broken;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development panel only: the next obstacle broken on a floor with a secret room leaves a secret pit.
        public static bool DevelopmentForceNextSecretPit { get; set; }
#endif

        public static int ObstacleMask => LayerMask.GetMask("Environment");

        public void Configure(string configuredObstacleId, int configuredRequiredHits, ResourceDropTable configuredTable,
            SpriteRenderer configuredVisual)
        {
            obstacleId = configuredObstacleId;
            variantId = "rock";
            requiredHits = Mathf.Max(1, configuredRequiredHits);
            dropTable = configuredTable;
            visual = configuredVisual;
        }

        public void ApplyVariant(ObstacleVariantDefinition variant)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));
            variantId = variant.VariantId;
            requiredHits = variant.RequiredHits;
            dropTable = variant.DropTable;
            intactColor = variant.IntactColor;
            crackedColor = variant.CrackedColor;
            if (visual != null) visual.color = intactColor;
        }

        // Called when the room is built. A broken state from an earlier visit removes the obstacle immediately, and a
        // secret pit it left is restored because the pit is part of the room, not a one-time pickup. Without a
        // secret room on the floor (null link) the secret pit candidate leaves the drop roll.
        public void Bind(RoomRunState configuredState, int roomContentSeed, Transform configuredDropParent,
            RunProgress configuredProgress, SecretRoomLink configuredSecretLink = null,
            RoomNode configuredSourceNode = null, RoomController configuredSourceRoom = null)
        {
            runState = configuredState;
            dropSeed = DeriveDropSeed(roomContentSeed, obstacleId);
            dropParent = configuredDropParent;
            runProgress = configuredProgress;
            secretLink = configuredSecretLink;
            sourceNode = configuredSourceNode;
            sourceRoom = configuredSourceRoom;
            if (runState != null && runState.IsObstacleDestroyed(obstacleId))
            {
                isBroken = true;
                hitsTaken = RequiredHits;
                if (TryRollDrop(out ResourceDropEntry entry) && IsSecretPit(entry)) SpawnDrop(entry);
                gameObject.SetActive(false);
            }
        }

        public static bool IsSecretPit(ResourceDropEntry entry) =>
            entry?.Prefab != null && entry.Prefab.GetComponent<SecretPit>() != null;

        public bool TryRollDrop(out ResourceDropEntry entry)
        {
            entry = null;
            return dropTable != null &&
                   dropTable.TryRoll(dropSeed, candidate => secretLink != null || !IsSecretPit(candidate), out entry);
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(obstacleId, "Obstacle", out error)) return false;
            if (!StableRoomId.TryValidate(variantId, "Obstacle variant", out error)) return false;
            Collider2D obstacleCollider = GetComponent<Collider2D>();
            if (obstacleCollider == null || !obstacleCollider.enabled || obstacleCollider.isTrigger)
            {
                error = $"Destructible obstacle '{obstacleId}' requires one enabled solid Collider2D.";
                return false;
            }

            if (gameObject.layer != LayerMask.NameToLayer("Environment"))
            {
                error = $"Destructible obstacle '{obstacleId}' must use the Environment layer.";
                return false;
            }

            if (dropTable != null && !dropTable.TryValidate(out error)) return false;
            error = null;
            return true;
        }

        // One player attack or skill hit. Returns true when the hit counted.
        public bool RegisterPlayerHit()
        {
            if (isBroken || !isActiveAndEnabled) return false;
            hitsTaken++;
            if (visual != null)
            {
                visual.color = Color.Lerp(intactColor, crackedColor, hitsTaken / (float)RequiredHits);
            }

            if (hitsTaken >= RequiredHits) Break();
            return true;
        }

        // Hits every destructible obstacle touched by a player attack area once.
        public static int HitInCircle(Vector2 center, float radius, ISet<DestructibleObstacle> alreadyHit = null)
        {
            int hits = 0;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ObstacleMask))
            {
                if (TryHitCollider(collider, alreadyHit)) hits++;
            }

            return hits;
        }

        public static int DestroyByBombInCircle(Vector2 center, float radius,
            ISet<DestructibleObstacle> alreadyDestroyed = null)
        {
            int destroyed = 0;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ObstacleMask))
            {
                DestructibleObstacle obstacle = collider != null
                    ? collider.GetComponentInParent<DestructibleObstacle>()
                    : null;
                if (obstacle == null ||
                    (alreadyDestroyed != null && !alreadyDestroyed.Add(obstacle)) ||
                    !obstacle.TryDestroyByBomb())
                {
                    continue;
                }

                destroyed++;
            }

            return destroyed;
        }

        public bool TryDestroyByBomb()
        {
            if (isBroken || !isActiveAndEnabled) return false;
            hitsTaken = RequiredHits;
            if (visual != null) visual.color = crackedColor;
            Break();
            return true;
        }

        public static bool TryHitCollider(Collider2D collider, ISet<DestructibleObstacle> alreadyHit = null)
        {
            DestructibleObstacle obstacle = collider != null ? collider.GetComponentInParent<DestructibleObstacle>() : null;
            if (obstacle == null || (alreadyHit != null && !alreadyHit.Add(obstacle))) return false;
            return obstacle.RegisterPlayerHit();
        }

        // Stable across runs and platforms (FNV-1a over the stable obstacle ID), unlike string.GetHashCode.
        public static int DeriveDropSeed(int roomContentSeed, string configuredObstacleId)
        {
            uint hash = 2166136261u;
            foreach (char character in configuredObstacleId ?? string.Empty)
            {
                hash = unchecked((hash ^ character) * 16777619u);
            }

            return FloorGenerator.DeriveSeed(roomContentSeed, unchecked((int)hash), DropSeedSalt);
        }

        private void Break()
        {
            isBroken = true;
            bool firstBreak = runState == null || runState.TryMarkObstacleDestroyed(obstacleId);
            if (firstBreak && TryRollDevelopmentPit(out ResourceDropEntry forcedPit)) SpawnDrop(forcedPit);
            else if (firstBreak && TryRollDrop(out ResourceDropEntry entry)) SpawnDrop(entry);

            Broken?.Invoke(this);
            gameObject.SetActive(false);
        }

        private bool TryRollDevelopmentPit(out ResourceDropEntry entry)
        {
            entry = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!DevelopmentForceNextSecretPit || secretLink == null || dropTable == null) return false;
            foreach (ResourceDropEntry candidate in dropTable.Entries)
            {
                if (!IsSecretPit(candidate)) continue;
                entry = candidate;
                DevelopmentForceNextSecretPit = false;
                return true;
            }
#endif
            return false;
        }

        private void SpawnDrop(ResourceDropEntry entry)
        {
            if (entry?.Prefab == null) return;
            LastDrop = Instantiate(entry.Prefab, transform.position, Quaternion.identity,
                dropParent != null ? dropParent : transform.parent);
            LastDrop.name = $"Obstacle Drop {entry.DropId} - {obstacleId}";
            if (runProgress != null && LastDrop.TryGetComponent(out RunResourcePickup resourcePickup))
            {
                resourcePickup.BindRunProgress(runProgress);
            }

            if (LastDrop.TryGetComponent(out SecretPit pit))
            {
                pit.Bind(secretLink, sourceNode, sourceRoom);
            }
        }
    }
}
