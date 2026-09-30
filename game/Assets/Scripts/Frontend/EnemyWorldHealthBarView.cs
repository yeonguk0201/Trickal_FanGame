using TrickalFanGame.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class EnemyWorldHealthBarView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image healthFill;

        private Health observedHealth;

        public Health ObservedHealth => observedHealth;
        public CanvasGroup CanvasGroup => canvasGroup;
        public Image HealthFill => healthFill;
        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.99f;

        public void Configure(CanvasGroup configuredCanvasGroup, Image configuredHealthFill)
        {
            Unbind();
            canvasGroup = configuredCanvasGroup;
            healthFill = configuredHealthFill;
            SetVisible(false);
        }

        public void Bind(Health health)
        {
            if (observedHealth == health)
            {
                Refresh();
                return;
            }

            Unbind();
            observedHealth = health;
            if (observedHealth != null)
            {
                observedHealth.Changed += OnHealthChanged;
                observedHealth.Died += OnDied;
                transform.SetParent(observedHealth.transform, false);
                transform.localPosition = new Vector3(0f, 0.7f, 0f);
                transform.localRotation = Quaternion.identity;
            }
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            Refresh(currentHealth, maxHealth);
        }

        private void OnDied()
        {
            SetVisible(false);
        }

        private void Refresh()
        {
            if (observedHealth == null)
            {
                SetVisible(false);
                return;
            }
            Refresh(observedHealth.CurrentHealth, observedHealth.MaxHealth);
        }

        private void Refresh(float currentHealth, float maxHealth)
        {
            if (healthFill != null)
                healthFill.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            SetVisible(currentHealth > 0f && currentHealth < maxHealth);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Unbind()
        {
            if (observedHealth != null)
            {
                observedHealth.Changed -= OnHealthChanged;
                observedHealth.Died -= OnDied;
            }
            observedHealth = null;
        }
    }
}
