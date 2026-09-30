using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    public sealed class GameBossHudView : MonoBehaviour
    {
        [SerializeField] private RoomGraphController roomGraph;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Image healthFill;

        private RoomNode observedNode;
        private BossController observedBoss;
        private Health observedHealth;

        public RoomGraphController RoomGraph => roomGraph;
        public CanvasGroup CanvasGroup => canvasGroup;
        public TMP_Text BossNameText => bossNameText;
        public TMP_Text PhaseText => phaseText;
        public TMP_Text HealthText => healthText;
        public Image HealthFill => healthFill;
        public BossController ObservedBoss => observedBoss;
        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.99f;

        public void Configure(
            RoomGraphController configuredRoomGraph,
            CanvasGroup configuredCanvasGroup,
            TMP_Text configuredBossNameText,
            TMP_Text configuredPhaseText,
            TMP_Text configuredHealthText,
            Image configuredHealthFill)
        {
            UnbindBoss();
            roomGraph = configuredRoomGraph;
            canvasGroup = configuredCanvasGroup;
            bossNameText = configuredBossNameText;
            phaseText = configuredPhaseText;
            healthText = configuredHealthText;
            healthFill = configuredHealthFill;
            observedNode = null;
            RefreshNow();
        }

        private void Awake()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            RefreshNow();
        }

        private void OnDisable()
        {
            UnbindBoss();
            observedNode = null;
        }

        private void OnDestroy()
        {
            UnbindBoss();
        }

        private void Update()
        {
            RefreshNow();
        }

        public void RefreshNow()
        {
            RoomNode currentNode = roomGraph != null ? roomGraph.CurrentNode : null;
            if (currentNode != observedNode || !IsBossUsable(observedBoss, currentNode))
            {
                observedNode = currentNode;
                BindBoss(FindActiveBoss(currentNode));
            }

            if (!IsBossUsable(observedBoss, currentNode))
            {
                SetVisible(false);
                return;
            }

            RefreshValues();
            SetVisible(true);
        }

        private static BossController FindActiveBoss(RoomNode node)
        {
            if (node == null || node.ContentRoot == null || node.Definition == null ||
                node.Definition.RoomType != RoomType.Boss)
            {
                return null;
            }

            BossController[] bosses = node.ContentRoot.GetComponentsInChildren<BossController>(true);
            foreach (BossController boss in bosses)
            {
                if (IsBossUsable(boss, node))
                {
                    return boss;
                }
            }

            return null;
        }

        private static bool IsBossUsable(BossController boss, RoomNode node)
        {
            if (boss == null || node == null || node.ContentRoot == null || node.Definition == null ||
                node.Definition.RoomType != RoomType.Boss || !boss.gameObject.activeInHierarchy)
            {
                return false;
            }

            Health health = boss.Health;
            return health != null && !health.IsDead && health.CurrentHealth > 0f &&
                   boss.transform.IsChildOf(node.ContentRoot.transform);
        }

        private void BindBoss(BossController boss)
        {
            if (observedBoss == boss)
            {
                return;
            }

            UnbindBoss();
            observedBoss = boss;
            observedHealth = boss != null ? boss.Health : null;
            if (observedHealth != null)
            {
                observedHealth.Changed += OnHealthChanged;
                observedHealth.Died += OnBossDied;
            }

            if (observedBoss != null)
            {
                observedBoss.PhaseChanged += OnPhaseChanged;
            }
        }

        private void UnbindBoss()
        {
            if (observedHealth != null)
            {
                observedHealth.Changed -= OnHealthChanged;
                observedHealth.Died -= OnBossDied;
            }

            if (observedBoss != null)
            {
                observedBoss.PhaseChanged -= OnPhaseChanged;
            }

            observedHealth = null;
            observedBoss = null;
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            RefreshValues(currentHealth, maxHealth);
        }

        private void OnPhaseChanged(int currentPhase, int phaseCount)
        {
            RefreshValues();
        }

        private void OnBossDied()
        {
            SetVisible(false);
        }

        private void RefreshValues()
        {
            if (observedHealth == null)
            {
                return;
            }

            RefreshValues(observedHealth.CurrentHealth, observedHealth.MaxHealth);
        }

        private void RefreshValues(float currentHealth, float maxHealth)
        {
            if (observedBoss == null)
            {
                return;
            }

            if (bossNameText != null) bossNameText.text = observedBoss.DisplayName;
            if (phaseText != null) phaseText.text = $"페이즈 {observedBoss.CurrentPhase} / {observedBoss.PhaseCount}";
            if (healthText != null) healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
            if (healthFill != null)
                healthFill.fillAmount = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
