using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class GameHudView : MonoBehaviour
    {
        private static readonly Color HpNormalColor = new(0.24f, 0.78f, 0.4f, 1f);
        public static readonly Color HeartHealthColor = new(0.93f, 0.27f, 0.33f, 1f);
        public static readonly Color HeartShieldColor = new(0.45f, 0.78f, 1f, 1f);
        public static readonly Color HeartEmptyColor = new(0.2f, 0.22f, 0.27f, 0.9f);
        private const string HeartBackgroundName = "Background";
        private const string HeartFillName = "Fill";
        private const float HeartPulseScale = 1.2f;
        private static readonly Color HpDamageColor = new(1f, 0.24f, 0.2f, 0.8f);
        private static readonly Color HpHealColor = new(0.35f, 1f, 0.62f, 0.8f);
        private static readonly Color SpActiveColor = new(0.3f, 0.78f, 1f, 1f);
        private static readonly Color SpInactiveColor = new(0.18f, 0.28f, 0.38f, 0.55f);
        private static readonly Color SpHalfColor = new(0.24f, 0.53f, 0.69f, 0.8f);
        private static readonly Color SpOverchargeColor = new(1f, 0.82f, 0.32f, 1f);
        private static readonly Color SkillReadyColor = new(0.3f, 0.88f, 0.72f, 1f);
        private static readonly Color SkillUnavailableColor = new(0.25f, 0.3f, 0.36f, 0.7f);

        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerSP playerSP;
        [SerializeField] private PlayerSkill lowerGradeSkill;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private Image healthFeedback;
        [SerializeField] private RectTransform spSlotsRoot;
        [SerializeField] private Image spSlotTemplate;
        [SerializeField] private TMP_Text spValueText;
        [SerializeField] private Image lowerGradeSkillState;
        [SerializeField] private TMP_Text lowerGradeSkillText;
        [SerializeField] private PlayerUltimate highGradeSkill;
        [SerializeField] private Image lowerGradeCooldownFill;
        [SerializeField] private Image highGradeSkillState;
        [SerializeField] private Image highGradeCooldownFill;
        [SerializeField] private TMP_Text highGradeCooldownText;
        [SerializeField] private TMP_Text highGradeSkillText;
        [SerializeField] private RectTransform heartRowRoot;
        [SerializeField] private RectTransform heartTemplate;
        [SerializeField, Min(4f)] private float maximumHeartSize = 36f;
        [SerializeField, Min(0f)] private float heartSpacing = 4f;
        [SerializeField, Min(0.05f)] private float feedbackDuration = 0.28f;

        private readonly List<Image> spSlots = new();
        private readonly List<HeartIcon> hearts = new();
        private float previousHealth;
        private int previousSP;
        private bool previousHalfSP;
        private float healthFeedbackUntil;
        private float heartPulseUntil;
        private float spFeedbackUntil;
        private bool subscribed;

        public Health PlayerHealth => playerHealth;
        public PlayerSP PlayerSP => playerSP;
        public PlayerSkill LowerGradeSkill => lowerGradeSkill;
        public Image HpFill => hpFill;
        public TMP_Text HpValueText => hpValueText;
        public Image HealthFeedback => healthFeedback;
        public RectTransform SpSlotsRoot => spSlotsRoot;
        public Image SpSlotTemplate => spSlotTemplate;
        public TMP_Text SpValueText => spValueText;
        public Image LowerGradeSkillState => lowerGradeSkillState;
        public TMP_Text LowerGradeSkillText => lowerGradeSkillText;
        public PlayerUltimate HighGradeSkill => highGradeSkill;
        public Image LowerGradeCooldownFill => lowerGradeCooldownFill;
        public Image HighGradeSkillState => highGradeSkillState;
        public Image HighGradeCooldownFill => highGradeCooldownFill;
        public TMP_Text HighGradeCooldownText => highGradeCooldownText;
        public TMP_Text HighGradeSkillText => highGradeSkillText;
        public RectTransform HeartRowRoot => heartRowRoot;
        public RectTransform HeartTemplate => heartTemplate;
        public int HeartCount => hearts.Count;
        public int HealthHeartCount { get; private set; }
        public int ShieldHeartCount { get; private set; }
        public float HeartSize { get; private set; }
        public int PulsingHeartCount { get; private set; }
        public int SlotCount => spSlots.Count;
        public int ActiveSlotCount { get; private set; }
        public bool IsShowingHalfSlot { get; private set; }
        public int OverchargeSlotCount { get; private set; }
        public bool IsLowerGradeSkillAvailable { get; private set; }
        public bool IsHighGradeSkillAvailable { get; private set; }
        public float HighGradeCooldownRemaining { get; private set; }
        public float LastHealthDelta { get; private set; }
        public int LastSPDelta { get; private set; }

        public void Configure(Health health, PlayerSP sp, PlayerSkill skill, Image configuredHpFill,
            TMP_Text configuredHpValueText, Image configuredHealthFeedback, RectTransform configuredSpSlotsRoot,
            Image configuredSpSlotTemplate, TMP_Text configuredSpValueText, Image configuredSkillState,
            TMP_Text configuredSkillText)
        {
            Unsubscribe();
            playerHealth = health;
            playerSP = sp;
            lowerGradeSkill = skill;
            hpFill = configuredHpFill;
            hpValueText = configuredHpValueText;
            healthFeedback = configuredHealthFeedback;
            spSlotsRoot = configuredSpSlotsRoot;
            spSlotTemplate = configuredSpSlotTemplate;
            spValueText = configuredSpValueText;
            lowerGradeSkillState = configuredSkillState;
            lowerGradeSkillText = configuredSkillText;
            if (isActiveAndEnabled)
            {
                Subscribe();
                if (Application.isPlaying) RefreshNow();
            }
        }

        public void ConfigureHearts(RectTransform configuredHeartRowRoot, RectTransform configuredHeartTemplate)
        {
            heartRowRoot = configuredHeartRowRoot;
            heartTemplate = configuredHeartTemplate;
            if (isActiveAndEnabled && Application.isPlaying) RefreshNow();
        }

        public float GetHeartFill(int index) => hearts[index].Fill.fillAmount;

        public bool IsShieldHeart(int index) => index >= HealthHeartCount && index < hearts.Count;

        public bool IsHeartPulsing(int index) => hearts[index].Root.localScale.x > 1.001f;

        public void ConfigureSkills(PlayerUltimate ultimate, Image configuredLowerCooldownFill,
            Image configuredHighState, Image configuredHighCooldownFill, TMP_Text configuredHighCooldownText,
            TMP_Text configuredHighSkillText)
        {
            highGradeSkill = ultimate;
            lowerGradeCooldownFill = configuredLowerCooldownFill;
            highGradeSkillState = configuredHighState;
            highGradeCooldownFill = configuredHighCooldownFill;
            highGradeCooldownText = configuredHighCooldownText;
            highGradeSkillText = configuredHighSkillText;
            if (isActiveAndEnabled && Application.isPlaying) RefreshSkillStateAt(Time.time);
        }

        private void OnEnable()
        {
            Subscribe();
            if (Application.isPlaying) RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            RefreshSkillStateAt(Time.time);
            float now = Time.unscaledTime;
            if (healthFeedback != null && healthFeedback.gameObject.activeSelf && now >= healthFeedbackUntil)
            {
                healthFeedback.gameObject.SetActive(false);
            }

            if (PulsingHeartCount > 0 && now >= heartPulseUntil) ResetHeartPulse();

            if (now >= spFeedbackUntil)
            {
                SetSlotScale(Vector3.one);
            }
        }

        public void RefreshNow()
        {
            if (playerHealth != null)
            {
                previousHealth = playerHealth.CurrentHealth;
                RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth, false);
            }

            if (playerSP != null)
            {
                previousSP = playerSP.CurrentSP;
                previousHalfSP = playerSP.HasHalfSP;
                RefreshSP(playerSP.CurrentSP, playerSP.MaxSP, false);
            }

            RefreshSkillStateAt(Time.time);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (playerHealth != null)
            {
                playerHealth.Changed += OnHealthChanged;
                playerHealth.ShieldChanged += OnShieldChanged;
            }
            if (playerSP != null) playerSP.Changed += OnSPChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (playerHealth != null)
            {
                playerHealth.Changed -= OnHealthChanged;
                playerHealth.ShieldChanged -= OnShieldChanged;
            }
            if (playerSP != null) playerSP.Changed -= OnSPChanged;
            subscribed = false;
        }

        private void OnHealthChanged(float current, float maximum)
        {
            LastHealthDelta = current - previousHealth;
            previousHealth = current;
            RefreshHealth(current, maximum, true);
        }

        private void OnShieldChanged(float shield)
        {
            if (playerHealth != null)
                RefreshHearts(playerHealth.CurrentHealth, playerHealth.MaxHealth, shield, true);
        }

        private void RefreshHealth(float current, float maximum, bool showFeedback)
        {
            RefreshHearts(current, maximum, playerHealth != null ? playerHealth.CurrentShield : 0f, showFeedback);
            float safeMaximum = Mathf.Max(0.1f, maximum);
            if (hpFill != null)
            {
                hpFill.fillAmount = Mathf.Clamp01(current / safeMaximum);
                hpFill.color = HpNormalColor;
            }
            if (hpValueText != null)
            {
                hpValueText.text = playerHealth != null && playerHealth.UsesHealthUnits
                    ? $"{HealthUnits.FormatHearts(current)} / {HealthUnits.FormatHearts(maximum)}"
                    : $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
            }
            if (!showFeedback || healthFeedback == null || Mathf.Approximately(LastHealthDelta, 0f)) return;
            healthFeedback.color = LastHealthDelta < 0f ? HpDamageColor : HpHealColor;
            healthFeedback.gameObject.SetActive(true);
            healthFeedbackUntil = Time.unscaledTime + feedbackDuration;
        }

        private void OnSPChanged(int current, int maximum)
        {
            LastSPDelta = current - previousSP;
            previousSP = current;
            bool halfChanged = playerSP != null && playerSP.HasHalfSP != previousHalfSP;
            previousHalfSP = playerSP != null && playerSP.HasHalfSP;
            RefreshSP(current, maximum, true, halfChanged);
            RefreshSkillStateAt(Time.time);
        }

        // Overcharge adds slots past the maximum; a half slot dims the next empty slot instead of filling it.
        private void RefreshSP(int current, int maximum, bool showFeedback, bool halfChanged = false)
        {
            EnsureSlotCount(Mathf.Max(1, Mathf.Max(maximum, current)));
            ActiveSlotCount = Mathf.Clamp(current, 0, spSlots.Count);
            OverchargeSlotCount = Mathf.Max(0, current - maximum);
            IsShowingHalfSlot = playerSP != null && playerSP.HasHalfSP && current < maximum;
            for (int i = 0; i < spSlots.Count; i++)
            {
                spSlots[i].color = i < ActiveSlotCount
                    ? i >= maximum ? SpOverchargeColor : SpActiveColor
                    : i == ActiveSlotCount && IsShowingHalfSlot ? SpHalfColor : SpInactiveColor;
            }
            if (spValueText != null)
                spValueText.text = $"SP {current}{(IsShowingHalfSlot ? ".5" : string.Empty)} / {maximum}";
            if (showFeedback && (LastSPDelta != 0 || halfChanged))
            {
                SetSlotScale(Vector3.one * 1.12f);
                spFeedbackUntil = Time.unscaledTime + feedbackDuration;
            }
        }

        // Values are half-heart units: every heart holds two units and fills its left half first.
        private void RefreshHearts(float current, float maximum, float shield, bool pulseChanges)
        {
            if (heartRowRoot == null || heartTemplate == null) return;
            int maximumUnits = Mathf.Max(0, Mathf.CeilToInt(maximum - 0.0001f));
            int currentUnits = Mathf.Clamp(Mathf.CeilToInt(current - 0.0001f), 0, maximumUnits);
            int shieldUnits = Mathf.Max(0, Mathf.CeilToInt(shield - 0.0001f));
            HealthHeartCount = (maximumUnits + HealthUnits.UnitsPerHeart - 1) / HealthUnits.UnitsPerHeart;
            ShieldHeartCount = (shieldUnits + HealthUnits.UnitsPerHeart - 1) / HealthUnits.UnitsPerHeart;
            EnsureHeartCount(HealthHeartCount + ShieldHeartCount);

            float width = heartRowRoot.rect.width;
            int count = Mathf.Max(1, hearts.Count);
            HeartSize = Mathf.Min(maximumHeartSize, (width - heartSpacing * (count - 1)) / count);
            HorizontalLayoutGroup layout = heartRowRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.spacing = heartSpacing;

            for (int i = 0; i < hearts.Count; i++)
            {
                bool isShield = i >= HealthHeartCount;
                int units = isShield
                    ? shieldUnits - (i - HealthHeartCount) * HealthUnits.UnitsPerHeart
                    : currentUnits - i * HealthUnits.UnitsPerHeart;
                HeartIcon heart = hearts[i];
                float fill = Mathf.Clamp(units, 0, HealthUnits.UnitsPerHeart) / (float)HealthUnits.UnitsPerHeart;
                bool changed = heart.Fill.color != (isShield ? HeartShieldColor : HeartHealthColor) ||
                               !Mathf.Approximately(heart.Fill.fillAmount, fill);
                heart.Root.sizeDelta = new Vector2(HeartSize, HeartSize);
                heart.Background.color = isShield ? Color.clear : HeartEmptyColor;
                heart.Fill.color = isShield ? HeartShieldColor : HeartHealthColor;
                heart.Fill.fillAmount = fill;
                // Only the hearts whose fill changed pop briefly, instead of flashing the whole row.
                if (pulseChanges && changed)
                {
                    heart.Root.localScale = Vector3.one * HeartPulseScale;
                    PulsingHeartCount++;
                    heartPulseUntil = Time.unscaledTime + feedbackDuration;
                }
            }
        }

        private void ResetHeartPulse()
        {
            for (int i = 0; i < hearts.Count; i++) hearts[i].Root.localScale = Vector3.one;
            PulsingHeartCount = 0;
        }

        private void EnsureHeartCount(int count)
        {
            while (hearts.Count < count)
            {
                RectTransform root = Instantiate(heartTemplate, heartRowRoot);
                root.name = "Heart" + (hearts.Count + 1);
                root.gameObject.SetActive(true);
                Image background = root.Find(HeartBackgroundName)?.GetComponent<Image>();
                Image fill = root.Find(HeartFillName)?.GetComponent<Image>();
                if (background == null || fill == null)
                {
                    DestroyObject(root.gameObject);
                    Debug.LogError("[GameHudView] Heart template requires Background and Fill images.", this);
                    return;
                }

                hearts.Add(new HeartIcon(root, background, fill));
            }

            while (hearts.Count > count)
            {
                int last = hearts.Count - 1;
                GameObject removed = hearts[last].Root.gameObject;
                hearts.RemoveAt(last);
                DestroyObject(removed);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void EnsureSlotCount(int count)
        {
            if (spSlotsRoot == null || spSlotTemplate == null) return;
            while (spSlots.Count < count)
            {
                Image slot = Instantiate(spSlotTemplate, spSlotsRoot);
                slot.name = $"SPSlot{spSlots.Count + 1}";
                slot.gameObject.SetActive(true);
                slot.raycastTarget = false;
                spSlots.Add(slot);
            }
            while (spSlots.Count > count)
            {
                int last = spSlots.Count - 1;
                Image slot = spSlots[last];
                spSlots.RemoveAt(last);
                if (Application.isPlaying) Destroy(slot.gameObject);
                else DestroyImmediate(slot.gameObject);
            }
        }

        public void RefreshSkillStateAt(float currentTime)
        {
            IsLowerGradeSkillAvailable = lowerGradeSkill != null && lowerGradeSkill.CanCast;
            if (lowerGradeSkillState != null)
                lowerGradeSkillState.color = IsLowerGradeSkillAvailable ? SkillReadyColor : SkillUnavailableColor;
            if (lowerGradeSkillText != null)
                lowerGradeSkillText.text = lowerGradeSkill != null && lowerGradeSkill.IsFiring
                    ? "발사 중"
                    : IsLowerGradeSkillAvailable ? "사용 가능" : "사용 불가";
            if (lowerGradeCooldownFill != null) lowerGradeCooldownFill.fillAmount = 0f;

            HighGradeCooldownRemaining = highGradeSkill != null
                ? highGradeSkill.GetCooldownRemaining(currentTime)
                : 0f;
            IsHighGradeSkillAvailable = highGradeSkill != null && highGradeSkill.IsReadyAt(currentTime);
            if (highGradeSkillState != null)
                highGradeSkillState.color = IsHighGradeSkillAvailable ? SkillReadyColor : SkillUnavailableColor;
            if (highGradeCooldownFill != null)
            {
                float duration = highGradeSkill != null ? Mathf.Max(0.01f, highGradeSkill.Cooldown) : 1f;
                highGradeCooldownFill.fillAmount = Mathf.Clamp01(HighGradeCooldownRemaining / duration);
            }
            if (highGradeCooldownText != null)
                highGradeCooldownText.text = HighGradeCooldownRemaining > 0.001f
                    ? Mathf.CeilToInt(HighGradeCooldownRemaining).ToString()
                    : string.Empty;
            if (highGradeSkillText != null)
            {
                highGradeSkillText.text = highGradeSkill != null && highGradeSkill.IsDashing
                    ? "사용 중"
                    : HighGradeCooldownRemaining > 0.001f
                        ? "쿨타임"
                        : IsHighGradeSkillAvailable ? "사용 가능" : "사용 불가";
            }
        }

        private void SetSlotScale(Vector3 scale)
        {
            for (int i = 0; i < spSlots.Count; i++) spSlots[i].rectTransform.localScale = scale;
        }

        private readonly struct HeartIcon
        {
            public HeartIcon(RectTransform root, Image background, Image fill)
            {
                Root = root;
                Background = background;
                Fill = fill;
            }

            public RectTransform Root { get; }
            public Image Background { get; }
            public Image Fill { get; }
        }
    }
}
