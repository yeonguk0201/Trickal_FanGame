using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private Collider2D blocker;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private bool remainBlockingWhenOpen;
        [SerializeField] private Color lockedColor = new(0.75f, 0.2f, 0.2f);
        [SerializeField] private Color openColor = new(0.2f, 0.75f, 0.3f);

        public bool IsLocked { get; private set; }
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
