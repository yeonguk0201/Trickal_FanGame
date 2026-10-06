using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // Enemy-6 (쥬비): a flying enemy body. Like the flying player (PlayerFlight) it passes over pits and low obstacles
    // only; walls, doors, chests and trees still stop it. Pits are skipped by excluding the Pit layer from the body.
    // Low obstacles share the Environment layer with walls, so each nearby one is ignored per collider pair. It keeps
    // colliding with the player, so contact damage works, and player attacks hit it as usual.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyFlight : MonoBehaviour
    {
        // Wide enough that an obstacle is ignored before the body reaches it within one physics step.
        public const float LowObstacleScanRadius = 2.5f;

        private readonly List<Collider2D> bodyColliders = new();
        private readonly List<Collider2D> nearbyColliders = new();
        private Rigidbody2D body;

        private void Awake()
        {
            ApplyToBody();
        }

        private void FixedUpdate()
        {
            IgnoreNearbyLowObstacles();
        }

        // Idempotent; also called by verification, where Awake does not run.
        public void ApplyToBody()
        {
            body = GetComponent<Rigidbody2D>();
            int pitLayer = RoomPit.Layer;
            if (body != null && pitLayer >= 0) body.excludeLayers |= 1 << pitLayer;
        }

        // Runs before each physics step. Unity resets an ignored pair when either collider is disabled, so obstacles
        // restored by a room rebuild are ignored again the next time the body comes near them.
        public void IgnoreNearbyLowObstacles()
        {
            if (body == null) ApplyToBody();
            if (body == null) return;
            body.GetAttachedColliders(bodyColliders);
            ContactFilter2D filter = new()
            {
                useLayerMask = true,
                layerMask = RoomMovementClass.EnvironmentMask,
                useTriggers = false,
            };
            Physics2D.OverlapCircle(body.position, LowObstacleScanRadius, filter, nearbyColliders);
            foreach (Collider2D obstacle in nearbyColliders)
            {
                if (!RoomMovementClass.IsLowObstacle(obstacle)) continue;
                foreach (Collider2D own in bodyColliders)
                    if (own != null) Physics2D.IgnoreCollision(own, obstacle, true);
            }
        }

        public bool IsIgnoring(Collider2D obstacle)
        {
            if (body == null || obstacle == null) return false;
            body.GetAttachedColliders(bodyColliders);
            foreach (Collider2D own in bodyColliders)
                if (own != null && !own.isTrigger && !Physics2D.GetIgnoreCollision(own, obstacle)) return false;
            return bodyColliders.Count > 0;
        }
    }
}
