using UnityEngine;

namespace TrickalFanGame.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SPPickup : MonoBehaviour
    {
        private bool isCollected;

        public bool IsCollected => isCollected;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            TrickalFanGame.Frontend.UserArtwork.ApplyResourcePickup(GetComponentInChildren<SpriteRenderer>(), "sp-pickup");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerSP playerSP = other.GetComponentInParent<PlayerSP>();
            if (playerSP != null)
            {
                Collect(playerSP);
            }
        }

        public bool Collect(PlayerSP playerSP)
        {
            if (isCollected || playerSP == null)
            {
                return false;
            }

            isCollected = true;
            playerSP.TryAdd();
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
