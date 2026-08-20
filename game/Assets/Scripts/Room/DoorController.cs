using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private Collider2D blocker;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Color lockedColor = new(0.75f, 0.2f, 0.2f);
        [SerializeField] private Color openColor = new(0.2f, 0.75f, 0.3f);

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            EnsureReferences();
        }

        public void SetLocked(bool isLocked)
        {
            EnsureReferences();
            IsLocked = isLocked;
            blocker.enabled = isLocked;

            if (visual != null)
            {
                visual.color = isLocked ? lockedColor : openColor;
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
