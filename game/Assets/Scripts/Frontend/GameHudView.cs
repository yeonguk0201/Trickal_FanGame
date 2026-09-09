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
        private static readonly Color HpDamageColor = new(1f, 0.24f, 0.2f, 0.8f);
        private static readonly Color HpHealColor = new(0.35f, 1f, 0.62f, 0.8f);
        private static readonly Color SpActiveColor = new(0.3f, 0.78f, 1f, 1f);
        private static readonly Color SpInactiveColor = new(0.18f, 0.28f, 0.38f, 0.55f);
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
        [SerializeField, Min(0.05f)] private float feedbackDuration = 0.28f;

        private readonly List<Image> spSlots = new();
        private float previousHealth;
        private int previousSP;
        private float healthFeedbackUntil;
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
        public int SlotCount => spSlots.Count;
        public int ActiveSlotCount { get; private set; }
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
                RefreshSP(playerSP.CurrentSP, playerSP.MaxSP, false);
            }

            RefreshSkillStateAt(Time.time);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (playerHealth != null) playerHealth.Changed += OnHealthChanged;
            if (playerSP != null) playerSP.Changed += OnSPChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
            if (playerSP != null) playerSP.Changed -= OnSPChanged;
            subscribed = false;
        }

        private void OnHealthChanged(float current, float maximum)
        {
            LastHealthDelta = current - previousHealth;
            previousHealth = current;
            RefreshHealth(current, maximum, true);
        }

        private void RefreshHealth(float current, float maximum, bool showFeedback)
        {
            float safeMaximum = Mathf.Max(0.1f, maximum);
            if (hpFill != null)
            {
                hpFill.fillAmount = Mathf.Clamp01(current / safeMaximum);
                hpFill.color = HpNormalColor;
            }
            if (hpValueText != null) hpValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
            if (!showFeedback || healthFeedback == null || Mathf.Approximately(LastHealthDelta, 0f)) return;
            healthFeedback.color = LastHealthDelta < 0f ? HpDamageColor : HpHealColor;
            healthFeedback.gameObject.SetActive(true);
            healthFeedbackUntil = Time.unscaledTime + feedbackDuration;
        }

        private void OnSPChanged(int current, int maximum)
        {
            LastSPDelta = current - previousSP;
            previousSP = current;
            RefreshSP(current, maximum, true);
            RefreshSkillStateAt(Time.time);
        }

        private void RefreshSP(int current, int maximum, bool showFeedback)
        {
            EnsureSlotCount(Mathf.Max(1, maximum));
            ActiveSlotCount = Mathf.Clamp(current, 0, maximum);
            for (int i = 0; i < spSlots.Count; i++)
            {
                spSlots[i].color = i < ActiveSlotCount ? SpActiveColor : SpInactiveColor;
            }
            if (spValueText != null) spValueText.text = $"SP {current} / {maximum}";
            if (showFeedback && LastSPDelta != 0)
            {
                SetSlotScale(Vector3.one * 1.12f);
                spFeedbackUntil = Time.unscaledTime + feedbackDuration;
            }
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
    }
}
