using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week19Encounter4Setup
    {
        public const int EncounterContentVersion = 9;
        public const int BaseSpawnPointCount = 3;

        public const int MinimumRoomEnemies = 2;
        public const int MaximumRoomEnemies = 7;
        public const int MaximumWaveEnemies = 4;
        public const int MaximumWaveRearFiring = 3;

        public const int ElitePairThreat = 8;

        private const SpawnPointPlacementRole MeleeRear =
            SpawnPointPlacementRole.MeleePressure | SpawnPointPlacementRole.RearFiring;

        public readonly struct AddedSpawnPoint
        {
            public AddedSpawnPoint(Vector2 position, SpawnPointPlacementRole role)
            {
                Position = position;
                Role = role;
            }

            public Vector2 Position { get; }
            public SpawnPointPlacementRole Role { get; }
        }

        // Existing SpawnPoints 1~3 keep their coordinates and roles; these are appended as SpawnPoint 4+.
        // Totals: Small 4, Basic/Wide/Tall 5, every Large Layout 6.
        public static readonly IReadOnlyDictionary<string, AddedSpawnPoint[]> AddedSpawnPoints =
            new Dictionary<string, AddedSpawnPoint[]>(StringComparer.Ordinal)
            {
                ["small-standard"] = new[] { new AddedSpawnPoint(new Vector2(3f, 1.5f), SpawnPointPlacementRole.AllCombat) },
                ["basic-standard"] = new[] { Point(-5f, -2f), Point(5f, -2f) },
                ["wide-standard"] = new[] { Point(-6f, -2f), Point(6f, -2f) },
                ["tall-standard"] = new[] { Point(3f, 3.5f), Point(-3f, -3.5f) },
                ["large-standard"] = new[] { Point(-6f, -3.5f), Point(6f, -3.5f), Point(-9f, 3f) },
                ["large-central-pillar"] = new[] { Point(-6f, -2.75f), Point(6f, -2.75f), Point(9f, -3.5f) },
                ["large-cover-blocks"] = new[] { Point(-8.5f, -4f), Point(8.5f, 4f), Point(0f, 2.5f) },
                ["large-split-lanes"] = new[] { Point(-8f, 4f), Point(8f, -4f), Point(-2f, 4f) },
                ["large-scattered-rubble"] = new[] { Point(-5f, 4f), Point(5f, -4f), Point(-9f, -4.5f) },
            };

        public readonly struct SwarmSpec
        {
            public SwarmSpec(string profileId, int firstWave, int secondWave)
            {
                ProfileId = profileId;
                FirstWave = firstWave;
                SecondWave = secondWave;
            }

            public string ProfileId { get; }
            public int FirstWave { get; }
            public int SecondWave { get; }
            public int Total => FirstWave + SecondWave;
            public int MinimumThreat => Total * 2;
            public int MaximumThreat => Total * 3;
        }

        // Small, Basic and Wide lose their central pressure point to vertical doors, so they keep three melee per wave.
        public static readonly SwarmSpec[] Swarms =
        {
            new(Week14Room3Setup.SmallProfileId, 3, 3),
            new(Week14Room1Setup.BasicProfileId, 3, 3),
            new(Week14Room3Setup.WideProfileId, 3, 3),
            new(Week14Room6Setup.TallProfileId, 4, 3),
            new(Week14Room6Setup.LargeProfileId, 4, 3),
        };

        [MenuItem("Trickal Fan Game/Week 19/Setup Encounter-4 Expanded Encounters")]
        public static void Setup()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week19Difficulty1Setup.EncounterContentVersion)
            {
                throw new InvalidOperationException("Encounter-4 requires the completed Difficulty-1 Game Scene.");
            }

            ExpandSpawnPoints(generator);
            RoomDifficultyTable table = generator.DifficultyTable;
            Undo.RecordObject(table, "Configure Encounter-4 size limits");
            table.ConfigureEncounterLimits(MinimumRoomEnemies, MaximumRoomEnemies, MaximumWaveEnemies,
                MaximumWaveRearFiring);
            EditorUtility.SetDirty(table);

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
                    throw new InvalidOperationException($"Encounter-4 catalog is invalid. {error}");
                }
            }

            Undo.RecordObject(generator, "Configure Encounter-4 Encounters");
            generator.ConfigureEncounters(Math.Max(EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Game Scene save failed during Encounter-4 setup.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Encounter-4 ready: SpawnPoints Small 4 / Basic, Wide, Tall 5 / Large Layouts 6, rooms hold " +
                      "2~7 enemies with at most 4 per wave (3 rear firing), and every profile adds a melee swarm " +
                      "and a sniper-charger elite pair.");
        }

        public static string[] CreatedAssetPaths() => Week19Spawn2Setup.Profiles
            .SelectMany(profile => new[]
            {
                Week19Spawn2Setup.EncounterPath(profile.ProfileId, "swarm"),
                Week19Spawn2Setup.EncounterPath(profile.ProfileId, "elite-pair"),
            }).ToArray();

        public static Vector2[] ExpectedSpawnPoints(RoomTemplateDefinition template) =>
            template.SpawnPoints.Take(BaseSpawnPointCount)
                .Concat(AddedSpawnPoints[template.TemplateId].Select(point => point.Position)).ToArray();

        private static void ExpandSpawnPoints(FloorGenerator generator)
        {
            foreach (KeyValuePair<string, AddedSpawnPoint[]> entry in AddedSpawnPoints)
            {
                RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                    candidate != null && candidate.TemplateId == entry.Key);
                int expectedCount = BaseSpawnPointCount + entry.Value.Length;
                if (template == null ||
                    (template.SpawnPoints.Count != BaseSpawnPointCount && template.SpawnPoints.Count != expectedCount))
                {
                    throw new InvalidOperationException(
                        $"Encounter-4 needs Room Template '{entry.Key}' with {BaseSpawnPointCount} or " +
                        $"{expectedCount} SpawnPoints.");
                }

                Vector2[] points = ExpectedSpawnPoints(template);
                SpawnPointPlacementRole[] roles = template.SpawnPointRoles.Take(BaseSpawnPointCount)
                    .Concat(entry.Value.Select(point => point.Role)).ToArray();

                string prefabPath = AssetDatabase.GetAssetPath(template.RoomPrefabAsset);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    RoomPrefab room = root.GetComponent<RoomPrefab>();
                    RoomSpawnPointAuthoring.Sync(room != null ? room.Controller : null, points);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    {
                        throw new InvalidOperationException($"Encounter-4 could not save {prefabPath}.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                Undo.RecordObject(template, "Configure Encounter-4 SpawnPoints");
                template.Configure(
                    template.TemplateId,
                    template.Profile,
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                    template.AllowedRoomTypes.ToArray(),
                    template.DoorSlots.ToArray(),
                    points,
                    template.MinimumFloor,
                    template.MaximumFloor,
                    roles);
                EditorUtility.SetDirty(template);
                if (!template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                {
                    throw new InvalidOperationException(
                        $"Encounter-4 SpawnPoints are invalid for '{template.TemplateId}'. {error}");
                }
            }
        }

        private static EncounterDefinition[] ConfigureEncounters()
        {
            List<EncounterDefinition> definitions = new();
            foreach (SwarmSpec spec in Swarms)
            {
                RoomProfile profile = LoadProfile(spec.ProfileId);
                EncounterEnemyCandidate[] melee =
                {
                    new(EncounterEnemyRole.Chaser, 70),
                    new(EncounterEnemyRole.FastChaser, 30),
                };
                EncounterDefinition swarm = CreateOrUpdate(spec.ProfileId, "swarm", profile,
                    Wave(1, new EncounterSpawnRule(melee, spec.FirstWave, null, "all")),
                    Wave(2, new EncounterSpawnRule(melee, spec.SecondWave, null, "all")));
                swarm.ConfigureDeclaredThreat(spec.MinimumThreat, spec.MaximumThreat);
                definitions.Add(swarm);

                // The charger claims its lane first so the sniper cannot occupy the only charge-lane point.
                EncounterDefinition elite = CreateOrUpdate(spec.ProfileId, "elite-pair", profile,
                    Wave(1,
                        new EncounterSpawnRule(new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Charging, 100) },
                            1, null, "all"),
                        new EncounterSpawnRule(new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, 100) },
                            1, null, "all")));
                elite.ConfigureDeclaredThreat(ElitePairThreat, ElitePairThreat);
                definitions.Add(elite);
            }

            return definitions.ToArray();
        }

        private static RoomProfile LoadProfile(string profileId)
        {
            Week19Spawn2Setup.ProfileSpec spec = Week19Spawn2Setup.Profiles.Single(candidate =>
                candidate.ProfileId == profileId);
            RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(spec.ProfilePath);
            if (profile == null || profile.ProfileId != profileId)
            {
                throw new InvalidOperationException($"Encounter-4 is missing Room Profile '{profileId}'.");
            }

            return profile;
        }

        private static EncounterDefinition CreateOrUpdate(string profileId, string pattern, RoomProfile profile,
            params EncounterWaveDefinition[] waves)
        {
            string path = Week19Spawn2Setup.EncounterPath(profileId, pattern);
            EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            Undo.RecordObject(definition, "Configure Encounter-4 Encounter");
            definition.Configure(Week19Spawn2Setup.EncounterId(profileId, pattern), new[] { profile }, 1, 3,
                Week14Encounter1Setup.MinimumPlayerDistance,
                Week14Encounter1Setup.MinimumDoorDistance,
                waves,
                EncounterClearCondition.AllWavesCleared);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static EncounterWaveDefinition Wave(int number, params EncounterSpawnRule[] rules) =>
            new(number,
                number == 1 ? EncounterWaveStartCondition.RoomEntered : EncounterWaveStartCondition.PreviousWaveCleared,
                EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                rules);

        private static AddedSpawnPoint Point(float x, float y) => new(new Vector2(x, y), MeleeRear);
    }
}
