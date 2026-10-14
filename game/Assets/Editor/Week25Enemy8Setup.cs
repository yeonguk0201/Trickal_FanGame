using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Enemy-8 (user decision, 2026-10-09): 저혈당 요정-활발 moves one way for 1.2 seconds (random, leaning toward the
    // player), stops and warns for 0.4 seconds, fires light shots up, down, left and right (speed 5 for 1.2 seconds,
    // range 6) and recovers for 0.6 seconds. Health 30, knockback weight 1, threat 3 on melee pressure SpawnPoints.
    // It joins the melee slots of the pressure and swarm Encounters (user decision, 2026-10-14). Re-running updates the
    // same Prefab and assets and keeps their GUIDs.
    public static class Week25Enemy8Setup
    {
        public const int EncounterContentVersion = 14;
        public const string PrefabPath = "Assets/Prefabs/LivelyLowBloodSugarFairy.prefab";
        public const string PrefabName = "LivelyLowBloodSugarFairy";
        public const string DisplayName = "저혈당 요정-활발";
        public const string ArtworkId = "low-blood-sugar-fairy";
        public const string ArtworkPath = FairyKingdomArtworkSetup.ArtRoot + "/Enemies/" + ArtworkId + ".png";
        // Placeholder bread drawn on the green onion's 512 canvas at the same scale until the user's art arrives.
        public const string ProjectilePath = EnemyAttackAnimationSetup.Folder + "/LivelyFairyBread_Projectile.png";

        public const float MaxHealth = 30f;
        public const float KnockbackWeight = 1f;
        public const float MoveSpeed = 2.5f;
        public const float DetectionRange = 12f;
        public const float MoveDuration = 1.2f;
        public const float TelegraphDuration = 0.4f;
        public const float RecoveryDuration = 0.6f;
        public const float ProjectileSpeed = 5f;
        public const float ProjectileLifetime = 1.2f;
        public const EnemyDamageTier ProjectileDamageTier = EnemyDamageTier.Light;
        public const int Threat = 3;

        // Melee slots of the pressure and swarm Encounters. 저혈당 요정-활발 scores 3 like 산사모-활발, so the
        // declared threat ranges stay as they were.
        public static EncounterEnemyCandidate[] MeleeCandidates() => new[]
        {
            new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 60),
            new EncounterEnemyCandidate(EncounterEnemyRole.FastChaser, 25),
            new EncounterEnemyCandidate(EncounterEnemyRole.FourWayShooter, 15),
        };

        [MenuItem("Trickal Fan Game/Week 25/Setup Enemy-8 Lively Low Blood Sugar Fairy")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-8 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week25Enemy7Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-8 requires the completed Enemy-7 Game Scene.");
            }

            GameObject prefab = EnsurePrefab();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);
            EncounterDefinition[] updated = Week19Spawn2Setup.ConfigureEncounters()
                .Concat(Week19Encounter4Setup.ConfigureEncounters()).ToArray();
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidateThreat(table, out string error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                {
                    throw new InvalidOperationException($"Enemy-8 catalog is invalid. {error}");
                }
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-8 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-8 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-8 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health, moves {MoveDuration} s at " +
                      $"{MoveSpeed}, warns {TelegraphDuration} s, fires four light shots at speed {ProjectileSpeed} " +
                      $"for {ProjectileLifetime} s and recovers {RecoveryDuration} s. Threat {Threat}; pressure and " +
                      "swarm melee slots draw it 15 in 100, and the Encounter content version is " +
                      $"{generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() => new[] { PrefabPath, ProjectilePath };

        public static GameObject EnsurePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week14Encounter2Setup.RangedPrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Enemy-8 needs {Week14Encounter2Setup.RangedPrefabPath}.");
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

            Sprite projectile = ImportProjectile();
            AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
            FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                contents.name = PrefabName;
                Remove<RangedEnemyController>(contents);
                Remove<LongRangeSniperController>(contents);
                Remove<MobileRangedEnemyController>(contents);
                Remove<EnemyAttackArtwork>(contents);
                Remove<EnemyMovementAnimator>(contents);
                Remove<LineRenderer>(contents);

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                contents.GetComponent<KnockbackReceiver>().ConfigureWeight(KnockbackWeight);
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                    contents.AddComponent<EnemyBehaviorContext>();
                if (contents.GetComponent<EnemyAttackPresentation>() == null)
                    contents.AddComponent<EnemyAttackPresentation>();

                FourWayShooterController controller = contents.GetComponent<FourWayShooterController>();
                if (controller == null) controller = contents.AddComponent<FourWayShooterController>();
                controller.Configure(MoveSpeed, DetectionRange, MoveDuration, TelegraphDuration, RecoveryDuration,
                    ProjectileSpeed, ProjectileDamageTier, ProjectileLifetime);
                controller.ConfigureProjectileSprite(projectile);

                if (!FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog))
                    throw new InvalidOperationException($"Enemy-8 could not bind the '{ArtworkId}' drawing.");
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

        // Same import as the green onion, so the bread is drawn at the onion's size and its hitbox matches.
        private static Sprite ImportProjectile()
        {
            AssetDatabase.ImportAsset(ProjectilePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(ProjectilePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Enemy-8 projectile: " + ProjectilePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = EnemyAttackAnimationSetup.ProjectilePixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectilePath);
            if (sprite == null) throw new InvalidOperationException("Could not import " + ProjectilePath);
            return sprite;
        }

        private static RoomDifficultyTable ConfigureThreat(RoomDifficultyTable table)
        {
            Undo.RecordObject(table, "Configure Enemy-8 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.FourWayShooter, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-8 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-8 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-8 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.FourWayShooter)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.FourWayShooter, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-8 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
