using TrickalFanGame.Audio;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum DoorVisualKind
    {
        Normal,
        Boss,
        KeyLockedTreasure,
        SecretPassage,
        Shop,
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class DoorController : MonoBehaviour
    {
        public static readonly Color NormalLockedColor = new(0.12f, 0.42f, 0.18f);
        public static readonly Color NormalOpenColor = new(0.2f, 0.75f, 0.3f);
        public static readonly Color BossLockedColor = new(0.34f, 0.12f, 0.5f);
        public static readonly Color BossOpenColor = new(0.64f, 0.28f, 0.84f);
        public static readonly Color KeyLockedColor = new(0.62f, 0.38f, 0.06f);
        public static readonly Color KeyOpenColor = new(0.95f, 0.72f, 0.2f);
        public static readonly Color SecretLockedColor = new(0.24f, 0.2f, 0.18f);
        public static readonly Color SecretOpenColor = new(0.46f, 0.38f, 0.32f);

        [SerializeField] private Collider2D blocker;
        [SerializeField] private Collider2D interiorBlocker;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private bool remainBlockingWhenOpen;
        [SerializeField] private DoorVisualKind visualKind;
        [SerializeField] private Color lockedColor = new(0.12f, 0.42f, 0.18f);
        [SerializeField] private Color openColor = new(0.2f, 0.75f, 0.3f);

        private bool encounterLocked;
        private bool keyLocked;
        private bool ignoresEncounterLock;

        public bool IsLocked { get; private set; }
        public DoorVisualKind VisualKind => visualKind;
        public Color VisualColor => visual != null ? visual.color : Color.clear;
        public bool IsPortalBarrier =>
            remainBlockingWhenOpen || GetComponentInParent<RoomNode>() != null;
        public bool IsPortalBarrierActive =>
            IsPortalBarrier && blocker != null && blocker.enabled && !blocker.isTrigger;

        public const string PickupBoundaryName = "Pickup Door Boundary";

        private BoxCollider2D pickupBoundary;

        public BoxCollider2D PickupBoundary => pickupBoundary;

        private void Awake()
        {
            EnsureReferences();
            if (Application.isPlaying) EnsurePickupBoundary();
        }

        // Pushed floor hearts and chests stop at the closed door's inner edge even after the door opens, so they
        // never plug the doorway the player walks through. The boundary copies the inner blocker's shape on the
        // Pickup layer and ignores the Player layer, so only pickups and chest pushes meet it.
        public BoxCollider2D EnsurePickupBoundary()
        {
            if (pickupBoundary != null) return pickupBoundary;
            if (interiorBlocker is not BoxCollider2D source) return null;

            Transform existing = source.transform.Find(PickupBoundaryName);
            GameObject boundary = existing != null ? existing.gameObject : new GameObject(PickupBoundaryName);
            boundary.transform.SetParent(source.transform, false);
            boundary.transform.localPosition = Vector3.zero;
            boundary.transform.localRotation = Quaternion.identity;
            boundary.transform.localScale = Vector3.one;
            boundary.layer = LayerMask.NameToLayer(TrickalFanGame.Resource.HealthPickup.LayerName);

            pickupBoundary = boundary.GetComponent<BoxCollider2D>();
            if (pickupBoundary == null) pickupBoundary = boundary.AddComponent<BoxCollider2D>();
            pickupBoundary.size = source.size;
            pickupBoundary.offset = source.offset;
            pickupBoundary.edgeRadius = source.edgeRadius;
            pickupBoundary.isTrigger = false;
            pickupBoundary.excludeLayers = LayerMask.GetMask("Player");
            pickupBoundary.enabled = true;
            return pickupBoundary;
        }

        public void SetLocked(bool isLocked)
        {
            encounterLocked = isLocked;
            RefreshLockState();
        }

        public void SetKeyLocked(bool isLocked)
        {
            keyLocked = isLocked;
            RefreshLockState();
        }

        private void RefreshLockState()
        {
            EnsureReferences();
            bool wasLocked = IsLocked;
            IsLocked = (encounterLocked && !ignoresEncounterLock) || keyLocked;
            // Sound-0: only the moment it opens, not every refresh. Awake
            // runs this once with both flags false, which is why the sound
            // needs the door to have actually been locked beforehand.
            if (wasLocked && !IsLocked) CombatSfx.Play(CombatSfxCue.DoorOpen);
            blocker.enabled = IsLocked || IsPortalBarrier;
            blocker.isTrigger = false;
            if (interiorBlocker != null) interiorBlocker.enabled = IsLocked;

            if (visual != null)
            {
                visual.color = IsLocked ? lockedColor : openColor;
            }
        }

        // An opened hidden passage stays passable while its room's fight is running.
        public void ConfigureEncounterLock(bool shouldIgnoreEncounterLock)
        {
            ignoresEncounterLock = shouldIgnoreEncounterLock;
            RefreshLockState();
        }

        public void ConfigurePortalBarrier(bool shouldRemainBlockingWhenOpen)
        {
            EnsureReferences();
            remainBlockingWhenOpen = shouldRemainBlockingWhenOpen;
            blocker.isTrigger = false;
            blocker.enabled = IsLocked || IsPortalBarrier;
        }

        public void ConfigureInteriorBlocker(Collider2D configuredBlocker)
        {
            interiorBlocker = configuredBlocker;
            if (interiorBlocker != null) interiorBlocker.enabled = IsLocked;
        }

        public void ConfigureVisualKind(DoorVisualKind configuredKind)
        {
            EnsureReferences();
            visualKind = configuredKind;
            lockedColor = configuredKind switch
            {
                DoorVisualKind.Boss => BossLockedColor,
                DoorVisualKind.KeyLockedTreasure => KeyLockedColor,
                DoorVisualKind.Shop => KeyLockedColor,
                DoorVisualKind.SecretPassage => SecretLockedColor,
                _ => NormalLockedColor,
            };
            openColor = configuredKind switch
            {
                DoorVisualKind.Boss => BossOpenColor,
                DoorVisualKind.KeyLockedTreasure => KeyOpenColor,
                DoorVisualKind.Shop => KeyOpenColor,
                DoorVisualKind.SecretPassage => SecretOpenColor,
                _ => NormalOpenColor,
            };
            if (visual != null)
            {
                visual.color = IsLocked ? lockedColor : openColor;
            }
        }

        private void EnsureReferences()
        {
            if (blocker == null)
            {
                blocker = GetComponent<Collider2D>();
            }

            if (visual == null)
            {
                visual = GetComponent<SpriteRenderer>();
            }
        }
    }
}
