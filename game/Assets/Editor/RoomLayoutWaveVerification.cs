using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Layout-Wave (2026-11-24): a room Layout may fix its waves with wave1~wave6. "auto" keeps the rolled
    // Encounter's wave (the old rule), and "chaser*3, sniper|ranged*2" places those enemies. A room without wave lines
    // keeps the old rule entirely. The room layout editor writes the same enemy IDs.
    // Layout-Wave-2 (2026-11-25): "!elite" fixes an entry's variant and "@5/6" pins it to SpawnPoints.
    public static class RoomLayoutWaveVerification
    {
        public const string EditorSourcePath = "../tools/room-layout-editor/editor.html";
        private const int SeedSearch = 1024;

        [MenuItem("Trickal Fan Game/Tools/Verify Room Layout Waves")]
        public static void Verify()
        {
            VerifyFormat();
            VerifyEditorEnemyIds();
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Layout-Wave requires the Game Scene generator.");
            VerifyTemplateWaves(generator);
            VerifyPinnedSpawnPoints(generator);
            VerifyLayoutVariants();
            string generation = VerifyGeneration(generator);
            Debug.Log("Room layout wave verification passed: wave1~6 parse and round-trip, editor enemy IDs match, " +
                      "auto waves reuse the Encounter and fixed waves place their own enemies, pinned entries take " +
                      $"their SpawnPoints and fixed variants replace the roll. {generation}");
        }

        private static void VerifyFormat()
        {
            Assert(RoomLayoutFormat.TryParseWave("auto", out RoomLayoutWave auto, out string error) && auto.IsAuto,
                error ?? "'auto' must parse as an auto wave.");
            Assert(RoomLayoutFormat.TryParseWave("chaser*3, sniper|ranged*2, jyubi", out RoomLayoutWave wave,
                       out error) && !wave.IsAuto && wave.SpawnRules.Count == 3 && wave.FixedEnemyCount == 6 &&
                   wave.SpawnRules[1].Candidates.Count == 2 && wave.SpawnRules[2].Count == 1,
                error ?? "A fixed wave must keep its entries, alternatives and counts.");
            Assert(RoomLayoutFormat.FormatWave(wave) == "chaser*3, sniper|ranged*2, jyubi",
                "A fixed wave must write back to the same text.");
            foreach (string broken in new[]
                     {
                         "", "boss", "chaser*0", "chaser*13", "chaser|chaser", "chaser,,sniper", "dragon",
                         "jyubi!elite", "chaser|jyubi!champion", "self-destruct!champion-blast", "chaser!king",
                         "chaser*3@1/2", "chaser@0", "chaser@1/1", "chaser@", "chaser@a",
                     })
                Assert(!RoomLayoutFormat.TryParseWave(broken, out _, out _), $"Wave '{broken}' must be rejected.");

            // Layout-Wave-2: variants and SpawnPoints round-trip; 1-based in text, 0-based in the entry.
            const string authored = "sniper*2!champion@5/6, chaser!elite, ranged*2@1/2/3, self-destruct!champion-shots";
            Assert(RoomLayoutFormat.TryParseWave(authored, out RoomLayoutWave pinned, out error), error);
            Assert(pinned.Entries[0].Variant == RoomLayoutVariant.Champion &&
                   pinned.Entries[0].SpawnPoints.SequenceEqual(new[] { 4, 5 }) &&
                   pinned.Entries[1].Variant == RoomLayoutVariant.Elite && !pinned.Entries[1].IsPinned &&
                   pinned.Entries[2].Variant == RoomLayoutVariant.Random && pinned.Entries[2].SpawnPoints.Count == 3 &&
                   pinned.Entries[3].Variant == RoomLayoutVariant.ChampionShots,
                "Variants and SpawnPoints must parse per entry.");
            Assert(RoomLayoutFormat.FormatWave(pinned) == authored, "A pinned wave must write back to the same text.");

            // A real room file with wave lines put in front of its grid.
            string sourcePath = RoomLayoutImporter.ListFiles(RoomLayoutImporter.LayoutFolderPath).First();
            string fileName = Path.GetFileName(sourcePath);
            string source = File.ReadAllText(sourcePath, Encoding.UTF8).Replace("\r\n", "\n");
            Assert(!source.Contains("wave1:"), $"{fileName} must not fix waves for this check.");
            string Text(string waves) => source.Replace(RoomLayoutFormat.GridMarker + "\n",
                waves + RoomLayoutFormat.GridMarker + "\n");
            Assert(RoomLayoutFormat.TryParse(Text(""), fileName, out RoomLayoutDocument plain, out error) &&
                   plain.Waves.Length == 0, error ?? "A room without wave lines keeps the old rule.");
            Assert(RoomLayoutFormat.TryParse(Text("wave1: auto\nwave2: chaser*2\nwave3: auto\n"), fileName,
                       out RoomLayoutDocument planned, out error) &&
                   planned.Waves.Length == 3 && planned.Waves[0].IsAuto && !planned.Waves[1].IsAuto &&
                   planned.Waves[2].IsAuto,
                error ?? "wave1~3 must parse in order.");
            Assert(!RoomLayoutFormat.TryParse(Text("wave2: auto\n"), fileName, out _, out error) &&
                   error.Contains("wave1"), "A wave line after a gap must be rejected.");
            Assert(!RoomLayoutFormat.TryParse(Text("wave7: auto\n"), fileName, out _, out _),
                "A seventh wave must be rejected.");
            Assert(!RoomLayoutFormat.TryParse(Text($"wave1: chaser@{plain.Profile.SpawnPointCount + 1}\n"), fileName,
                       out _, out error) && error.Contains("does not exist"),
                "A SpawnPoint past the profile's last must be rejected.");
            Assert(RoomLayoutFormat.TryParse(Text($"wave1: chaser@{plain.Profile.SpawnPointCount}\n"), fileName,
                    out _, out error), error ?? "The profile's last SpawnPoint must be accepted.");
        }

        // The editor's ENEMIES list and the format's enemy IDs must be the same set.
        private static void VerifyEditorEnemyIds()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EditorSourcePath));
            Assert(File.Exists(path), $"Missing room layout editor source {path}.");
            string html = File.ReadAllText(path, Encoding.UTF8);
            Match list = Regex.Match(html, @"const ENEMIES=\[(.*?)\];", RegexOptions.Singleline);
            Assert(list.Success, "The editor must declare its ENEMIES list.");
            HashSet<string> editorIds = new(Regex.Matches(list.Groups[1].Value, @"\['([a-z-]+)'")
                .Select(match => match.Groups[1].Value), StringComparer.Ordinal);
            HashSet<string> formatIds = new(RoomLayoutFormat.EnemyIds.Keys, StringComparer.Ordinal);
            Assert(editorIds.SetEquals(formatIds),
                $"Editor enemy IDs ({string.Join(", ", editorIds.OrderBy(id => id))}) must match the format's " +
                $"({string.Join(", ", formatIds.OrderBy(id => id))}).");
            foreach (KeyValuePair<string, EncounterEnemyRole> entry in RoomLayoutFormat.EnemyIds)
            {
                Match floor = Regex.Match(list.Groups[1].Value, $@"\['{entry.Key}','[^']*',(\d+)");
                Assert(floor.Success && int.Parse(floor.Groups[1].Value) ==
                       EncounterEnemyFloorPool.IntroductionFloor(entry.Value),
                    $"The editor's first floor for '{entry.Key}' must be " +
                    $"{EncounterEnemyFloorPool.IntroductionFloor(entry.Value)}.");
            }
        }

        private static void VerifyTemplateWaves(FloorGenerator generator)
        {
            RoomTemplateDefinition source = generator.RoomTemplates.First(template =>
                template != null && RoomLayoutImporter.IsImportedTemplate(template) &&
                template.Profile.ProfileId == Week14Room6Setup.LargeProfileId);
            EncounterDefinition encounter = generator.EncounterDefinitions.First(definition =>
                definition != null && definition.Supports(source.Profile, 1));
            RoomTemplateDefinition template = UnityEngine.Object.Instantiate(source);
            try
            {
                Assert(RoomLayoutFormat.TryParseWave("chaser*3, sniper", out RoomLayoutWave fixedWave,
                    out string error), error);
                template.ConfigureLayoutWaves(new[] { RoomLayoutWave.Auto, fixedWave, RoomLayoutWave.Auto });
                Assert(template.TryValidate(out error), error);
                ResolvedEncounterSpawn[][] encounterWaves = new ResolvedEncounterSpawn[encounter.Waves.Count][];
                for (int index = 0; index < encounterWaves.Length; index++)
                    Assert(encounter.TryResolveWave(source, 1, null, index, 17, out encounterWaves[index], out error),
                        error);

                Assert(template.TryApplyLayoutWaves(encounter, encounterWaves, 1, null, 17,
                    out ResolvedEncounterSpawn[][] waves, out error), error);
                Assert(waves.Length == 3 && Same(waves[0], encounterWaves[0]) &&
                       Same(waves[2], encounterWaves[2 % encounterWaves.Length]),
                    "Auto waves must take the Encounter's waves, cycling past its last wave.");
                Assert(waves[1].Count(spawn => spawn.Role == EncounterEnemyRole.Chaser) == 3 &&
                       waves[1].Count(spawn => spawn.Role == EncounterEnemyRole.Sniper) == 1 && waves[1].Length == 4 &&
                       waves[1].Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == 4 &&
                       waves[1].All(spawn => template.SupportsEnemyRole(spawn.SpawnPointIndex, spawn.Role)),
                    "A fixed wave must place exactly its enemies on distinct, role-compatible SpawnPoints.");
                Assert(template.TryApplyLayoutWaves(encounter, encounterWaves, 1, null, 17,
                           out ResolvedEncounterSpawn[][] replay, out _) &&
                       replay.Zip(waves, Same).All(same => same),
                    "The same seed must place the same Layout waves.");

                Assert(RoomLayoutFormat.TryParseWave("ranged*2", out RoomLayoutWave early, out error), error);
                template.ConfigureLayoutWaves(new[] { early });
                Assert(!RoomLayoutImporter.TryValidateLayoutWaves(template, encounter, 1, null, out error) &&
                       error.Contains("floor 2") &&
                       RoomLayoutImporter.TryValidateLayoutWaves(template, encounter, 2, null, out _),
                    "An enemy that appears from floor 2 must be rejected on floor 1 and accepted on floor 2.");

                Assert(RoomLayoutFormat.TryParseWave("charging*3", out RoomLayoutWave lanes, out error), error);
                template.ConfigureLayoutWaves(new[] { lanes });
                Assert(!RoomLayoutImporter.TryValidateLayoutWaves(template, encounter, 2, null, out _),
                    "More chargers than charge lanes must be rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        // Layout-Wave-2: an entry pinned to SpawnPoints uses only those, and pinned entries are placed before free
        // ones so a free entry never takes a pinned point.
        private static void VerifyPinnedSpawnPoints(FloorGenerator generator)
        {
            RoomTemplateDefinition source = generator.RoomTemplates.First(template =>
                template != null && RoomLayoutImporter.IsImportedTemplate(template) &&
                template.Profile.ProfileId == Week14Room6Setup.LargeProfileId);
            RoomTemplateDefinition template = UnityEngine.Object.Instantiate(source);
            try
            {
                ResolvedEncounterSpawn[] Resolve(string text, out RoomLayoutVariant[] variants)
                {
                    Assert(RoomLayoutFormat.TryParseWave(text, out RoomLayoutWave wave, out string parseError),
                        parseError);
                    template.ConfigureLayoutWaves(new[] { wave });
                    Assert(template.TryValidate(out parseError), parseError);
                    Assert(template.TryResolveLayoutWave(0, 2, null, 17, 1.5f, 2f, out ResolvedEncounterSpawn[] spawns,
                        out variants, out parseError), $"'{text}' must place. {parseError}");
                    return spawns;
                }

                // Free chasers take the closest points; pinning one to the farthest of them moves it there.
                int[] free = Resolve("chaser*4", out _).Select(spawn => spawn.SpawnPointIndex).ToArray();
                int far = free[^1];
                string farText = $"chaser@{far + 1}";
                Assert(Resolve(farText, out _).Single().SpawnPointIndex == far,
                    $"'{farText}' must stand on SpawnPoint {far + 1}, not the closest free point {free[0] + 1}.");

                // The free entry comes first in the text but must not take the pinned point.
                string mixedText = $"chaser*2, chaser@{free[0] + 1}";
                ResolvedEncounterSpawn[] mixed = Resolve(mixedText, out _);
                Assert(mixed.Length == 3 && mixed[0].SpawnPointIndex == free[0] &&
                       mixed.Select(spawn => spawn.SpawnPointIndex).Distinct().Count() == 3,
                    $"'{mixedText}' must keep SpawnPoint {free[0] + 1} for its pinned entry.");

                string choiceText = $"chaser*2!elite@{free[1] + 1}/{free[2] + 1}/{free[3] + 1}";
                ResolvedEncounterSpawn[] choice = Resolve(choiceText, out RoomLayoutVariant[] choiceVariants);
                Assert(choice.Length == 2 && choice.All(spawn => free.Skip(1).Contains(spawn.SpawnPointIndex)) &&
                       choiceVariants.All(variant => variant == RoomLayoutVariant.Elite),
                    $"'{choiceText}' must pick its two points among the listed three and keep its variant.");

                // A role the point cannot host fails the placement (the importer rejects the room).
                int meleeOnly = Enumerable.Range(0, template.SpawnPoints.Count).Where(index =>
                    template.SupportsEnemyRole(index, EncounterEnemyRole.Chaser) &&
                    !template.SupportsEnemyRole(index, EncounterEnemyRole.Charging)).DefaultIfEmpty(-1).First();
                Assert(meleeOnly >= 0, $"{template.TemplateId} must have a SpawnPoint without a charge lane.");
                Assert(RoomLayoutFormat.TryParseWave($"charging@{meleeOnly + 1}", out RoomLayoutWave lane,
                    out string error), error);
                template.ConfigureLayoutWaves(new[] { lane });
                Assert(!template.TryResolveLayoutWave(0, 2, null, 17, 1.5f, 2f, out _, out _, out _),
                    "A charger pinned to a point without a charge lane must be rejected.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        // Layout-Wave-2: fixed variants replace the floor's roll; an authored elite removes the rolled elite.
        private static void VerifyLayoutVariants()
        {
            ResolvedEncounterSpawn[][] rolled =
            {
                new[]
                {
                    new ResolvedEncounterSpawn(EncounterEnemyRole.Chaser, 0, EnemyVariantKind.Elite),
                    new ResolvedEncounterSpawn(EncounterEnemyRole.Chaser, 1),
                    new ResolvedEncounterSpawn(EncounterEnemyRole.Sniper, 2, EnemyVariantKind.ChampionPuddle),
                    new ResolvedEncounterSpawn(EncounterEnemyRole.Ranged, 3),
                },
                new[] { new ResolvedEncounterSpawn(EncounterEnemyRole.Chaser, 0, EnemyVariantKind.ChampionShots) },
            };
            Assert(ReferenceEquals(EnemyVariantRules.ApplyLayoutVariants(rolled, null, 7, 27), rolled) &&
                   ReferenceEquals(EnemyVariantRules.ApplyLayoutVariants(rolled,
                       new[] { new RoomLayoutVariant[4], new RoomLayoutVariant[1] }, 7, 27), rolled),
                "Random everywhere must keep the rolled variants.");

            ResolvedEncounterSpawn[][] result = EnemyVariantRules.ApplyLayoutVariants(rolled, new[]
            {
                new[]
                {
                    RoomLayoutVariant.Random, RoomLayoutVariant.Elite, RoomLayoutVariant.Normal,
                    RoomLayoutVariant.Champion,
                },
                new[] { RoomLayoutVariant.ChampionUndying },
            }, 7, 27);
            Assert(result[0][0].Variant == EnemyVariantKind.None && result[0][1].Variant == EnemyVariantKind.Elite &&
                   result[0][2].Variant == EnemyVariantKind.None &&
                   EnemyVariant.IsChampionKind(result[0][3].Variant) &&
                   result[0][3].Variant == EnemyVariantRules.PickChampionKind(EncounterEnemyRole.Ranged, 7, 27, 0, 3) &&
                   result[1][0].Variant == EnemyVariantKind.ChampionUndying &&
                   rolled[0][0].Variant == EnemyVariantKind.Elite,
                "Fixed variants must replace the roll (an authored elite clears the rolled one) without changing the " +
                "rolled arrays.");
            Assert(result.SelectMany(wave => wave).Zip(rolled.SelectMany(wave => wave), (after, before) =>
                    after.Role == before.Role && after.SpawnPointIndex == before.SpawnPointIndex).All(same => same),
                "Fixed variants must not move or change enemies.");
        }

        // Floor generation with a Layout's waves (set in memory only, then restored).
        private static string VerifyGeneration(FloorGenerator generator)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.First(candidate =>
                candidate != null && RoomLayoutImporter.IsImportedTemplate(candidate) &&
                candidate.Profile.ProfileId == Week14Room6Setup.LargeProfileId && !candidate.HasLayoutWaves);
            RoomLayoutWave[] original = template.LayoutWaves.ToArray();
            try
            {
                Assert(RoomLayoutFormat.TryParseWave("sniper*2!champion-shots, chaser|fast-chaser*4!normal",
                    out RoomLayoutWave fixedWave,
                    out string error), error);
                template.ConfigureLayoutWaves(new[] { fixedWave, RoomLayoutWave.Auto, RoomLayoutWave.Auto });
                int rooms = 0;
                for (int seed = 1; seed <= SeedSearch && rooms < 3; seed++)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out error),
                        $"Seed {seed} failed with Layout waves. {error}");
                    foreach (GeneratedRoomNode node in graph.Nodes.Where(node =>
                                 node.Template == template && node.Encounter != null))
                    {
                        rooms++;
                        IReadOnlyList<ResolvedEncounterSpawn[]> waves = node.ResolvedEncounterWaves;
                        Assert(waves.Count == 3 && waves[0].Length == 6 &&
                               waves[0].Count(spawn => spawn.Role == EncounterEnemyRole.Sniper) == 2 &&
                               waves[0].Count(spawn => spawn.Role is EncounterEnemyRole.Chaser or
                                   EncounterEnemyRole.FastChaser) == 4 &&
                               waves[0].All(spawn => spawn.Variant == (spawn.Role == EncounterEnemyRole.Sniper
                                   ? EnemyVariantKind.ChampionShots
                                   : EnemyVariantKind.None)),
                            $"Seed {seed} room {node.RoomId} must fight its fixed first wave (2 champion snipers, 4 plain " +
                            "chasers) and two auto waves.");
                        Assert(waves[1].Length > 0 && waves[2].Length > 0,
                            $"Seed {seed} room {node.RoomId} must keep its auto waves.");
                    }
                }

                Assert(rooms > 0, $"No seed up to {SeedSearch} placed {template.TemplateId}.");
                return $"{rooms} generated {template.TemplateId} rooms used the Layout waves.";
            }
            finally
            {
                template.ConfigureLayoutWaves(original);
            }
        }

        private static bool Same(ResolvedEncounterSpawn[] first, ResolvedEncounterSpawn[] second) =>
            first.Length == second.Length && first.Zip(second, (a, b) =>
                a.Role == b.Role && a.SpawnPointIndex == b.SpawnPointIndex && a.Variant == b.Variant).All(same => same);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
