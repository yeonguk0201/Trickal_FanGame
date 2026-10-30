using System;
using System.Collections.Generic;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class EncounterRuntimeWave
    {
        private readonly GameObject[] enemyPrefabs;
        private readonly Transform[] spawnPoints;
        private readonly EnemyVariantKind[] variants;

        // Variant-0: variants are optional and line up with the prefabs; missing entries are plain enemies.
        public EncounterRuntimeWave(GameObject[] configuredPrefabs, Transform[] configuredSpawnPoints,
            EnemyVariantKind[] configuredVariants = null)
        {
            enemyPrefabs = configuredPrefabs ?? Array.Empty<GameObject>();
            spawnPoints = configuredSpawnPoints ?? Array.Empty<Transform>();
            variants = configuredVariants ?? Array.Empty<EnemyVariantKind>();
        }

        public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;
        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
        public IReadOnlyList<EnemyVariantKind> Variants => variants;

        public EnemyVariantKind VariantAt(int index) =>
            index >= 0 && index < variants.Length ? variants[index] : EnemyVariantKind.None;

        public bool TryValidate(out string error)
        {
            if (enemyPrefabs.Length == 0 || enemyPrefabs.Length != spawnPoints.Length)
            {
                error = "An Encounter wave needs matching, non-empty enemy prefab and SpawnPoint lists.";
                return false;
            }

            if (variants.Length > enemyPrefabs.Length)
            {
                error = "An Encounter wave has more enemy variants than enemy prefabs.";
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
