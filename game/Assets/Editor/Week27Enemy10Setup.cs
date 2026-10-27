using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Enemy-10 (user decisions, 2026-10-24 and 2026-10-27, docs/28 §8): 자폭형 chases the player, stops within 1.2
    // and warns for one second, then blows itself up. Whatever kills it, the same blast goes off where it fell
    // (a full second later, or with the time left when it was already warning). The blast reaches 1.5, hurts the
    // player at the medium tier of the floor, hurts other enemies like a bomb (30) and breaks obstacles like a bomb.
    // It first appears on floor 2, scores threat 3 and joins the pressure and swarm melee slots at weight 15.
    // The user's drawing is not ready, so it borrows the chocolate crumb minion drawing for now. Re-running updates
    // the same Prefab and assets and keeps their GUIDs.
    public static class Week27Enemy10Setup
    {
        public const int EncounterContentVersion = 17;
        public const string PrefabPath = "Assets/Prefabs/SelfDestructFairy.prefab";
        public const string PrefabName = "SelfDestructFairy";
        // Working name until the user names it.
        public const string DisplayName = "자폭형";
        // Placeholder drawing until the user's art (docs/28 §7).
        public const string ArtworkId = "crumb-minion-chocolate";
        public const string ArtworkPath = FairyKingdomArtworkSetup.ArtRoot + "/Enemies/" + ArtworkId + ".png";
        public const float ArtworkWidth = 1.2f;

        // Implementation defaults (2026-10-27): lighter and a little quicker than the chaser so it closes in.
        public const float MaxHealth = 30f;
        public const float MoveSpeed = 2.8f;
        public const float KnockbackWeight = 0.8f;
        public const float DetectionRange = 8f;
        public const float StopDistance = 0f;
        public const float TriggerRange = SelfDestructEnemy.DefaultTriggerRange;

        // User decisions (2026-10-24, 2026-10-27).
        public const float TelegraphDuration = 1f;
        public const float BlastRadius = 1.5f;
        public const EnemyDamageTier BlastDamageTier = EnemyDamageTier.Medium;
        public const float BlastEnemyDamage = PlacedBomb.DefaultEnemyDamage;

        public const int Threat = 3;
        public const int MeleeWeight = 15;

        // Pressure melee slots: Enemy-9's 50/25/15/10 plus 자폭형.
        public static EncounterEnemyCandidate[] PressureMeleeCandidates() => Week25Enemy9Setup
            .PressureMeleeCandidates()
            .Append(new EncounterEnemyCandidate(EncounterEnemyRole.SelfDestruct, MeleeWeight)).ToArray();

        // Swarm melee slots: Enemy-8's 60/25/15 plus 자폭형. Threat 3 like 산사모-활발 keeps the swarm ranges.
        public static EncounterEnemyCandidate[] SwarmMeleeCandidates() => Week25Enemy8Setup.MeleeCandidates()
            .Append(new EncounterEnemyCandidate(EncounterEnemyRole.SelfDestruct, MeleeWeight)).ToArray();

        [MenuItem("Trickal Fan Game/Week 27/Setup Enemy-10 Self Destruct")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-10 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Curve1Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-10 requires the completed Curve-1 Game Scene.");
            }

            GameObject prefab = EnsurePrefab();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);

            // Spawn-2 owns pressure and crossfire, Encounter-4 owns swarm and elite-pair. Both read the Enemy-10
            // melee lists, and only their declared ranges are restored here (they are unchanged by threat 3).
            EncounterDefinition[] updated = Week19Spawn2Setup.ConfigureEncounters()
                .Concat(Week19Encounter4Setup.ConfigureEncounters()).ToArray();
            foreach (EncounterDefinition definition in updated.Where(Week25Enemy9Setup.IsPressure))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.PressureMinimumThreat,
                    Week19Difficulty1Setup.PressureMaximumThreat);
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out string error) ||
                    !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                {
                    throw new InvalidOperationException($"Enemy-10 catalog is invalid. {error}");
                }

                EditorUtility.SetDirty(definition);
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-10 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-10 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-10 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health and chases at " +
                      $"{MoveSpeed}; within {TriggerRange} it warns {TelegraphDuration} s and blows up (radius " +
                      $"{BlastRadius}, {BlastDamageTier} damage to the player, {BlastEnemyDamage} to enemies, " +
                      "obstacles break like a bomb), and any death leaves the same blast. Threat " +
                      $"{Threat}, floor {EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.SelfDestruct)}+, " +
                      $"pressure and swarm melee weight {MeleeWeight}, Encounter content version " +
                      $"{generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() => new[] { PrefabPath };

        public static bool IsSwarm(EncounterDefinition definition) =>
            definition != null && definition.EncounterId.EndsWith("-swarm-v1", StringComparison.Ordinal) &&
            !definition.EncounterId.EndsWith("-jyubi-swarm-v1", StringComparison.Ordinal);

        public static GameObject EnsurePrefab()
        {
            GameObject contents = LoadFromChaser();
            try
            {
                contents.transform.localScale = Vector3.one;
                // The chaser's drawn frames belong to another character, and 자폭형 only hurts by exploding.
                Remove<EnemyAttackArtwork>(contents);
                Remove<EnemyMovementAnimator>(contents);
                Remove<MeleeEnemyAttack>(contents);
                Remove<EnemyAttackPresentation>(contents);
                Remove<ContactDamage>(contents);

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                contents.GetComponent<KnockbackReceiver>().ConfigureWeight(KnockbackWeight);
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                    contents.AddComponent<EnemyBehaviorContext>();
                contents.GetComponent<EnemyChase>().Configure(MoveSpeed, DetectionRange, StopDistance);

                EnemyDeathExplosion explosion = contents.GetComponent<EnemyDeathExplosion>();
                if (explosion == null) explosion = contents.AddComponent<EnemyDeathExplosion>();
                explosion.Configure(TelegraphDuration, BlastRadius, HealthUnits.GetEnemyDamageUnits(BlastDamageTier, 1));
                explosion.ConfigureDamageTier(true, BlastDamageTier);
                explosion.ConfigureEnemyAndObstacleHits(true, BlastEnemyDamage);
                SelfDestructEnemy controller = contents.GetComponent<SelfDestructEnemy>();
                if (controller == null) controller = contents.AddComponent<SelfDestructEnemy>();
                controller.Configure(TriggerRange);

                AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
                FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
                if (!FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog))
                    throw new InvalidOperationException($"Enemy-10 could not bind '{ArtworkId}' to {PrefabPath}.");
                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                    throw new InvalidOperationException($"Could not save {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // Starts as an unpacked copy of the chaser, so later chaser tuning does not leak into it.
        private static GameObject LoadFromChaser()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Enemy-10 needs {Week15Enemy0Setup.ChaserPrefabPath}.");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    instance.name = PrefabName;
                    if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                        throw new InvalidOperationException($"Could not create {PrefabPath}.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            contents.name = PrefabName;
            return contents;
        }

        private static RoomDifficultyTable ConfigureThreat(RoomDifficultyTable table)
        {
            Undo.RecordObject(table, "Configure Enemy-10 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.SelfDestruct, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-10 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-10 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-10 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.SelfDestruct)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.SelfDestruct, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-10 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
