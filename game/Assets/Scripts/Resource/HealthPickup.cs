using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Resource
{
    // A pushable floor heart. It is collected only when its whole heal amount fits under the player's
    // maximum HP and the 15-heart health + shield limit; otherwise it stays on the floor as a physical body the
    // player can push around.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class HealthPickup : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        public const string LayerName = "Pickup";
        public const int DefaultHealUnits = HealthUnits.UnitsPerHeart;

        private const float MissingHealthTolerance = 0.0001f;

        [SerializeField, Min(1)] private int healUnits = DefaultHealUnits;

        private bool isCollected;

        public int HealUnits => Mathf.Max(1, healUnits);
        public bool IsCollected => isCollected;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = false;
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            TrickalFanGame.Frontend.UserArtwork.ApplyResourcePickup(GetComponentInChildren<SpriteRenderer>(), "hp-pickup");
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryCollectFrom(collision.collider);
        }

        // Stay keeps a heart the player is already pressing against collectible once damage makes room for it.
        private void OnCollisionStay2D(Collision2D collision)
        {
            TryCollectFrom(collision.collider);
        }

        public bool TryCollectFrom(Collider2D other)
        {
            PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
            return player != null && Collect(player.GetComponent<Health>());
        }

        public bool CanCollect(Health playerHealth)
        {
            return !isCollected &&
                   playerHealth != null &&
                   !playerHealth.IsDead &&
                   playerHealth.MissingHealth + MissingHealthTolerance >= HealUnits;
        }

        public bool Collect(Health playerHealth)
        {
            if (!CanCollect(playerHealth))
            {
                return false;
            }

            isCollected = true;
            playerHealth.Heal(HealUnits);
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
