using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    // Enemy-7 (user decision, 2026-10-09): 쥬비 also appears outside 셰이디의 랜덤박스 through one jyubi-swarm
    // Encounter per profile (threat 1 each), and a 쫄따구 that 부스러기 summoned leaves a blast where it dies. The
    // renamed enemies keep their Prefab names and stable IDs; only the display names in EnemyDisplayNames changed.
    // Re-running updates the same assets and keeps their GUIDs.
    public static class Week25Enemy7Setup
    {
        public const int EncounterContentVersion = 13;
        public const string Pattern = "jyubi-swarm";
        public const int JyubiThreat = 1;

        // The Prefab file name is the stable key. Renamed (2026-10-09): 산사모 → 산사모-활발, the moving ranged
        // enemy → 산사모-순수, 저혈당 요정 → 저혈당 요정-순수.
        public static readonly IReadOnlyDictionary<string, string> EnemyDisplayNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SansamoEnemy"] = "산사모-활발",
                ["QuickRangedFairy"] = "산사모-순수",
                ["ChargingEnemy"] = "저혈당 요정-순수",
            };

        [MenuItem("Trickal Fan Game/Week 25/Setup Enemy-7 Jyubi Swarm and Crumb Blast")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-7 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week19Encounter4Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-7 requires the completed Encounter-4 Game Scene.");
            }

            ConfigureCrumbMinion();
            ConfigureBuseureogiDefeat();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster();
            EncounterDefinition[] added = ConfigureEncounters();
            HashSet<string> addedIds = new(added.Select(definition => definition.EncounterId), StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !addedIds.Contains(definition.EncounterId))
                .Concat(added)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidateThreat(table, out string error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                {
                    throw new InvalidOperationException($"Enemy-7 catalog is invalid. {error}");
                }
            }

            Undo.RecordObjects(new UnityEngine.Object[] { generator, assembler }, "Configure Enemy-7 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-7 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-7 ready: every profile adds a {Pattern} Encounter of threat-{JyubiThreat} Jyubi " +
                      "(Small, Basic, Wide 3+3 / Tall, Large 4+3), the crumb minion leaves a " +
                      $"{EnemyDeathExplosion.DefaultTelegraphDuration}-second telegraphed radius-" +
                      $"{EnemyDeathExplosion.DefaultRadius} blast for one heart, and the Encounter content version " +
                      $"is {generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() => Week19Encounter4Setup.Swarms
            .Select(spec => Week19Spawn2Setup.EncounterPath(spec.ProfileId, Pattern)).ToArray();

        private static void ConfigureCrumbMinion()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss1Setup.MinionPrefabPath);
            try
            {
                EnemyDeathExplosion explosion = contents.GetComponent<EnemyDeathExplosion>();
                if (explosion == null) explosion = contents.AddComponent<EnemyDeathExplosion>();
                explosion.Configure(EnemyDeathExplosion.DefaultTelegraphDuration, EnemyDeathExplosion.DefaultRadius,
                    EnemyDeathExplosion.DefaultDamageUnits);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss1Setup.MinionPrefabPath) == null)
                    throw new InvalidOperationException("Enemy-7 could not save the crumb minion Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // 부스러기's 쫄따구, obstacles and projectiles stay after it is defeated (user decision, 2026-10-13).
        private static void ConfigureBuseureogiDefeat()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Week15Boss0Setup.BossPrefabPath);
            try
            {
                BossController boss = contents.GetComponent<BossController>();
                if (boss == null || contents.GetComponent<BuseureogiBossPatternRuntime>() == null)
                    throw new InvalidOperationException("Enemy-7 requires the Boss-1 Buseureogi Prefab.");
                boss.ConfigureDefeat(true);
                if (PrefabUtility.SaveAsPrefabAsset(contents, Week15Boss0Setup.BossPrefabPath) == null)
                    throw new InvalidOperationException("Enemy-7 could not save the Buseureogi boss Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static RoomDifficultyTable ConfigureThreat(RoomDifficultyTable table)
        {
            Undo.RecordObject(table, "Configure Enemy-7 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) || !table.TryGetThreat(EncounterEnemyRole.Jyubi, out int threat) ||
                threat != JyubiThreat)
            {
                throw new InvalidOperationException($"Enemy-7 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster()
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            GameObject jyubi = AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath);
            if (roster == null || jyubi == null)
                throw new InvalidOperationException("Enemy-7 requires the enemy roster and the Enemy-6 Jyubi Prefab.");

            Undo.RecordObject(roster, "Configure Enemy-7 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.Jyubi)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.Jyubi, jyubi)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-7 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        // Same size as the melee swarm, and the same pressure points, but nothing other than 쥬비.
        private static EncounterDefinition[] ConfigureEncounters()
        {
            List<EncounterDefinition> definitions = new();
            foreach (Week19Encounter4Setup.SwarmSpec spec in Week19Encounter4Setup.Swarms)
            {
                Week19Spawn2Setup.ProfileSpec profileSpec = Week19Spawn2Setup.Profiles.Single(candidate =>
                    candidate.ProfileId == spec.ProfileId);
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(profileSpec.ProfilePath);
                if (profile == null || profile.ProfileId != spec.ProfileId)
                    throw new InvalidOperationException($"Enemy-7 is missing Room Profile '{spec.ProfileId}'.");

                string path = Week19Spawn2Setup.EncounterPath(spec.ProfileId, Pattern);
                EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Undo.RecordObject(definition, "Configure Enemy-7 Encounter");
                definition.Configure(Week19Spawn2Setup.EncounterId(spec.ProfileId, Pattern), new[] { profile }, 1, 3,
                    Week14Encounter1Setup.MinimumPlayerDistance,
                    Week14Encounter1Setup.MinimumDoorDistance,
                    new[] { Wave(1, spec.FirstWave), Wave(2, spec.SecondWave) },
                    EncounterClearCondition.AllWavesCleared);
                definition.ConfigureDeclaredThreat(spec.Total * JyubiThreat, spec.Total * JyubiThreat);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            return definitions.ToArray();
        }

        private static EncounterWaveDefinition Wave(int number, int count) =>
            new(number,
                number == 1 ? EncounterWaveStartCondition.RoomEntered : EncounterWaveStartCondition.PreviousWaveCleared,
                EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                new[]
                {
                    new EncounterSpawnRule(new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Jyubi, 100) },
                        count, null, "all"),
                });
    }
}
