using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // T7: the room text format, its importer and the runtime paths the imported rooms use (fixed obstacle kinds,
    // Layout chests, weighted template selection).
    public static class RoomLayoutImporterVerification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6200f, 6200f);
        private const int GeneratedSeedCount = 400;
        private const int DeterminismSeedCount = 32;
        private const int WeightSampleCount = 140000;
        // How far the 0.5 grid may push an outer cell past the walkable floor edge.
        private const float GridOverhang = 0.05f;

        [MenuItem("Trickal Fan Game/Tools/Import and Verify Room Layouts")]
        public static void ImportAndVerifyBatch()
        {
            RoomLayoutImporter.Report first = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
            Assert(first.Rejected.Count == 0, first.Format());
            Assert(first.Imported.Count > 0, "The room Layout folder has no room file.");
            string[] paths = first.Imported
                .SelectMany(room => new[]
                    { RoomLayoutImporter.TemplatePath(room.Id), RoomLayoutImporter.PrefabPath(room.Id) })
                .ToArray();
            Dictionary<string, string> guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Dictionary<string, string> contents = paths.ToDictionary(path => path, File.ReadAllText,
                StringComparer.Ordinal);

            RoomLayoutImporter.Report second = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
            Assert(second.Rejected.Count == 0 && second.Removed.Count == 0 &&
                   second.Imported.Count == first.Imported.Count && second.Imported.All(room => !room.Rebuilt),
                $"A second import of unchanged files must rebuild and remove nothing.\n{second.Format()}");
            foreach (string path in paths)
            {
                Assert(guids[path] == AssetDatabase.AssetPathToGUID(path) && contents[path] == File.ReadAllText(path),
                    $"A second import of unchanged files changed {path}.");
            }

            RoomLayoutImporter.Report forced = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true, true);
            Assert(forced.Rejected.Count == 0 && forced.Imported.All(room => room.Rebuilt),
                $"A forced import must rebuild every room.\n{forced.Format()}");
            foreach (string path in paths)
            {
                Assert(!string.IsNullOrWhiteSpace(guids[path]) && guids[path] == AssetDatabase.AssetPathToGUID(path),
                    $"A forced import changed or lost the GUID for {path}.");
            }

            ValidateRejectedEditAndRemoval(first.Imported.Select(room => room.Id).ToArray());
            Verify();
        }

        // A room that is edited into an invalid one keeps its last good import, and a room whose file is deleted
        // leaves the catalog with its own assets only. Uses a temporary room file and removes it again.
        private static void ValidateRejectedEditAndRemoval(string[] existingIds)
        {
            const string id = "basic-verification-temporary";
            string filePath = Path.Combine(RoomLayoutImporter.LayoutFolderPath, id + RoomLayoutFormat.FileSuffix);
            string templatePath = RoomLayoutImporter.TemplatePath(id);
            string prefabPath = RoomLayoutImporter.PrefabPath(id);
            Assert(!File.Exists(filePath), $"Remove the leftover verification room file {filePath}.");
            FloorGenerator Generator() => GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                .GetComponent<FloorGenerator>();
            bool InCatalog() => Generator().RoomTemplates.Any(template => template.TemplateId == id);
            try
            {
                File.WriteAllText(filePath, Basic(id, (0, 0, 'G'), (6, 12, '#')), new System.Text.UTF8Encoding(false));
                RoomLayoutImporter.Report added = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
                Assert(added.Rejected.Count == 0 && added.Imported.Count(room => room.Rebuilt) == 1 &&
                       added.Imported.Single(room => room.Rebuilt).Id == id && InCatalog(),
                    $"Adding one room file must build and register only that room.\n{added.Format()}");
                string templateGuid = AssetDatabase.AssetPathToGUID(templatePath);
                string prefabText = File.ReadAllText(prefabPath);

                File.WriteAllText(filePath, Basic(id, (3, 0, '#')), new System.Text.UTF8Encoding(false));
                RoomLayoutImporter.Report rejected = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
                Assert(rejected.Rejected.Count == 1 &&
                       rejected.Rejected[0].file == id + RoomLayoutFormat.FileSuffix &&
                       rejected.Rejected[0].reason.Contains("door passage", StringComparison.Ordinal) &&
                       rejected.Removed.Count == 0 && InCatalog() &&
                       AssetDatabase.AssetPathToGUID(templatePath) == templateGuid &&
                       File.ReadAllText(prefabPath) == prefabText,
                    $"A room edited into an invalid one must be rejected and keep its last good import.\n" +
                    rejected.Format());

                File.Delete(filePath);
                RoomLayoutImporter.Report removed = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
                Assert(removed.Rejected.Count == 0 && !InCatalog() &&
                       removed.Removed.OrderBy(path => path, StringComparer.Ordinal)
                           .SequenceEqual(new[] { prefabPath, templatePath }
                               .OrderBy(path => path, StringComparer.Ordinal)) &&
                       AssetDatabase.LoadAssetAtPath<Object>(templatePath) == null &&
                       AssetDatabase.LoadAssetAtPath<Object>(prefabPath) == null,
                    $"Deleting a room file must remove that room's template and Prefab only.\n{removed.Format()}");
                foreach (string existing in existingIds)
                {
                    Assert(AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(
                               RoomLayoutImporter.TemplatePath(existing)) != null &&
                           Generator().RoomTemplates.Any(template => template.TemplateId == existing),
                        $"Removing another room deleted or unregistered '{existing}'.");
                }
            }
            finally
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }

        [MenuItem("Trickal Fan Game/Tools/Verify Room Layouts")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= RoomLayoutImporter.RoomContentVersion,
                "Import the room Layouts before verification.");
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.ChestContentTable != null,
                "Room Layout verification needs the Game Scene assembler and its chest content table.");

            ValidateFormatRejections();
            List<RoomLayoutDocument> documents = ValidateCatalog(generator);
            ValidateRuntime(documents, assembler);
            ValidateWeightedPick();
            ValidateGeneration(generator, documents);
            ValidateForcedTemplate(generator, documents);
            Debug.Log($"Room Layout verification passed: the text format rejects malformed grids, blocked door " +
                      $"passages, unreachable SpawnPoints and unsafe chests with a reason, {documents.Count} imported " +
                      "rooms match their files (seeded rocks, fixed obstacle kinds, trees, merged pits, SpawnPoint " +
                      "roles, chests, weight and difficulty) and accept every Encounter of their profile for every " +
                      "door set, fixed kinds survive the seeded variant roll, Layout chests are placed once, restored " +
                      "after a rebuild and stay gone after leaving the floor, template selection follows the " +
                      $"weights, {GeneratedSeedCount} seeds generate with every imported room in use, and the " +
                      "development forced template reaches the rooms it fits.");
        }

        [MenuItem("Trickal Fan Game/Tools/Verify Room Layouts With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week19Encounter4Verification.Verify();
            Week19Spawn2Verification.Verify();
            Week19Difficulty1Verification.Verify();
            Week22Terrain0Verification.Verify();
            Week22Chest1Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week23Obstacle6Verification.VerifyWithRegressionsBatch();
            Week23Obstacle7Verification.Verify();
            Debug.Log("Room Layout regression verification passed.");
        }

        private static readonly string[] BaseBasicGrid =
        {
            ".............",
            ".............",
            "..1.......5..",
            "......3......",
            ".............",
            "..4.......2..",
            ".............",
        };

        private static bool Same(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) < 0.001f && Mathf.Abs(a.yMin - b.yMin) < 0.001f &&
            Mathf.Abs(a.xMax - b.xMax) < 0.001f && Mathf.Abs(a.yMax - b.yMax) < 0.001f;

        private static string Basic(string id, params (int row, int column, char symbol)[] edits)
        {
            char[][] rows = BaseBasicGrid.Select(row => row.ToCharArray()).ToArray();
            foreach ((int row, int column, char symbol) in edits) rows[row][column] = symbol;
            return $"id: {id}\nprofile: basic\ngrid:\n{string.Join("\n", rows.Select(row => new string(row)))}\n";
        }

        private static void ValidateFormatRejections()
        {
            const string id = "basic-verification";
            string file = id + RoomLayoutFormat.FileSuffix;
            // The grid is the largest unit grid on the walkable floor: no outer cell stands on a wall and no further
            // whole cell would fit.
            foreach (RoomLayoutProfile profile in RoomLayoutFormat.Profiles)
            {
                Rect floor = FairyVillageArtworkSetup.WalkableFloor(profile.RoomSize);
                Rect grid = profile.GridRect;
                Assert(Same(profile.Floor, floor) && Same(profile.CellRect(0, 0, profile.Columns, profile.Rows), grid) &&
                       profile.Columns == Mathf.FloorToInt(floor.width) && profile.Rows == Mathf.FloorToInt(floor.height) &&
                       grid.xMin >= floor.xMin - GridOverhang && grid.xMax <= floor.xMax + GridOverhang &&
                       grid.yMin >= floor.yMin - GridOverhang && grid.yMax <= floor.yMax + GridOverhang,
                    $"Profile '{profile.ProfileId}' grid {profile.Columns}x{profile.Rows} does not fit its walkable " +
                    $"floor {floor.xMin:0.###}..{floor.xMax:0.###} x {floor.yMin:0.###}..{floor.yMax:0.###}.");
            }

            Assert(RoomLayoutFormat.TryParse(Basic(id), file, out RoomLayoutDocument valid, out string error) &&
                   RoomLayoutImporter.TryValidateGeometry(valid, out error),
                $"The empty Basic verification room must be valid. {error}");
            Assert(valid.Weight == RoomTemplateDefinition.DefaultSelectionWeight && valid.Difficulty == 0 &&
                   valid.MinimumFloor == 1 && valid.MaximumFloor == 99 && valid.SpawnPoints.Length == 5 &&
                   valid.SpawnPoints[2] == valid.Profile.CellCenter(6, 3) && Mathf.Abs(valid.SpawnPoints[2].x) < 0.001f,
                "An empty room must default to weight 100, difficulty 0, every floor and cell-centered SpawnPoints.");
            Assert(RoomLayoutFormat.TryParse(Basic(id, (0, 0, '#')), file, out RoomLayoutDocument rocky, out error) &&
                   rocky.Difficulty == Week19Difficulty1Setup.ObstacleLayoutModifier,
                $"A room with terrain must default to the obstacle Layout difficulty. {error}");

            (string label, string text, string fileName, string expected)[] malformed =
            {
                ("a missing grid", "id: basic-verification\nprofile: basic\n", file, "grid:"),
                ("an unknown header key", Basic(id).Replace("grid:", "size: 3\ngrid:"), file, "Unknown header key"),
                ("a repeated header key", Basic(id).Replace("grid:", "weight: 5\nweight: 6\ngrid:"), file, "twice"),
                ("an unknown profile", Basic(id).Replace("profile: basic", "profile: huge"), file, "Unknown profile"),
                ("an ID that differs from its file", Basic(id), "basic-other" + RoomLayoutFormat.FileSuffix,
                    "must match its file name"),
                ("an ID without its profile prefix", Basic("verification-room"),
                    "verification-room" + RoomLayoutFormat.FileSuffix, "must start with its profile"),
                ("an uppercase ID", Basic("basic-Room"), "basic-Room" + RoomLayoutFormat.FileSuffix, "lowercase"),
                ("a zero weight", Basic(id).Replace("grid:", "weight: 0\ngrid:"), file, "weight"),
                ("a reversed floor range", Basic(id).Replace("grid:", "floors: 3-1\ngrid:"), file, "floors"),
                ("a missing row", Basic(id).Replace("\n.............\n", "\n"), file, "grid rows"),
                ("a short row", Basic(id).Replace("......3......", "......3....."), file, "symbols"),
                ("an unknown symbol", Basic(id, (0, 0, '?')), file, "unknown symbol"),
                ("a missing SpawnPoint", Basic(id, (3, 6, '.')), file, "exactly once"),
                ("a repeated SpawnPoint", Basic(id, (0, 0, '3')), file, "exactly once"),
                ("a SpawnPoint beyond the profile", Basic(id, (0, 0, '6')), file, "only SpawnPoints"),
            };
            foreach ((string label, string text, string fileName, string expected) in malformed)
            {
                Assert(!RoomLayoutFormat.TryParse(text, fileName, out _, out error) &&
                       error.Contains(expected, StringComparison.Ordinal),
                    $"The room format must reject {label} with its reason. Got: {error}");
            }

            (string label, string text, string expected)[] invalidGeometry =
            {
                ("an obstacle in a door passage", Basic(id, (3, 0, '#')), "door passage"),
                ("a pit in a door passage", Basic(id, (0, 6, 'o')), "door passage"),
                ("a SpawnPoint in a door passage", Basic(id, (3, 6, '.'), (0, 6, '3')), "door passage"),
                ("a wall that cuts the doors apart",
                    Basic(id, Enumerable.Range(0, 7).Select(row => (row, 3, '#')).ToArray()), "safe entry"),
                ("a SpawnPoint walled in by obstacles",
                    Basic(id, (2, 6, '#'), (4, 6, '#'), (3, 5, '#'), (3, 7, '#')), "not reachable"),
                ("a chest in a door passage", Basic(id, (3, 1, 'c')), "door passage"),
                ("two chests side by side", Basic(id, (0, 1, 'c'), (0, 2, 'g')), "Chest at"),
            };
            foreach ((string label, string text, string expected) in invalidGeometry)
            {
                Assert(RoomLayoutFormat.TryParse(text, file, out RoomLayoutDocument document, out error), error);
                Assert(!RoomLayoutImporter.TryValidateGeometry(document, out error) &&
                       error.Contains(expected, StringComparison.Ordinal),
                    $"The importer must reject {label} with its reason. Got: {error}");
            }

            // Pit cells merge into rectangles and keep a position-based ID.
            Assert(RoomLayoutFormat.TryParse(
                       Basic(id, (0, 0, 'o'), (0, 1, 'o'), (1, 0, 'o'), (1, 1, 'o'), (1, 3, 'o')), file,
                       out RoomLayoutDocument pitted, out error), error);
            List<RoomLayoutPit> pits = pitted.Pits();
            Assert(pits.Count == 2 && pits[0].PitId == "pit-c00-r00" &&
                   Same(pits[0].Bounds, pitted.Profile.CellRect(0, 0, 2, 2)) && pits[1].PitId == "pit-c03-r01" &&
                   Same(pits[0].Bounds, Rect.MinMaxRect(-6.5f, 1.5f, -4.5f, 3.5f)) &&
                   Same(pits[1].Bounds, Rect.MinMaxRect(-3.5f, 1.5f, -2.5f, 2.5f)),
                "Neighbouring pit cells must merge into one rectangle with a position-based ID.");
        }

        private static List<RoomLayoutDocument> ValidateCatalog(FloorGenerator generator)
        {
            RoomLayoutImporter.Report report = new();
            List<RoomLayoutDocument> documents = RoomLayoutImporter.ReadDocuments(
                RoomLayoutImporter.LayoutFolderPath, generator, report);
            Assert(report.Rejected.Count == 0, report.Format());
            RoomTemplateDefinition[] imported = generator.RoomTemplates
                .Where(RoomLayoutImporter.IsImportedTemplate).ToArray();
            Assert(imported.Select(template => template.TemplateId).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(documents.Select(document => document.Id)
                        .OrderBy(value => value, StringComparer.Ordinal)),
                "The Game Scene catalog must hold exactly one imported template per room file. Import again.");
            Assert(generator.RoomTemplates.Select(template => template.TemplateId).Distinct().Count() ==
                   generator.RoomTemplates.Count,
                "The room catalog duplicates a template ID.");
            foreach (RoomTemplateDefinition authored in generator.RoomTemplates.Except(imported))
            {
                Assert(authored.SelectionWeight == RoomTemplateDefinition.DefaultSelectionWeight &&
                       authored.AuthoredChests.Count == 0,
                    $"Authored template '{authored.TemplateId}' must keep the default weight and no Layout chest.");
            }

            foreach (RoomLayoutDocument document in documents)
            {
                RoomTemplateDefinition template = imported.Single(candidate => candidate.TemplateId == document.Id);
                string label = $"Imported room '{document.Id}'";
                Assert(AssetDatabase.GetAssetPath(template) == RoomLayoutImporter.TemplatePath(document.Id) &&
                       AssetDatabase.GetAssetPath(template.RoomPrefabAsset) ==
                       RoomLayoutImporter.PrefabPath(document.Id),
                    $"{label} must use its own template and Prefab paths.");
                Assert(RoomLayoutImporter.TryValidateBuilt(template, generator, out string error), $"{label}: {error}");
                Assert(template.Profile.ProfileId == document.Profile.ProfileId &&
                       template.AllowedRoomTypes.SequenceEqual(new[] { RoomType.Normal }) &&
                       template.SelectionWeight == document.Weight &&
                       template.LayoutDifficultyModifier == document.Difficulty &&
                       template.MinimumFloor == document.MinimumFloor && template.MaximumFloor == document.MaximumFloor,
                    $"{label} does not match its header.");
                Assert(template.SpawnPoints.Count == document.SpawnPoints.Length &&
                       template.SpawnPoints.Select((point, index) => point == document.SpawnPoints[index])
                           .All(same => same) &&
                       template.SpawnPointRoles.SequenceEqual(
                           RoomLayoutImporter.BuildSpawnPointRoles(document.Profile, document.SpawnPoints)),
                    $"{label} SpawnPoints or roles do not match its grid.");

                RoomLayoutCell[] chestCells = document.CellsOf(RoomLayoutCellKind.Chest).ToArray();
                Assert(template.AuthoredChests.Count == chestCells.Length &&
                       chestCells.All(cell => template.AuthoredChests.Any(chest =>
                           chest.ChestId == cell.Id(RoomLayoutImporter.ChestIdPrefix) &&
                           chest.Kind == cell.Symbol.ChestKind && chest.LocalPosition == cell.Center)),
                    $"{label} Layout chests do not match its grid.");

                GameObject prefab = template.RoomPrefabAsset;
                Dictionary<string, DestructibleObstacle> obstacles = prefab
                    .GetComponentsInChildren<DestructibleObstacle>(true)
                    .ToDictionary(obstacle => obstacle.ObstacleId, StringComparer.Ordinal);
                RoomLayoutCell[] obstacleCells = document.Obstacles.ToArray();
                Assert(obstacles.Count == obstacleCells.Length, $"{label} has {obstacles.Count} obstacles, not " +
                                                                $"{obstacleCells.Length}.");
                foreach (RoomLayoutCell cell in obstacleCells)
                {
                    Assert(obstacles.TryGetValue(RoomLayoutImporter.ObstacleId(cell), out DestructibleObstacle obstacle) &&
                           (Vector2)prefab.transform.InverseTransformPoint(obstacle.transform.position) == cell.Center &&
                           obstacle.TryValidate(out error),
                        $"{label} is missing obstacle {RoomLayoutImporter.ObstacleId(cell)} on its cell. {error}");
                    bool hasSlot = obstacle.GetComponent<RoomObstacleVariantSlot>() != null;
                    bool matches = cell.Symbol.Kind switch
                    {
                        RoomLayoutCellKind.Rock => hasSlot && obstacle.FixedVariant == null &&
                                                   obstacle.VariantId == "rock" && !obstacle.BlocksFlight,
                        RoomLayoutCellKind.Tree => !hasSlot && obstacle.FixedVariant == null && obstacle.BlocksFlight &&
                                                   obstacle.VariantId == DestructibleObstacle.TreeVariantId &&
                                                   obstacle.DropTable == null,
                        _ => !hasSlot && obstacle.FixedVariant != null &&
                             obstacle.FixedVariant.VariantId == cell.Symbol.VariantId &&
                             obstacle.VariantId == cell.Symbol.VariantId &&
                             obstacle.RequiredHits == obstacle.FixedVariant.RequiredHits &&
                             obstacle.DropTable == obstacle.FixedVariant.DropTable,
                    };
                    Assert(matches, $"{label} obstacle {obstacle.ObstacleId} is not the kind its symbol " +
                                    $"'{cell.Symbol.Symbol}' asks for.");
                }

                Assert(RoomObstacleLayout.TryCollectFootprints(prefab, out List<RoomObstacleFootprint> footprints,
                    out error), error);
                RoomObstacleFootprint[] pitFootprints = footprints.Where(footprint => footprint.IsPit).ToArray();
                List<RoomLayoutPit> pits = document.Pits();
                Assert(pitFootprints.Length == pits.Count && pits.All(pit => pitFootprints.Any(footprint =>
                           footprint.ObstacleId == pit.PitId && Same(footprint.Bounds, pit.Bounds))),
                    $"{label} pits do not match its grid.");
            }

            return documents;
        }

        private static void ValidateRuntime(List<RoomLayoutDocument> documents, RoomGraphAssembler assembler)
        {
            Assert(documents.Any(document => document.CellsOf(RoomLayoutCellKind.FixedObstacle).Any()) &&
                   documents.Any(document => document.CellsOf(RoomLayoutCellKind.Chest).Any()),
                "The sample rooms must include a fixed obstacle kind and a Layout chest for the runtime checks.");
            GameObject progressHolder = new("Room Layout Verification Progress");
            List<GameObject> roots = new() { progressHolder };
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                foreach (RoomLayoutDocument document in documents)
                {
                    RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(
                        RoomLayoutImporter.TemplatePath(document.Id));
                    RoomLayoutCell[] fixedCells = document.CellsOf(RoomLayoutCellKind.FixedObstacle).ToArray();
                    RoomRunState state = new($"layout-{document.Id}");
                    for (int seed = 1; seed <= 24 && fixedCells.Length > 0; seed++)
                    {
                        RoomPrefab room = Spawn(template, roots);
                        Assert(RoomObstacleVariantSlot.TryResolveForRoom(room, seed, out string error), error);
                        Dictionary<string, DestructibleObstacle> obstacles = room
                            .GetComponentsInChildren<DestructibleObstacle>(true)
                            .ToDictionary(obstacle => obstacle.ObstacleId, StringComparer.Ordinal);
                        foreach (DestructibleObstacle obstacle in obstacles.Values)
                            obstacle.Bind(new RoomRunState($"layout-{document.Id}-{seed}"), seed, room.transform, progress);
                        foreach (RoomLayoutCell cell in fixedCells)
                        {
                            DestructibleObstacle obstacle = obstacles[RoomLayoutImporter.ObstacleId(cell)];
                            ObstacleVariantDefinition variant = RoomLayoutImporter.LoadVariant(cell.Symbol.VariantId);
                            Assert(obstacle.VariantId == variant.VariantId && obstacle.BreakRule == variant.BreakRule &&
                                   obstacle.DropCount == variant.DropCount &&
                                   obstacle.RequiredHits == variant.RequiredHits,
                                $"Room '{document.Id}' fixed obstacle {obstacle.ObstacleId} lost its kind for seed {seed}.");
                        }

                        // A fixed kind is not a candidate slot: at most one seeded rock turns special.
                        int seededSpecials = document.CellsOf(RoomLayoutCellKind.Rock)
                            .Count(cell => obstacles[RoomLayoutImporter.ObstacleId(cell)].VariantId != "rock");
                        Assert(seededSpecials <= 1,
                            $"Room '{document.Id}' rolled {seededSpecials} seeded special obstacles for seed {seed}.");
                    }

                    if (template.AuthoredChests.Count == 0) continue;
                    ChestContentTable table = assembler.ChestContentTable;
                    RoomPrefab first = Spawn(template, roots);
                    RoomChestSite site = new(first, template, state, progress);
                    Assert(RoomChestSpawner.TrySpawnAuthoredChests(table, site, 77, out List<TreasureChest> spawned,
                               out string chestError) && spawned.Count == template.AuthoredChests.Count,
                        $"Room '{document.Id}' must place every Layout chest on its first build. {chestError}");
                    foreach (RoomTemplateChest authored in template.AuthoredChests)
                    {
                        TreasureChest chest = spawned.Single(candidate => candidate.ChestId == authored.ChestId);
                        ChestRunState record = state.GetChest(authored.ChestId);
                        Assert(chest.Kind == authored.Kind && chest.IsClosed && record != null && record.HasPosition &&
                               record.LocalPosition == authored.LocalPosition && record.HasContentSeed &&
                               record.ContentSeed == RoomChestSpawner.DeriveAuthoredSeed(77, authored.ChestId) &&
                               Vector2.Distance(chest.transform.position,
                                   first.transform.TransformPoint(authored.LocalPosition)) < 0.001f,
                            $"Room '{document.Id}' Layout chest {authored.ChestId} is not on its cell with its kind and seed.");
                    }

                    Assert(RoomChestSpawner.TrySpawnAuthoredChests(table, site, 77, out spawned, out chestError) &&
                           spawned.Count == 0 &&
                           first.GetComponentsInChildren<TreasureChest>(true).Length == template.AuthoredChests.Count,
                        $"Room '{document.Id}' placed a Layout chest twice in one build. {chestError}");

                    // A rebuilt room: the clear reward spawner restores the recorded chests, the Layout adds none.
                    RoomPrefab rebuilt = Spawn(template, roots);
                    RoomChestSite rebuiltSite = new(rebuilt, template, state, progress);
                    RewardSpawner(rebuilt).ConfigureChest(table, rebuiltSite,
                        RoomClearRewardSpawner.DeriveChestSeed(77));
                    Assert(RoomChestSpawner.TrySpawnAuthoredChests(table, rebuiltSite, 77, out spawned,
                               out chestError) && spawned.Count == 0 &&
                           rebuilt.GetComponentsInChildren<TreasureChest>(true).Length == template.AuthoredChests.Count &&
                           rebuilt.GetComponentsInChildren<TreasureChest>(true).All(chest => chest.IsClosed),
                        $"Room '{document.Id}' must restore each Layout chest exactly once after a rebuild. {chestError}");

                    // Leaving the floor discards unopened chests for the rest of the Run.
                    Assert(state.DiscardClosedChests() == template.AuthoredChests.Count,
                        $"Room '{document.Id}' Layout chests must leave with their floor.");
                    RoomPrefab later = Spawn(template, roots);
                    RoomChestSite laterSite = new(later, template, state, progress);
                    RewardSpawner(later).ConfigureChest(table, laterSite, RoomClearRewardSpawner.DeriveChestSeed(77));
                    Assert(RoomChestSpawner.TrySpawnAuthoredChests(table, laterSite, 77, out spawned, out chestError) &&
                           spawned.Count == 0 && later.GetComponentsInChildren<TreasureChest>(true).Length == 0,
                        $"Room '{document.Id}' brought back a Layout chest that left with its floor. {chestError}");
                }
            }
            finally
            {
                foreach (GameObject root in roots)
                    if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static RoomPrefab Spawn(RoomTemplateDefinition template, List<GameObject> roots)
        {
            GameObject instance = Object.Instantiate(template.RoomPrefabAsset, Origin, Quaternion.identity);
            roots.Add(instance);
            return instance.GetComponent<RoomPrefab>();
        }

        private static RoomClearRewardSpawner RewardSpawner(RoomPrefab room) =>
            room.Controller.TryGetComponent(out RoomClearRewardSpawner spawner)
                ? spawner
                : room.Controller.gameObject.AddComponent<RoomClearRewardSpawner>();

        private static void ValidateWeightedPick()
        {
            int[] weights = { 100, 30, 10 };
            RoomTemplateDefinition[] candidates = weights.Select(weight =>
            {
                RoomTemplateDefinition template = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
                Assert(template.SelectionWeight == RoomTemplateDefinition.DefaultSelectionWeight,
                    "A new template must start with the default selection weight.");
                template.ConfigureSelectionWeight(weight);
                return template;
            }).ToArray();
            try
            {
                int[] counts = new int[weights.Length];
                for (int sample = 0; sample < WeightSampleCount; sample++)
                {
                    int seed = FloorGenerator.DeriveSeed(sample, 1, 0x2545F491u);
                    int pick = RoomTemplateSelector.PickWeighted(candidates, seed);
                    Assert(pick == RoomTemplateSelector.PickWeighted(candidates, seed),
                        "A weighted template pick must repeat for the same seed.");
                    counts[pick]++;
                }

                int total = weights.Sum();
                for (int index = 0; index < weights.Length; index++)
                {
                    float expected = weights[index] / (float)total;
                    float actual = counts[index] / (float)WeightSampleCount;
                    Assert(Mathf.Abs(actual - expected) < 0.01f,
                        $"Weight {weights[index]} of {total} was picked {actual:P1} of the time, not {expected:P1}.");
                }
            }
            finally
            {
                foreach (RoomTemplateDefinition template in candidates) Object.DestroyImmediate(template);
            }
        }

        private static void ValidateGeneration(FloorGenerator generator, List<RoomLayoutDocument> documents)
        {
            Dictionary<string, int> uses = documents.ToDictionary(document => document.Id, _ => 0,
                StringComparer.Ordinal);
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error),
                    $"Seed {seed} failed to generate with the imported rooms. {error}");
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat), $"Seed {seed} changed its room templates on regeneration.");
                }

                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (node.TemplateId == null || !uses.ContainsKey(node.TemplateId)) continue;
                    Assert(node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null,
                        $"Seed {seed} put imported room '{node.TemplateId}' into {node.Role} room {node.RoomId}.");
                    uses[node.TemplateId]++;
                }
            }

            foreach (KeyValuePair<string, int> entry in uses)
                Assert(entry.Value > 0, $"Imported room '{entry.Key}' never appeared in {GeneratedSeedCount} seeds.");

            // Among rooms of one profile a lower weight must appear less often.
            foreach (IGrouping<RoomLayoutProfile, RoomLayoutDocument> group in documents.GroupBy(document =>
                         document.Profile))
            {
                RoomLayoutDocument heaviest = group.OrderByDescending(document => document.Weight).First();
                RoomLayoutDocument lightest = group.OrderBy(document => document.Weight).First();
                if (heaviest.Weight < lightest.Weight * 2) continue;
                Assert(uses[heaviest.Id] > uses[lightest.Id],
                    $"'{heaviest.Id}' (weight {heaviest.Weight}) appeared {uses[heaviest.Id]} times and " +
                    $"'{lightest.Id}' (weight {lightest.Weight}) {uses[lightest.Id]} times.");
            }
        }

        private static void ValidateForcedTemplate(FloorGenerator generator, List<RoomLayoutDocument> documents)
        {
            string forcedId = documents.First(document =>
                document.Profile.ProfileId == Week14Room1Setup.BasicProfileId).Id;
            int Count(GeneratedFloorGraph graph) => graph.Nodes.Count(node => node.TemplateId == forcedId);
            Assert(string.IsNullOrEmpty(RoomTemplateSelector.DevelopmentForcedTemplateId),
                "A forced template was left set before verification.");
            try
            {
                for (int seed = 1; seed <= 16; seed++)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph normal, out string error), error);
                    RoomTemplateSelector.DevelopmentForcedTemplateId = forcedId;
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph forced, out error),
                        $"Seed {seed} failed to generate with a forced template. {error}");
                    RoomTemplateSelector.DevelopmentForcedTemplateId = null;
                    int combatRooms = forced.Nodes.Count(node => node.Role == GeneratedRoomRole.Intermediate);
                    Assert(Count(forced) > Count(normal) && Count(forced) * 2 > combatRooms,
                        $"Seed {seed} forced '{forcedId}' into {Count(forced)} of {combatRooms} combat rooms.");
                    Assert(forced.Nodes.Where(node => node.Role == GeneratedRoomRole.Start)
                            .All(node => node.TemplateId == RoomTemplateSelector.StartingRoomTemplateId),
                        "A forced template must not replace the starting room.");
                }
            }
            finally
            {
                RoomTemplateSelector.DevelopmentForcedTemplateId = null;
            }
        }

        private static string Signature(GeneratedFloorGraph graph) =>
            string.Join("|", graph.Nodes.Select(node => $"{node.RoomId}:{node.TemplateId}"));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
