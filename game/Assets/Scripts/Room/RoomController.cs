using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RoomController : MonoBehaviour
    {
        [Header("Progress")]
        [SerializeField, Min(1)] private int floorNumber = 1;
        [SerializeField, Min(1)] private int roomNumber = 1;
        [SerializeField] private RunProgress runProgress;

        [Header("Encounter")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject[] enemyPrefabs = Array.Empty<GameObject>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Health[] preplacedEnemies = Array.Empty<Health>();
        [SerializeField] private DoorController[] doors = Array.Empty<DoorController>();

        private readonly Dictionary<Health, Action> enemyDeathHandlers = new();
        private EncounterRuntimeWave[] encounterWaves = Array.Empty<EncounterRuntimeWave>();
        private Health playerHealth;
        private RoomRunState runState;
        private RoomClearRewardSpawner clearRewardSpawner;

        public RoomState State { get; private set; } = RoomState.Waiting;
        public int AliveEnemyCount => enemyDeathHandlers.Count;
        public bool HasStarted { get; private set; }
        public bool IsProgressionStopped { get; private set; }
        public int FloorNumber => floorNumber;
        public int RoomNumber => roomNumber;
        public int CurrentWaveNumber { get; private set; }
        public int WaveCount => encounterWaves.Length;
        public IReadOnlyList<EncounterRuntimeWave> EncounterWaves => encounterWaves;
        public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public IReadOnlyList<Health> PreplacedEnemies => preplacedEnemies;

        public event Action<RoomState> StateChanged;
        public event Action<GameObject> EnemySpawned;
        public event Action<int> WaveStarted;
        public event Action<int> WaveCompleted;

        public void BindRunState(RoomRunState configuredState, bool startsCleared)
        {
            runState = configuredState;
            if (runState != null && (runState.IsCleared || startsCleared))
            {
                if (startsCleared && !runState.IsCleared) runState.MarkPreCleared();
                HasStarted = true;
                State = RoomState.Cleared;
                SetDoorsLocked(false);
            }
        }

        public void Configure(
            int configuredFloorNumber,
            int configuredRoomNumber,
            RunProgress configuredRunProgress,
            GameObject configuredEnemyPrefab,
            Transform[] configuredSpawnPoints,
            DoorController[] configuredDoors)
        {
            floorNumber = Mathf.Max(1, configuredFloorNumber);
            roomNumber = Mathf.Max(1, configuredRoomNumber);
            runProgress = configuredRunProgress;
            enemyPrefab = configuredEnemyPrefab;
            enemyPrefabs = Array.Empty<GameObject>();
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Transform>();
            doors = configuredDoors ?? Array.Empty<DoorController>();
            SetDoorsLocked(false);
        }

        public void ConfigurePreplacedEnemies(Health[] configuredPreplacedEnemies)
        {
            preplacedEnemies = configuredPreplacedEnemies ?? Array.Empty<Health>();
        }

        public void ConfigureEnemyPrefabs(GameObject[] configuredEnemyPrefabs)
        {
            enemyPrefab = null;
            enemyPrefabs = configuredEnemyPrefabs ?? Array.Empty<GameObject>();
            encounterWaves = Array.Empty<EncounterRuntimeWave>();
        }

        public void ConfigureEnemyPrefabs(GameObject[] configuredEnemyPrefabs,
            Transform[] configuredSpawnPoints)
        {
            ConfigureEnemyPrefabs(configuredEnemyPrefabs);
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Transform>();
            encounterWaves = enemyPrefabs.Length > 0
                ? new[] { new EncounterRuntimeWave(enemyPrefabs, spawnPoints) }
                : Array.Empty<EncounterRuntimeWave>();
        }

        public void ConfigureEncounterWaves(EncounterRuntimeWave[] configuredWaves)
        {
            enemyPrefab = null;
            encounterWaves = configuredWaves ?? Array.Empty<EncounterRuntimeWave>();
            if (encounterWaves.Length > 0 && encounterWaves[0] != null)
            {
                enemyPrefabs = new GameObject[encounterWaves[0].EnemyPrefabs.Count];
                spawnPoints = new Transform[encounterWaves[0].SpawnPoints.Count];
                for (int index = 0; index < enemyPrefabs.Length; index++)
                {
                    enemyPrefabs[index] = encounterWaves[0].EnemyPrefabs[index];
                    spawnPoints[index] = encounterWaves[0].SpawnPoints[index];
                }
            }
            else
            {
                enemyPrefabs = Array.Empty<GameObject>();
                spawnPoints = Array.Empty<Transform>();
            }
        }

        public void ConfigureClearReward(RoomClearRewardSpawner configuredSpawner)
        {
            clearRewardSpawner = configuredSpawner;
        }

        public bool TryValidateEncounterConfiguration(out string error)
        {
            if (encounterWaves.Length > 0)
            {
                for (int index = 0; index < encounterWaves.Length; index++)
                {
                    if (encounterWaves[index] == null)
                    {
                        error = $"Encounter wave {index + 1} is missing.";
                        return false;
                    }
                    if (!encounterWaves[index].TryValidate(out error))
                    {
                        error = $"Encounter wave {index + 1} is invalid. {error}";
                        return false;
                    }
                }

                error = null;
                return true;
            }

            if (enemyPrefab != null && enemyPrefabs.Length > 0)
            {
                error = "A room cannot use both one repeated prefab and a per-spawn prefab list.";
                return false;
            }

            if (enemyPrefabs.Length > 0 && enemyPrefabs.Length != spawnPoints.Length)
            {
                error = $"Per-spawn enemy prefab count {enemyPrefabs.Length} does not match spawn point count {spawnPoints.Length}.";
                return false;
            }

            foreach (GameObject configuredPrefab in enemyPrefabs)
            {
                if (configuredPrefab == null || configuredPrefab.GetComponent<Health>() == null)
                {
                    error = "Every configured enemy prefab must exist and contain Health.";
                    return false;
                }
            }

            if (enemyPrefabs.Length > 0)
            {
                foreach (Transform spawnPoint in spawnPoints)
                {
                    if (spawnPoint == null)
                    {
                        error = "Per-spawn enemy configurations cannot contain a missing spawn point.";
                        return false;
                    }
                }
            }

            if (enemyPrefab != null && enemyPrefab.GetComponent<Health>() == null)
            {
                error = "The repeated enemy prefab must contain Health.";
                return false;
            }

            error = null;
            return true;
        }

        private void Awake()
        {
            Collider2D entryTrigger = GetComponent<Collider2D>();
            entryTrigger.isTrigger = true;

            foreach (Health enemy in preplacedEnemies)
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                }
            }

            SetDoorsLocked(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlayer();

            foreach (KeyValuePair<Health, Action> entry in enemyDeathHandlers)
            {
                if (entry.Key != null)
                {
                    entry.Key.Died -= entry.Value;
                }
            }

            enemyDeathHandlers.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player == null)
            {
                return;
            }

            BeginCombat(player.GetComponent<Health>());
        }

        public void BeginCombat(Health enteringPlayerHealth)
        {
            if (HasStarted || enteringPlayerHealth == null || enteringPlayerHealth.IsDead)
            {
                return;
            }

            if (!TryValidateEncounterConfiguration(out string error))
            {
                Debug.LogError($"{name}: Invalid encounter configuration. {error}", this);
                return;
            }

            HasStarted = true;
            playerHealth = enteringPlayerHealth;
            playerHealth.Died += OnPlayerDied;
            runProgress?.RecordRoomEntry(floorNumber, roomNumber);
            runState?.MarkVisited();

            ChangeState(RoomState.Combat);
            SetDoorsLocked(true);
            ActivatePreplacedEnemies();
            if (encounterWaves.Length > 0)
            {
                int nextWaveIndex = runState != null ? runState.CompletedWaveCount : 0;
                if (nextWaveIndex < encounterWaves.Length) StartWave(nextWaveIndex);
            }
            else
            {
                SpawnConfiguredEnemies();
            }

            if (AliveEnemyCount == 0)
            {
                ClearRoom();
            }
        }

        public void RegisterEnemy(Health enemyHealth)
        {
            if (enemyHealth == null || enemyHealth.IsDead || enemyDeathHandlers.ContainsKey(enemyHealth))
            {
                return;
            }

            Action deathHandler = () => OnEnemyDied(enemyHealth);
            enemyDeathHandlers.Add(enemyHealth, deathHandler);
            enemyHealth.Died += deathHandler;
        }

        private void ActivatePreplacedEnemies()
        {
            foreach (Health enemy in preplacedEnemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                enemy.gameObject.SetActive(true);
                FloorDifficultyScaler.ApplyScaling(enemy.gameObject, floorNumber);
                enemy.ResetHealth();
                AlertEnemy(enemy);
                RegisterEnemy(enemy);
            }
        }

        private void SpawnConfiguredEnemies()
        {
            if (enemyPrefabs.Length > 0)
            {
                for (int index = 0; index < spawnPoints.Length; index++)
                {
                    SpawnEnemy(enemyPrefabs[index], spawnPoints[index]);
                }

                return;
            }

            if (enemyPrefab == null)
            {
                return;
            }

            foreach (Transform spawnPoint in spawnPoints)
            {
                SpawnEnemy(enemyPrefab, spawnPoint);
            }
        }

        private void StartWave(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= encounterWaves.Length || AliveEnemyCount > 0) return;

            EncounterRuntimeWave wave = encounterWaves[waveIndex];
            CurrentWaveNumber = waveIndex + 1;
            WaveStarted?.Invoke(CurrentWaveNumber);
            for (int index = 0; index < wave.EnemyPrefabs.Count; index++)
                SpawnEnemy(wave.EnemyPrefabs[index], wave.SpawnPoints[index]);
        }

        private void SpawnEnemy(GameObject configuredPrefab, Transform spawnPoint)
        {
            if (configuredPrefab == null || spawnPoint == null)
            {
                return;
            }

            GameObject enemy = Instantiate(configuredPrefab, spawnPoint.position, spawnPoint.rotation, transform);
            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null)
            {
                Debug.LogError($"{name}: Enemy prefab must have a Health component.", enemy);
                Destroy(enemy);
                return;
            }

            FloorDifficultyScaler.ApplyScaling(enemy, floorNumber);
            EnemySpawned?.Invoke(enemy);
            AlertEnemy(enemyHealth);
            RegisterEnemy(enemyHealth);
        }

        private void AlertEnemy(Health enemyHealth)
        {
            EnemyBehaviorContext behavior = enemyHealth != null
                ? enemyHealth.GetComponent<EnemyBehaviorContext>()
                : null;
            behavior?.BeginCombat(playerHealth != null ? playerHealth.transform : null);
        }

        private void OnEnemyDied(Health enemyHealth)
        {
            if (!enemyDeathHandlers.Remove(enemyHealth, out Action deathHandler))
            {
                return;
            }

            enemyHealth.Died -= deathHandler;
            runProgress?.RecordKill();

            if (State == RoomState.Combat && !IsProgressionStopped && AliveEnemyCount == 0)
            {
                CompleteCurrentWaveOrRoom();
            }
        }

        private void CompleteCurrentWaveOrRoom()
        {
            if (encounterWaves.Length == 0)
            {
                ClearRoom();
                return;
            }

            int completedWave = CurrentWaveNumber;
            if (completedWave < 1) return;
            bool newlyCompleted = runState == null || runState.TryMarkWaveCompleted(completedWave);
            if (!newlyCompleted) return;

            WaveCompleted?.Invoke(completedWave);
            if (completedWave < encounterWaves.Length)
            {
                StartWave(completedWave);
                return;
            }

            ClearRoom();
        }

        private void ClearRoom()
        {
            if (State == RoomState.Cleared) return;
            runState?.MarkCleared();
            ChangeState(RoomState.Cleared);
            SetDoorsLocked(false);
            clearRewardSpawner?.TrySpawn();
            UnsubscribeFromPlayer();
        }

        private void OnPlayerDied()
        {
            IsProgressionStopped = true;
            runProgress?.StopProgression();
            SetDoorsLocked(true);
            UnsubscribeFromPlayer();
        }

        private void UnsubscribeFromPlayer()
        {
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.Died -= OnPlayerDied;
            playerHealth = null;
        }

        private void SetDoorsLocked(bool isLocked)
        {
            foreach (DoorController door in doors)
            {
                door?.SetLocked(isLocked);
            }
        }

        private void ChangeState(RoomState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            State = nextState;
            StateChanged?.Invoke(State);
            Debug.Log($"{name}: Room state changed to {State}.", this);
        }
    }
}
