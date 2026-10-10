using System;
using System.Collections.Generic;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Player
{
    // Flight-0 (시스트의 가짜 날개): once started, the player flies until the Run ends. Flight passes over pits and low
    // obstacles only; walls, doors and chests still stop the body, and damage rules do not change. Pits are skipped by
    // excluding the Pit layer from the body. Low obstacles share the Environment layer with walls, so each nearby one
    // is ignored per collider pair instead of turning the whole layer off. The Game Scene reloads for every Run, so a
    // new Run always starts on foot.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerFlight : MonoBehaviour
    {
        // Wide enough that an obstacle is ignored before a dash reaches it within one physics step.
        public const float LowObstacleScanRadius = 2.5f;
        public const int FlyingSortingOrderBoost = 2;
        public const string ShadowObjectName = "Flight Shadow";
        private const float ShadowWidth = 0.8f;
        private const float ShadowGap = 0.12f;
        private const float ShadowBobSpeed = 3f;
        private const float ShadowBobScale = 0.08f;
        private readonly List<Collider2D> bodyColliders = new();
        private readonly List<Collider2D> nearbyColliders = new();
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer shadow;
        private Vector3 shadowBaseScale;

        public bool IsFlying { get; private set; }
        public SpriteRenderer Shadow => shadow;
        public event Action FlightStarted;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Returns false when already flying, so a second source never restarts it.
        public bool TryStartFlying()
        {
            if (IsFlying) return false;
            if (body == null) Awake();
            IsFlying = true;
            int pitLayer = RoomPit.Layer;
            if (pitLayer >= 0) body.excludeLayers |= 1 << pitLayer;
            IgnoreNearbyLowObstacles();
            if (spriteRenderer != null) spriteRenderer.sortingOrder += FlyingSortingOrderBoost;
            CreateShadow();
            FlightStarted?.Invoke();
            return true;
        }

        private void FixedUpdate()
        {
            if (IsFlying) IgnoreNearbyLowObstacles();
        }

        private void LateUpdate()
        {
            if (shadow == null) return;
            float bob = 1f + Mathf.Sin(Time.time * ShadowBobSpeed) * ShadowBobScale;
            shadow.transform.localScale = shadowBaseScale * bob;
        }

        // Runs before each physics step. Unity resets an ignored pair when either collider is disabled, so obstacles
        // restored by a room rebuild are ignored again the next time the player comes near them.
        public void IgnoreNearbyLowObstacles()
        {
            if (!IsFlying || body == null) return;
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
                // Obstacle-5: a flying body never collides with a vault, so overlapping it is the touch that spends
                // a key. Opening only deactivates the vault, which leaves this list intact.
                if (obstacle.TryGetComponent(out DestructibleObstacle destructible) &&
                    destructible.BreakRule == ObstacleBreakRule.BombOrKey && IsOverlapping(obstacle))
                {
                    destructible.TryOpenWithKey();
                }
            }
        }

        // Hitbox-0: the feet are what a walking player presses against a vault, so flight uses them too. A body
        // built without feet falls back to its own colliders.
        private bool IsOverlapping(Collider2D obstacle)
        {
            PlayerFeet feet = GetComponentInChildren<PlayerFeet>();
            if (feet != null) return feet.Overlaps(obstacle);
            foreach (Collider2D own in bodyColliders)
                if (own != null && !own.isTrigger && own.Distance(obstacle).isOverlapped) return true;
            return false;
        }

        public bool IsIgnoring(Collider2D obstacle)
        {
            if (body == null || obstacle == null) return false;
            body.GetAttachedColliders(bodyColliders);
            foreach (Collider2D own in bodyColliders)
                if (own != null && !own.isTrigger && !Physics2D.GetIgnoreCollision(own, obstacle)) return false;
            return bodyColliders.Count > 0;
        }

        private void CreateShadow()
        {
            if (shadow != null) return;
            GameObject shadowObject = new(ShadowObjectName, typeof(SpriteRenderer));
            shadowObject.transform.SetParent(transform, false);
            float feet = spriteRenderer != null && spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.min.y
                : -0.5f;
            shadowObject.transform.localPosition = new Vector3(0f, feet - ShadowGap, 0f);
            shadow = shadowObject.GetComponent<SpriteRenderer>();
            shadow.sprite = GetShadowSprite();
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            if (spriteRenderer != null)
            {
                shadow.sortingLayerID = spriteRenderer.sortingLayerID;
                shadow.sortingOrder = spriteRenderer.sortingOrder - 1;
            }

            // The shadow keeps its world size even if the player root is scaled.
            Vector3 parentScale = transform.lossyScale;
            shadowBaseScale = new Vector3(ShadowWidth / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                ShadowWidth * 12f / 32f / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
            shadowObject.transform.localScale = shadowBaseScale;
        }

        private static Sprite GetShadowSprite() => TrickalFanGame.Frontend.GroundShadow.SharedSprite;
    }
}
