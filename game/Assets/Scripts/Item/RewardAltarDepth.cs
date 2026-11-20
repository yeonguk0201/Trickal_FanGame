using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrickalFanGame.Item
{
    // The reward altar is a solid prop the player walks around. While the player's feet are behind (above) the
    // middle of its floor footprint, the altar and its gift draw over the player; in front, under the player.
    // Presentation only: it never moves anything or changes the footprint collider. Flight passes over the footprint
    // only once the altar is empty (BlocksFlight); with its reward still on it, a flying player stops like a walking one.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class RewardAltarDepth : MonoBehaviour
    {
        // Above the walking player's body, below a flying player's (BodySortingOrder + FlyingSortingOrderBoost) and
        // pickups (AboveBodySortingOrder).
        public const int BehindPlayerSortingOrder = PlayerMovement.BodySortingOrder + 1;
        // An altar still holding its reward also covers a flying player behind it (flight stops there too). An emptied
        // altar stays under a flying player, who passes over it.
        public const int BehindFlyingPlayerSortingOrder =
            PlayerMovement.BodySortingOrder + PlayerFlight.FlyingSortingOrderBoost + 1;
        private const float PlayerSearchInterval = 0.5f;

        [SerializeField] private Collider2D footprint;
        [SerializeField] private int frontSortingOrder = 1;

        private SortingGroup group;
        private RewardRoom reward;
        private PlayerMovement player;
        private PlayerFlight playerFlight;
        private float nextPlayerSearch;

        public Collider2D Footprint => footprint;
        public int FrontSortingOrder => frontSortingOrder;

        public bool BlocksFlight
        {
            get
            {
                if (reward == null) reward = GetComponentInParent<RewardRoom>(true);
                return reward == null || !reward.HasRewarded;
            }
        }

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
                playerFlight = null;
            }
            // PlayerFlight is added only when the wings are taken, after the player was found.
            if (playerFlight == null && player != null) playerFlight = player.GetComponent<PlayerFlight>();
            bool behind = player != null && player.gameObject.activeInHierarchy && IsBehind(player.FeetPosition);
            bool flying = playerFlight != null && playerFlight.IsFlying;
            group.sortingOrder = SortingOrderFor(behind, flying, BlocksFlight, frontSortingOrder);
        }

        public static int SortingOrderFor(bool playerBehind, bool playerFlying, bool blocksFlight, int frontOrder)
        {
            if (!playerBehind) return frontOrder;
            if (!playerFlying) return BehindPlayerSortingOrder;
            return blocksFlight ? BehindFlyingPlayerSortingOrder : frontOrder;
        }
    }
}
