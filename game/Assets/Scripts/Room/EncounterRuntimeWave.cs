using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class EncounterRuntimeWave
    {
        private readonly GameObject[] enemyPrefabs;
        private readonly Transform[] spawnPoints;

        public EncounterRuntimeWave(GameObject[] configuredPrefabs, Transform[] configuredSpawnPoints)
        {
            enemyPrefabs = configuredPrefabs ?? Array.Empty<GameObject>();
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Transform>();
        }

        public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;

        public bool TryValidate(out string error)
        {
            if (enemyPrefabs.Length == 0 || enemyPrefabs.Length != spawnPoints.Length)
            {
                error = "An Encounter wave needs matching, non-empty enemy prefab and SpawnPoint lists.";
                return false;
            }

            for (int index = 0; index < enemyPrefabs.Length; index++)
            {
                if (enemyPrefabs[index] == null || enemyPrefabs[index].GetComponent<Health>() == null ||
                    spawnPoints[index] == null)
                {
                    error = $"Encounter wave entry {index + 1} needs an enemy prefab with Health and a SpawnPoint.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
