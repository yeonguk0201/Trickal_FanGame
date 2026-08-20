using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
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
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Health[] preplacedEnemies = Array.Empty<Health>();
        [SerializeField] private DoorController[] doors = Array.Empty<DoorController>();

        private readonly Dictionary<Health, Action> enemyDeathHandlers = new();
        private Health playerHealth;

        public RoomState State { get; private set; } = RoomState.Waiting;
        public int AliveEnemyCount => enemyDeathHandlers.Count;
        public bool HasStarted { get; private set; }
        public bool IsProgressionStopped { get; private set; }

        public event Action<RoomState> StateChanged;

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
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Transform>();
            doors = configuredDoors ?? Array.Empty<DoorController>();
            SetDoorsLocked(false);
        }

        public void ConfigurePreplacedEnemies(Health[] configuredPreplacedEnemies)
        {
            preplacedEnemies = configuredPreplacedEnemies ?? Array.Empty<Health>();
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

            HasStarted = true;
            playerHealth = enteringPlayerHealth;
            playerHealth.Died += OnPlayerDied;
            runProgress?.RecordRoomEntry(floorNumber, roomNumber);

            ChangeState(RoomState.Combat);
            SetDoorsLocked(true);
            ActivatePreplacedEnemies();
            SpawnConfiguredEnemies();

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
                enemy.ResetHealth();
                RegisterEnemy(enemy);
            }
        }

        private void SpawnConfiguredEnemies()
        {
            if (enemyPrefab == null)
            {
                return;
            }

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
                Health enemyHealth = enemy.GetComponent<Health>();
                if (enemyHealth == null)
                {
                    Debug.LogError($"{name}: Enemy prefab must have a Health component.", enemy);
                    Destroy(enemy);
                    continue;
                }

                RegisterEnemy(enemyHealth);
            }
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
                ClearRoom();
            }
        }

        private void ClearRoom()
        {
            ChangeState(RoomState.Cleared);
            SetDoorsLocked(false);
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
