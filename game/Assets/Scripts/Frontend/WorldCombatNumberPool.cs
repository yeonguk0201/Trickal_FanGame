using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace TrickalFanGame.Frontend
{
    public enum WorldCombatNumberKind
    {
        Normal = 0,
        Critical = 1,
        // Burn, poison, shock and auras: smaller so they do not hide direct hits.
        Periodic = 2,
        // A hit stopped by invulnerability: "무적" over a 0.
        Blocked = 3,
    }

    public sealed class WorldCombatNumberPool : MonoBehaviour
    {
        public const string BlockedLabel = "무적";
        private const float CriticalPopDuration = 0.12f;
        private const float BlockedRepeatInterval = 0.35f;

        private sealed class Entry
        {
            public TMP_Text Text;
            public SpriteRenderer Burst;
            public TMP_Text Shadow;
            public TMP_Text Label;
            public WorldCombatNumberKind Kind;
            public Vector3 StartPosition;
            public Color TextColor;
            public Color BurstColor;
            public float Scale;
            public float BurstSpin;
            public float Elapsed;
        }

        [SerializeField] private TMP_Text template;
        [SerializeField] private EnemyWorldHealthBarView healthBarTemplate;
        [SerializeField, Range(0.6f, 1f)] private float lifetime = 0.8f;
        [SerializeField, Min(0f)] private float riseDistance = 0.65f;
        [SerializeField, Min(0f)] private float horizontalScatter = 0.18f;
        // Every number: orange-tinted white inside a darker orange outline, with a faint shadow. A critical hit
        // only adds the star (CriticalBurstSprites) and a larger size. The outlines live in material assets made
        // by Number0Setup: the TMP SDF shader draws them only with the OUTLINE_ON keyword, and a build keeps that
        // shader variant only when a saved material uses it.
        [SerializeField, FormerlySerializedAs("criticalMaterial")] private Material numberMaterial;
        [SerializeField, FormerlySerializedAs("criticalShadowMaterial")] private Material shadowMaterial;
        [SerializeField] private Material blockedLabelMaterial;
        [SerializeField, FormerlySerializedAs("criticalTextColor")]
        private Color numberColor = new(1f, 0.95f, 0.86f, 1f);
        [SerializeField] private Color criticalBurstColor = new(1f, 1f, 1f, 0.95f);
        // At the usual camera (orthographic size about 5.25), 0.01 world units is about one pixel at 1080p, so
        // the shadow sits roughly 4 px right and 3 px down.
        [SerializeField, FormerlySerializedAs("criticalShadowColor")]
        private Color shadowColor = new(0.36f, 0.13f, 0.02f, 0.5f);
        [SerializeField, FormerlySerializedAs("criticalShadowOffset")]
        private Vector2 shadowOffset = new(0.04f, -0.03f);
        // The blocked label: RGB(255,247,138) inside; its RGB(194,143,38) outline is in blockedLabelMaterial.
        [SerializeField] private Color blockedLabelFaceColor = new(1f, 247f / 255f, 138f / 255f, 1f);
        // Letter spacing in TMP em/100 units: numbers a little tighter, the blocked label almost touching.
        [SerializeField] private float numberCharacterSpacing = -6f;
        [SerializeField] private float blockedLabelCharacterSpacing = -18f;
        [SerializeField, Min(1f)] private float criticalScale = 1.35f;
        [SerializeField, Range(0.4f, 1f)] private float periodicScale = 0.72f;

        private readonly List<Entry> active = new();
        private readonly Queue<Entry> available = new();
        private readonly Dictionary<Health, float> lastBlockedAt = new();
        private int spawnSequence;
        private float clock;

        public TMP_Text Template => template;
        public EnemyWorldHealthBarView HealthBarTemplate => healthBarTemplate;
        public float Lifetime => lifetime;
        public float RiseDistance => riseDistance;
        public float HorizontalScatter => horizontalScatter;
        public float CriticalScale => criticalScale;
        public float PeriodicScale => periodicScale;
        public int ActiveCount => active.Count;
        public int PooledCount => available.Count;
        public int TotalInstanceCount => active.Count + available.Count;

        public Material NumberMaterial => numberMaterial;
        public Material ShadowMaterial => shadowMaterial;
        public Material BlockedLabelMaterial => blockedLabelMaterial;

        public void ConfigureMaterials(Material number, Material shadow, Material blockedLabel)
        {
            numberMaterial = number;
            shadowMaterial = shadow;
            blockedLabelMaterial = blockedLabel;
        }

        public void Configure(TMP_Text configuredTemplate, EnemyWorldHealthBarView configuredHealthBarTemplate,
            float configuredLifetime, float configuredRiseDistance, float configuredHorizontalScatter)
        {
            template = configuredTemplate;
            healthBarTemplate = configuredHealthBarTemplate;
            lifetime = Mathf.Clamp(configuredLifetime, 0.6f, 1f);
            riseDistance = Mathf.Max(0f, configuredRiseDistance);
            horizontalScatter = Mathf.Max(0f, configuredHorizontalScatter);
            if (template != null) template.gameObject.SetActive(false);
            if (healthBarTemplate != null) healthBarTemplate.gameObject.SetActive(false);
        }

        private void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
            if (healthBarTemplate != null) healthBarTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            RefreshBindings();
        }

        public void RefreshBindings()
        {
            Health.AnyDamageResolved -= OnAnyDamageResolved;
            Health.AnyDamageResolved += OnAnyDamageResolved;
            Health.AnyDamageBlocked -= OnAnyDamageBlocked;
            Health.AnyDamageBlocked += OnAnyDamageBlocked;
        }

        private void OnDisable()
        {
            Health.AnyDamageResolved -= OnAnyDamageResolved;
            Health.AnyDamageBlocked -= OnAnyDamageBlocked;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnAnyDamageResolved(Health target, DamageContext context, DamageResult result)
        {
            if (target == null || result.FinalDamage <= 0f || target.GetComponent<PlayerMovement>() != null)
                return;

            TestEnemy regularEnemy = target.GetComponent<TestEnemy>();
            BossController boss = target.GetComponent<BossController>();
            if (regularEnemy == null && boss == null) return;
            WorldCombatNumberKind kind = result.IsCritical
                ? WorldCombatNumberKind.Critical
                : context.DeliveryType == DamageDeliveryType.Periodic
                    ? WorldCombatNumberKind.Periodic
                    : WorldCombatNumberKind.Normal;
            Spawn(target.transform.position, FormatAmount(result.FinalDamage), kind);
            if (regularEnemy != null && boss == null) EnsureHealthBar(target);
        }

        // Only direct hits show "무적": ticks on an invulnerable target would only repeat it. The same target shows
        // it at most once per short interval so a volley does not stack a column of labels.
        private void OnAnyDamageBlocked(Health target, DamageContext context)
        {
            if (target == null || context.DeliveryType != DamageDeliveryType.Direct ||
                target.GetComponent<PlayerMovement>() != null)
                return;
            if (target.GetComponent<TestEnemy>() == null && target.GetComponent<BossController>() == null) return;
            if (lastBlockedAt.TryGetValue(target, out float last) && clock - last < BlockedRepeatInterval) return;
            if (lastBlockedAt.Count > 64) lastBlockedAt.Clear();
            lastBlockedAt[target] = clock;
            Spawn(target.transform.position, "0", WorldCombatNumberKind.Blocked);
        }

        // Development panel: one of each kind over a position, to compare them on screen.
        public void SpawnSamples(Vector3 position)
        {
            Spawn(position + Vector3.left * 2.1f, "12", WorldCombatNumberKind.Normal);
            Spawn(position + Vector3.left * 0.7f, "48", WorldCombatNumberKind.Critical);
            Spawn(position + Vector3.right * 0.7f, "3", WorldCombatNumberKind.Periodic);
            Spawn(position + Vector3.right * 2.1f, "0", WorldCombatNumberKind.Blocked);
        }

        public void Tick(float deltaTime)
        {
            float safeDelta = Mathf.Max(0f, deltaTime);
            clock += safeDelta;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Entry entry = active[i];
                entry.Elapsed += safeDelta;
                float progress = lifetime > 0f ? Mathf.Clamp01(entry.Elapsed / lifetime) : 1f;
                entry.Text.transform.position = entry.StartPosition + Vector3.up * (riseDistance * progress);
                float alpha = 1f - progress;
                entry.Text.color = WithAlpha(entry.TextColor, alpha);
                if (entry.Kind == WorldCombatNumberKind.Critical)
                {
                    float pop = 1f - Mathf.Clamp01(entry.Elapsed / CriticalPopDuration);
                    entry.Text.transform.localScale = Vector3.one * (entry.Scale * (1f + 0.35f * pop * pop));
                    entry.Burst.color = WithAlpha(entry.BurstColor, entry.BurstColor.a * alpha);
                    entry.Burst.transform.Rotate(0f, 0f, entry.BurstSpin * safeDelta, Space.Self);
                }

                entry.Shadow.color = WithAlpha(shadowColor, shadowColor.a * alpha);
                if (entry.Kind == WorldCombatNumberKind.Blocked)
                    entry.Label.color = WithAlpha(blockedLabelFaceColor, alpha);
                if (entry.Elapsed >= lifetime) ReleaseAt(i);
            }
        }

        public string GetActiveText(int index) => active[index].Text.text;
        public Vector3 GetActiveStartPosition(int index) => active[index].StartPosition;
        public WorldCombatNumberKind GetActiveKind(int index) => active[index].Kind;
        public float GetActiveScale(int index) => active[index].Scale;
        public Color GetActiveTextColor(int index) => active[index].TextColor;
        public SpriteRenderer GetActiveBurst(int index) => active[index].Burst;
        public TMP_Text GetActiveShadow(int index) => active[index].Shadow;
        public Material GetActiveTextMaterial(int index) => active[index].Text.fontSharedMaterial;
        public Vector2 ShadowOffset => shadowOffset;
        public float NumberCharacterSpacing => numberCharacterSpacing;
        public float BlockedLabelCharacterSpacing => blockedLabelCharacterSpacing;
        public TMP_Text GetActiveLabel(int index) => active[index].Label;

        private void Spawn(Vector3 targetPosition, string value, WorldCombatNumberKind kind)
        {
            if (template == null) return;
            Entry entry = available.Count > 0 ? available.Dequeue() : CreateEntry();
            int side = spawnSequence++ % 2 == 0 ? -1 : 1;
            int band = (spawnSequence / 2) % 3;
            float offset = side * horizontalScatter * (1f + band * 0.35f);
            entry.Kind = kind;
            entry.StartPosition = targetPosition + new Vector3(offset, 1.05f, 0f);
            entry.Elapsed = 0f;
            entry.Scale = kind switch
            {
                WorldCombatNumberKind.Critical => criticalScale,
                WorldCombatNumberKind.Periodic => periodicScale,
                _ => 1f,
            };
            entry.TextColor = numberColor;
            entry.Text.transform.position = entry.StartPosition;
            entry.Text.transform.localScale = Vector3.one * entry.Scale;
            entry.Text.text = value;
            entry.Text.color = entry.TextColor;
            ConfigureBurst(entry, kind == WorldCombatNumberKind.Critical);
            ConfigureShadow(entry);
            ConfigureLabel(entry, kind == WorldCombatNumberKind.Blocked);
            entry.Text.gameObject.SetActive(true);
            active.Add(entry);
        }

        private void ConfigureBurst(Entry entry, bool show)
        {
            if (!show)
            {
                if (entry.Burst != null) entry.Burst.gameObject.SetActive(false);
                return;
            }

            if (entry.Burst == null)
            {
                GameObject burstObject = new("Critical Burst");
                burstObject.transform.SetParent(entry.Text.transform, false);
                entry.Burst = burstObject.AddComponent<SpriteRenderer>();
                Renderer textRenderer = entry.Text.GetComponent<Renderer>();
                entry.Burst.sortingLayerID = textRenderer.sortingLayerID;
                entry.Burst.sortingOrder = textRenderer.sortingOrder - 2;
            }

            // A different shape, turn and stretch for every critical hit.
            entry.Burst.sprite = CriticalBurstSprites.Get(Random.Range(0, CriticalBurstSprites.VariantCount));
            float size = Random.Range(0.82f, 0.98f);
            entry.Burst.transform.localPosition = new Vector3(Random.Range(-0.06f, 0.06f), Random.Range(-0.02f, 0.06f), 0f);
            entry.Burst.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            entry.Burst.transform.localScale = new Vector3(size * Random.Range(0.9f, 1.15f),
                size * Random.Range(0.95f, 1.3f), 1f);
            entry.BurstSpin = Random.Range(-24f, 24f);
            entry.BurstColor = criticalBurstColor;
            entry.Burst.color = criticalBurstColor;
            entry.Burst.gameObject.SetActive(true);
        }

        // A copy of the number in the shadow colour, one sorting step behind it and in front of the star.
        private void ConfigureShadow(Entry entry)
        {
            // Local units are scaled by the number, so the offset is divided back to stay the same on screen.
            entry.Shadow.transform.localPosition = (Vector3)(shadowOffset / Mathf.Max(0.01f, entry.Scale));
            entry.Shadow.text = entry.Text.text;
            entry.Shadow.color = shadowColor;
        }

        private void ConfigureLabel(Entry entry, bool show)
        {
            if (!show)
            {
                if (entry.Label != null) entry.Label.gameObject.SetActive(false);
                return;
            }

            if (entry.Label == null)
            {
                entry.Label = Instantiate(template, entry.Text.transform);
                entry.Label.name = "Blocked Label";
                entry.Label.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                entry.Label.transform.localRotation = Quaternion.identity;
                entry.Label.transform.localScale = Vector3.one * 0.62f;
                if (blockedLabelMaterial != null) entry.Label.fontSharedMaterial = blockedLabelMaterial;
                entry.Label.characterSpacing = blockedLabelCharacterSpacing;
            }

            entry.Label.text = BlockedLabel;
            entry.Label.color = blockedLabelFaceColor;
            entry.Label.gameObject.SetActive(true);
        }

        // Shared materials only, so pooled numbers never make material instances. The shadow is a child, so it
        // shows and hides with its number.
        private Entry CreateEntry()
        {
            TMP_Text text = Instantiate(template, transform);
            text.name = "Combat Number";
            text.gameObject.SetActive(false);
            if (numberMaterial != null) text.fontSharedMaterial = numberMaterial;
            text.characterSpacing = numberCharacterSpacing;
            TMP_Text shadow = Instantiate(template, text.transform);
            shadow.name = "Number Shadow";
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localScale = Vector3.one;
            if (shadowMaterial != null) shadow.fontSharedMaterial = shadowMaterial;
            shadow.characterSpacing = numberCharacterSpacing;
            Renderer textRenderer = text.GetComponent<Renderer>();
            Renderer shadowRenderer = shadow.GetComponent<Renderer>();
            shadowRenderer.sortingLayerID = textRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = textRenderer.sortingOrder - 1;
            shadow.gameObject.SetActive(true);
            return new Entry { Text = text, Shadow = shadow };
        }

        private void ReleaseAt(int index)
        {
            Entry entry = active[index];
            active.RemoveAt(index);
            entry.Text.gameObject.SetActive(false);
            available.Enqueue(entry);
        }

        private void EnsureHealthBar(Health target)
        {
            if (healthBarTemplate == null) return;
            EnemyWorldHealthBarView view = target.GetComponentInChildren<EnemyWorldHealthBarView>(true);
            if (view == null)
            {
                view = Instantiate(healthBarTemplate, target.transform);
                view.name = "Enemy Health Bar";
                view.gameObject.SetActive(true);
            }
            view.Bind(target);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static string FormatAmount(float amount) => Mathf.Max(1, Mathf.CeilToInt(amount)).ToString();
    }
}
