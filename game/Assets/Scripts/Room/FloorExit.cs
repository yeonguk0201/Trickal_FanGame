using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FloorExit : MonoBehaviour
    {
        [SerializeField] private BossController unlockBoss;
        [SerializeField] private Transform destination;
        [SerializeField, Min(1)] private int destinationFloor = 2;
        [SerializeField] private RunProgress runProgress;
        [SerializeField] private SpriteRenderer indicator;

        private Collider2D exitTrigger;
        public bool IsUnlocked { get; private set; }

        public void Configure(
            BossController configuredUnlockBoss,
            Transform configuredDestination,
            int configuredDestinationFloor,
            RunProgress configuredRunProgress,
            SpriteRenderer configuredIndicator)
        {
            unlockBoss = configuredUnlockBoss;
            destination = configuredDestination;
            destinationFloor = Mathf.Max(1, configuredDestinationFloor);
            runProgress = configuredRunProgress;
            indicator = configuredIndicator;
        }

        private void Awake()
        {
            exitTrigger = GetComponent<Collider2D>();
            exitTrigger.isTrigger = true;
            SetUnlocked(false);

            if (unlockBoss != null)
            {
                unlockBoss.Died += OnBossDied;
            }
        }

        private void OnDestroy()
        {
            if (unlockBoss != null)
            {
                unlockBoss.Died -= OnBossDied;
            }
        }

        private void OnBossDied()
        {
            SetUnlocked(true);
            Debug.Log($"[FloorExit] Floor {destinationFloor} exit unlocked.", this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsUnlocked || destination == null)
            {
                return;
            }

            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                TryEnter(player);
            }
        }

        public bool TryEnter(PlayerMovement player)
        {
            if (!IsUnlocked || destination == null || player == null ||
                runProgress?.IsRewardSelectionPending == true ||
                player.GetComponent<PlayerActionState>()?.CanTransition == false)
            {
                return false;
            }

            // Hitbox-1: the destination is where the player stands; a larger body's root sits above its feet.
            Vector2 rootPosition = player.RootPositionForStanding(destination.position);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = rootPosition;
            }
            else
            {
                player.transform.position = rootPosition;
            }

            runProgress?.RecordRoomEntry(destinationFloor, 1);
            Debug.Log($"[FloorExit] Entered floor {destinationFloor}.", this);
            return true;
        }

        private void SetUnlocked(bool unlocked)
        {
            IsUnlocked = unlocked;
            if (exitTrigger != null)
            {
                exitTrigger.enabled = unlocked;
            }

            if (indicator != null)
            {
                indicator.color = unlocked
                    ? new Color(0.25f, 0.9f, 1f, 0.9f)
                    : new Color(0.18f, 0.25f, 0.3f, 0.35f);
            }
        }
    }
}
