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
            if (!CanPlace || !runProgress.TrySpendResource(RunResourceType.Bomb)) return false;

            pendingBomb = Instantiate(bombPrefab, transform.position, Quaternion.identity);
            pendingBomb.name = "Placed Bomb";
            pendingBomb.Configure(gameObject, health, currentTime);
            pendingBomb.Exploded += OnBombExploded;
            return true;
        }

        private void OnBombExploded(PlacedBomb explodedBomb)
        {
            if (explodedBomb != null) explodedBomb.Exploded -= OnBombExploded;
            if (pendingBomb == explodedBomb) pendingBomb = null;
        }
    }
}
