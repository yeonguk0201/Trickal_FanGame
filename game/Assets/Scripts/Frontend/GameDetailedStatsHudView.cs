using TMPro;
using TrickalFanGame.Data;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameDetailedStatsHudView : MonoBehaviour
    {
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private TMP_Text valuesText;

        public PlayerStats PlayerStats => playerStats;
        public CanvasGroup Content => content;
        public TMP_Text ValuesText => valuesText;

        public void Configure(PlayerStats configuredPlayerStats, CanvasGroup configuredContent, TMP_Text configuredValuesText)
        {
            playerStats = configuredPlayerStats;
            content = configuredContent;
            valuesText = configuredValuesText;
            RefreshNow();
        }

        private void OnEnable()
        {
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
