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
    // Enemy-13 (docs/28 §8.4): 잠복형 moves under the floor, where it cannot be hit, until it is under the player's
    // feet. There it stops, a red ellipse warns where it will burst out, and it pops up hurting the player standing
    // on the ellipse at the medium tier of the floor. Up, it is stuck in the ground (the player cannot push it,
    // knockback-immune), touching it costs one heart, and it can be hit; then it sinks again (user request,
    // 2026-10-30: no forced burst after a long burrow, the player can lead it around). It first appears on
    // floor 3, scores threat 4 (like 누루링) and joins the pressure melee slots at weight 15. It shows the user's
    // 불효자손-냉정 drawing (2026-10-30); buried and attacking are plain colours until the user's art comes.
    // Re-running updates the same Prefab and assets and keeps their GUIDs.
    public static class Week27Enemy13Setup
    {
        public const int EncounterContentVersion = 20;
        public const string PrefabPath = "Assets/Prefabs/AmbushFairy.prefab";
        public const string PrefabName = "AmbushFairy";
        // Named after the user's drawing file 불효자손-냉정 (2026-10-30).
        public const string DisplayName = "불효자손-냉정";
        public const string ArtworkId = "bulhyojason-cold";
        public const string ArtworkPath = FairyKingdomArtworkSetup.ArtRoot + "/Enemies/" + ArtworkId + ".png";
        // The visible drawing is about 0.8 wide per 1 tall, so 0.96 wide stands about 1.2 tall.
        public const float ArtworkWidth = 0.96f;

        // Implementation defaults (2026-10-30).
        public const float MaxHealth = 35f;
        public const float KnockbackWeight = 1.2f;
        public const float BurrowedMoveSpeed = 3.2f;
        public const float DetectionRange = 12f;
        // Its shadow within this of the player's shadow: under the feet (user request, 2026-10-30).
        public const float EmergeRange = 0.3f;
        public const float MinimumBurrowDuration = 1f;
        public const float EmergeTelegraphDuration = 0.7f;
        public const float AttackDuration = 0.3f;
        public const float SurfacedDuration = 2f;
        public const float BurrowDuration = 0.4f;
        public const float HitRadius = 1f;
        public const EnemyDamageTier DamageTier = EnemyDamageTier.Medium;
        // User request (2026-10-30): touching it while it is up costs one heart (the light tier on every floor).
        public const EnemyDamageTier ContactDamageTier = EnemyDamageTier.Light;
        public const float ContactDamageCooldown = 1f;

        public const int Threat = 4;
        public const int MeleeWeight = 15;

        // Pressure melee slots: Enemy-10's 50/25/15/10/15 plus 잠복형.
        public static EncounterEnemyCandidate[] PressureMeleeCandidates() => Week27Enemy10Setup
            .PressureMeleeCandidates()
            .Append(new EncounterEnemyCandidate(EncounterEnemyRole.Ambusher, MeleeWeight)).ToArray();

        [MenuItem("Trickal Fan Game/Week 27/Setup Enemy-13 Ambush Enemy")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-13 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Enemy12Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-13 requires the completed Enemy-12 Game Scene.");
            }

            GameObject prefab = EnsurePrefab();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);

            // Spawn-2 owns pressure and crossfire and reads the Enemy-13 melee list. Threat 4 matches 누루링 in the
            // same slots, so both declared ranges are restored unchanged.
            EncounterDefinition[] updated = Week19Spawn2Setup.ConfigureEncounters();
            foreach (EncounterDefinition definition in updated.Where(Week25Enemy9Setup.IsPressure))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.PressureMinimumThreat,
                    Week19Difficulty1Setup.PressureMaximumThreat);
            foreach (EncounterDefinition definition in updated.Where(Week27Enemy12Setup.IsCrossfire))
                definition.ConfigureDeclaredThreat(Week19Difficulty1Setup.CrossfireMinimumThreat,
                    Week19Difficulty1Setup.CrossfireMaximumThreat);
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
                    throw new InvalidOperationException($"Enemy-13 catalog is invalid. {error}");
                }

                EditorUtility.SetDirty(definition);
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-13 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-13 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-13 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health; it moves under the " +
                      $"floor at {BurrowedMoveSpeed} out of reach, within {EmergeRange} of the player's feet it warns " +
                      $"{EmergeTelegraphDuration} s and bursts out ({HitRadius} wide ellipse, {DamageTier} damage to " +
                      $"the player), stays up {SurfacedDuration} s anchored and knockback-immune ({ContactDamageTier} " +
                      $"contact damage) and sinks again. Threat {Threat}, floor " +
                      $"{EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.Ambusher)}+, pressure melee " +
                      $"weight {MeleeWeight}, Encounter content version {generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() =>
            new[] { PrefabPath, FairyKingdomArtworkSetup.DisplayRoot + "/" + ArtworkId + ".prefab" };

        public static GameObject EnsurePrefab()
        {
            GameObject contents = LoadFromChaser();
            try
            {
                contents.transform.localScale = Vector3.one;
                // The chaser's drawn frames belong to another character; 잠복형 moves itself and only hurts by
                // bursting out.
                Remove<EnemyAttackArtwork>(contents);
                Remove<EnemyMovementAnimator>(contents);
                Remove<MeleeEnemyAttack>(contents);
                Remove<EnemyAttackPresentation>(contents);
                Remove<EnemyChase>(contents);

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                KnockbackReceiver knockback = contents.GetComponent<KnockbackReceiver>();
                knockback.ConfigureWeight(KnockbackWeight);
                // Stuck in the ground: attacks never knock it back (user request, 2026-10-30).
                knockback.ConfigureKnockbackImmunity(true);
                // Its body circle is off under the floor, so only an ambusher that is up hurts on touch.
                ContactDamage contact = contents.GetComponent<ContactDamage>();
                if (contact == null) contact = contents.AddComponent<ContactDamage>();
                contact.Configure(ContactDamageTier, ContactDamageCooldown);
                contact.ConfigureHalfHeart(false);
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                    contents.AddComponent<EnemyBehaviorContext>();

                AmbushEnemyController controller = contents.GetComponent<AmbushEnemyController>();
                if (controller == null) controller = contents.AddComponent<AmbushEnemyController>();
                controller.Configure(BurrowedMoveSpeed, DetectionRange, EmergeRange, MinimumBurrowDuration,
                    EmergeTelegraphDuration, AttackDuration, SurfacedDuration, BurrowDuration, HitRadius, DamageTier);

                AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
                FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
                FairyKingdomArtworkSetup.BuildDisplayPrefab(catalog, ArtworkId);
                if (!FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog))
                    throw new InvalidOperationException($"Enemy-13 could not bind '{ArtworkId}' to {PrefabPath}.");
                // Binding keeps a width already on the Prefab, so the chaser's width is replaced here.
                FairyKingdomArtworkView view = contents.GetComponent<FairyKingdomArtworkView>();
                if (view.ArtworkId != ArtworkId || !Mathf.Approximately(view.LocalWidth, ArtworkWidth))
                    view.Configure(ArtworkId, view.Mode, view.Visual, ArtworkWidth);
                // The saved colour stays plain; the controller tints the buried and attacking states at runtime.
                if (view.Visual != null) view.Visual.color = Color.white;
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
                    throw new InvalidOperationException($"Enemy-13 needs {Week15Enemy0Setup.ChaserPrefabPath}.");
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
            Undo.RecordObject(table, "Configure Enemy-13 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.Ambusher, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-13 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-13 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-13 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.Ambusher)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.Ambusher, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-13 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
