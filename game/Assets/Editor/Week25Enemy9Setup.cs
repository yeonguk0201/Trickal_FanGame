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
    // Enemy-9 (user decision, 2026-10-09): 누루링-요정전사 is a 40-health chaser with the chaser's melee lunge
    // (medium damage) that splits into two small 누루링 (10 health, speed 3.25, light contact damage) 0.5 to its
    // left and right whatever killed it. The splits never split again and wait out the shared 0.3-second spawn delay.
    // Threat 4 on melee pressure SpawnPoints, only in the pressure Encounters' melee slots (user decision,
    // 2026-10-14). The splits are drawn smaller than the body (user request, 2026-10-14). Re-running updates the same
    // Prefabs and assets and keeps their GUIDs.
    public static class Week25Enemy9Setup
    {
        public const int EncounterContentVersion = 15;
        public const string PrefabPath = "Assets/Prefabs/NururingFairyWarrior.prefab";
        public const string PrefabName = "NururingFairyWarrior";
        public const string DisplayName = "누루링-요정전사";
        public const string SplitPrefabPath = "Assets/Prefabs/NururingSplit.prefab";
        public const string SplitPrefabName = "NururingSplit";
        public const string SplitDisplayName = "작은 누루링";
        public const string ArtworkId = "nururing";
        public const string ArtworkPath = FairyKingdomArtworkSetup.ArtRoot + "/Enemies/" + ArtworkId + ".png";

        public const float MaxHealth = 40f;
        public const float MoveSpeed = 2.5f;
        public const float KnockbackWeight = 1.2f;
        public const EnemyDamageTier MeleeDamageTier = EnemyDamageTier.Medium;
        public const EnemyDamageTier ContactDamageTier = EnemyDamageTier.Light;
        // The drawing is wider than tall, so 1.3 wide stands about 1.05 tall, close to the other chasers.
        public const float ArtworkWidth = 1.3f;

        public const float SplitMaxHealth = 10f;
        // 3.75 in the first pass; slowed a little after the user's Play check (2026-10-14).
        public const float SplitMoveSpeed = 3.25f;
        public const float SplitKnockbackWeight = 0.7f;
        public const EnemyDamageTier SplitContactDamageTier = EnemyDamageTier.Light;
        public const float SplitContactCooldown = 1f;
        // The whole split is scaled: drawing, collider and contact reach are 70% of the body's.
        public const float SplitScale = 0.7f;
        public const int SplitCount = 2;
        public const float SplitSideOffset = 0.5f;

        public const float DetectionRange = 6f;
        public const float StopDistance = 0.8f;
        public const int Threat = 4;

        // Melee slots of the pressure Encounters. The swarm keeps Enemy-8's three candidates: six or seven 4-point
        // bodies would pass every floor cap.
        public static EncounterEnemyCandidate[] PressureMeleeCandidates() => new[]
        {
            new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 50),
            new EncounterEnemyCandidate(EncounterEnemyRole.FastChaser, 25),
            new EncounterEnemyCandidate(EncounterEnemyRole.FourWayShooter, 15),
            new EncounterEnemyCandidate(EncounterEnemyRole.SplittingChaser, 10),
        };

        [MenuItem("Trickal Fan Game/Week 25/Setup Enemy-9 Nururing Fairy Warrior")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-9 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week25Enemy8Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-9 requires the completed Enemy-8 Game Scene.");
            }

            GameObject split = EnsureSplitPrefab();
            GameObject prefab = EnsurePrefab(split);
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);
            EncounterDefinition[] updated = Week19Spawn2Setup.ConfigureEncounters();
            foreach (EncounterDefinition definition in updated.Where(IsPressure))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.PressureMinimumThreat,
                    Week19Difficulty1Setup.PressureMaximumThreat);
            // Spawn-2 also owns crossfire, whose declared range Difficulty-1 keeps. Only the IDs are replaced.
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
                    throw new InvalidOperationException($"Enemy-9 catalog is invalid. {error}");
                }

                EditorUtility.SetDirty(definition);
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-9 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-9 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-9 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health, chases at {MoveSpeed} " +
                      $"with a {MeleeDamageTier} melee lunge and splits into {SplitCount} {SplitDisplayName} " +
                      $"({SplitPrefabName}, {SplitMaxHealth} health, speed {SplitMoveSpeed}, scale {SplitScale}). " +
                      $"Threat {Threat}; pressure melee slots draw it 10 in 100, pressure declares " +
                      $"{Week19Difficulty1Setup.PressureMinimumThreat}~{Week19Difficulty1Setup.PressureMaximumThreat}, " +
                      $"and the Encounter content version is {generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() => new[] { PrefabPath, SplitPrefabPath };

        public static bool IsPressure(EncounterDefinition definition) =>
            definition != null && definition.EncounterId.EndsWith("-pressure-v1", StringComparison.Ordinal);

        public static GameObject EnsureSplitPrefab()
        {
            GameObject contents = LoadFromChaser(SplitPrefabPath, SplitPrefabName);
            try
            {
                contents.transform.localScale = Vector3.one * SplitScale;
                // A small 누루링 only rams the player: no melee swing.
                Remove<MeleeEnemyAttack>(contents);
                Remove<EnemyAttackPresentation>(contents);
                Remove<EnemySplitOnDeath>(contents);
                ConfigureCommon(contents, SplitMaxHealth, SplitKnockbackWeight);
                contents.GetComponent<EnemyChase>().Configure(SplitMoveSpeed, DetectionRange, 0f);
                contents.GetComponent<ContactDamage>().Configure(SplitContactDamageTier, SplitContactCooldown);
                Save(contents, SplitPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(SplitPrefabPath);
        }

        public static GameObject EnsurePrefab(GameObject split)
        {
            if (split == null) throw new InvalidOperationException("Enemy-9 needs the split Prefab first.");
            MeleeEnemyAttack chaserMelee = AssetDatabase.LoadAssetAtPath<GameObject>(
                Week15Enemy0Setup.ChaserPrefabPath).GetComponent<MeleeEnemyAttack>();
            ContactDamage chaserContact = AssetDatabase.LoadAssetAtPath<GameObject>(
                Week15Enemy0Setup.ChaserPrefabPath).GetComponent<ContactDamage>();
            if (chaserMelee == null || chaserContact == null)
                throw new InvalidOperationException("Enemy-9 needs the chaser's melee lunge and contact damage.");

            GameObject contents = LoadFromChaser(PrefabPath, PrefabName);
            try
            {
                contents.transform.localScale = Vector3.one;
                ConfigureCommon(contents, MaxHealth, KnockbackWeight);
                if (contents.GetComponent<EnemyAttackPresentation>() == null)
                    contents.AddComponent<EnemyAttackPresentation>();
                MeleeEnemyAttack melee = contents.GetComponent<MeleeEnemyAttack>();
                if (melee == null) melee = contents.AddComponent<MeleeEnemyAttack>();
                // The chaser's own lunge timing and reach, at medium damage.
                melee.Configure(chaserMelee.AttackRange, chaserMelee.TelegraphDuration, chaserMelee.ActiveDuration,
                    chaserMelee.RecoveryDuration, AttackCooldown(chaserMelee), MeleeDamageTier);
                melee.ConfigureLunge(chaserMelee.LungeTriggerRange, chaserMelee.LungeDistance,
                    chaserMelee.AimLockLeadTime, chaserMelee.LungeHitForwardOffset, chaserMelee.LungeHitRadius);
                contents.GetComponent<EnemyChase>().Configure(MoveSpeed, DetectionRange, StopDistance);
                contents.GetComponent<ContactDamage>().Configure(ContactDamageTier, chaserContact.Cooldown);

                EnemySplitOnDeath splitter = contents.GetComponent<EnemySplitOnDeath>();
                if (splitter == null) splitter = contents.AddComponent<EnemySplitOnDeath>();
                splitter.Configure(split, SplitCount, SplitSideOffset);
                Save(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // Both Prefabs start as an unpacked copy of the chaser, so later chaser tuning does not leak into them.
        private static GameObject LoadFromChaser(string path, string objectName)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChaserPrefabPath);
                if (source == null)
                    throw new InvalidOperationException($"Enemy-9 needs {Week15Enemy0Setup.ChaserPrefabPath}.");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                    instance.name = objectName;
                    if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                        throw new InvalidOperationException($"Could not create {path}.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            contents.name = objectName;
            return contents;
        }

        private static void ConfigureCommon(GameObject contents, float maxHealth, float knockbackWeight)
        {
            // The chaser's drawn walk and swing frames belong to another character.
            Remove<EnemyAttackArtwork>(contents);
            Remove<EnemyMovementAnimator>(contents);
            Health health = contents.GetComponent<Health>();
            SerializedObject serializedHealth = new(health);
            serializedHealth.FindProperty("maxHealth").floatValue = maxHealth;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            contents.GetComponent<KnockbackReceiver>().ConfigureWeight(knockbackWeight);
            if (contents.GetComponent<EnemyBehaviorContext>() == null)
                contents.AddComponent<EnemyBehaviorContext>();
        }

        private static void Save(GameObject contents, string path)
        {
            AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
            FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
            if (!FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog))
                throw new InvalidOperationException($"Enemy-9 could not bind the '{ArtworkId}' drawing to {path}.");
            if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new InvalidOperationException($"Could not save {path}.");
        }

        private static float AttackCooldown(MeleeEnemyAttack melee)
        {
            SerializedObject serialized = new(melee);
            return serialized.FindProperty("attackCooldown").floatValue;
        }

        private static RoomDifficultyTable ConfigureThreat(RoomDifficultyTable table)
        {
            Undo.RecordObject(table, "Configure Enemy-9 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.SplittingChaser, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-9 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-9 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-9 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.SplittingChaser)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.SplittingChaser, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-9 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
