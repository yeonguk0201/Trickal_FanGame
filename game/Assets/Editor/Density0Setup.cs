using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Density-0 (docs/28 §10.2, beta feedback 2026-11-23): big rooms felt empty, so the number of enemies follows
    // the room size. Per wave: small 4 (unchanged), basic 5, wide/tall 6, large 8. Play showed 8 and 15 per wave as
    // cluttered and dull (user, 2026-11-23), so big rooms vary their waves and leave the rest to the obstacles and
    // traps of their Layouts. This class owns the wave layout of all six Encounter patterns for every size;
    // Spawn-2 and Enemy-13 read it, so re-running them keeps these waves.
    //
    // Each size gets its own enemy limits, floor ranges and tier bands in the difficulty table. The small values
    // are the table-wide ones; a bigger size scales them by how many more enemies its Encounters hold on average.
    // The rooms get more SpawnPoints from RoomSpawnPointDensity: the importer adds them to every Layout, and this
    // setup adds them to the templates authored before the importer. Re-running rewrites the same assets.
    public static class Density0Setup
    {
        public const int EncounterContentVersion = 24;
        // Balance-1: the two-wave large rooms. Density-0 owns the wave layouts, so re-running it stamps this version.
        public const int Balance1EncounterContentVersion = 27;

        private const SpawnPointPlacementRole MeleeRear =
            SpawnPointPlacementRole.MeleePressure | SpawnPointPlacementRole.RearFiring;

        public readonly struct SizeSpec
        {
            public SizeSpec(string profileId, int maximumWaveEnemies, int maximumWaveRearFiring,
                int spawnPointTarget)
            {
                ProfileId = profileId;
                MaximumWaveEnemies = maximumWaveEnemies;
                MaximumWaveRearFiring = maximumWaveRearFiring;
                SpawnPointTarget = spawnPointTarget;
            }

            public string ProfileId { get; }
            public int MaximumWaveEnemies { get; }
            public int MaximumWaveRearFiring { get; }
            // Authored plus generated SpawnPoints: a few more than a wave can hold, because the authored points
            // near a door are unsafe while that door is open (basic has two spare, its floor is small).
            public int SpawnPointTarget { get; }
        }

        public const int MinimumRoomEnemies = Week19Encounter4Setup.MinimumRoomEnemies;

        public static readonly SizeSpec[] Sizes =
        {
            new(Week14Room3Setup.SmallProfileId, Week19Encounter4Setup.MaximumWaveEnemies,
                Week19Encounter4Setup.MaximumWaveRearFiring, 4),
            new(Week14Room1Setup.BasicProfileId, 5, 3, 7),
            new(Week14Room3Setup.WideProfileId, 6, 4, 9),
            new(Week14Room6Setup.TallProfileId, 6, 4, 9),
            new(Week14Room6Setup.LargeProfileId, 8, 5, 11),
        };

        public static SizeSpec Size(string profileId) =>
            Sizes.Single(size => string.Equals(size.ProfileId, profileId, StringComparison.Ordinal));

        public static int SpawnPointTarget(string profileId) => Size(profileId).SpawnPointTarget;

        // ---- Wave layouts -------------------------------------------------------------------------------------

        public enum SlotKind
        {
            PressureMelee,
            PressureRear,
            CrossfireRear,
            // Crossfire's chasers, also the elite pair's escort and the nest's follow-up.
            Chaser,
            Charger,
            Sniper,
            SwarmMelee,
            Jyubi,
            JyubiKing,
        }

        public readonly struct Slot
        {
            public Slot(SlotKind kind, int count)
            {
                Kind = kind;
                Count = count;
            }

            public SlotKind Kind { get; }
            public int Count { get; }
        }

        private static Slot S(SlotKind kind, int count) => new(kind, count);
        private static Slot[] W(params Slot[] slots) => slots;
        private static Slot[][] Repeat(int waves, Slot[] wave) => Enumerable.Repeat(wave, waves).ToArray();

        // Balance-1 (2026-11-24): a large room is two or three waves of 10~14 enemies in all, down from three waves
        // of up to 20, so a long fight no longer drains the player before the floor's boss. Crossfire and the nest
        // keep three smaller waves; the others are two.
        public const int LargeMinimumWaves = 2;
        public const int LargeMaximumWaves = 3;
        public const int LargeMinimumEnemies = 10;
        public const int LargeMaximumEnemies = 14;

        // Small keeps the waves it had before Density-0, in the same rule order. A charger comes first in its
        // wave so it claims the charge lane before a rear-firing enemy can.
        public static Slot[][] Layout(string profileId, string pattern)
        {
            bool small = profileId == Week14Room3Setup.SmallProfileId;
            bool basic = profileId == Week14Room1Setup.BasicProfileId;
            bool large = profileId == Week14Room6Setup.LargeProfileId;
            return pattern switch
            {
                "pressure" when small => Repeat(2, W(S(SlotKind.PressureMelee, 1), S(SlotKind.PressureRear, 1))),
                "pressure" when basic => Repeat(2, W(S(SlotKind.PressureMelee, 2), S(SlotKind.PressureRear, 1))),
                "pressure" when large => new[]
                {
                    W(S(SlotKind.PressureMelee, 4), S(SlotKind.PressureRear, 2)),
                    W(S(SlotKind.PressureMelee, 4), S(SlotKind.PressureRear, 3)),
                },
                "pressure" => new[]
                {
                    W(S(SlotKind.PressureMelee, 3), S(SlotKind.PressureRear, 2)),
                    W(S(SlotKind.PressureMelee, 4), S(SlotKind.PressureRear, 2)),
                },

                "crossfire" when small => new[]
                    { W(S(SlotKind.CrossfireRear, 3)), W(S(SlotKind.Chaser, 1), S(SlotKind.Charger, 1)) },
                "crossfire" when basic => new[]
                    { W(S(SlotKind.CrossfireRear, 3)), W(S(SlotKind.Charger, 1), S(SlotKind.Chaser, 2)) },
                "crossfire" when large => new[]
                {
                    W(S(SlotKind.CrossfireRear, 4), S(SlotKind.Chaser, 1)),
                    W(S(SlotKind.Charger, 1), S(SlotKind.Chaser, 2), S(SlotKind.CrossfireRear, 1)),
                    W(S(SlotKind.CrossfireRear, 3), S(SlotKind.Chaser, 2)),
                },
                "crossfire" => new[]
                {
                    W(S(SlotKind.CrossfireRear, 3), S(SlotKind.Chaser, 1)),
                    W(S(SlotKind.Charger, 1), S(SlotKind.Chaser, 2), S(SlotKind.CrossfireRear, 2)),
                    W(S(SlotKind.CrossfireRear, 3), S(SlotKind.Chaser, 2)),
                },

                "swarm" when small => Repeat(2, W(S(SlotKind.SwarmMelee, 3))),
                "swarm" when basic => Repeat(2, W(S(SlotKind.SwarmMelee, 4))),
                "swarm" when large => Repeat(2, W(S(SlotKind.SwarmMelee, 7))),
                "swarm" => Repeat(2, W(S(SlotKind.SwarmMelee, 6))),

                "elite-pair" when small => new[] { W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 1)) },
                "elite-pair" when basic => new[]
                    { W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 1), S(SlotKind.Chaser, 1)) },
                "elite-pair" when large => new[]
                {
                    W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 1), S(SlotKind.Chaser, 3)),
                    W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 2), S(SlotKind.Chaser, 3)),
                },
                "elite-pair" => new[]
                {
                    W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 1), S(SlotKind.Chaser, 2)),
                    W(S(SlotKind.Charger, 1), S(SlotKind.Sniper, 2), S(SlotKind.Chaser, 3)),
                },

                "jyubi-swarm" when small => Repeat(2, W(S(SlotKind.Jyubi, 3))),
                "jyubi-swarm" when basic => Repeat(2, W(S(SlotKind.Jyubi, 4))),
                "jyubi-swarm" when large => Repeat(2, W(S(SlotKind.Jyubi, 7))),
                "jyubi-swarm" => Repeat(2, W(S(SlotKind.Jyubi, 6))),

                "jyubi-nest" when small => new[]
                    { W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 2)), W(S(SlotKind.Chaser, 3)) },
                "jyubi-nest" when basic => new[]
                    { W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 3)), W(S(SlotKind.Chaser, 4)) },
                "jyubi-nest" when large => new[]
                {
                    W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 3)),
                    W(S(SlotKind.Chaser, 4)),
                    W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 2), S(SlotKind.Chaser, 3)),
                },
                "jyubi-nest" => new[]
                {
                    W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 3)),
                    W(S(SlotKind.Chaser, 4)),
                    W(S(SlotKind.JyubiKing, 1), S(SlotKind.Jyubi, 2), S(SlotKind.Chaser, 3)),
                },

                _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern,
                    "Density-0 has no wave layout for this Encounter pattern."),
            };
        }

        // Density-1 lowers the chaser's weight in small rooms.
        public static EncounterEnemyCandidate[] Candidates(SlotKind kind, Week19Spawn2Setup.ProfileSpec spec) =>
            Density1Setup.ForProfile(spec.ProfileId, BaseCandidates(kind, spec));

        // Attribute-0 adds the new 불효자손·산사모 to the slots they fit.
        public static EncounterEnemyCandidate[] BaseCandidates(SlotKind kind, Week19Spawn2Setup.ProfileSpec spec) =>
            EnemyAttribute0Setup.Extend(kind, AuthoredCandidates(kind, spec));

        private static EncounterEnemyCandidate[] AuthoredCandidates(SlotKind kind,
            Week19Spawn2Setup.ProfileSpec spec) =>
            kind switch
            {
                SlotKind.PressureMelee => Week27Enemy13Setup.PressureMeleeCandidates(),
                SlotKind.PressureRear => new[]
                {
                    new EncounterEnemyCandidate(EncounterEnemyRole.Ranged, spec.QuickRangedWeight),
                    new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, spec.SniperWeight),
                },
                SlotKind.CrossfireRear => Week27Enemy12Setup.CrossfireRearCandidates(spec),
                SlotKind.Chaser => Week27Enemy11Setup.FollowUpCandidates(),
                SlotKind.Charger => new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Charging, 100) },
                SlotKind.Sniper => new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Sniper, 100) },
                SlotKind.SwarmMelee => Week27Enemy10Setup.SwarmMeleeCandidates(),
                SlotKind.Jyubi => new[] { new EncounterEnemyCandidate(EncounterEnemyRole.Jyubi, 100) },
                SlotKind.JyubiKing => new[] { new EncounterEnemyCandidate(EncounterEnemyRole.JyubiKing, 100) },
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };

        public static EncounterWaveDefinition[] Waves(Week19Spawn2Setup.ProfileSpec spec, string pattern)
        {
            Slot[][] layout = Layout(spec.ProfileId, pattern);
            EncounterWaveDefinition[] waves = new EncounterWaveDefinition[layout.Length];
            for (int index = 0; index < layout.Length; index++)
            {
                waves[index] = new EncounterWaveDefinition(index + 1,
                    index == 0 ? EncounterWaveStartCondition.RoomEntered : EncounterWaveStartCondition.PreviousWaveCleared,
                    EncounterWaveCompletionCondition.AllRequiredEnemiesDefeated,
                    layout[index].Select(slot => new EncounterSpawnRule(Candidates(slot.Kind, spec), slot.Count, null,
                        "all")).ToArray());
            }

            return waves;
        }

        // Small rooms keep the waves they had before Density-0, so the older verifiers check their exact values
        // there and leave the bigger sizes to Density0Verification.
        public static bool IsSmall(EncounterDefinition definition) =>
            definition != null && definition.AllowedProfiles.Count == 1 &&
            definition.AllowedProfiles[0] != null &&
            definition.AllowedProfiles[0].ProfileId == Week14Room3Setup.SmallProfileId;

        public static bool IsSmall(string profileId) => profileId == Week14Room3Setup.SmallProfileId;

        public static int EnemyCount(string profileId, string pattern) =>
            Layout(profileId, pattern).Sum(wave => wave.Sum(slot => slot.Count));

        // ---- Encounter assets ---------------------------------------------------------------------------------

        // Rewrites the waves of one pattern for every size, keeping each asset's ID, profile and Curve-1 floors,
        // and declares the threat range its candidates can produce.
        public static EncounterDefinition[] ConfigureEncounters(IEnumerable<string> patterns,
            RoomDifficultyTable table)
        {
            if (table == null) throw new InvalidOperationException("Density-0 needs the room difficulty table.");
            List<EncounterDefinition> definitions = new();
            foreach (string pattern in patterns)
            foreach (Week19Spawn2Setup.ProfileSpec spec in Week19Spawn2Setup.Profiles)
            {
                RoomProfile profile = AssetDatabase.LoadAssetAtPath<RoomProfile>(spec.ProfilePath);
                if (profile == null || profile.ProfileId != spec.ProfileId)
                    throw new InvalidOperationException($"Density-0 is missing Room Profile '{spec.ProfileId}'.");

                string path = Week19Spawn2Setup.EncounterPath(spec.ProfileId, pattern);
                EncounterDefinition definition = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<EncounterDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Undo.RecordObject(definition, "Configure Density-0 Encounter");
                definition.Configure(Week19Spawn2Setup.EncounterId(spec.ProfileId, pattern), new[] { profile },
                    Week27Curve1Setup.MinimumFloor(pattern), Week27Curve1Setup.MaximumFloor,
                    Week14Encounter1Setup.MinimumPlayerDistance,
                    Week14Encounter1Setup.MinimumDoorDistance,
                    Waves(spec, pattern),
                    EncounterClearCondition.AllWavesCleared);
                if (!table.TryComputeThreatRange(definition, out int minimum, out int maximum, out string error))
                    throw new InvalidOperationException($"Density-0 cannot score '{definition.EncounterId}'. {error}");
                definition.ConfigureDeclaredThreat(minimum, maximum);
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            return definitions.OrderBy(definition => definition.EncounterId, StringComparer.Ordinal).ToArray();
        }

        public static RoomDifficultyTable LoadTable()
        {
            RoomDifficultyTable table =
                AssetDatabase.LoadAssetAtPath<RoomDifficultyTable>(Week19Difficulty1Setup.TablePath);
            if (table == null)
                throw new InvalidOperationException($"Density-0 needs {Week19Difficulty1Setup.TablePath}.");
            return table;
        }

        // ---- Difficulty table ---------------------------------------------------------------------------------

        // How many more enemies a size's Encounters hold on average than the small ones, in hundredths.
        public static int ScalePercent(string profileId)
        {
            double small = Week27Curve1Setup.Patterns.Average(pattern =>
                (double)EnemyCount(Week14Room3Setup.SmallProfileId, pattern));
            double size = Week27Curve1Setup.Patterns.Average(pattern => (double)EnemyCount(profileId, pattern));
            return (int)Math.Round(size / small * 100d);
        }

        private static int Scale(int value, int percent) => (int)Math.Round(value * percent / 100d);

        public static RoomSizeDifficulty[] SizeDifficulties()
        {
            return Sizes.Select(size =>
            {
                int percent = ScalePercent(size.ProfileId);
                int maximumRoom = Week27Curve1Setup.Patterns.Max(pattern => EnemyCount(size.ProfileId, pattern));
                return new RoomSizeDifficulty(size.ProfileId, MinimumRoomEnemies,
                    size.ProfileId == Week14Room3Setup.SmallProfileId
                        ? Week19Encounter4Setup.MaximumRoomEnemies
                        : maximumRoom,
                    size.MaximumWaveEnemies, size.MaximumWaveRearFiring,
                    Week19Difficulty1Setup.TierBands
                        .Select(band => new RoomDifficultyTierBand(band.Tier, Scale(band.MinimumScore, percent)))
                        .ToArray(),
                    Week19Difficulty1Setup.FloorRanges
                        .Select(range => new FloorDifficultyRange(range.FloorNumber,
                            Scale(range.MinimumScore, percent), Scale(range.MaximumScore, percent)))
                        .ToArray());
            }).ToArray();
        }

        // ---- SpawnPoints of the templates authored before the importer ----------------------------------------

        public static bool IsAuthoredCombatTemplate(RoomTemplateDefinition template) =>
            template != null && !RoomLayoutImporter.IsImportedTemplate(template) &&
            template.SupportsRoomType(RoomType.Normal) && template.Profile != null &&
            Sizes.Any(size => size.ProfileId == template.Profile.ProfileId);

        // The SpawnPoints Encounter-4 gave the template; the ones after them are generated.
        public static int AuthoredSpawnPointCount(RoomTemplateDefinition template) =>
            Week19Encounter4Setup.AddedSpawnPoints.TryGetValue(template.TemplateId,
                out Week19Encounter4Setup.AddedSpawnPoint[] added)
                ? Week19Encounter4Setup.BaseSpawnPointCount + added.Length
                : Week19Encounter4Setup.BaseSpawnPointCount;

        public static Vector2[] ResolveSpawnPoints(RoomTemplateDefinition template)
        {
            if (!RoomObstacleLayout.TryCollectFootprints(template.RoomPrefabAsset,
                    out List<RoomObstacleFootprint> footprints, out string error))
                throw new InvalidOperationException($"Density-0 cannot read '{template.TemplateId}'. {error}");
            Vector2 chestSize = Vector2.one * ChestPlacement.ChestWorldSize;
            footprints.AddRange(template.AuthoredChests.Select(chest => new RoomObstacleFootprint(chest.ChestId,
                new Rect(chest.LocalPosition - chestSize * 0.5f, chestSize), false)));
            return RoomSpawnPointDensity.Extend(template.TemplateId, template.Profile.MovementBounds,
                template.Profile.EncounterBounds, template.DoorSlots,
                template.SpawnPoints.Take(AuthoredSpawnPointCount(template)).ToArray(), footprints,
                RoomObstacleLayout.CollectFloorTraps(template.RoomPrefabAsset),
                SpawnPointTarget(template.Profile.ProfileId));
        }

        private static void ExpandAuthoredTemplates(FloorGenerator generator)
        {
            foreach (RoomTemplateDefinition template in generator.RoomTemplates.Where(IsAuthoredCombatTemplate))
            {
                int authored = AuthoredSpawnPointCount(template);
                if (template.SpawnPoints.Count < authored)
                    throw new InvalidOperationException(
                        $"Density-0 needs Room Template '{template.TemplateId}' with its {authored} Encounter-4 " +
                        "SpawnPoints.");

                Vector2[] points = ResolveSpawnPoints(template);
                SpawnPointPlacementRole[] roles = template.SpawnPointRoles.Take(authored)
                    .Concat(Enumerable.Repeat(MeleeRear, points.Length - authored)).ToArray();
                if (template.SpawnPoints.SequenceEqual(points) && template.SpawnPointRoles.SequenceEqual(roles))
                    continue;

                string prefabPath = AssetDatabase.GetAssetPath(template.RoomPrefabAsset);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    RoomPrefab room = root.GetComponent<RoomPrefab>();
                    RoomSpawnPointAuthoring.Sync(room != null ? room.Controller : null, points);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                        throw new InvalidOperationException($"Density-0 could not save {prefabPath}.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                Undo.RecordObject(template, "Configure Density-0 SpawnPoints");
                template.Configure(template.TemplateId, template.Profile,
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), template.AllowedRoomTypes.ToArray(),
                    template.DoorSlots.ToArray(), points, template.MinimumFloor, template.MaximumFloor, roles);
                EditorUtility.SetDirty(template);
                if (!template.TryValidate(out string error) || !template.TryValidateLayout(out error))
                    throw new InvalidOperationException(
                        $"Density-0 SpawnPoints are invalid for '{template.TemplateId}'. {error}");
            }
        }

        // ---- Setup --------------------------------------------------------------------------------------------

        [MenuItem("Trickal Fan Game/Beta/Setup Density-0 Enemies by Room Size")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before Density-0 setup.");
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null || generator.DifficultyTable == null ||
                generator.EncounterContentVersion < Week27Variant2Setup.EncounterContentVersion)
                throw new InvalidOperationException("Density-0 requires the completed Variant-2 Game Scene.");

            RoomDifficultyTable table = generator.DifficultyTable;
            Undo.RecordObject(table, "Configure Density-0 room sizes");
            table.ConfigureSizeDifficulties(SizeDifficulties());
            EditorUtility.SetDirty(table);
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"Density-0 difficulty table is invalid. {error}");

            EncounterDefinition[] updated = ConfigureEncounters(Week27Curve1Setup.Patterns, table);
            HashSet<string> updatedIds = new(updated.Select(definition => definition.EncounterId),
                StringComparer.Ordinal);
            EncounterDefinition[] definitions = generator.EncounterDefinitions
                .Where(definition => definition != null && !updatedIds.Contains(definition.EncounterId))
                .Concat(updated)
                .OrderBy(definition => definition.EncounterId, StringComparer.Ordinal)
                .ToArray();
            foreach (EncounterDefinition definition in definitions)
            {
                if (!definition.TryValidate(out error) || !definition.TryValidateThreat(table, out error) ||
                    !table.TryValidateEncounterSize(definition, out error))
                    throw new InvalidOperationException($"Density-0 catalog is invalid. {error}");
            }

            Undo.RecordObject(generator, "Configure Density-0 Encounters");
            generator.ConfigureEncounters(Math.Max(Balance1EncounterContentVersion, generator.EncounterContentVersion),
                definitions);
            EditorUtility.SetDirty(generator);
            AssetDatabase.SaveAssets();

            ExpandAuthoredTemplates(generator);
            if (!TryValidatePlacement(generator, generator.RoomTemplates.Where(IsAuthoredCombatTemplate), out error))
                throw new InvalidOperationException($"Density-0 authored rooms cannot hold the Encounters. {error}");

            // Rebuilds every Layout room once (BuildRevision 4) with its generated SpawnPoints and proves each
            // against every Encounter; a rejected room would keep its old SpawnPoints, so it stops the setup.
            RoomLayoutImporter.Report report = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
            if (report.Rejected.Count > 0)
                throw new InvalidOperationException($"Density-0 Layout import rejected rooms.\n{report.Format()}");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during Density-0 setup.");
            AssetDatabase.SaveAssets();

            Debug.Log("Density-0 ready: " + string.Join(", ", table.SizeDifficulties.Select(size =>
                          $"{size.ProfileId} {size.MaximumWaveEnemies}/wave {size.MinimumRoomEnemies}~" +
                          $"{size.MaximumRoomEnemies}/room x{ScalePercent(size.ProfileId)}% floors " +
                          string.Join("/", size.FloorRanges.Select(range => range.MaximumScore)) + " tiers " +
                          string.Join("/", size.TierBands.Select(band => band.MinimumScore)) + " SpawnPoints " +
                          SpawnPointTarget(size.ProfileId))) +
                      $"; {report.Imported.Count(room => room.Rebuilt)} Layout rooms rebuilt, Encounter content " +
                      $"version {generator.EncounterContentVersion}.");
        }

        // Every Encounter of a template's size must be placeable on every floor and with any set of open doors.
        public static bool TryValidatePlacement(FloorGenerator generator,
            IEnumerable<RoomTemplateDefinition> templates, out string error)
        {
            foreach (RoomTemplateDefinition template in templates)
            {
                if (!RoomLayoutImporter.TryValidateBuilt(template, generator, out error))
                {
                    error = $"Room Template '{template.TemplateId}': {error}";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
