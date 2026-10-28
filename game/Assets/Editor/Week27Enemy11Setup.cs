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
    // Enemy-11 (user decisions, 2026-10-24 and 2026-10-28, docs/28 §8): 대왕쥬비 is the 쥬비 drawing on a bigger
    // flying body. It keeps about four tiles from the player while buzzing around like a bee, and every three
    // seconds it glows for 0.6 s and releases two 쥬비, stopping while eight of its own 쥬비 are alive. The 쥬비
    // stay when it dies. It enters on floor 3 through its own jyubi-nest Encounter: 대왕쥬비 and two 쥬비, then a
    // wave of three chasers. Re-running updates the same Prefab and assets and keeps their GUIDs.
    public static class Week27Enemy11Setup
    {
        public const int EncounterContentVersion = 18;
        public const string Pattern = "jyubi-nest";
        public const string PrefabPath = "Assets/Prefabs/JyubiKing.prefab";
        public const string PrefabName = "JyubiKing";
        public const string DisplayName = "대왕쥬비";

        // Twice the 쥬비 (0.5): the drawing, collider and contact reach all grow with the root.
        public const float Scale = 1f;
        // Implementation defaults (2026-10-28).
        public const float MaxHealth = 50f;
        public const float KnockbackWeight = 1.5f;
        public const EnemyDamageTier ContactDamageTier = EnemyDamageTier.Light;
        public const float ContactCooldown = 1f;
        public const float MoveSpeed = JyubiKingController.DefaultMoveSpeed;
        public const float PreferredDistance = JyubiKingController.DefaultPreferredDistance;

        // User decisions (2026-10-28).
        public const float SummonInterval = 3f;
        public const float SummonTelegraph = 0.6f;
        public const int SummonCount = 2;
        public const int MaxSummons = 8;

        public const int Threat = 6;
        public const int NestJyubi = 2;
        public const int NestFollowUp = 3;
        public const int MinimumThreat = Threat + NestJyubi * Week25Enemy7Setup.JyubiThreat + NestFollowUp * 2;
        public const int MaximumThreat = Threat + NestJyubi * Week25Enemy7Setup.JyubiThreat + NestFollowUp * 3;

        // The second wave: the crossfire's chasers.
        public static EncounterEnemyCandidate[] FollowUpCandidates() => new[]
        {
            new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 70),
            new EncounterEnemyCandidate(EncounterEnemyRole.FastChaser, 30),
        };

        [MenuItem("Trickal Fan Game/Week 27/Setup Enemy-11 Jyubi King")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Enemy-11 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Enemy10Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Enemy-11 requires the completed Enemy-10 Game Scene.");
            }

            GameObject prefab = EnsurePrefab();
            RoomDifficultyTable table = ConfigureThreat(generator.DifficultyTable);
            EncounterEnemyRoster roster = ConfigureRoster(prefab);
            EncounterDefinition[] added = ConfigureEncounters();
            HashSet<string> addedIds = new(added.Select(definition => definition.EncounterId), StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !addedIds.Contains(definition.EncounterId))
                .Concat(added)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out string error) ||
                    !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                {
                    throw new InvalidOperationException($"Enemy-11 catalog is invalid. {error}");
                }
            }

            Undo.RecordObjects(new Object[] { generator, assembler }, "Configure Enemy-11 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Enemy-11 setup.");

            AssetDatabase.SaveAssets();
            Debug.Log($"Enemy-11 ready: {DisplayName} ({PrefabName}) has {MaxHealth} health, flies at {MoveSpeed} " +
                      $"about {PreferredDistance} from the player while buzzing around, and every {SummonInterval} s " +
                      $"glows {SummonTelegraph} s and releases {SummonCount} Jyubi (at most {MaxSummons} of its own " +
                      $"alive). Threat {Threat}, floor " +
                      $"{EncounterEnemyFloorPool.IntroductionFloor(EncounterEnemyRole.JyubiKing)}+, {Pattern} " +
                      $"Encounters {MinimumThreat}~{MaximumThreat}, Encounter content version " +
                      $"{generator.EncounterContentVersion}.");
        }

        public static string[] CreatedAssetPaths() => Week19Spawn2Setup.Profiles
            .Select(profile => Week19Spawn2Setup.EncounterPath(profile.ProfileId, Pattern))
            .Prepend(PrefabPath).ToArray();

        public static bool IsNest(EncounterDefinition definition) =>
            definition != null && IsNest(definition.EncounterId);

        public static bool IsNest(string encounterId) => encounterId != null &&
            encounterId.EndsWith($"-{Pattern}-v1", StringComparison.Ordinal);

        public static GameObject EnsurePrefab()
        {
            GameObject jyubi = AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath);
            if (jyubi == null)
                throw new InvalidOperationException($"Enemy-11 needs the Jyubi Prefab at {Week23Enemy6Setup.PrefabPath}.");
            GameObject contents = LoadFromJyubi(jyubi);
            try
            {
                contents.transform.localScale = Vector3.one * Scale;
                // 대왕쥬비 moves on its own controller instead of flying straight at the player.
                Remove<EnemyChase>(contents);

                Health health = contents.GetComponent<Health>();
                SerializedObject serializedHealth = new(health);
                serializedHealth.FindProperty("maxHealth").floatValue = MaxHealth;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                contents.GetComponent<KnockbackReceiver>().ConfigureWeight(KnockbackWeight);
                if (contents.GetComponent<EnemyBehaviorContext>() == null)
                    contents.AddComponent<EnemyBehaviorContext>();
                if (contents.GetComponent<EnemyFlight>() == null) contents.AddComponent<EnemyFlight>();

                ContactDamage contact = contents.GetComponent<ContactDamage>();
                if (contact == null) contact = contents.AddComponent<ContactDamage>();
                contact.Configure(ContactDamageTier, ContactCooldown);
                contact.ConfigureHalfHeart(false);

                JyubiKingController controller = contents.GetComponent<JyubiKingController>();
                if (controller == null) controller = contents.AddComponent<JyubiKingController>();
                controller.ConfigureMovement(MoveSpeed, JyubiKingController.DefaultDetectionRange, PreferredDistance,
                    JyubiKingController.DefaultDistanceBand);
                controller.ConfigureSummon(jyubi, JyubiKingController.DefaultFirstSummonDelay, SummonInterval,
                    SummonTelegraph, SummonCount, MaxSummons, JyubiKingController.DefaultSummonSideOffset);

                FairyKingdomArtworkCatalog catalog = FairyKingdomArtworkSetup.RefreshCatalog();
                FairyKingdomArtworkSetup.BindPrefabContents(contents, catalog);
                if (contents.GetComponent<FairyKingdomArtworkView>()?.ArtworkId != "jyubi")
                    throw new InvalidOperationException($"Enemy-11 could not bind the Jyubi drawing to {PrefabPath}.");
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

        // Starts as an unpacked copy of 쥬비, so later 쥬비 tuning does not leak into it.
        private static GameObject LoadFromJyubi(GameObject jyubi)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(jyubi);
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
            Undo.RecordObject(table, "Configure Enemy-11 threat score");
            table.Configure(Week19Difficulty1Setup.ThreatScores, table.TierBands.ToArray(),
                table.FloorRanges.ToArray(), table.DistanceWeights.ToArray());
            if (!table.TryValidate(out string error) ||
                !table.TryGetThreat(EncounterEnemyRole.JyubiKing, out int threat) || threat != Threat)
            {
                throw new InvalidOperationException($"Enemy-11 difficulty table is invalid. {error}");
            }

            EditorUtility.SetDirty(table);
            return table;
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject prefab)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            if (roster == null) throw new InvalidOperationException("Enemy-11 requires the enemy roster.");

            Undo.RecordObject(roster, "Configure Enemy-11 enemy roster");
            roster.Configure(roster.Bindings.Where(binding => binding.Role != EncounterEnemyRole.JyubiKing)
                .Append(new EncounterEnemyPrefabBinding(EncounterEnemyRole.JyubiKing, prefab)).ToArray());
            if (!roster.TryValidate(out string error))
                throw new InvalidOperationException($"Enemy-11 enemy roster is invalid. {error}");
            EditorUtility.SetDirty(roster);
            return roster;
        }

        // One per profile, floor 3 only: 대왕쥬비 on a far rear point with two 쥬비 near it, then three chasers.
        private static EncounterDefinition[] ConfigureEncounters()
        {
            List<EncounterDefinition> definitions = new();
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(spec.ProfilePath);
                if (profile == null || profile.ProfileId != spec.ProfileId)
                    throw new InvalidOperationException($"Enemy-11 is missing Room Profile '{spec.ProfileId}'.");

                string path = Week19Spawn2Setup.EncounterPath(spec.ProfileId, Pattern);
                EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Undo.RecordObject(definition, "Configure Enemy-11 Encounter");
                definition.Configure(Week19Spawn2Setup.EncounterId(spec.ProfileId, Pattern), new[] { profile },
                    Week27Curve1Setup.MinimumFloor(Pattern), Week27Curve1Setup.MaximumFloor,
                    Week14Encounter1Setup.MinimumPlayerDistance,
                    Week14Encounter1Setup.MinimumDoorDistance,
                    new[]
                    {
                        new EncounterWaveDefinition(1, EncounterWaveStartCondition.RoomEntered,
                            EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[]
                            {
                                Rule(1, new EncounterEnemyCandidate(EncounterEnemyRole.JyubiKing, 100)),
                                Rule(NestJyubi, new EncounterEnemyCandidate(EncounterEnemyRole.Jyubi, 100)),
                            }),
                        new EncounterWaveDefinition(2, EncounterWaveStartCondition.PreviousWaveCleared,
                            EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated, new[]
                            {
                                Rule(NestFollowUp, FollowUpCandidates()),
                            }),
                    },
                    EncounterClearCondition.AllWavesCleared);
                definition.ConfigureDeclaredThreat(MinimumThreat, MaximumThreat);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            return definitions.ToArray();
        }

        private static EncounterSpawnRule Rule(int count, params EncounterEnemyCandidate[] candidates) =>
            new(candidates, count, null, "all");

        private static void Remove<T>(GameObject target) where T : Component
        {
            foreach (T component in target.GetComponents<T>()) Object.DestroyImmediate(component, true);
        }
    }
}
