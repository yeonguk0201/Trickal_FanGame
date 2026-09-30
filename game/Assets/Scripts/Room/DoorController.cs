using UnityEngine;

namespace TrickalFanGame.Room
{
    public enum DoorVisualKind
    {
        Normal,
        Boss,
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class DoorController : MonoBehaviour
    {
        public static readonly Color NormalLockedColor = new(0.12f, 0.42f, 0.18f);
        public static readonly Color NormalOpenColor = new(0.2f, 0.75f, 0.3f);
        public static readonly Color BossLockedColor = new(0.34f, 0.12f, 0.5f);
        public static readonly Color BossOpenColor = new(0.64f, 0.28f, 0.84f);

        [SerializeField] private Collider2D blocker;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private bool remainBlockingWhenOpen;
        [SerializeField] private DoorVisualKind visualKind;
        [SerializeField] private Color lockedColor = new(0.12f, 0.42f, 0.18f);
        [SerializeField] private Color openColor = new(0.2f, 0.75f, 0.3f);

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
            EnsureReferences();
            IsLocked = isLocked;
            blocker.enabled = isLocked || IsPortalBarrier;
            blocker.isTrigger = false;

            if (visual != null)
            {
                visual.color = isLocked ? lockedColor : openColor;
            }
        }

        public void ConfigurePortalBarrier(bool shouldRemainBlockingWhenOpen)
        {
            EnsureReferences();
            remainBlockingWhenOpen = shouldRemainBlockingWhenOpen;
            blocker.isTrigger = false;
            blocker.enabled = IsLocked || IsPortalBarrier;
        }

        public void ConfigureVisualKind(DoorVisualKind configuredKind)
        {
            EnsureReferences();
            visualKind = configuredKind;
            lockedColor = configuredKind == DoorVisualKind.Boss ? BossLockedColor : NormalLockedColor;
            openColor = configuredKind == DoorVisualKind.Boss ? BossOpenColor : NormalOpenColor;
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
