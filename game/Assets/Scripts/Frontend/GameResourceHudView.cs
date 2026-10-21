using System;
using TMPro;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    // UX-1: gold, keys and bombs under the minimap. Counts are two digits like 07 (the Run cap is 99) and pop
    // briefly when they change.
    [DisallowMultipleComponent]
    public sealed class GameResourceHudView : MonoBehaviour
    {
        public static readonly RunResourceType[] DisplayOrder =
            { RunResourceType.Gold, RunResourceType.Key, RunResourceType.Bomb };
        public const string AllFixedText = "0123456789골드열쇠폭탄";

        private const float PulseScale = 1.25f;
        private const float PulseDuration = 0.25f;

        [SerializeField] private RunProgress runProgress;
        [SerializeField] private Image[] icons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[] countTexts = Array.Empty<TMP_Text>();

        private readonly float[] pulseUntil = new float[3];
        private bool subscribed;

        public RunProgress Progress => runProgress;
        public int EntryCount => Mathf.Min(icons.Length, countTexts.Length);
        public Image GetIcon(int index) => icons[index];
        public TMP_Text GetCountText(int index) => countTexts[index];

        public void Configure(RunProgress configuredProgress, Image[] configuredIcons, TMP_Text[] configuredCounts)
        {
            Unsubscribe();
            runProgress = configuredProgress;
            icons = configuredIcons ?? Array.Empty<Image>();
            countTexts = configuredCounts ?? Array.Empty<TMP_Text>();
            if (isActiveAndEnabled)
            {
                Subscribe();
                RefreshNow();
            }
        }

        public static string FormatCount(int count) => Mathf.Clamp(count, 0, RunResourceWallet.MaxCount).ToString("00");

        public static string LabelOf(RunResourceType type) => type switch
        {
            RunResourceType.Gold => "골드",
            RunResourceType.Key => "열쇠",
            RunResourceType.Bomb => "폭탄",
            _ => string.Empty,
        };

        public string GetShownText(RunResourceType type)
        {
            int index = Array.IndexOf(DisplayOrder, type);
            return index >= 0 && index < countTexts.Length && countTexts[index] != null
                ? countTexts[index].text
                : string.Empty;
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshNow();
        }

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int index = 0; index < EntryCount; index++)
            {
                if (pulseUntil[index] <= 0f || now < pulseUntil[index]) continue;
                pulseUntil[index] = 0f;
                if (icons[index] != null) icons[index].rectTransform.localScale = Vector3.one;
            }
        }

        public void RefreshNow()
        {
            for (int index = 0; index < DisplayOrder.Length && index < countTexts.Length; index++)
            {
                if (countTexts[index] == null) continue;
                int count = runProgress != null ? runProgress.GetResourceCount(DisplayOrder[index]) : 0;
                countTexts[index].text = FormatCount(count);
            }
        }

        private void Subscribe()
        {
            if (subscribed || runProgress == null) return;
            runProgress.ResourceChanged += HandleResourceChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (runProgress != null) runProgress.ResourceChanged -= HandleResourceChanged;
            subscribed = false;
        }

        private void HandleResourceChanged(RunResourceType type, int count)
        {
            int index = Array.IndexOf(DisplayOrder, type);
            if (index < 0 || index >= countTexts.Length) return;
            if (countTexts[index] != null) countTexts[index].text = FormatCount(count);
            if (index < icons.Length && icons[index] != null && Application.isPlaying)
            {
                icons[index].rectTransform.localScale = Vector3.one * PulseScale;
                pulseUntil[index] = Time.unscaledTime + PulseDuration;
            }
        }
    }
}
