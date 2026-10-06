using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week19Spawn2Setup
    {
        public const int EncounterContentVersion = 7;
        public const string QuickRangedPrefabPath = "Assets/Prefabs/QuickRangedFairy.prefab";
        public const string EncounterPrefix = "spawn";

        public const float QuickPreferredDistance = 5.6f;
        public const float QuickMoveSpeed = 3.25f;
        public const float QuickDetectionRange = 12f;
        public const float QuickShotInterval = 0.4f;
        public const float QuickBurstRestDuration = 1.5f;
        public const float QuickFourShotChance = 0.4f;
        public const float QuickMaximumPredictionTime = 0.25f;

        public readonly struct ProfileSpec
        {
            public ProfileSpec(string profileId, string profilePath, int sniperWeight)
            {
                ProfileId = profileId;
                ProfilePath = profilePath;
                SniperWeight = sniperWeight;
            }

            public string ProfileId { get; }
            public string ProfilePath { get; }
            public int SniperWeight { get; }
            public int QuickRangedWeight => 100 - SniperWeight;
        }

        public static readonly ProfileSpec[] Profiles =
        {
            new(Week14Room3Setup.SmallProfileId, Week14Room3Setup.SmallProfilePath, 10),
            new(Week14Room1Setup.BasicProfileId, Week14Room1Setup.BasicProfilePath, 20),
            new(Week14Room3Setup.WideProfileId, Week14Room3Setup.WideProfilePath, 50),
            new(Week14Room6Setup.TallProfileId, Week14Room6Setup.TallProfilePath, 50),
            new(Week14Room6Setup.LargeProfileId, Week14Room6Setup.LargeProfilePath, 60),
        };

        [MenuItem("Trickal Fan Game/Week 19/Setup Spawn-2 Weighted Spawn Candidates")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = UnityEngine.Object.FindFirstObjectByType<RoomGraphAssembler>();
            if (generator == null || assembler == null ||
                generator.EncounterContentVersion < Week19Spawn1Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Spawn-2 requires the completed Spawn-1 Game Scene.");
            }

            GameObject quickRanged = EnsureQuickRangedPrefab();
            EncounterEnemyRoster roster = ConfigureRoster(quickRanged);
            EncounterDefinition[] owned = ConfigureEncounters();
            HashSet<string> ownedIds = new(owned.Select(definition => definition.EncounterId), StringComparer.Ordinal);
            // Keep Encounters added by later setups (Encounter-4) when Spawn-2 is re-run.
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !ownedIds.Contains(definition.EncounterId))
                .Concat(owned)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();

            Undo.RecordObjects(new UnityEngine.Object[] { generator, assembler }, "Configure Spawn-2 candidates");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            assembler.ConfigureEncounterRoster(roster);
            EditorUtility.SetDirty(generator);
            EditorUtility.SetDirty(assembler);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Game Scene save failed during Spawn-2 setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Spawn-2 ready: each profile has pressure and crossfire Encounters with deterministic " +
                      "per-SpawnPoint weighted candidates; the quick ranged placeholder circles while firing " +
                      "predictive three- or four-shot bursts at 70% of the sniper preferred distance.");
        }

        public static string EncounterId(string profileId, string pattern) =>
            $"{EncounterPrefix}-{profileId}-{pattern}-v1";

        public static string EncounterPath(string profileId, string pattern) =>
            $"{Week14Encounter1Setup.EncounterFolder}/{EncounterId(profileId, pattern)}.asset";

        public static string[] CreatedAssetPaths() => new[] { QuickRangedPrefabPath }
            .Concat(Profiles.SelectMany(profile => new[]
            {
                EncounterPath(profile.ProfileId, "pressure"),
                EncounterPath(profile.ProfileId, "crossfire"),
            })).ToArray();

        private static GameObject EnsureQuickRangedPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(QuickRangedPrefabPath) == null &&
                !AssetDatabase.CopyAsset(Week15Enemy4Setup.SniperPrefabPath, QuickRangedPrefabPath))
            {
                throw new InvalidOperationException("Spawn-2 could not create the quick ranged placeholder Prefab.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(QuickRangedPrefabPath);
            try
            {
                root.name = "QuickRangedFairy";
                RangedEnemyController legacy = root.GetComponent<RangedEnemyController>();
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy, true);
                LongRangeSniperController sniper = root.GetComponent<LongRangeSniperController>();
                if (sniper != null) UnityEngine.Object.DestroyImmediate(sniper, true);
                LineRenderer aimPath = root.GetComponent<LineRenderer>();
                if (aimPath != null) UnityEngine.Object.DestroyImmediate(aimPath, true);
                MobileRangedEnemyController controller = root.GetComponent<MobileRangedEnemyController>();
                if (controller == null) controller = root.AddComponent<MobileRangedEnemyController>();
                controller.Configure(
                    QuickMoveSpeed,
                    QuickDetectionRange,
                    QuickPreferredDistance,
                    0.8f,
                    0.35f,
                    QuickShotInterval,
                    QuickBurstRestDuration,
                    QuickFourShotChance,
                    6.5f,
                    Week19Tune1Setup.QuickRangedProjectileDamageTier,
                    Week21Range0Setup.QuickRangedProjectileLifetime,
                    QuickMaximumPredictionTime);

                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = new Color(0.45f, 0.8f, 1f, 1f);
                if (PrefabUtility.SaveAsPrefabAsset(root, QuickRangedPrefabPath) == null)
                {
                    throw new InvalidOperationException("Spawn-2 could not save the quick ranged Prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(QuickRangedPrefabPath);
        }

        private static EncounterEnemyRoster ConfigureRoster(GameObject quickRanged)
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            GameObject chaser = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy2Setup.BulhyojasonPrefabPath);
            GameObject fastChaser = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy2Setup.SansamoPrefabPath);
            GameObject charging = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.ChargingPrefabPath);
            GameObject sniper = AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy4Setup.SniperPrefabPath);
            if (roster == null || quickRanged == null || chaser == null || fastChaser == null ||
                charging == null || sniper == null)
            {
                throw new InvalidOperationException("Spawn-2 requires the verified normal enemy roster and Prefabs.");
            }

            Undo.RecordObject(roster, "Configure Spawn-2 enemy roster");
            roster.Configure(new[]
            {
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Chaser, chaser),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.FastChaser, fastChaser),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Ranged, quickRanged),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Charging, charging),
                new EncounterEnemyPrefabBinding(EncounterEnemyRole.Sniper, sniper),
            });
            EditorUtility.SetDirty(roster);
            return roster;
        }

        private static EncounterDefinition[] ConfigureEncounters()
        {
            List<EncounterDefinition> definitions = new();
            foreach (ProfileSpec spec in Profiles)
            {
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(spec.ProfilePath);
                if (profile == null || profile.ProfileId != spec.ProfileId)
                {
                    throw new InvalidOperationException($"Spawn-2 is missing Room Profile '{spec.ProfileId}'.");
                }

                EncounterSpawnRule melee = CandidateRule(
                    new EncounterEnemyCandidate(EncounterEnemyRole.Chaser, 70),
                    new EncounterEnemyCandidate(EncounterEnemyRole.FastChaser, 30));
                EncounterSpawnRule rear = CandidateRule(
                    new EncounterEnemyCandidate(EncounterEnemyRole.Ranged, spec.QuickRangedWeight),
                    new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, spec.SniperWeight));
                EncounterSpawnRule charge = CandidateRule(
                    new EncounterEnemyCandidate(EncounterEnemyRole.Charging, 100));

                definitions.Add(CreateOrUpdate(spec.ProfileId, "pressure", profile,
                    Wave(1, melee, rear),
                    Wave(2, melee, rear)));
                definitions.Add(CreateOrUpdate(spec.ProfileId, "crossfire", profile,
                    Wave(1, CandidateRule(3,
                        new EncounterEnemyCandidate(EncounterEnemyRole.Ranged, spec.QuickRangedWeight),
                        new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, spec.SniperWeight))),
                    Wave(2, melee, charge)));
            }

            return definitions.OrderBy(definition => definition.EncounterId, StringComparer.Ordinal).ToArray();
        }

        private static EncounterDefinition CreateOrUpdate(string profileId, string pattern,
            RoomProfile profile, params EncounterWaveDefinition[] waves)
        {
            string path = EncounterPath(profileId, pattern);
            EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(EncounterId(profileId, pattern), new[] { profile }, 1, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                waves,
                EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static EncounterWaveDefinition Wave(int number, params EncounterSpawnRule[] rules) =>
            new(number,
                number == 1 ? EncounterWaveStartCondition.RoomEntered :
                    EncounterWaveStartCondition.PreviousWaveCleared,
                EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                rules);

        private static EncounterSpawnRule CandidateRule(params EncounterEnemyCandidate[] candidates) =>
            CandidateRule(1, candidates);

        private static EncounterSpawnRule CandidateRule(int count, params EncounterEnemyCandidate[] candidates) =>
            new(candidates, count, null, "all");
    }
}
