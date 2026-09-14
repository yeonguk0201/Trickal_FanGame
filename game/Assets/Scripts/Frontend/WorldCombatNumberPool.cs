using System.Collections.Generic;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    public sealed class WorldCombatNumberPool : MonoBehaviour
    {
        private sealed class Entry
        {
            public TMP_Text Text;
            public Vector3 StartPosition;
            public float Elapsed;
        }

        [SerializeField] private TMP_Text template;
        [SerializeField] private EnemyWorldHealthBarView healthBarTemplate;
        [SerializeField, Range(0.6f, 1f)] private float lifetime = 0.8f;
        [SerializeField, Min(0f)] private float riseDistance = 0.65f;
        [SerializeField, Min(0f)] private float horizontalScatter = 0.18f;
        [SerializeField] private Color damageColor = new(1f, 0.34f, 0.28f, 1f);

        private readonly List<Entry> active = new();
        private readonly Queue<Entry> available = new();
        private int spawnSequence;

        public TMP_Text Template => template;
        public EnemyWorldHealthBarView HealthBarTemplate => healthBarTemplate;
        public float Lifetime => lifetime;
        public float RiseDistance => riseDistance;
        public float HorizontalScatter => horizontalScatter;
        public int ActiveCount => active.Count;
        public int PooledCount => available.Count;
        public int TotalInstanceCount => active.Count + available.Count;

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
        }

        private void OnDisable()
        {
            Health.AnyDamageResolved -= OnAnyDamageResolved;
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
            Spawn(target.transform.position, FormatAmount(result.FinalDamage), damageColor);
            if (regularEnemy != null && boss == null) EnsureHealthBar(target);
        }

        public void Tick(float deltaTime)
        {
            float safeDelta = Mathf.Max(0f, deltaTime);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Entry entry = active[i];
                entry.Elapsed += safeDelta;
                float progress = lifetime > 0f ? Mathf.Clamp01(entry.Elapsed / lifetime) : 1f;
                entry.Text.transform.position = entry.StartPosition + Vector3.up * (riseDistance * progress);
                Color color = entry.Text.color;
                color.a = 1f - progress;
                entry.Text.color = color;
                if (entry.Elapsed >= lifetime) ReleaseAt(i);
            }
        }

        public string GetActiveText(int index) => active[index].Text.text;
        public Vector3 GetActiveStartPosition(int index) => active[index].StartPosition;

        private void Spawn(Vector3 targetPosition, string value, Color color)
        {
            if (template == null) return;
            Entry entry = available.Count > 0 ? available.Dequeue() : CreateEntry();
            int side = spawnSequence++ % 2 == 0 ? -1 : 1;
            int band = (spawnSequence / 2) % 3;
            float offset = side * horizontalScatter * (1f + band * 0.35f);
            entry.StartPosition = targetPosition + new Vector3(offset, 1.05f, 0f);
            entry.Elapsed = 0f;
            entry.Text.transform.position = entry.StartPosition;
            entry.Text.text = value;
            entry.Text.color = color;
            entry.Text.gameObject.SetActive(true);
            active.Add(entry);
        }

        private Entry CreateEntry()
        {
            TMP_Text text = Instantiate(template, transform);
            text.name = "Combat Number";
            text.gameObject.SetActive(false);
            return new Entry { Text = text };
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

        private static string FormatAmount(float amount) => Mathf.Max(1, Mathf.CeilToInt(amount)).ToString();
    }
}
