using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
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
        [SerializeField] private RunProgress runProgress;

        [Header("Initial item loadout")]
        [Tooltip("Play Mode 시작 시 적용할 아이템·스펠과 스택 수입니다. 0이면 목록에는 남지만 획득하지 않습니다.")]
        [SerializeField] private ItemLoadoutEntry[] itemLoadout = Array.Empty<ItemLoadoutEntry>();

        [Header("Enemy placements")]
        [Tooltip("이 오브젝트 기준 로컬 좌표입니다. Play Mode에서 Respawn Enemies로 다시 배치할 수 있습니다.")]
        [SerializeField] private EnemyPlacement[] enemyPlacements = Array.Empty<EnemyPlacement>();

        [Header("Test room")]
        [SerializeField] private Vector2 roomSize = new(16f, 9f);
        [SerializeField] private bool applyLoadoutOnStart = true;
        [SerializeField] private bool spawnEnemiesOnStart = true;
        [SerializeField] private bool showDebugPanel = true;

        [Header("Resources")]
        [Tooltip("디버그 패널의 Spawn Heart로 플레이어 옆에 생성할 체력 회복 픽업입니다.")]
        [SerializeField] private HealthPickup healthPickupPrefab;
        [Tooltip("디버그 패널의 Spawn 버튼으로 생성할 엘리프·열쇠·폭탄 픽업입니다. 자원 종류별로 하나씩 둡니다.")]
        [SerializeField] private RunResourcePickup[] resourcePickupPrefabs = Array.Empty<RunResourcePickup>();

        private readonly List<GameObject> spawnedEnemies = new();
        private Vector2 panelScroll;

        public IReadOnlyList<ItemLoadoutEntry> ItemLoadout => itemLoadout;
        public IReadOnlyList<EnemyPlacement> EnemyPlacements => enemyPlacements;
        public Vector2 RoomSize => roomSize;
        public bool HasAppliedLoadout { get; private set; }
        public int SpawnedEnemyCount => spawnedEnemies.Count(enemy => enemy != null);
        public HealthPickup HealthPickupPrefab => healthPickupPrefab;
        public IReadOnlyList<RunResourcePickup> ResourcePickupPrefabs => resourcePickupPrefabs;
        public int CurrentFloor => runProgress != null ? Mathf.Max(1, runProgress.CurrentFloor) : 1;
        public BossController ActiveBoss => spawnedEnemies
            .Where(enemy => enemy != null)
            .Select(enemy => enemy.GetComponent<BossController>())
            .FirstOrDefault(boss => boss != null && boss.Health != null && !boss.Health.IsDead);

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
                return;
            }

            if (applyLoadoutOnStart)
            {
                ApplyConfiguredLoadout();
            }

            EnsureTestFloorInitialized();

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

        public void SetHealthPickupPrefab(HealthPickup prefab)
        {
            healthPickupPrefab = prefab;
        }

        public void SetResourcePickupPrefabs(RunResourcePickup[] prefabs)
        {
            resourcePickupPrefabs = prefabs ?? Array.Empty<RunResourcePickup>();
        }

        public RunResourcePickup SpawnResourcePickup(RunResourceType type)
        {
            RunResourcePickup prefab = resourcePickupPrefabs.FirstOrDefault(
                candidate => candidate != null && candidate.ResourceType == type);
            ResolveRunProgress();
            if (prefab == null || playerHealth == null || runProgress == null)
            {
                return null;
            }

            Vector3 position = playerHealth.transform.position + Vector3.right * 1.5f;
            RunResourcePickup pickup = Instantiate(prefab, position, Quaternion.identity, transform);
            pickup.BindRunProgress(runProgress);
            return pickup;
        }

        public void DamagePlayerOneAndHalfHearts()
        {
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.TakeDamage(3f);
            }
        }

        public HealthPickup SpawnHealthPickup()
        {
            if (healthPickupPrefab == null || playerHealth == null)
            {
                return null;
            }

            Vector3 position = playerHealth.transform.position + Vector3.right * 1.5f;
            return Instantiate(healthPickupPrefab, position, Quaternion.identity, transform);
        }

        public void AdvanceTestFloor()
        {
            ResolveRunProgress();
            runProgress?.RecordRoomEntry(CurrentFloor + 1, 1);
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

        private void DrawRunResourceControls()
        {
            ResolveRunProgress();
            if (runProgress == null)
            {
                return;
            }

            GUILayout.Label(
                $"ELIF {runProgress.GetResourceCount(RunResourceType.Elif)}  " +
                $"KEY {runProgress.GetResourceCount(RunResourceType.Key)}  " +
                $"BOMB {runProgress.GetResourceCount(RunResourceType.Bomb)}  (max {RunResourceWallet.MaxCount})",
                GUI.skin.box);
            GUILayout.BeginHorizontal();
            foreach (RunResourceType type in new[] { RunResourceType.Elif, RunResourceType.Key, RunResourceType.Bomb })
            {
                GUI.enabled = resourcePickupPrefabs.Any(prefab => prefab != null && prefab.ResourceType == type);
                if (GUILayout.Button($"Spawn {type}"))
                {
                    SpawnResourcePickup(type);
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("+98 All"))
            {
                runProgress.TryAddResource(RunResourceType.Elif, 98);
                runProgress.TryAddResource(RunResourceType.Key, 98);
                runProgress.TryAddResource(RunResourceType.Bomb, 98);
            }
            GUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            if (!showDebugPanel || playerInventory == null || playerHealth == null || playerStats == null)
            {
                return;
            }

            const float width = 360f;
            Rect area = new(Screen.width - width - 10f, 10f, width, Mathf.Min(Screen.height - 20f, 720f));
            GUILayout.BeginArea(area, "Item Test Room", GUI.skin.window);
            BossController activeBoss = ActiveBoss;
            string bossVitals = activeBoss == null
                ? "BOSS HP --"
                : $"BOSS {activeBoss.DisplayName}  HP {activeBoss.Health.CurrentHealth:0.#}/{activeBoss.Health.MaxHealth:0.#}";
            GUILayout.Label(
                $"FLOOR {CurrentFloor}  PLAYER HP {playerHealth.CurrentHealth:0.#}/{playerHealth.MaxHealth:0.#}  Shield {playerHealth.CurrentShield:0.#}\n" +
                bossVitals + "\n" +
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HP -1.5 Hearts"))
            {
                DamagePlayerOneAndHalfHearts();
            }
            GUI.enabled = healthPickupPrefab != null;
            if (GUILayout.Button("Spawn Heart"))
            {
                SpawnHealthPickup();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            DrawRunResourceControls();

            GUILayout.Label("Items / Spells — +1은 현재 Play에서 즉시 획득", GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            foreach (ItemLoadoutEntry entry in itemLoadout)
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
            GUILayout.Label("생명의 보석: HP 25% → 회복 확인 → Next Floor → 다시 HP 25%.\n스택 초기화는 Play Mode를 다시 시작하세요.");
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
