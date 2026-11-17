using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrickalFanGame.Item
{
    // The reward altar is a solid prop the player walks around. While the player's feet are behind (above) the
    // middle of its floor footprint, the altar and its gift draw over the player; in front, under the player.
    // Presentation only: it never moves anything or changes the footprint collider.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class RewardAltarDepth : MonoBehaviour
    {
        // Above the walking player's body, below a flying player's (BodySortingOrder + FlyingSortingOrderBoost) and
        // pickups (AboveBodySortingOrder).
        public const int BehindPlayerSortingOrder = PlayerMovement.BodySortingOrder + 1;
        private const float PlayerSearchInterval = 0.5f;

        [SerializeField] private Collider2D footprint;
        [SerializeField] private int frontSortingOrder = 1;

        private SortingGroup group;
        private PlayerMovement player;
        private float nextPlayerSearch;

        public Collider2D Footprint => footprint;
        public int FrontSortingOrder => frontSortingOrder;

        public void Configure(Collider2D configuredFootprint, int configuredFrontSortingOrder)
        {
            footprint = configuredFootprint;
            frontSortingOrder = configuredFrontSortingOrder;
            group = GetComponent<SortingGroup>();
            group.sortingOrder = frontSortingOrder;
        }

        // True when feet at this height stand behind the altar.
        public bool IsBehind(Vector2 feet) => footprint != null && feet.y > footprint.bounds.center.y;

        private void Awake() => group = GetComponent<SortingGroup>();

        private void OnDisable()
        {
            if (group != null) group.sortingOrder = frontSortingOrder;
        }

        private void LateUpdate()
        {
            if (player == null && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + PlayerSearchInterval;
                player = FindFirstObjectByType<PlayerMovement>();
            }
            bool behind = player != null && player.gameObject.activeInHierarchy && IsBehind(player.FeetPosition);
            group.sortingOrder = behind ? BehindPlayerSortingOrder : frontSortingOrder;
        }
    }
}
