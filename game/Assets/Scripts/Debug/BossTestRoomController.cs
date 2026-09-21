using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Debugging
{
    public sealed class BossTestRoomController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerStats playerStats;

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
        public BossController ActiveBoss => spawnedBoss != null
            ? spawnedBoss.GetComponent<BossController>()
            : null;
        public bool ShowCrayonRecognitionRadius => showCrayonRecognitionRadius;

        public void Configure(
            PlayerInventory configuredInventory,
            Health configuredHealth,
            PlayerStats configuredStats,
            GameObject[] configuredBossPrefabs,
            string[] configuredBossNames,
            Vector2 configuredRoomSize)
        {
            playerInventory = configuredInventory;
            playerHealth = configuredHealth;
            playerStats = configuredStats;
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
        }

        private void OnGUI()
        {
            if (!showDebugPanel || playerInventory == null || playerHealth == null || playerStats == null)
            {
                return;
            }

            const float width = 350f;
            Rect area = new(Screen.width - width - 10f, 10f, width, Mathf.Min(Screen.height - 20f, 500f));
            GUILayout.BeginArea(area, "Boss Test Room", GUI.skin.window);

            // Player and Boss vitals
            BossController activeBoss = ActiveBoss;
            string bossVitals = activeBoss == null || activeBoss.Health == null
                ? "BOSS HP --"
                : $"BOSS {activeBoss.DisplayName}  HP {activeBoss.Health.CurrentHealth:0.#}/{activeBoss.Health.MaxHealth:0.#}";
            GUILayout.Label(
                $"PLAYER HP {playerHealth.CurrentHealth:0.#}/{playerHealth.MaxHealth:0.#}  Shield {playerHealth.CurrentShield:0.#}\n" +
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
            panelScroll = GUILayout.BeginScrollView(panelScroll, GUILayout.Height(200f));

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

            GUILayout.EndScrollView();
            GUILayout.Label("보스를 클릭하면 해당 보스가 소환됩니다.");
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.65f);
            Gizmos.DrawWireCube(transform.position, roomSize);
        }
    }
}
