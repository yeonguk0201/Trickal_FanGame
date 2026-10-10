using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum EnemyAttackPhase
    {
        Idle,
        Telegraph,
        Active,
        Recovery,
    }

    /// <summary>
    /// Shared non-color-only readability for enemy attacks. Controllers own timing and
    /// damage; this component owns the visible silhouette for each lifecycle phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttackPresentation : MonoBehaviour
    {
        [SerializeField] private Vector2 telegraphScale = Vector2.one;
        [SerializeField] private Vector2 activeScale = Vector2.one;
        [SerializeField] private Vector2 recoveryScale = Vector2.one;

        private Vector3 idleScale;

        public EnemyAttackPhase Phase { get; private set; }
        public Vector2 CurrentScaleMultiplier => Phase switch
        {
            EnemyAttackPhase.Telegraph => telegraphScale,
            EnemyAttackPhase.Active => activeScale,
            EnemyAttackPhase.Recovery => recoveryScale,
            _ => Vector2.one,
        };

        private void Awake()
        {
            idleScale = transform.localScale;
            SetPhase(EnemyAttackPhase.Idle);
        }

        private void OnDisable()
        {
            SetPhase(EnemyAttackPhase.Idle);
        }

        public void SetPhase(EnemyAttackPhase phase)
        {
            if (idleScale == Vector3.zero)
            {
                idleScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            }

            Phase = phase;
            Vector2 multiplier = CurrentScaleMultiplier;
            transform.localScale = new Vector3(
                idleScale.x * multiplier.x,
                idleScale.y * multiplier.y,
                idleScale.z);
        }
    }
}
