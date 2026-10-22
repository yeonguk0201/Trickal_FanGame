using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Resource
{
    // A pushable floor pickup for gold, keys, bombs or the permanent elif. It is collected when the Run can still hold
    // more of its resource; at the 99 cap it stays on the floor like a heart that does not fit. Elif has no Run cap.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class RunResourcePickup : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        public const string GoldArtworkKey = "gold-pickup";

        [SerializeField] private RunResourceType resourceType;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private RunProgress runProgress;

        private bool isCollected;

        public RunResourceType ResourceType => resourceType;
        public int Amount => Mathf.Max(1, amount);
        public bool IsCollected => isCollected;
        // Development panel elif: usable in this Run but never saved as the Run's gain (contract 27 §7.2).
        public bool IsDevelopmentGrant { get; private set; }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            ApplyArtwork();
        }

        // Keys and bombs keep the tinted placeholder circle until their artwork is supplied.
        private void ApplyArtwork()
        {
            string key = resourceType switch
            {
                RunResourceType.Gold => GoldArtworkKey,
                RunResourceType.Elif => PermanentElif.ArtworkKey,
                _ => null,
            };
            if (key != null)
                TrickalFanGame.Frontend.UserArtwork.ApplyResourcePickup(GetComponentInChildren<SpriteRenderer>(), key);
        }

        // Elif-2: a just spawned gold pickup becomes one permanent elif instead of gold (contract 27 §3.1).
        public void ConvertToPermanentElif(bool developmentGrant = false)
        {
            if (isCollected || resourceType == RunResourceType.Elif) return;
            resourceType = RunResourceType.Elif;
            amount = 1;
            IsDevelopmentGrant = developmentGrant;
            ApplyArtwork();
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
            if (!CanCollect(progress))
            {
                return false;
            }

            if (resourceType == RunResourceType.Elif && IsDevelopmentGrant)
            {
                progress.AddDevelopmentElif(Amount);
            }
            else if (progress.TryAddResource(resourceType, Amount) <= 0)
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
