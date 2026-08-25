using System;
using UnityEngine;

namespace TrickalFanGame.Player
{
    public enum PlayerActionPhase
    {
        Normal,
        UltimateDashing,
        UltimateImpactRecovery,
        UltimateCoastRecovery,
    }

    [DisallowMultipleComponent]
    public sealed class PlayerActionState : MonoBehaviour
    {
        public PlayerActionPhase Phase { get; private set; } = PlayerActionPhase.Normal;
        public Vector2 DashDirection { get; private set; } = Vector2.down;
        public float DashSpeedMultiplier { get; private set; } = 1f;

        public bool IsDashing => Phase == PlayerActionPhase.UltimateDashing;
        public bool IsImpactRecovering => Phase == PlayerActionPhase.UltimateImpactRecovery;
        public bool IsCoastRecovering => Phase == PlayerActionPhase.UltimateCoastRecovery;
        public bool IsRecovering => IsImpactRecovering || IsCoastRecovering;
        public bool CanMove => Phase == PlayerActionPhase.Normal || IsDashing || IsCoastRecovering;
        public bool CanBasicAttack => Phase == PlayerActionPhase.Normal;
        public bool CanUseLowerGradeSkill => Phase == PlayerActionPhase.Normal;
        public bool CanStartUltimate => Phase == PlayerActionPhase.Normal;
        public bool CanTransition => Phase == PlayerActionPhase.Normal;

        public event Action<PlayerActionPhase> Changed;

        public bool TryBeginUltimate(Vector2 initialDirection, float speedMultiplier)
        {
            if (!CanStartUltimate)
            {
                return false;
            }

            DashDirection = initialDirection.sqrMagnitude > 0.001f
                ? initialDirection.normalized
                : Vector2.down;
            DashSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
            SetPhase(PlayerActionPhase.UltimateDashing);
            return true;
        }

        public bool TryUpdateDashDirection(Vector2 direction)
        {
            if (!IsDashing || direction.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            DashDirection = direction.normalized;
            return true;
        }

        public bool TryBeginImpactRecovery()
        {
            if (!IsDashing)
            {
                return false;
            }

            DashSpeedMultiplier = 0f;
            SetPhase(PlayerActionPhase.UltimateImpactRecovery);
            return true;
        }

        public bool TryBeginCoastRecovery()
        {
            if (!IsDashing)
            {
                return false;
            }

            SetPhase(PlayerActionPhase.UltimateCoastRecovery);
            return true;
        }

        public bool TryUpdateCoastSpeed(float speedMultiplier)
        {
            if (!IsCoastRecovering)
            {
                return false;
            }

            DashSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
            return true;
        }

        public bool TryCompleteRecovery()
        {
            if (!IsRecovering)
            {
                return false;
            }

            DashSpeedMultiplier = 1f;
            SetPhase(PlayerActionPhase.Normal);
            return true;
        }

        public void ForceNormal()
        {
            DashSpeedMultiplier = 1f;
            SetPhase(PlayerActionPhase.Normal);
        }

        private void SetPhase(PlayerActionPhase nextPhase)
        {
            if (Phase == nextPhase)
            {
                return;
            }

            Phase = nextPhase;
            Changed?.Invoke(Phase);
        }
    }
}
