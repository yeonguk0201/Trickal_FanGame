using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week15Enemy5Verification
    {
        private readonly struct VisualContract
        {
            public VisualContract(string name, string prefabPath, string spritePath, EncounterEnemyRole role)
            {
                Name = name;
                PrefabPath = prefabPath;
                SpritePath = spritePath;
                Role = role;
            }

            public string Name { get; }
            public string PrefabPath { get; }
            public string SpritePath { get; }
            public EncounterEnemyRole Role { get; }
        }

        [MenuItem("Trickal Fan Game/Week 15/Verify Enemy-5 Fairy Enemy Assets and Encounters")]
        public static void Verify()
        {
            ValidateImportedSpritesAndPrefabs();
            ValidateRoster();
            ValidateEncounterContracts();
            ValidateSeedCoverage();
            Week15Enemy2Verification.Verify();
            Week15Enemy3Verification.Verify();
            Week15Enemy4Verification.Verify();
            Debug.Log(
                "Week 15 Enemy-5 verification passed: all four supplied fairy-kingdom visuals are imported " +
                "and bound to the intended behaviors; Small rooms complete without snipers, Large rooms use " +
                "the sniper, and sniper/charge pressure never overlaps in one wave.");
        }

        public static void SetupAndVerifyBatch()
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week15Enemy5Setup.Setup();
            string[] tracked = Week15Enemy5Setup.SpritePaths()
                .Concat(Week15Enemy5Setup.EncounterPaths())
                .Concat(Visuals().Select(visual => visual.PrefabPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Dictionary<string, string> guids = tracked.ToDictionary(
                path => path,
                AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);

            Week15Enemy5Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Enemy-5 Setup changed the Game Scene GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) &&
                       AssetDatabase.AssetPathToGUID(entry.Key) == entry.Value,
                    $"Enemy-5 Setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        private static void ValidateImportedSpritesAndPrefabs()
        {
            HashSet<Sprite> sprites = new();
            foreach (VisualContract visual in Visuals())
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(visual.SpritePath);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(visual.SpritePath);
                TextureImporter importer = AssetImporter.GetAtPath(visual.SpritePath) as TextureImporter;
                Assert(texture != null && sprite != null && importer != null,
                    $"{visual.Name} must import as a Sprite texture.");
                Assert(texture.width == 1254 && texture.height == 1254,
                    $"{visual.Name} source must preserve the supplied 1254x1254 canvas.");
                Assert(importer.textureType == TextureImporterType.Sprite &&
                       importer.spriteImportMode == SpriteImportMode.Single &&
                       Mathf.Approximately(importer.spritePixelsPerUnit, 1000f) &&
                       importer.alphaIsTransparency && !importer.mipmapEnabled,
                    $"{visual.Name} has incorrect runtime sprite import settings.");
                Assert(sprites.Add(sprite), $"{visual.Name} must use a distinct supplied sprite asset.");

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(visual.PrefabPath);
                SpriteRenderer renderer = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
                Assert(renderer != null && renderer.sprite == sprite && renderer.color == Color.white,
                    $"{visual.Name} prefab must render its supplied sprite without a role-color tint.");
                Assert(MatchesBehavior(prefab, visual.Role),
                    $"{visual.Name} prefab does not implement its intended behavior role {visual.Role}.");
            }
        }

        private static void ValidateRoster()
        {
            EncounterEnemyRoster roster = AssetDatabase.LoadAssetAtPath<EncounterEnemyRoster>(
                Week14Encounter2Setup.RosterPath);
            Assert(roster != null, "Enemy-5 requires the Encounter enemy roster.");
            Assert(roster.TryValidate(out string error), error);
            foreach (VisualContract visual in Visuals())
            {
                GameObject expected = AssetDatabase.LoadAssetAtPath<GameObject>(visual.PrefabPath);
                Assert(roster.TryResolve(visual.Role, out GameObject actual, out error) && actual == expected,
                    $"{visual.Name} is not bound to Encounter role {visual.Role}. {error}");
            }

            Assert(roster.TryResolve(EncounterEnemyRole.Ranged, out GameObject legacyRanged, out error) &&
                   legacyRanged == AssetDatabase.LoadAssetAtPath<GameObject>(Week15Enemy0Setup.RangedPrefabPath),
                "Enemy-5 must preserve the non-sniper ranged role used by completed Room encounters.");
        }

        private static void ValidateEncounterContracts()
        {
            EncounterDefinition small = LoadEncounter(Week15Enemy5Setup.SmallEncounterPath);
            EncounterDefinition standard = LoadEncounter(Week15Enemy5Setup.StandardEncounterPath);
            EncounterDefinition large = LoadEncounter(Week15Enemy5Setup.LargeEncounterPath);
            Assert(ProfileIds(small).SequenceEqual(new[] { Week14Room3Setup.SmallProfileId }),
                "The compact fairy Encounter must support only the Small profile.");
            Assert(!AllRoles(small).Contains(EncounterEnemyRole.Sniper) &&
                   AllRoles(small).Contains(EncounterEnemyRole.Chaser) &&
                   AllRoles(small).Contains(EncounterEnemyRole.FastChaser) &&
                   AllRoles(small).Contains(EncounterEnemyRole.Charging),
                "Small rooms must exercise both melee variants and the charger without the sniper.");
            Assert(ProfileIds(standard).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    new[]
                    {
                        Week14Room1Setup.BasicProfileId,
                        Week14Room6Setup.TallProfileId,
                        Week14Room3Setup.WideProfileId,
                    }.OrderBy(id => id, StringComparer.Ordinal)),
                "The standard fairy Encounter must support Basic, Wide, and Tall profiles.");
            Assert(!AllRoles(standard).Contains(EncounterEnemyRole.Sniper),
                "The long-range sniper must not leak into non-Large fairy Encounters.");
            Assert(ProfileIds(large).SequenceEqual(new[] { Week14Room6Setup.LargeProfileId }) &&
                   Visuals().All(visual => AllRoles(large).Contains(visual.Role)),
                "The Large fairy Encounter must exercise all four supplied enemy visuals.");

            foreach (EncounterWaveDefinition wave in large.Waves)
            {
                EncounterEnemyRole[] roles = wave.SpawnRules.Select(rule => rule.EnemyRole).ToArray();
                Assert(!(roles.Contains(EncounterEnemyRole.Sniper) &&
                         roles.Contains(EncounterEnemyRole.Charging)),
                    "Sniper aim and charge telegraphs must not overlap in the same Large-room wave.");
                Assert(roles.Count(role => role == EncounterEnemyRole.Sniper) <= 1 &&
                       roles.Count(role => role == EncounterEnemyRole.Charging) <= 1,
                    "A wave may contain at most one long-range aim or charge threat.");
            }

            RoomTemplateDefinition smallTemplate = LoadTemplate(Week14Room3Setup.SmallTemplatePath);
            RoomTemplateDefinition basicTemplate = LoadTemplate(Week14Room1Setup.BasicTemplatePath);
            RoomTemplateDefinition wideTemplate = LoadTemplate(Week14Room3Setup.WideTemplatePath);
            RoomTemplateDefinition tallTemplate = LoadTemplate(Week14Room6Setup.TallTemplatePath);
            RoomTemplateDefinition largeTemplate = LoadTemplate(Week14Room6Setup.LargeTemplatePath);
            RoomTemplateDefinition pillarTemplate = LoadTemplate(Week14Room7Setup.TemplatePath);
            ValidateResolution(small, smallTemplate);
            ValidateResolution(standard, basicTemplate);
            ValidateResolution(standard, wideTemplate);
            ValidateResolution(standard, tallTemplate);
            ValidateResolution(large, largeTemplate);
            ValidateResolution(large, pillarTemplate);

            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null &&
                   generator.EncounterContentVersion >= Week15Enemy5Setup.EncounterContentVersion &&
                   generator.EncounterDefinitions.Any(candidate =>
                       candidate.EncounterId == Week15Enemy5Setup.SmallEncounterId) &&
                   generator.EncounterDefinitions.Any(candidate =>
                       candidate.EncounterId == Week15Enemy5Setup.StandardEncounterId) &&
                   generator.EncounterDefinitions.Any(candidate =>
                       candidate.EncounterId == Week15Enemy5Setup.LargeEncounterId),
                "The runtime Encounter catalog is missing Enemy-5 content.");
        }

        private static void ValidateResolution(
            EncounterDefinition encounter,
            RoomTemplateDefinition template)
        {
            Assert(encounter.TryValidateFor(template, 2, out string error), error);
            for (int waveIndex = 0; waveIndex < encounter.Waves.Count; waveIndex++)
            {
                Assert(encounter.TryResolveWave(template, 2, null, waveIndex,
                           out ResolvedEncounterSpawn[] resolved, out error), error);
                Assert(resolved.Length == resolved.Select(spawn => spawn.SpawnPointIndex).Distinct().Count(),
                    $"Encounter '{encounter.EncounterId}' wave {waveIndex + 1} reused a SpawnPoint.");
            }
        }

        private static void ValidateSeedCoverage()
        {
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            HashSet<string> seen = new(StringComparer.Ordinal);
            Assert(generator != null, "Enemy-5 seed coverage requires the configured FloorGenerator.");
            for (int seed = 1; seed <= 1024 && seen.Count < 3; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes.Where(node => node.Encounter != null))
                {
                    if (node.EncounterId == Week15Enemy5Setup.SmallEncounterId ||
                        node.EncounterId == Week15Enemy5Setup.StandardEncounterId ||
                        node.EncounterId == Week15Enemy5Setup.LargeEncounterId)
                    {
                        seen.Add(node.EncounterId);
                    }
                }
            }

            Assert(seen.SetEquals(new[]
                {
                    Week15Enemy5Setup.SmallEncounterId,
                    Week15Enemy5Setup.StandardEncounterId,
                    Week15Enemy5Setup.LargeEncounterId,
                }),
                "1,024 seeds must expose every Enemy-5 mixed Encounter.");
        }

        private static bool MatchesBehavior(GameObject prefab, EncounterEnemyRole role) => role switch
        {
            EncounterEnemyRole.Chaser => prefab.GetComponent<EnemyChase>() != null,
            EncounterEnemyRole.FastChaser => prefab.GetComponent<EnemyChase>() != null,
            EncounterEnemyRole.Charging => prefab.GetComponent<ChargingEnemyController>() != null,
            EncounterEnemyRole.Sniper => prefab.GetComponent<LongRangeSniperController>() != null,
            _ => false,
        };

        private static VisualContract[] Visuals() => new[]
        {
            new VisualContract("Bulhyojason", Week15Enemy2Setup.BulhyojasonPrefabPath,
                Week15Enemy5Setup.BulhyojasonSpritePath, EncounterEnemyRole.Chaser),
            new VisualContract("Sansamo", Week15Enemy2Setup.SansamoPrefabPath,
                Week15Enemy5Setup.SansamoSpritePath, EncounterEnemyRole.FastChaser),
            new VisualContract("LowBloodSugarFairy", Week15Enemy0Setup.ChargingPrefabPath,
                Week15Enemy5Setup.LowBloodSugarSpritePath, EncounterEnemyRole.Charging),
            new VisualContract("HighBloodSugarFairy", Week15Enemy4Setup.SniperPrefabPath,
                Week15Enemy5Setup.HighBloodSugarSpritePath, EncounterEnemyRole.Sniper),
        };

        private static EncounterDefinition LoadEncounter(string path)
        {
            EncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            Assert(encounter != null, $"Missing Enemy-5 Encounter at {path}.");
            Assert(encounter.TryValidate(out string error), error);
            return encounter;
        }

        private static RoomTemplateDefinition LoadTemplate(string path)
        {
            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(path);
            Assert(template != null, $"Missing Room Template at {path}.");
            Assert(template.TryValidate(out string error), error);
            return template;
        }

        private static IEnumerable<string> ProfileIds(EncounterDefinition encounter) =>
            encounter.AllowedProfiles.Select(profile => profile.ProfileId);

        private static HashSet<EncounterEnemyRole> AllRoles(EncounterDefinition encounter) =>
            encounter.Waves.SelectMany(wave => wave.SpawnRules)
                .Select(rule => rule.EnemyRole)
                .ToHashSet();

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
