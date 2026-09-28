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
        public const int DefaultRequiredHits = 5;
        public const uint DropSeedSalt = 0x27D4EB2Fu;

        [SerializeField] private string obstacleId = "obstacle-01";
        [SerializeField, Min(1)] private int requiredHits = DefaultRequiredHits;
        [SerializeField] private ResourceDropTable dropTable;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color intactColor = new(0.62f, 0.45f, 0.3f);
        [SerializeField] private Color crackedColor = new(0.3f, 0.2f, 0.14f);

        private RoomRunState runState;
        private RunProgress runProgress;
        private Transform dropParent;
        private int dropSeed;
        private int hitsTaken;
        private bool isBroken;

        public string ObstacleId => obstacleId;
        public int RequiredHits => Mathf.Max(1, requiredHits);
        public int HitsTaken => hitsTaken;
        public bool IsBroken => isBroken;
        public ResourceDropTable DropTable => dropTable;
        public int DropSeed => dropSeed;
        public GameObject LastDrop { get; private set; }
        public event Action<DestructibleObstacle> Broken;

        public static int ObstacleMask => LayerMask.GetMask("Environment");

        public void Configure(string configuredObstacleId, int configuredRequiredHits, ResourceDropTable configuredTable,
            SpriteRenderer configuredVisual)
        {
            obstacleId = configuredObstacleId;
            requiredHits = Mathf.Max(1, configuredRequiredHits);
            dropTable = configuredTable;
            visual = configuredVisual;
        }

        // Called when the room is built. A broken state from an earlier visit removes the obstacle immediately.
        public void Bind(RoomRunState configuredState, int roomContentSeed, Transform configuredDropParent,
            RunProgress configuredProgress)
        {
            runState = configuredState;
            dropSeed = DeriveDropSeed(roomContentSeed, obstacleId);
            dropParent = configuredDropParent;
            runProgress = configuredProgress;
            if (runState != null && runState.IsObstacleDestroyed(obstacleId))
            {
                isBroken = true;
                hitsTaken = RequiredHits;
                gameObject.SetActive(false);
            }
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(obstacleId, "Obstacle", out error)) return false;
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
            if (firstBreak && dropTable != null && dropTable.TryRoll(dropSeed, out ResourceDropEntry entry) &&
                entry.Prefab != null)
            {
                LastDrop = Instantiate(entry.Prefab, transform.position, Quaternion.identity,
                    dropParent != null ? dropParent : transform.parent);
                LastDrop.name = $"Obstacle Drop {entry.DropId} - {obstacleId}";
                if (runProgress != null && LastDrop.TryGetComponent(out RunResourcePickup resourcePickup))
                {
                    resourcePickup.BindRunProgress(runProgress);
                }
            }

            Broken?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
