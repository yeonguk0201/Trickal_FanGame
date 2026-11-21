using TrickalFanGame.Player;
using TrickalFanGame.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using System.Collections.Generic;

namespace TrickalFanGame.Room
{
    // The way to the next floor once the boss is down and its reward taken. Portal-E (2026-11-22): the player walks
    // up to it and presses E, like the reward altar, instead of being taken on touch. It has no solid collider; the
    // trigger is only the zone where "[E] 이동" shows. It is drawn standing up, so feet behind its base put the
    // player behind it.
    [RequireComponent(typeof(Collider2D))]
    public sealed class FloorAdvancePortal : MonoBehaviour
    {
        public const string EnterPrompt = "[E] 이동";
        // Where E works: about the reward altar's zone, centered a little under the portal's middle so it covers the
        // ground in front of it.
        public static readonly Vector2 InteractionZoneSize = new(2.6f, 2.6f);
        public static readonly Vector2 InteractionZoneOffset = new(0f, -0.6f);
        // The bottom of the drawn ring, as a share of one frame's height under the portal's center (PortalAurora's
        // ring center -0.015 and half height 0.405). Feet above it stand behind the portal.
        public const float BaseHeightFraction = -0.42f;
        public const float PromptHeight = 1.5f;
        private const float PlayerSearchInterval = 0.5f;
        public static readonly Color LockedColor = new(0.12f, 0.4f, 0.46f, 0.55f);
        public static readonly Color UnlockedColor = new(0.25f, 0.9f, 1f, 0.9f);

        public const float RevealDuration = 0.65f;
        public const float PickupClearanceRadius = 1.65f;
        private readonly List<Collider2D> nearbyPickups = new();
        private readonly HashSet<Transform> movedPickups = new();
        private Sprite[] frames;
        private Material portalMaterial;
        private Material originalMaterial;
        private PortalAurora aurora;
        private static readonly int PortalPhase = Shader.PropertyToID("_PortalPhase");
        private float visibleTime;
        public bool IsRevealed => IsUnlocked && visibleTime >= RevealDuration;

        private RoomGraphAssembler assembler;
        private RoomController bossRoom;
        private RunProgress runProgress;
        private string requiredRewardId;
        private int destinationFloor;
        private Collider2D portalTrigger;
        private SpriteRenderer indicator;
        private TextMeshPro prompt;
        private PlayerMovement playerInside;
        private PlayerMovement player;
        private PlayerFlight playerFlight;
        private float nextPlayerSearch;

        public int DestinationFloor => destinationFloor;
        public bool IsUnlocked { get; private set; }
        public SpriteRenderer Indicator => indicator;
        public string RequiredRewardId => requiredRewardId;
        public TextMeshPro Prompt => prompt;
        public bool IsPlayerInRange => playerInside != null;
        public bool CanInteract => IsUnlocked && IsRevealed && playerInside != null &&
                                   assembler?.Progress?.IsRewardSelectionPending != true && !RoomTransitionWipe.IsBusy;
        // The ground line of the drawn ring, in world space.
        public float BaseY => transform.position.y + (indicator != null && indicator.sprite != null
            ? indicator.sprite.rect.height / indicator.sprite.pixelsPerUnit * transform.lossyScale.y * BaseHeightFraction
            : 0f);

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
            if (portalMaterial != null)
            {
                if (indicator != null) indicator.sharedMaterial = originalMaterial;
                if (Application.isPlaying) Destroy(portalMaterial);
                else DestroyImmediate(portalMaterial);
            }
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
            PlayerMovement entering = other.GetComponentInParent<PlayerMovement>();
            if (entering != null) playerInside = entering;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerMovement leaving = other.GetComponentInParent<PlayerMovement>();
            if (leaving != null && leaving == playerInside) playerInside = null;
        }

        // Verification stands the player in the zone without physics.
        public void SetPlayerInRangeForVerification(PlayerMovement configuredPlayer) => playerInside = configuredPlayer;

        public bool TryEnter(PlayerMovement player)
        {
            if (!IsUnlocked || (Application.isPlaying && !IsRevealed) || player == null || assembler == null || bossRoom == null ||
                assembler.Progress?.IsRewardSelectionPending == true ||
                player.GetComponent<PlayerActionState>()?.CanTransition == false) return false;

            // Floor-Wipe: in play the screen closes first, the floor loads in the dark and the screen opens on it.
            // The boss music fades out under the closing screen and the floor music returns as it opens.
            // Edit-mode verifiers have no game clock, so they keep the immediate load below.
            if (Application.isPlaying)
            {
                if (RoomTransitionWipe.IsBusy) return false;
                PlayerMovement traveller = player;
                if (!RoomTransitionWipe.Play(() => LoadDestination(traveller))) return false;
                CombatSfx.Play(CombatSfxCue.BossPortal);
                GameAudioController.Instance?.LeaveBossMusic(RoomTransitionWipe.DarkUntil,
                    RoomTransitionWipe.DarkUntil);
                return true;
            }

            return LoadDestination(player, playPortalSound: true);
        }

        private bool LoadDestination(PlayerMovement player) => LoadDestination(player, playPortalSound: false);

        private bool LoadDestination(PlayerMovement player, bool playPortalSound)
        {
            if (assembler == null || player == null) return false;
            if (assembler.TryLoadFloor(destinationFloor, player, out string error))
            {
                // Sound-0: only a portal that actually took the player.
                if (playPortalSound) CombatSfx.Play(CombatSfxCue.BossPortal);
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

            if (portalTrigger is BoxCollider2D zone)
            {
                zone.size = InteractionZoneSize;
                zone.offset = InteractionZoneOffset;
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
            Shader shader = Resources.Load<Shader>("CombatEffects/PortalInterior");
            if (shader == null)
            {
                Debug.LogError("Portal interior shader is missing.", this);
                return;
            }
            originalMaterial = indicator.sharedMaterial;
            portalMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            portalMaterial.SetVector("_CellSize", new Vector4((float)width / sheet.width,
                (float)height / sheet.height, 0f, 0f));
            indicator.sharedMaterial = portalMaterial;
            aurora = GetComponent<PortalAurora>();
            if (aurora == null) aurora = gameObject.AddComponent<PortalAurora>();
            aurora.Configure(indicator);
        }

        private void Update()
        {
            if (!IsUnlocked)
            {
                if (prompt != null) prompt.gameObject.SetActive(false);
                return;
            }

            visibleTime += Time.deltaTime;
            ApplyVisual();
            if (!Application.isPlaying) return;
            EnsurePrompt();
            bool canInteract = CanInteract && Time.timeScale > 0f;
            if (prompt != null) prompt.gameObject.SetActive(canInteract);
            if (canInteract && Keyboard.current?.eKey.wasPressedThisFrame == true) TryEnter(playerInside);
        }

        private void LateUpdate()
        {
            if (indicator == null || !Application.isPlaying) return;
            if (player == null && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + PlayerSearchInterval;
                player = FindFirstObjectByType<PlayerMovement>();
                playerFlight = null;
            }

            // PlayerFlight is added only when the wings are taken, after the player was found.
            if (playerFlight == null && player != null) playerFlight = player.GetComponent<PlayerFlight>();
            bool behind = player != null && player.gameObject.activeInHierarchy && player.FeetPosition.y > BaseY;
            bool flying = playerFlight != null && playerFlight.IsFlying;
            indicator.sortingOrder = SortingOrderFor(behind, flying);
            if (aurora != null && aurora.Glow != null) aurora.Glow.sortingOrder = indicator.sortingOrder - 1;
        }

        // In front the portal sits low (1, its aurora under it at 0); behind, the ring and its aurora both go over
        // the player's body, a flying body included.
        public static int SortingOrderFor(bool playerBehind, bool playerFlying)
        {
            if (!playerBehind) return 1;
            return PlayerMovement.BodySortingOrder + 2 + (playerFlying ? PlayerFlight.FlyingSortingOrderBoost : 0);
        }

        // The same world-space label as the reward altar's "[E] 보상 선택", over the portal's top.
        private void EnsurePrompt()
        {
            if (prompt != null) return;
            GameObject label = new("Interaction Prompt", typeof(RectTransform), typeof(TextMeshPro));
            label.transform.SetParent(transform, false);
            prompt = label.GetComponent<TextMeshPro>();
            prompt.text = EnterPrompt;
            prompt.fontSize = 3.5f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = new Color(0.9f, 0.98f, 1f, 1f);
            prompt.sortingOrder = PlayerMovement.AboveBodySortingOrder + PlayerFlight.FlyingSortingOrderBoost + 1;
            RectTransform rect = (RectTransform)label.transform;
            rect.localPosition = new Vector3(0f, PromptHeight, 0f);
            rect.sizeDelta = new Vector2(5f, 1f);
            label.SetActive(false);
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
            if (aurora != null) aurora.Evaluate(visibleTime, IsUnlocked ? reveal : 0f);
            // Keep the mesh, alpha and outer ring fixed; only blend the inner water-like light.
            if (portalMaterial != null)
                portalMaterial.SetFloat(PortalPhase, visibleTime * 2f % 4f);
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
