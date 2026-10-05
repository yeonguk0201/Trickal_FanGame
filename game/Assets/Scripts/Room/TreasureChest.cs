using System;
using System.Collections.Generic;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Chest-0: a chest on the room floor. There is no open key: a normal chest opens when the player touches it, a
    // golden chest opens on touch by spending one key (nothing happens without one), and a diamond chest opens only
    // from a player bomb explosion. The room's RoomRunState records the open state, so repeated contacts, overlapping
    // explosions, revisits and floor rebuilds open it once. The chest is a solid kinematic body on the Environment
    // layer, so it blocks the player, enemies, projectiles and floor pickups like an obstacle, and enemy navigation
    // steers around it (2026-10-03 decision). The player pushes it by walking into it: like a floor heart it moves away
    // from the player in any direction (Jjangsem-1, D4: no longer only along the four axes), at PushSpeed, far slower
    // than a heart. It stops short of walls, obstacles, other chests and enemies, and a push into one of them at an
    // angle slides along it; a wall it only touches never holds it. A floor pickup in the way is shoved ahead unless
    // something holds that pickup, in which case the chest stops too. Physics alone cannot give that feel because the player sets its velocity every step.
    // Contents are not generated here; Chest-1 spawns them from the Opened event.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
    public sealed class TreasureChest : MonoBehaviour
    {
        public const string LayerName = "Environment";
        // Units per second while pushed; the player walks at 5 and pushes a heart almost at that speed.
        public const float PushSpeed = 1.2f;
        // The movement intent must point this much into the push axis, so brushing past a chest does not move it.
        public const float MinimumPushAlignment = 0.5f;
        public const float PushSkin = 0.02f;
        private static readonly RaycastHit2D[] PushHits = new RaycastHit2D[8];
        private static readonly RaycastHit2D[] PickupHits = new RaycastHit2D[8];
        public const string DefaultChestId = "chest-01";

        public static readonly Color NormalColor = new(0.66f, 0.43f, 0.22f, 1f);
        public static readonly Color GoldenColor = new(1f, 0.8f, 0.2f, 1f);
        public static readonly Color DiamondColor = new(0.45f, 0.92f, 1f, 1f);
        public static readonly Color OpenedTint = new(0.45f, 0.45f, 0.45f, 0.75f);

        [SerializeField] private string chestId = DefaultChestId;
        [SerializeField] private ChestKind kind;
        [SerializeField] private SpriteRenderer visual;

        private RoomRunState runState;
        private RunProgress runProgress;
        private ChestRunState record;
        private Rigidbody2D body;
        private bool hasPendingPush;
        private Vector2 pendingPushDirection;

        public string ChestId => chestId;
        public ChestKind Kind => kind;
        public SpriteRenderer Visual => visual;
        public RoomRunState RunState => runState;
        public ChestRunState Record => record;
        public bool IsBound => record != null;
        public bool IsOpened => record?.IsOpened == true;
        public bool IsClosed => record?.IsClosed == true;
        public event Action<TreasureChest, ChestOpenMethod> Opened;

        public static int ChestMask => LayerMask.GetMask(LayerName);

        public static ChestOpenMethod OpenMethodFor(ChestKind chestKind) => chestKind switch
        {
            ChestKind.Golden => ChestOpenMethod.TouchWithKey,
            ChestKind.Diamond => ChestOpenMethod.Bomb,
            _ => ChestOpenMethod.Touch,
        };

        public static Color ColorFor(ChestKind chestKind) => chestKind switch
        {
            ChestKind.Golden => GoldenColor,
            ChestKind.Diamond => DiamondColor,
            _ => NormalColor,
        };

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = false;
            body = GetComponent<Rigidbody2D>();
            ConfigureBody(body);
            ApplyVisuals();
        }

        public static void ConfigureBody(Rigidbody2D chestBody)
        {
            chestBody.bodyType = RigidbodyType2D.Kinematic;
            chestBody.gravityScale = 0f;
            chestBody.freezeRotation = true;
            chestBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Enemy projectiles are kinematic triggers; full contacts let them meet the chest like a wall.
            chestBody.useFullKinematicContacts = true;
        }

        public static int PushBlockMask => LayerMask.GetMask("Environment", RoomPit.LayerName, "Enemy", "Pickup");

        // Sets the room-local ID and kind of a fresh instance, before it is bound to the room state.
        public void Configure(string configuredChestId, ChestKind configuredKind)
        {
            if (record != null)
                throw new InvalidOperationException($"Chest '{chestId}' is already bound and cannot be reconfigured.");
            chestId = configuredChestId;
            kind = configuredKind;
            ApplyVisuals();
        }

        public void ConfigureDisplay(SpriteRenderer configuredVisual)
        {
            visual = configuredVisual;
            ApplyVisuals();
        }

        // Called when the room is built. A chest discarded with its floor removes itself; an opened one stays open.
        // Returns true while the chest can still be opened.
        public bool Bind(RoomRunState configuredState, RunProgress configuredProgress)
        {
            if (configuredState == null) throw new ArgumentNullException(nameof(configuredState));
            if (configuredProgress == null) throw new ArgumentNullException(nameof(configuredProgress));
            runState = configuredState;
            runProgress = configuredProgress;
            record = runState.RegisterChest(chestId, kind);
            ApplyVisuals();
            if (record.IsDiscarded) gameObject.SetActive(false);
            return record.IsClosed;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(chestId, "Chest", out error)) return false;
            if (!Enum.IsDefined(typeof(ChestKind), kind))
            {
                error = $"Chest '{chestId}' has an undefined kind {(int)kind}.";
                return false;
            }

            Collider2D chestCollider = GetComponent<Collider2D>();
            if (chestCollider == null || !chestCollider.enabled || chestCollider.isTrigger)
            {
                error = $"Chest '{chestId}' requires one enabled solid Collider2D.";
                return false;
            }

            Rigidbody2D chestBody = GetComponent<Rigidbody2D>();
            if (chestBody == null || chestBody.bodyType != RigidbodyType2D.Kinematic || !chestBody.freezeRotation ||
                !chestBody.useFullKinematicContacts)
            {
                error = $"Chest '{chestId}' requires a non-rotating kinematic Rigidbody2D with full contacts.";
                return false;
            }

            if (gameObject.layer != LayerMask.NameToLayer(LayerName))
            {
                error = $"Chest '{chestId}' must use the {LayerName} layer.";
                return false;
            }

            error = null;
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision);

        // Stay lets a player still pressing against a golden chest open it once they pick up a key, and keeps a push
        // going while the player walks into the chest.
        private void OnCollisionStay2D(Collision2D collision) => HandlePlayerContact(collision);

        private void HandlePlayerContact(Collision2D collision)
        {
            PlayerMovement player = collision.collider.GetComponentInParent<PlayerMovement>();
            if (player == null) return;
            TryOpenByTouch(player);
            RegisterPush(player.transform.position, player.MovementIntent);
        }

        private void FixedUpdate()
        {
            StepPush(Time.fixedDeltaTime);
        }

        // A pusher at pusherPosition walking along intent. The push follows the line from the pusher's center through
        // the chest's, like a heart, and only when the intent points into it. The next StepPush applies it once.
        public bool RegisterPush(Vector2 pusherPosition, Vector2 intent)
        {
            if (intent.sqrMagnitude < 0.0001f) return false;
            Vector2 toChest = (Vector2)transform.position - pusherPosition;
            if (toChest.sqrMagnitude < 0.0001f) return false;
            Vector2 direction = toChest.normalized;
            if (Vector2.Dot(intent.normalized, direction) < MinimumPushAlignment) return false;
            hasPendingPush = true;
            pendingPushDirection = direction;
            return true;
        }

        // Moves the chest by one step of a registered push, short of anything that blocks it. A chest already against
        // something slides along it by the part of the push that does not point into it. Returns the distance.
        public float StepPush(float deltaTime)
        {
            if (!hasPendingPush) return 0f;
            hasPendingPush = false;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null || deltaTime <= 0f || !isActiveAndEnabled) return 0f;

            Vector2 direction = pendingPushDirection;
            float reach = PushSpeed * deltaTime;
            float distance = CastPush(direction, reach, out Vector2 blockNormal);
            if (distance <= 0f && blockNormal != Vector2.zero)
            {
                Vector2 slide = direction - Vector2.Dot(direction, blockNormal) * blockNormal;
                if (slide.sqrMagnitude < 0.0001f) return 0f;
                reach *= slide.magnitude;
                direction = slide.normalized;
                distance = CastPush(direction, reach, out _);
            }

            if (distance <= 0f) return 0f;
            body.MovePosition(body.position + direction * distance);
            return distance;
        }

        // The free distance along direction up to reach, and the normal of the nearest blocker that shortened it.
        // A surface the chest touches but does not move into (a wall it slides along, the player behind it) never
        // blocks, and a floor pickup only blocks when it cannot move on itself: otherwise the chest shoves it.
        private float CastPush(Vector2 direction, float reach, out Vector2 blockNormal)
        {
            blockNormal = Vector2.zero;
            ContactFilter2D filter = new() { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(PushBlockMask);
            float distance = reach;
            int count = body.Cast(direction, filter, PushHits, reach + PushSkin);
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = PushHits[index];
                if (!IsMovingInto(hit, direction)) continue;
                float allowed = Mathf.Max(0f, hit.distance - PushSkin);
                if (allowed >= distance || CanShove(hit, direction, reach)) continue;
                distance = allowed;
                blockNormal = hit.normal;
            }

            return distance;
        }

        private static bool IsMovingInto(RaycastHit2D hit, Vector2 direction) =>
            Vector2.Dot(hit.normal, direction) < -0.01f;

        // A dynamic floor pickup the chest runs into moves ahead of it unless a wall, pit or enemy holds it in place.
        private static bool CanShove(RaycastHit2D hit, Vector2 direction, float reach)
        {
            if (hit.collider == null || hit.collider.gameObject.layer != LayerMask.NameToLayer(HealthPickup.LayerName))
                return false;
            Rigidbody2D pickup = hit.rigidbody;
            if (pickup == null || pickup.bodyType != RigidbodyType2D.Dynamic) return false;

            ContactFilter2D filter = new() { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(LayerMask.GetMask("Environment", RoomPit.LayerName, "Enemy"));
            int count = pickup.Cast(direction, filter, PickupHits, reach + PushSkin);
            for (int index = 0; index < count; index++)
            {
                // The chest sits behind the pickup, so its normal never points against the push.
                RaycastHit2D pickupHit = PickupHits[index];
                if (IsMovingInto(pickupHit, direction) && pickupHit.distance - PushSkin < reach) return false;
            }

            return true;
        }

        public bool TryOpenByTouch(PlayerMovement player)
        {
            if (player == null || !CanOpen()) return false;
            switch (kind)
            {
                case ChestKind.Normal:
                    return Open(ChestOpenMethod.Touch);
                case ChestKind.Golden:
                    if (!runProgress.TrySpendResource(RunResourceType.Key)) return false;
                    if (Open(ChestOpenMethod.TouchWithKey)) return true;
                    // A single-threaded Unity frame cannot normally reach this branch, but keep the spend atomic.
                    runProgress.TryAddResource(RunResourceType.Key, 1);
                    return false;
                default:
                    return false;
            }
        }

        public bool TryOpenByBomb()
        {
            return kind == ChestKind.Diamond && CanOpen() && Open(ChestOpenMethod.Bomb);
        }

        // Opens every diamond chest within the explosion once, however many explosions overlap it.
        public static int OpenByBombInCircle(Vector2 center, float radius)
        {
            int opened = 0;
            HashSet<TreasureChest> visited = new();
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(center, radius, ChestMask))
            {
                TreasureChest chest = collider != null ? collider.GetComponentInParent<TreasureChest>() : null;
                if (chest != null && visited.Add(chest) && chest.TryOpenByBomb()) opened++;
            }

            return opened;
        }

        private bool CanOpen() =>
            record != null && record.IsClosed && isActiveAndEnabled && !runProgress.IsProgressionStopped;

        private bool Open(ChestOpenMethod method)
        {
            if (!runState.TryOpenChest(chestId)) return false;
            ApplyVisuals();
            Opened?.Invoke(this, method);
            return true;
        }

        private void ApplyVisuals()
        {
            if (visual == null) return;
            Color color = ColorFor(kind);
            visual.color = IsOpened ? color * OpenedTint : color;
        }
    }
}
