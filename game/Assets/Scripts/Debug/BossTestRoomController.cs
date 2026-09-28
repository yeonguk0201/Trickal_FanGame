using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Debugging
{
    public sealed class BossTestRoomController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private RunProgress runProgress;

        [Header("Item catalog")]
        [Tooltip("Play Mode에서 보스전과 함께 개별 획득해 볼 수 있는 아이템·스펠 목록입니다.")]
        [SerializeField] private ItemTestRoomController.ItemLoadoutEntry[] itemCatalog =
            Array.Empty<ItemTestRoomController.ItemLoadoutEntry>();

        [Header("Boss Prefabs")]
        [Tooltip("소환 가능한 보스 프리팹 목록입니다.")]
        [SerializeField] private GameObject[] bossPrefabs = Array.Empty<GameObject>();

        [Tooltip("보스 이름 목록입니다. bossPrefabs와 같은 순서여야 합니다.")]
        [SerializeField] private string[] bossNames = Array.Empty<string>();

        [Header("Test room")]
        [SerializeField] private Vector2 roomSize = new(16f, 12f);
        [SerializeField] private bool showDebugPanel = true;
        [SerializeField] private bool showCrayonRecognitionRadius;

        private GameObject spawnedBoss;
        private int selectedBossIndex;
        private Vector2 panelScroll;

        public int BossPrefabCount => bossPrefabs.Length;
        public IReadOnlyList<ItemTestRoomController.ItemLoadoutEntry> ItemCatalog => itemCatalog;
        public int CurrentFloor => runProgress != null ? Mathf.Max(1, runProgress.CurrentFloor) : 1;
        public BossController ActiveBoss => spawnedBoss != null
            ? spawnedBoss.GetComponent<BossController>()
            : null;
        public bool ShowCrayonRecognitionRadius => showCrayonRecognitionRadius;

        public void Configure(
            PlayerInventory configuredInventory,
            Health configuredHealth,
            PlayerStats configuredStats,
            ItemTestRoomController.ItemLoadoutEntry[] configuredItemCatalog,
            GameObject[] configuredBossPrefabs,
            string[] configuredBossNames,
            Vector2 configuredRoomSize)
        {
            playerInventory = configuredInventory;
            playerHealth = configuredHealth;
            playerStats = configuredStats;
            itemCatalog = configuredItemCatalog ?? Array.Empty<ItemTestRoomController.ItemLoadoutEntry>();
            bossPrefabs = configuredBossPrefabs ?? Array.Empty<GameObject>();
            bossNames = configuredBossNames ?? Array.Empty<string>();
            roomSize = new Vector2(
                Mathf.Max(1f, configuredRoomSize.x),
                Mathf.Max(1f, configuredRoomSize.y));
        }

        private void Awake()
        {
            ResolvePlayerReferences();
        }

        private void Start()
        {
            if (!TryValidateConfiguration(out string error))
            {
                Debug.LogError($"[BossTestRoom] Invalid configuration: {error}", this);
                return;
            }

            if (bossPrefabs.Length > 0)
            {
                SpawnBoss(0);
            }

            EnsureTestFloorInitialized();
        }

        public bool TryValidateConfiguration(out string error)
        {
            ResolvePlayerReferences();
            if (playerInventory == null || playerHealth == null || playerStats == null)
            {
                error = "PlayerInventory, Health, and PlayerStats references are required.";
                return false;
            }

            foreach (GameObject prefab in bossPrefabs)
            {
                if (prefab == null)
                {
                    error = "Boss prefab is null.";
                    return false;
                }
                if (prefab.GetComponent<Health>() == null)
                {
                    error = $"Boss prefab {prefab.name} is missing Health component.";
                    return false;
                }
                if (prefab.GetComponent<BossController>() == null)
                {
                    error = $"Boss prefab {prefab.name} is missing BossController component.";
                    return false;
                }
            }

            HashSet<string> itemIds = new(StringComparer.Ordinal);
            foreach (ItemTestRoomController.ItemLoadoutEntry entry in itemCatalog)
            {
                if (entry?.Item == null || !entry.Item.IsValid)
                {
                    error = "Every item catalog entry must reference a valid ItemDefinition.";
                    return false;
                }

                if (!itemIds.Add(entry.Item.ItemId))
                {
                    error = $"Duplicate item catalog entry: {entry.Item.ItemId}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public void SpawnBoss(int index)
        {
            if (index < 0 || index >= bossPrefabs.Length)
            {
                Debug.LogError($"[BossTestRoom] Invalid boss index: {index}", this);
                return;
            }

            DespawnCurrentBoss();

            GameObject prefab = bossPrefabs[index];
            Vector3 spawnPosition = transform.position;
            spawnedBoss = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
            spawnedBoss.name = $"Test Boss - {GetBossName(index)}";
            selectedBossIndex = index;
            ApplyCrayonRecognitionRadius();

            Debug.Log($"[BossTestRoom] Spawned {GetBossName(index)}.", this);
        }

        public void DespawnCurrentBoss()
        {
            if (spawnedBoss != null)
            {
                Destroy(spawnedBoss);
                spawnedBoss = null;
            }
        }

        public void RespawnCurrentBoss()
        {
            SpawnBoss(selectedBossIndex);
        }

        public void ResetPlayerVitals()
        {
            playerHealth?.ResetHealth();
        }

        public void DamagePlayerToLowHealth()
        {
            if (playerHealth == null || playerHealth.IsDead)
            {
                return;
            }

            float targetHealth = Mathf.Max(1f, Mathf.Floor(playerHealth.MaxHealth * 0.25f));
            float damage = playerHealth.CurrentHealth - targetHealth;
            if (damage > 0f)
            {
                playerHealth.TakeDamage(damage);
            }
        }

        public void AdvanceTestFloor()
        {
            ResolveRunProgress();
            runProgress?.RecordRoomEntry(CurrentFloor + 1, 1);
        }

        public void SetCrayonRecognitionRadiusVisible(bool visible)
        {
            showCrayonRecognitionRadius = visible;
            ApplyCrayonRecognitionRadius();
        }

        private void ApplyCrayonRecognitionRadius()
        {
            spawnedBoss?.GetComponent<CrayonHeroBossPatternRuntime>()
                ?.SetRecognitionRadiusDebugVisible(showCrayonRecognitionRadius);
        }

        private string GetBossName(int index)
        {
            if (index < 0 || index >= bossPrefabs.Length)
            {
                return "Unknown";
            }
            if (index < bossNames.Length && !string.IsNullOrEmpty(bossNames[index]))
            {
                return bossNames[index];
            }
            return bossPrefabs[index]?.name ?? "Unknown";
        }

        private void ResolvePlayerReferences()
        {
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            }
            if (playerInventory == null)
            {
                return;
            }

            playerHealth ??= playerInventory.GetComponent<Health>();
            playerStats ??= playerInventory.GetComponent<PlayerStats>();
            ResolveRunProgress();
        }

        private void ResolveRunProgress()
        {
            if (runProgress == null)
            {
                runProgress = FindFirstObjectByType<RunProgress>();
            }
        }

        private void EnsureTestFloorInitialized()
        {
            ResolveRunProgress();
            if (runProgress != null && runProgress.CurrentFloor < 1)
            {
                runProgress.RecordRoomEntry(1, 1);
            }
        }

        private void OnGUI()
        {
            if (!showDebugPanel || playerInventory == null || playerHealth == null || playerStats == null)
            {
                return;
            }

            const float width = 380f;
            Rect area = new(Screen.width - width - 10f, 10f, width, Mathf.Min(Screen.height - 20f, 720f));
            GUILayout.BeginArea(area, "Boss Test Room", GUI.skin.window);

            // Player and Boss vitals
            BossController activeBoss = ActiveBoss;
            string bossVitals = activeBoss == null || activeBoss.Health == null
                ? "BOSS HP --"
                : $"BOSS {activeBoss.DisplayName}  HP {activeBoss.Health.CurrentHealth:0.#}/{activeBoss.Health.MaxHealth:0.#}";
            GUILayout.Label(
                $"FLOOR {CurrentFloor}  PLAYER HP {playerHealth.CurrentHealth:0.#}/{playerHealth.MaxHealth:0.#}  Shield {playerHealth.CurrentShield:0.#}\n" +
                bossVitals + "\n" +
                $"ATK {playerStats.AttackDamage:0.##}  ASPD {playerStats.AttackSpeed:0.##}  " +
                $"CRIT {playerStats.CriticalChance:P0}\n" +
                $"MOVE {playerStats.MoveSpeed:0.##}  SHOT {playerStats.ProjectileCount}  PIERCE {playerStats.PierceCount}",
                GUI.skin.box);

            // Control buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal Player"))
            {
                ResetPlayerVitals();
            }
            if (GUILayout.Button("Respawn Boss"))
            {
                RespawnCurrentBoss();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HP 25% (Life Gem)"))
            {
                DamagePlayerToLowHealth();
            }
            if (GUILayout.Button("Next Floor"))
            {
                AdvanceTestFloor();
            }
            GUILayout.EndHorizontal();

            CrayonHeroBossPatternRuntime crayonRuntime = spawnedBoss != null
                ? spawnedBoss.GetComponent<CrayonHeroBossPatternRuntime>()
                : null;
            if (crayonRuntime != null)
            {
                bool requested = GUILayout.Toggle(showCrayonRecognitionRadius,
                    $"Show Crayon Recognition Radius ({crayonRuntime.SwingTriggerRange:0.00})");
                if (requested != showCrayonRecognitionRadius)
                    SetCrayonRecognitionRadiusVisible(requested);
            }

            // Boss selection
            GUILayout.Label("Boss Selection", GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);

            for (int i = 0; i < bossPrefabs.Length; i++)
            {
                string bossName = GetBossName(i);
                bool isSelected = i == selectedBossIndex;
                string buttonLabel = isSelected ? $"▶ {bossName} (Current)" : bossName;

                GUILayout.BeginHorizontal();
                GUI.enabled = !isSelected;
                if (GUILayout.Button(buttonLabel))
                {
                    SpawnBoss(i);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("보스를 클릭하면 해당 보스가 소환됩니다.");

            GUILayout.Label("Items / Spells — 보스전 중 개별 +1", GUI.skin.box);
            foreach (ItemTestRoomController.ItemLoadoutEntry entry in itemCatalog)
            {
                if (entry?.Item == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                int current = playerInventory.GetStackCount(entry.Item.ItemId);
                string kind = entry.Item.Kind == ItemKind.Spell ? "SPELL" : "ART";
                GUILayout.Label($"[{kind}] {entry.Item.DisplayName}  {current}/{entry.Item.MaxStacks}");
                GUI.enabled = entry.Item.MaxStacks <= 0 || current < entry.Item.MaxStacks;
                if (GUILayout.Button("+1", GUILayout.Width(42f)))
                {
                    playerInventory.TryAcquire(entry.Item);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            PlayerDamageAura aura = playerInventory.GetComponent<PlayerDamageAura>();
            if (aura != null && aura.StackCount > 0)
            {
                GUILayout.Label($"광기의 가면 범위: {aura.Radius:0.##}m (Scene 뷰 Gizmo 표시 중)");
            }
            GUILayout.Label("아이템은 보스를 바꿔도 유지됩니다. 초기화는 Play Mode를 다시 시작하세요.");
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.65f);
            Gizmos.DrawWireCube(transform.position, roomSize);
        }
    }
}
