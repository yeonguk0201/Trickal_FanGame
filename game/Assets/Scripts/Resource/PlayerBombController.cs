using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrickalFanGame.Resource
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerActionState))]
    public sealed class PlayerBombController : MonoBehaviour
    {
        public const float BombPlacementClearance = 0.25f;
        public const float BombPlacementSearchDistance = 2.5f;

        [SerializeField] private RunProgress runProgress;
        [SerializeField] private PlacedBomb bombPrefab;

        private Health health;
        private PlayerActionState actionState;
        private PlacedBomb pendingBomb;

        public RunProgress Progress => runProgress;
        public PlacedBomb BombPrefab => bombPrefab;
        public PlacedBomb PendingBomb => pendingBomb;
        public bool CanPlace => health != null && !health.IsDead && actionState != null &&
                                actionState.CanBasicAttack && runProgress != null &&
                                !runProgress.IsProgressionStopped && bombPrefab != null && pendingBomb == null &&
                                Time.timeScale > 0f && runProgress.GetResourceCount(RunResourceType.Bomb) > 0;

        private void Awake()
        {
            health = GetComponent<Health>();
            actionState = GetComponent<PlayerActionState>();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Keyboard.current?.fKey.wasPressedThisFrame != true) return;
            TryPlaceBomb(Time.time);
        }

        public void Configure(RunProgress configuredProgress, PlacedBomb configuredBombPrefab)
        {
            runProgress = configuredProgress;
            bombPrefab = configuredBombPrefab;
            if (health == null) Awake();
        }

        public bool TryPlaceBomb(float currentTime)
        {
            // Hitbox-1: a bomb lands where the player stands, not at the raised center of a larger body.
            Vector2 standing = TryGetComponent(out PlayerMovement movement)
                ? movement.StandingPosition
                : (Vector2)transform.position;
            if (!CanPlace || !TryResolvePlacement(standing, out Vector2 position) ||
                !runProgress.TrySpendResource(RunResourceType.Bomb))
            {
                return false;
            }

            pendingBomb = Instantiate(bombPrefab, position, Quaternion.identity);
            pendingBomb.name = "Placed Bomb";
            pendingBomb.Configure(gameObject, health, currentTime);
            pendingBomb.Exploded += OnBombExploded;
            return true;
        }

        // Flight-0 (D3): only a flying player can stand over a pit or obstacle. Over a pit the bomb cannot be placed and
        // is not spent; over an obstacle it lands on the nearest walkable point instead.
        public static bool TryResolvePlacement(Vector2 playerPosition, out Vector2 position)
        {
            position = playerPosition;
            if (RoomMovementClass.IsOverPit(playerPosition)) return false;
            if (!RoomMovementClass.IsOverLowObstacle(playerPosition)) return true;
            return RoomMovementClass.TryFindNearestWalkablePoint(playerPosition, BombPlacementClearance,
                BombPlacementSearchDistance, out position);
        }

        private void OnBombExploded(PlacedBomb explodedBomb)
        {
            if (explodedBomb != null) explodedBomb.Exploded -= OnBombExploded;
            if (pendingBomb == explodedBomb) pendingBomb = null;
        }
    }
}
