using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameDetailedStatsHudView : MonoBehaviour
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private TMP_Text valuesText;
        // The critical chance icon is the critical-number burst, which is drawn at runtime, so it is assigned here.
        [SerializeField] private Image criticalChanceIcon;

        public PlayerStats PlayerStats => playerStats;
        public CanvasGroup Content => content;
        public TMP_Text ValuesText => valuesText;
        public Image CriticalChanceIcon => criticalChanceIcon;

        public void Configure(PlayerStats configuredPlayerStats, CanvasGroup configuredContent, TMP_Text configuredValuesText)
        {
            playerStats = configuredPlayerStats;
            content = configuredContent;
            valuesText = configuredValuesText;
            RefreshNow();
        }

        public void ConfigureCriticalChanceIcon(Image configuredIcon)
        {
            criticalChanceIcon = configuredIcon;
            // The burst sprite exists only at runtime; saving it into the scene would leave a missing reference.
            if (Application.isPlaying) ApplyCriticalChanceIcon(criticalChanceIcon);
        }

        public static void ApplyCriticalChanceIcon(Image image)
        {
            if (image == null) return;
            image.sprite = CriticalBurstSprites.Get(0);
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private void OnEnable()
        {
            ApplyCriticalChanceIcon(criticalChanceIcon);
            LocalSettings.DetailedStatsHudChanged += RefreshNow;
            RefreshNow();
        }

        private void OnDisable()
        {
            LocalSettings.DetailedStatsHudChanged -= RefreshNow;
        }

        private void LateUpdate()
        {
            if (LocalSettings.DetailedStatsHud) RefreshValues();
        }

        public void RefreshNow()
        {
            bool visible = LocalSettings.DetailedStatsHud;
            if (content != null)
            {
                content.alpha = visible ? 1f : 0f;
                content.interactable = false;
                content.blocksRaycasts = false;
            }
            if (visible) RefreshValues();
        }

        private void RefreshValues()
        {
            if (valuesText == null) return;
            valuesText.text = playerStats == null
                ? "-\n-\n-\n-"
                : $"{playerStats.AttackDamage:0.##}\n" +
                  $"{playerStats.AttackSpeed:0.##}\n" +
                  $"{playerStats.MoveSpeed:0.##}\n" +
                  $"{playerStats.CriticalChance * 100f:0.#}%";
        }
    }
}
