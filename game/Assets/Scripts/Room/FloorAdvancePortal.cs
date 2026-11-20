using TrickalFanGame.Player;
using TrickalFanGame.Audio;
using UnityEngine;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using System.Collections.Generic;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FloorAdvancePortal : MonoBehaviour
    {
        public static readonly Color LockedColor = new(0.12f, 0.4f, 0.46f, 0.55f);
        public static readonly Color UnlockedColor = new(0.25f, 0.9f, 1f, 0.9f);

        public const float RevealDuration = 0.65f;
        public const float PickupClearanceRadius = 1.65f;
        private readonly List<Collider2D> nearbyPickups = new();
        private readonly HashSet<Transform> movedPickups = new();
        private Sprite[] frames;
        private float visibleTime;
        public bool IsRevealed => IsUnlocked && visibleTime >= RevealDuration;

        private RoomGraphAssembler assembler;
        private RoomController bossRoom;
        private RunProgress runProgress;
        private string requiredRewardId;
        private int destinationFloor;
        private Collider2D portalTrigger;
        private SpriteRenderer indicator;

        public int DestinationFloor => destinationFloor;
        public bool IsUnlocked { get; private set; }
        public SpriteRenderer Indicator => indicator;
        public string RequiredRewardId => requiredRewardId;

        public void Configure(
            RoomGraphAssembler configuredAssembler,
            RoomController configuredBossRoom,
            int configuredDestinationFloor,
            SpriteRenderer configuredIndicator)
        {
            Configure(configuredAssembler, configuredBossRoom, configuredDestinationFloor,
                configuredIndicator, null, null);
        }

        public void Configure(
            RoomGraphAssembler configuredAssembler,
            RoomController configuredBossRoom,
            int configuredDestinationFloor,
            SpriteRenderer configuredIndicator,
            RunProgress configuredProgress,
            string configuredRequiredRewardId)
        {
            if (bossRoom != null)
            {
                bossRoom.StateChanged -= OnBossRoomStateChanged;
            }
            if (runProgress != null)
            {
                runProgress.RewardSelectionStateChanged -= RefreshUnlocked;
            }

            assembler = configuredAssembler;
            bossRoom = configuredBossRoom;
            destinationFloor = Mathf.Max(1, configuredDestinationFloor);
            indicator = configuredIndicator;
            runProgress = configuredProgress;
            requiredRewardId = configuredRequiredRewardId;
            EnsureTrigger();
            LoadArtwork();
            visibleTime = 0f;

            if (bossRoom != null)
            {
                bossRoom.StateChanged += OnBossRoomStateChanged;
            }
            if (runProgress != null)
            {
                runProgress.RewardSelectionStateChanged += RefreshUnlocked;
            }

            RefreshUnlocked();
        }

        private void Awake() => EnsureTrigger();

        private void OnDestroy()
        {
            ReleaseRuntimeBindings();
            if (frames != null)
                foreach (Sprite frame in frames)
                    if (frame != null)
                    {
                        if (Application.isPlaying) Destroy(frame);
                        else DestroyImmediate(frame);
                    }
        }

        public void ReleaseRuntimeBindings()
        {
            if (bossRoom != null)
            {
                bossRoom.StateChanged -= OnBossRoomStateChanged;
            }
            if (runProgress != null)
            {
                runProgress.RewardSelectionStateChanged -= RefreshUnlocked;
            }
        }

        private void OnBossRoomStateChanged(RoomState state) => RefreshUnlocked();

        private void RefreshUnlocked()
        {
            bool rewardComplete = string.IsNullOrWhiteSpace(requiredRewardId) ||
                                  runProgress?.GetRewardSelection(requiredRewardId)?.IsCompleted == true;
            SetUnlocked(bossRoom != null && bossRoom.State == RoomState.Cleared && rewardComplete);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null) TryEnter(player);
        }

        public bool TryEnter(PlayerMovement player)
        {
            if (!IsUnlocked || (Application.isPlaying && !IsRevealed) || player == null || assembler == null || bossRoom == null ||
                assembler.Progress?.IsRewardSelectionPending == true ||
                player.GetComponent<PlayerActionState>()?.CanTransition == false) return false;

            if (assembler.TryLoadFloor(destinationFloor, player, out string error))
            {
                // Sound-0: only a portal that actually took the player.
                CombatSfx.Play(CombatSfxCue.BossPortal);
                Debug.Log($"[FloorAdvancePortal] Entered floor {destinationFloor}.", this);
                return true;
            }

            Debug.LogError($"[FloorAdvancePortal] Could not enter floor {destinationFloor}. {error}", this);
            return false;
        }

        private void EnsureTrigger()
        {
            if (portalTrigger == null)
            {
                portalTrigger = GetComponent<Collider2D>();
            }

            if (portalTrigger != null)
            {
                portalTrigger.isTrigger = true;
            }
        }

        private void LoadArtwork()
        {
            if (frames != null || indicator == null) return;
            Texture2D sheet = Resources.Load<Texture2D>("CombatEffects/boss-room-portal-idle");
            if (sheet == null)
            {
                Debug.LogError("Boss room portal artwork is missing.", this);
                return;
            }
            // The source has an odd height: omit the one transparent middle row.
            int width = sheet.width / 2;
            int height = sheet.height / 2;
            frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
            {
                int y = i < 2 ? sheet.height - height : 0;
                frames[i] = Sprite.Create(sheet, new Rect(i % 2 * width, y, width, height),
                    new Vector2(0.5f, 0.5f), 220f);
                frames[i].name = $"Portal idle {i + 1}";
            }
            indicator.sprite = frames[0];
        }

        private void Update()
        {
            if (!IsUnlocked) return;
            visibleTime += Time.deltaTime;
            ApplyVisual();
        }

        private void FixedUpdate()
        {
            if (!IsUnlocked) return;
            ClearOverlappingPickups();
            portalTrigger.enabled = IsRevealed;
        }

        // Query triggers explicitly, independent of the project's global queriesHitTriggers setting.
        public void ClearOverlappingPickups()
        {
            if (!IsUnlocked) return;
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new() { useTriggers = true };
            Physics2D.OverlapCircle(transform.position, PickupClearanceRadius, filter, nearbyPickups);
            movedPickups.Clear();
            foreach (Collider2D hit in nearbyPickups)
            {
                Component pickup = hit.GetComponentInParent<ItemPickup>();
                if (pickup == null) pickup = hit.GetComponentInParent<SingleUseItemPickup>();
                if (pickup == null) pickup = hit.GetComponentInParent<ActivePowerPickup>();
                if (pickup == null) pickup = hit.GetComponentInParent<HealthPickup>();
                if (pickup == null) pickup = hit.GetComponentInParent<RunResourcePickup>();
                if (pickup == null) pickup = hit.GetComponentInParent<SPPickup>();
                if (pickup == null || !movedPickups.Add(pickup.transform)) continue;
                Vector2 offset = (Vector2)pickup.transform.position - (Vector2)transform.position;
                float radius = 0f;
                foreach (Collider2D shape in pickup.GetComponentsInChildren<Collider2D>())
                    radius = Mathf.Max(radius, Vector2.Distance(pickup.transform.position, shape.bounds.center)
                        + ((Vector2)shape.bounds.extents).magnitude);
                float distance = PickupClearanceRadius + radius + 0.1f;
                if (offset.magnitude >= distance) continue;
                Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.down;
                Vector2 target = (Vector2)transform.position + direction * distance;
                pickup.transform.position = new Vector3(target.x, target.y, pickup.transform.position.z);
                Rigidbody2D body = pickup.GetComponent<Rigidbody2D>();
                if (body != null) body.position = target;
            }
            Physics2D.SyncTransforms();
        }

        private void ApplyVisual()
        {
            if (indicator == null) return;
            indicator.enabled = IsUnlocked;
            float reveal = Mathf.SmoothStep(0f, 1f, visibleTime / RevealDuration);
            indicator.color = new Color(1f, 1f, 1f, reveal);
            if (frames != null)
                indicator.sprite = frames[Mathf.FloorToInt(visibleTime * 6f) % frames.Length];
        }

        private void SetUnlocked(bool unlocked)
        {
            if (unlocked != IsUnlocked) visibleTime = 0f;
            // Sound-2: the portal opening once the boss is down and its reward taken.
            if (unlocked && !IsUnlocked) CombatSfx.Play(CombatSfxCue.PortalAppear);
            IsUnlocked = unlocked;
            EnsureTrigger();
            // Edit-mode integration verifiers exercise transitions without a game clock.
            if (portalTrigger != null)
                portalTrigger.enabled = unlocked && (!Application.isPlaying || IsRevealed);
            ApplyVisual();
        }
    }
}
