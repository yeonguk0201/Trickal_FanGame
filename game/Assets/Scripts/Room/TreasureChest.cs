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
    // steers around it (2026-10-03 decision). The player pushes it by walking into it: it slides along that axis at
    // PushSpeed, far slower than a heart, and stops short of walls, obstacles, other chests, enemies and pickups.
    // Physics alone cannot give that feel because the player sets its velocity every step. Contents are not generated
    // here; Chest-1 spawns them from the Opened event.
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

        // A pusher at pusherPosition walking along intent. The push follows the chest axis facing the pusher, and only
        // when the intent points into it. The next StepPush applies it once.
        public bool RegisterPush(Vector2 pusherPosition, Vector2 intent)
        {
            if (intent.sqrMagnitude < 0.0001f) return false;
            Vector2 toChest = (Vector2)transform.position - pusherPosition;
            Vector2 axis = Mathf.Abs(toChest.x) >= Mathf.Abs(toChest.y)
                ? new Vector2(Mathf.Sign(toChest.x), 0f)
                : new Vector2(0f, Mathf.Sign(toChest.y));
            if (Vector2.Dot(intent.normalized, axis) < MinimumPushAlignment) return false;
            hasPendingPush = true;
            pendingPushDirection = axis;
            return true;
        }

        // Moves the chest by one step of a registered push, short of anything that blocks it. Returns the distance.
        public float StepPush(float deltaTime)
        {
            if (!hasPendingPush) return 0f;
            hasPendingPush = false;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null || deltaTime <= 0f || !isActiveAndEnabled) return 0f;

            float distance = PushSpeed * deltaTime;
            ContactFilter2D filter = new() { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(PushBlockMask);
            int count = body.Cast(pendingPushDirection, filter, PushHits, distance + PushSkin);
            for (int index = 0; index < count; index++)
                distance = Mathf.Min(distance, Mathf.Max(0f, PushHits[index].distance - PushSkin));
            if (distance <= 0f) return 0f;
            body.MovePosition(body.position + pendingPushDirection * distance);
            return distance;
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
