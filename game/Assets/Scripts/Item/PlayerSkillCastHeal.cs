using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 네르의 엘드르 깃발: every N successful lower-grade skill casts heal the player. The count advances at full
    // health too and that heal is dropped, like 코미의 베개.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerSkillCastHeal : MonoBehaviour
    {
        private Health health;
        private PlayerSkill skill;
        private float healAmount;

        public int RequiredCastCount { get; private set; }
        public int CastProgress { get; private set; }
        public bool IsConfigured => RequiredCastCount > 0 && healAmount > 0f;

        private void OnEnable()
        {
            BindSkill();
        }

        private void OnDisable()
        {
            if (skill != null) skill.SalvoStarted -= HandleCast;
        }

        // The first stack sets the cast count and heal amount; each later stack lowers the count by one cast.
        public void AddStack(float configuredHealAmount, int configuredCastCount)
        {
            if (configuredHealAmount <= 0f || configuredCastCount <= 0)
            {
                return;
            }

            if (IsConfigured)
            {
                RequiredCastCount = Mathf.Max(1, RequiredCastCount - 1);
            }
            else
            {
                healAmount = configuredHealAmount;
                RequiredCastCount = configuredCastCount;
            }

            BindSkill();
        }

        // Returns the health restored by this cast.
        public float RegisterCast()
        {
            if (!IsConfigured)
            {
                return 0f;
            }

            CastProgress++;
            if (CastProgress < RequiredCastCount)
            {
                return 0f;
            }

            CastProgress = 0;
            if (health == null) health = GetComponent<Health>();
            return health.Heal(healAmount);
        }

        private void HandleCast()
        {
            RegisterCast();
        }

        private void BindSkill()
        {
            if (skill == null) skill = GetComponent<PlayerSkill>();
            if (skill == null) return;
            skill.SalvoStarted -= HandleCast;
            skill.SalvoStarted += HandleCast;
        }
    }
}
