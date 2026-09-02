using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Debugging
{
    public sealed class ItemTestRoomController : MonoBehaviour
    {
        [Serializable]
        public sealed class ItemLoadoutEntry
        {
            [SerializeField] private ItemDefinition item;
            [SerializeField, Min(0)] private int startingStacks;

            public ItemDefinition Item => item;
            public int StartingStacks => Mathf.Max(0, startingStacks);

            public ItemLoadoutEntry(ItemDefinition configuredItem, int configuredStacks)
            {
                item = configuredItem;
                startingStacks = Mathf.Max(0, configuredStacks);
            }
        }

        [Serializable]
        public sealed class EnemyPlacement
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private GameObject enemyPrefab;
            [SerializeField] private Vector2 localPosition;
            [SerializeField] private float rotationDegrees;

            public bool Enabled => enabled;
            public GameObject EnemyPrefab => enemyPrefab;
            public Vector2 LocalPosition => localPosition;
            public float RotationDegrees => rotationDegrees;

            public EnemyPlacement(
                GameObject configuredPrefab,
                Vector2 configuredPosition,
                float configuredRotation = 0f)
            {
                enabled = true;
                enemyPrefab = configuredPrefab;
                localPosition = configuredPosition;
                rotationDegrees = configuredRotation;
            }
        }

        [Header("Player")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerStats playerStats;

        [Header("Initial item loadout")]
        [Tooltip("Play Mode 시작 시 적용할 아티팩트와 스택 수입니다. 0이면 목록에는 남지만 획득하지 않습니다.")]
        [SerializeField] private ItemLoadoutEntry[] itemLoadout = Array.Empty<ItemLoadoutEntry>();

        [Header("Enemy placements")]
        [Tooltip("이 오브젝트 기준 로컬 좌표입니다. Play Mode에서 Respawn Enemies로 다시 배치할 수 있습니다.")]
        [SerializeField] private EnemyPlacement[] enemyPlacements = Array.Empty<EnemyPlacement>();

        [Header("Test room")]
        [SerializeField] private Vector2 roomSize = new(16f, 9f);
        [SerializeField] private bool applyLoadoutOnStart = true;
        [SerializeField] private bool spawnEnemiesOnStart = true;
        [SerializeField] private bool showDebugPanel = true;

        private readonly List<GameObject> spawnedEnemies = new();
        private Vector2 panelScroll;

        public IReadOnlyList<ItemLoadoutEntry> ItemLoadout => itemLoadout;
        public IReadOnlyList<EnemyPlacement> EnemyPlacements => enemyPlacements;
        public Vector2 RoomSize => roomSize;
        public bool HasAppliedLoadout { get; private set; }
        public int SpawnedEnemyCount => spawnedEnemies.Count(enemy => enemy != null);

        public void Configure(
            PlayerInventory configuredInventory,
            Health configuredHealth,
            PlayerStats configuredStats,
            ItemLoadoutEntry[] configuredLoadout,
            EnemyPlacement[] configuredPlacements,
            Vector2 configuredRoomSize)
        {
            playerInventory = configuredInventory;
            playerHealth = configuredHealth;
            playerStats = configuredStats;
            itemLoadout = configuredLoadout ?? Array.Empty<ItemLoadoutEntry>();
            enemyPlacements = configuredPlacements ?? Array.Empty<EnemyPlacement>();
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
                Debug.LogError($"[ItemTestRoom] Invalid configuration: {error}", this);
                enabled = false;
                return;
            }

            if (applyLoadoutOnStart)
            {
                ApplyConfiguredLoadout();
            }

            if (spawnEnemiesOnStart)
            {
                RespawnEnemies();
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

            HashSet<string> itemIds = new(StringComparer.Ordinal);
            foreach (ItemLoadoutEntry entry in itemLoadout)
            {
                if (entry?.Item == null || !entry.Item.IsValid)
                {
                    error = "Every item loadout entry must reference a valid ItemDefinition.";
                    return false;
                }

                if (!itemIds.Add(entry.Item.ItemId))
                {
                    error = $"Duplicate item loadout entry: {entry.Item.ItemId}.";
                    return false;
                }

                if (entry.Item.MaxStacks > 0 && entry.StartingStacks > entry.Item.MaxStacks)
                {
                    error = $"{entry.Item.ItemId} starting stacks exceed its maximum of {entry.Item.MaxStacks}.";
                    return false;
                }
            }

            foreach (EnemyPlacement placement in enemyPlacements)
            {
                if (placement == null || !placement.Enabled)
                {
                    continue;
                }

                if (placement.EnemyPrefab == null || placement.EnemyPrefab.GetComponent<Health>() == null)
                {
                    error = "Every enabled enemy placement must reference a prefab with Health.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool ApplyConfiguredLoadout()
        {
            if (HasAppliedLoadout || playerInventory == null)
            {
                return false;
            }

            foreach (ItemLoadoutEntry entry in itemLoadout)
            {
                for (int stack = 0; stack < entry.StartingStacks; stack++)
                {
                    if (!playerInventory.TryAcquire(entry.Item))
                    {
                        Debug.LogError(
                            $"[ItemTestRoom] Failed to apply {entry.Item.ItemId} stack {stack + 1}.",
                            this);
                        return false;
                    }
                }
            }

            HasAppliedLoadout = true;
            Debug.Log("[ItemTestRoom] Configured item loadout applied.", this);
            return true;
        }

        public void RespawnEnemies()
        {
            for (int index = 0; index < spawnedEnemies.Count; index++)
            {
                if (spawnedEnemies[index] != null)
                {
                    Destroy(spawnedEnemies[index]);
                }
            }
            spawnedEnemies.Clear();

            foreach (EnemyPlacement placement in enemyPlacements)
            {
                if (placement == null || !placement.Enabled || placement.EnemyPrefab == null)
                {
                    continue;
                }

                Vector3 worldPosition = transform.TransformPoint(placement.LocalPosition);
                Quaternion rotation = Quaternion.Euler(0f, 0f, placement.RotationDegrees);
                GameObject enemy = Instantiate(placement.EnemyPrefab, worldPosition, rotation, transform);
                enemy.name = $"Debug Enemy - {placement.EnemyPrefab.name}";
                spawnedEnemies.Add(enemy);
            }

            Debug.Log($"[ItemTestRoom] Spawned {SpawnedEnemyCount} configured enemies.", this);
        }

        public void ResetPlayerVitals()
        {
            playerHealth?.ResetHealth();
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

            const float width = 330f;
            Rect area = new(Screen.width - width - 10f, 10f, width, Mathf.Min(Screen.height - 20f, 650f));
            GUILayout.BeginArea(area, "Item Test Room", GUI.skin.window);
            GUILayout.Label(
                $"HP {playerHealth.CurrentHealth:0.#}/{playerHealth.MaxHealth:0.#}  Shield {playerHealth.CurrentShield:0.#}\n" +
                $"ATK {playerStats.AttackDamage:0.##}  ASPD {playerStats.AttackSpeed:0.##}  " +
                $"CRIT {playerStats.CriticalChance:P0}\n" +
                $"MOVE {playerStats.MoveSpeed:0.##}  SHOT {playerStats.ProjectileCount}  PIERCE {playerStats.PierceCount}",
                GUI.skin.box);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal / Reset HP"))
            {
                ResetPlayerVitals();
            }
            if (GUILayout.Button("Respawn Enemies"))
            {
                RespawnEnemies();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Items — +1은 현재 Play에서 즉시 획득", GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            foreach (ItemLoadoutEntry entry in itemLoadout)
            {
                if (entry?.Item == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                int current = playerInventory.GetStackCount(entry.Item.ItemId);
                GUILayout.Label($"{entry.Item.DisplayName}  {current}/{entry.Item.MaxStacks}");
                GUI.enabled = entry.Item.MaxStacks <= 0 || current < entry.Item.MaxStacks;
                if (GUILayout.Button("+1", GUILayout.Width(42f)))
                {
                    playerInventory.TryAcquire(entry.Item);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label("스택을 줄이거나 초기화하려면 Play Mode를 다시 시작하세요.");
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.65f);
            Gizmos.DrawWireCube(transform.position, roomSize);

            foreach (EnemyPlacement placement in enemyPlacements)
            {
                if (placement == null || !placement.Enabled)
                {
                    continue;
                }

                Gizmos.color = placement.EnemyPrefab == null
                    ? Color.red
                    : new Color(1f, 0.35f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(transform.TransformPoint(placement.LocalPosition), 0.45f);
            }
        }
    }
}
