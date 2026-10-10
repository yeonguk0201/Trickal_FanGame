using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Resource
{
    // A pushable floor pickup for gold, keys or bombs. It is collected when the Run can still hold more of its
    // resource; at the 99 cap it stays on the floor like a heart that does not fit.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class RunResourcePickup : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        [SerializeField] private RunResourceType resourceType;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private RunProgress runProgress;

        private bool isCollected;

        public RunResourceType ResourceType => resourceType;
        public int Amount => Mathf.Max(1, amount);
        public bool IsCollected => isCollected;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
        }

        public void BindRunProgress(RunProgress progress)
        {
            runProgress = progress;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryCollectFrom(collision.collider);
        }

        // Stay lets a capped pickup the player is pressing against be collected once the resource is spent.
        private void OnCollisionStay2D(Collision2D collision)
        {
            TryCollectFrom(collision.collider);
        }

        public bool TryCollectFrom(Collider2D other)
        {
            PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
            if (player == null)
            {
                return false;
            }

            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }

            return Collect(runProgress);
        }

        public bool CanCollect(RunProgress progress)
        {
            return !isCollected && progress != null && progress.CanAcceptResource(resourceType);
        }

        public bool Collect(RunProgress progress)
        {
            if (!CanCollect(progress) || progress.TryAddResource(resourceType, Amount) <= 0)
            {
                return false;
            }

            isCollected = true;
            DestroyPickup();
            return true;
        }

        private void DestroyPickup()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
