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

        private void Awake()
        {
            EnsureReferences();
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
            IsLocked = (encounterLocked && !ignoresEncounterLock) || keyLocked;
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
