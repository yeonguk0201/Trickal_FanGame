using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // T7: turns the room text files under game/RoomLayouts into Room Prefabs and Room Templates and registers them in
    // the Game Scene catalog. A room that breaks the Layout contract is rejected with its reason and its last good
    // import stays. Rooms whose text did not change are not rebuilt, so their assets keep their content as well as
    // their GUIDs; a room whose file is gone leaves the catalog and its assets are deleted.
    // The 19 templates authored before the importer are not part of it and are never touched.
    public static class RoomLayoutImporter
    {
        public const int RoomContentVersion = 12;
        // Folder beside Assets, so the text files need no .meta.
        public const string LayoutFolderName = "RoomLayouts";
        public const string TemplateFolder = Week14Room1Setup.TemplateFolder + "/Layouts";
        public const string PrefabFolder = Week8GridFloorSetup.PrefabFolder + "/Layouts";
        public const string ReportPath = "Logs/room-layout-import-report.txt";
        public const string RockIdPrefix = "obstacle";
        public const string TreeIdPrefix = "tree";
        public const string ChestIdPrefix = "chest-layout";
        // Part of each room's content hash. Raise it when the importer builds a different Prefab from the same text.
        public const int BuildRevision = 3;

        private const string StagingId = "staging";
        private const SpawnPointPlacementRole MeleeRear =
            SpawnPointPlacementRole.MeleePressure | SpawnPointPlacementRole.RearFiring;

        public sealed class RoomSummary
        {
            public string Id;
            public string ProfileId;
            public int Weight;
            public int Difficulty;
            public int Rocks;
            public int FixedObstacles;
            public int Trees;
            public int PitCells;
            public int Chests;
            public float Density;
            public bool Rebuilt;
        }

        public sealed class Report
        {
            public readonly List<RoomSummary> Imported = new();
            public readonly List<(string file, string reason)> Rejected = new();
            public readonly List<string> Removed = new();
            public readonly List<string> Warnings = new();
            public int RoomContentVersion;
            public bool WroteAssets;

            public string Format()
            {
                StringBuilder text = new();
                text.AppendLine($"Room Layout {(WroteAssets ? "import" : "validation")}: {Imported.Count} accepted " +
                                $"({Imported.Count(room => room.Rebuilt)} rebuilt), {Rejected.Count} rejected, " +
                                $"{Removed.Count} removed. Room content version {RoomContentVersion}.");
                foreach (RoomSummary room in Imported)
                {
                    text.AppendLine($"  {room.Id}  [{room.ProfileId}] weight {room.Weight} difficulty +{room.Difficulty}  " +
                                    $"rock {room.Rocks} fixed {room.FixedObstacles} tree {room.Trees} " +
                                    $"pit {room.PitCells} chest {room.Chests}  density {room.Density:P0}" +
                                    (room.Rebuilt ? "  (rebuilt)" : string.Empty));
                }

                foreach (string removed in Removed) text.AppendLine($"  removed {removed}");
                foreach (string warning in Warnings) text.AppendLine($"  warning: {warning}");
                foreach ((string file, string reason) in Rejected) text.AppendLine($"  REJECTED {file}: {reason}");
                return text.ToString().TrimEnd();
            }
        }

        public static string LayoutFolderPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, LayoutFolderName);

        public static string TemplatePath(string id) => $"{TemplateFolder}/{id}.asset";
        public static string PrefabPath(string id) => $"{PrefabFolder}/room-{id}.prefab";
        public static bool IsImportedTemplate(RoomTemplateDefinition template) =>
            template != null && AssetDatabase.GetAssetPath(template).StartsWith(TemplateFolder + "/",
                StringComparison.Ordinal);

        [MenuItem("Trickal Fan Game/Tools/Validate Room Layouts")]
        public static void Validate() => Finish(Run(LayoutFolderPath, false));

        [MenuItem("Trickal Fan Game/Tools/Import Room Layouts")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing room Layouts.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Finish(Run(LayoutFolderPath, true));
        }

        // Rebuilds every room even when its text did not change.
        [MenuItem("Trickal Fan Game/Tools/Reimport All Room Layouts")]
        public static void Reimport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing room Layouts.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Finish(Run(LayoutFolderPath, true, true));
        }

        // Reads every room file of a folder. With writeAssets the accepted rooms are built and registered; without
        // it nothing outside the staging assets is written and the Game Scene is not saved.
        public static Report Run(string folderPath, bool writeAssets, bool forceRebuild = false)
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            if (generator == null) throw new InvalidOperationException("The room Layout importer requires the Game Scene generator.");
            EnsureFolder(TemplateFolder);
            EnsureFolder(PrefabFolder);

            Report report = new();
            report.WroteAssets = writeAssets;
            List<RoomLayoutDocument> documents = ReadDocuments(folderPath, generator, report);
            HashSet<string> fileIds = new(ListFiles(folderPath).Select(path =>
                Path.GetFileName(path).Substring(0, Path.GetFileName(path).Length - RoomLayoutFormat.FileSuffix.Length)),
                StringComparer.Ordinal);

            try
            {
                foreach (RoomLayoutDocument document in documents)
                {
                    bool unchanged = !forceRebuild && IsUpToDate(document);
                    if (!unchanged)
                    {
                        // The staging build proves the room against the Prefab-level contract and every Encounter
                        // before the real assets change.
                        RoomTemplateDefinition staged = Build(document, TemplatePath(StagingId), PrefabPath(StagingId));
                        if (!TryValidateBuilt(staged, generator, out string error))
                        {
                            report.Rejected.Add((document.Id + RoomLayoutFormat.FileSuffix, error));
                            continue;
                        }

                        if (writeAssets)
                        {
                            RoomTemplateDefinition built = Build(document, TemplatePath(document.Id),
                                PrefabPath(document.Id));
                            if (!TryValidateBuilt(built, generator, out error))
                                throw new InvalidOperationException(
                                    $"Room '{document.Id}' passed staging but its final build is invalid. {error}");
                            StoreHash(document);
                        }
                    }

                    report.Imported.Add(Summarize(document, !unchanged));
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemplatePath(StagingId));
                AssetDatabase.DeleteAsset(PrefabPath(StagingId));
            }

            AddMirrorWarnings(documents, report);
            report.RoomContentVersion = generator.RoomContentVersion;
            if (writeAssets)
            {
                Register(generator, documents, fileIds, report);
                report.RoomContentVersion = generator.RoomContentVersion;
                AssetDatabase.SaveAssets();
            }

            return report;
        }

        public static IEnumerable<string> ListFiles(string folderPath) => Directory.Exists(folderPath)
            ? Directory.GetFiles(folderPath, "*" + RoomLayoutFormat.FileSuffix, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

        // Parses and checks every file against the rules that need no Prefab: the grid, the door passages, the
        // obstacle Layout contract and chest positions.
        public static List<RoomLayoutDocument> ReadDocuments(string folderPath, FloorGenerator generator, Report report)
        {
            List<RoomLayoutDocument> documents = new();
            foreach (string path in ListFiles(folderPath))
            {
                string fileName = Path.GetFileName(path);
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (!RoomLayoutFormat.TryParse(text, fileName, out RoomLayoutDocument document, out string error) ||
                    !TryValidateGeometry(document, out error))
                {
                    report.Rejected.Add((fileName, error));
                    continue;
                }

                RoomTemplateDefinition authored = generator.RoomTemplates.FirstOrDefault(template =>
                    template != null && template.TemplateId == document.Id && !IsImportedTemplate(template));
                if (authored != null)
                {
                    report.Rejected.Add((fileName, $"Room ID '{document.Id}' belongs to a template authored outside " +
                                                   "the importer. Use a new ID."));
                    continue;
                }

                documents.Add(document);
            }

            return documents;
        }

        public static bool TryValidateGeometry(RoomLayoutDocument document, out string error)
        {
            RoomLayoutProfile profile = document.Profile;
            // The walkable floor is narrower than the profile's movement bounds, so reachability is judged on the
            // floor the player can actually stand on. The built template is validated against the profile again.
            Rect movementBounds = profile.Floor;
            Rect encounterBounds = Week14Room3Setup.EncounterBounds(profile.RoomSize);
            RoomTemplateDoor[] doors = Week14Room3Setup.BuildDoorContracts(profile.RoomSize);
            Vector2[] spawnPoints = document.SpawnPoints;
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                if (!encounterBounds.Contains(spawnPoints[index]))
                {
                    error = $"SpawnPoint {index + 1} is outside the Encounter bounds.";
                    return false;
                }

                foreach (RoomTemplateDoor door in doors)
                {
                    if (!RoomTemplateGeometry.IsInsideRequiredDoorPassage(door, spawnPoints[index])) continue;
                    error = $"SpawnPoint {index + 1} sits in the {door.Direction} door passage.";
                    return false;
                }
            }

            List<RoomObstacleFootprint> terrain = new();
            foreach (RoomLayoutCell cell in document.Obstacles)
            {
                terrain.Add(new RoomObstacleFootprint(ObstacleId(cell), profile.CellRect(cell.Column, cell.Row), true));
            }

            foreach (RoomLayoutPit pit in document.Pits())
                terrain.Add(new RoomObstacleFootprint(pit.PitId, pit.Bounds, false, true));

            // A chest is a solid body the player can push, so the door and SpawnPoint checks also run with every
            // chest standing on its cell.
            List<RoomObstacleFootprint> withChests = new(terrain);
            List<RoomLayoutCell> chests = document.CellsOf(RoomLayoutCellKind.Chest).ToList();
            Vector2 chestSize = Vector2.one * ChestPlacement.ChestWorldSize;
            foreach (RoomLayoutCell chest in chests)
            {
                withChests.Add(new RoomObstacleFootprint(chest.Id(ChestIdPrefix),
                    new Rect(chest.Center - chestSize * 0.5f, chestSize), false));
            }

            if (!RoomObstacleLayout.TryValidate(document.Id, movementBounds, encounterBounds, doors, spawnPoints,
                    withChests, out error))
            {
                return false;
            }

            if (chests.Count == 0) return true;
            Func<Vector2, bool> isReachable = RoomObstacleLayout.CreateReachability(movementBounds,
                doors[0].SafeEntryPosition, terrain);
            List<Rect> doorPassages = doors.Select(door => RoomObstacleLayout.Expand(
                RoomTemplateGeometry.RequiredDoorPassageBounds(door),
                RoomObstacleLayout.ActorRadius + ChestPlacement.ChestHalfSize)).ToList();
            List<Vector2> placed = new();
            foreach (RoomLayoutCell chest in chests)
            {
                if (!ChestPlacement.IsSafe(chest.Center, isReachable, doorPassages, placed))
                {
                    error = $"Chest at column {chest.Column + 1}, row {chest.Row + 1} must be reachable, outside " +
                            $"every door passage and at least {ChestPlacement.MinimumChestSpacing} from another chest.";
                    return false;
                }

                placed.Add(chest.Center);
            }

            error = null;
            return true;
        }

        // Every Encounter of the room's profile must be placeable whatever doors a generated room opens; a room
        // that fails would stop floor generation for the seeds that pick it.
        public static bool TryValidateBuilt(RoomTemplateDefinition template, FloorGenerator generator,
            out string error)
        {
            if (template == null)
            {
                error = "The room could not be built.";
                return false;
            }

            if (!template.TryValidate(out error) || !template.TryValidateLayout(out error)) return false;
            RoomDoorDirection[] directions = (RoomDoorDirection[])Enum.GetValues(typeof(RoomDoorDirection));
            bool anyEncounter = false;
            foreach (EncounterDefinition encounter in generator.EncounterDefinitions)
            {
                if (encounter == null) continue;
                int firstFloor = Math.Max(template.MinimumFloor, encounter.MinimumFloor);
                int lastFloor = Math.Min(template.MaximumFloor, encounter.MaximumFloor);
                for (int floor = firstFloor; floor <= lastFloor; floor++)
                {
                    if (!encounter.Supports(template.Profile, floor)) continue;
                    anyEncounter = true;
                    for (int mask = 1; mask < 1 << directions.Length; mask++)
                    {
                        GeneratedRoomConnection[] connections = directions
                            .Where((_, index) => (mask & (1 << index)) != 0)
                            .Select(direction => new GeneratedRoomConnection(direction, "layout-import")).ToArray();
                        if (encounter.TryValidateFor(template, floor, connections, out error)) continue;
                        error = $"Encounter '{encounter.EncounterId}' cannot be placed with doors " +
                                $"{string.Join("+", connections.Select(connection => connection.Direction))} on " +
                                $"floor {floor}. {error}";
                        return false;
                    }
                }
            }

            if (!anyEncounter)
            {
                error = $"No Encounter supports profile '{template.Profile.ProfileId}' on the room's floors.";
                return false;
            }

            error = null;
            return true;
        }

        private static RoomTemplateDefinition Build(RoomLayoutDocument document, string templatePath,
            string prefabPath)
        {
            RoomLayoutProfile profile = document.Profile;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(profile.SourcePrefabPath);
            if (source == null)
                throw new InvalidOperationException($"The importer is missing source Prefab {profile.SourcePrefabPath}.");

            Vector2[] spawnPoints = document.SpawnPoints;
            Week14Room3Setup.ConfigureLayout(profile.ProfileId, document.Id, profile.ProfilePath, templatePath,
                prefabPath, $"Room {document.Id}", profile.RoomSize, spawnPoints, source, null,
                document.MinimumFloor, document.MaximumFloor, false);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return null;
            PlaceContent(document, prefabPath);
            // ConfigureLayout rebuilds the walls with placeholder sprites; restore the connected artwork.
            FairyVillageArtworkSetup.ApplyToPrefab(prefabPath);

            // Prefab editing can unload assets held only from C#, so everything is loaded again by path.
            RoomTemplateDefinition template = AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(templatePath);
            RoomProfile profileAsset = AssetDatabase.LoadAssetAtPath<RoomProfile>(profile.ProfilePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (template == null || profileAsset == null || prefab == null) return null;
            template.Configure(document.Id, profileAsset, prefab, new[] { RoomType.Normal },
                Week14Room3Setup.BuildDoorContracts(profile.RoomSize), spawnPoints, document.MinimumFloor,
                document.MaximumFloor, BuildSpawnPointRoles(profile, spawnPoints));
            template.ConfigureLayoutDifficultyModifier(document.Difficulty);
            template.ConfigureSelectionWeight(document.Weight);
            template.ConfigureAuthoredChests(document.CellsOf(RoomLayoutCellKind.Chest)
                .Select(cell => new RoomTemplateChest(cell.Id(ChestIdPrefix), cell.Symbol.ChestKind, cell.Center))
                .ToArray());
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
            return template;
        }

        // SpawnPoints 1~3 keep the roles every authored template uses (1 and 2 melee pressure, 3 the charge lane,
        // all rear firing); the Encounter-4 points after them take melee and rear firing. A Small room has one
        // extra point, which takes every role.
        public static SpawnPointPlacementRole[] BuildSpawnPointRoles(RoomLayoutProfile profile,
            IReadOnlyList<Vector2> spawnPoints)
        {
            SpawnPointPlacementRole[] roles = RoomTemplateDefinition
                .BuildDefaultSpawnPointRoles(spawnPoints.Take(Week19Encounter4Setup.BaseSpawnPointCount).ToArray())
                .Concat(Enumerable.Repeat(
                    spawnPoints.Count == Week19Encounter4Setup.BaseSpawnPointCount + 1
                        ? SpawnPointPlacementRole.AllCombat
                        : MeleeRear,
                    spawnPoints.Count - Week19Encounter4Setup.BaseSpawnPointCount))
                .ToArray();
            return roles;
        }

        private static void PlaceContent(RoomLayoutDocument document, string prefabPath)
        {
            GameObject obstaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            GameObject pitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            ObstacleVariantTable variantTable = AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(
                Week20Obstacle4Setup.FairyKingdomVariantTablePath);
            ResourceDropTable basicTable =
                AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath);
            Sprite fillSprite = Week13FrontendUiAssets.LoadPlaceholderFillSprite();
            int environment = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            if (obstaclePrefab == null || pitPrefab == null || variantTable == null || basicTable == null)
            {
                throw new InvalidOperationException("The importer needs the Obstacle-1 obstacle Prefab and drop " +
                                                    "table, the Terrain-0 pit Prefab and the Obstacle-4 variant table.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                RoomPrefab room = root.GetComponent<RoomPrefab>();
                Transform content = room?.Node?.ContentRoot != null ? room.Node.ContentRoot.transform : null;
                if (content == null) throw new InvalidOperationException($"{prefabPath} has no room content root.");
                Transform obstacles = EmptyContainer(content, Week18Obstacle2Setup.ObstacleContainerName);
                Transform trees = EmptyContainer(content, Week23Obstacle6Setup.TreeContainerName);
                Transform pits = EmptyContainer(content, Week22Terrain0Setup.PitContainerName);

                foreach (RoomLayoutCell cell in document.Obstacles)
                {
                    string id = ObstacleId(cell);
                    if (cell.Symbol.Kind == RoomLayoutCellKind.Tree)
                    {
                        GameObject tree = new($"Tree {id}");
                        tree.transform.SetParent(trees, false);
                        Week23Obstacle6Setup.ConfigureTree(tree, id, cell.Center, fillSprite, environment);
                        continue;
                    }

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab, obstacles);
                    instance.name = $"Obstacle {id}";
                    instance.transform.localPosition = cell.Center;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
                    obstacle.Configure(id, DestructibleObstacle.DefaultRequiredHits, basicTable,
                        instance.GetComponentInChildren<SpriteRenderer>(true));
                    RoomObstacleVariantSlot slot = instance.GetComponent<RoomObstacleVariantSlot>();
                    if (cell.Symbol.Kind == RoomLayoutCellKind.Rock)
                    {
                        if (slot == null) slot = instance.AddComponent<RoomObstacleVariantSlot>();
                        slot.Configure(variantTable);
                        continue;
                    }

                    // A fixed kind is not a candidate for the room's one seeded special obstacle.
                    if (slot != null) Object.DestroyImmediate(slot);
                    ObstacleVariantDefinition variant = LoadVariant(cell.Symbol.VariantId);
                    obstacle.ConfigureFixedVariant(variant);
                    if (!obstacle.TryValidate(out string error)) throw new InvalidOperationException(error);
                }

                foreach (RoomLayoutPit pit in document.Pits())
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(pitPrefab, pits);
                    instance.name = $"Pit {pit.PitId}";
                    instance.transform.localPosition = pit.Bounds.center;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Week22Terrain0Setup.ApplySize(instance, pit.Bounds.size);
                    instance.GetComponent<RoomPit>().Configure(pit.PitId);
                }

                foreach (Transform container in new[] { obstacles, trees, pits })
                    if (container.childCount == 0) Object.DestroyImmediate(container.gameObject);
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"The importer could not save {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static string ObstacleId(RoomLayoutCell cell) =>
            cell.Id(cell.Symbol.Kind == RoomLayoutCellKind.Tree ? TreeIdPrefix : RockIdPrefix);

        public static ObstacleVariantDefinition LoadVariant(string variantId)
        {
            ObstacleVariantDefinition variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(
                $"{Week20Obstacle4Setup.VariantFolder}/{variantId}.asset");
            if (variant == null || variant.VariantId != variantId || !variant.TryValidate(out _))
                throw new InvalidOperationException($"The importer is missing obstacle kind '{variantId}'.");
            return variant;
        }

        private static Transform EmptyContainer(Transform content, string name)
        {
            Transform container = content.Find(name);
            if (container == null)
            {
                container = new GameObject(name).transform;
                container.SetParent(content, false);
            }

            container.localPosition = Vector3.zero;
            container.localRotation = Quaternion.identity;
            container.localScale = Vector3.one;
            foreach (Transform child in container.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            return container;
        }

        private static void Register(FloorGenerator generator, List<RoomLayoutDocument> documents,
            HashSet<string> fileIds, Report report)
        {
            // A rejected room keeps its file, so its last good import stays; only a room without a file leaves.
            List<RoomTemplateDefinition> templates = new();
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                if (template == null) continue;
                if (IsImportedTemplate(template) && !fileIds.Contains(template.TemplateId)) continue;
                templates.Add(template);
            }

            foreach (RoomLayoutDocument document in documents)
            {
                RoomTemplateDefinition template =
                    AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(TemplatePath(document.Id));
                if (template != null) templates.Add(template);
            }

            RoomTemplateDefinition[] catalog = templates
                .GroupBy(template => template.TemplateId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
                .ToArray();
            Undo.RecordObject(generator, "Configure room Layout catalog");
            generator.ConfigureTemplates(Math.Max(RoomContentVersion, generator.RoomContentVersion), catalog);
            EditorUtility.SetDirty(generator);

            foreach (string guid in AssetDatabase.FindAssets("t:Object", new[] { TemplateFolder, PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                string id = path.StartsWith(PrefabFolder + "/", StringComparison.Ordinal) &&
                            name.StartsWith("room-", StringComparison.Ordinal)
                    ? name.Substring("room-".Length)
                    : name;
                if (fileIds.Contains(id)) continue;
                if (AssetDatabase.DeleteAsset(path)) report.Removed.Add(path);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Game Scene save failed during the room Layout import.");
        }

        private static RoomSummary Summarize(RoomLayoutDocument document, bool rebuilt)
        {
            int pitCells = document.CellsOf(RoomLayoutCellKind.Pit).Count();
            int rocks = document.CellsOf(RoomLayoutCellKind.Rock).Count();
            int fixedObstacles = document.CellsOf(RoomLayoutCellKind.FixedObstacle).Count();
            int trees = document.CellsOf(RoomLayoutCellKind.Tree).Count();
            return new RoomSummary
            {
                Id = document.Id,
                ProfileId = document.Profile.ProfileId,
                Weight = document.Weight,
                Difficulty = document.Difficulty,
                Rocks = rocks,
                FixedObstacles = fixedObstacles,
                Trees = trees,
                PitCells = pitCells,
                Chests = document.CellsOf(RoomLayoutCellKind.Chest).Count(),
                Density = (rocks + fixedObstacles + trees + pitCells) /
                          (float)(document.Profile.Columns * document.Profile.Rows),
                Rebuilt = rebuilt,
            };
        }

        private static void AddMirrorWarnings(List<RoomLayoutDocument> documents, Report report)
        {
            for (int first = 0; first < documents.Count; first++)
            for (int second = first + 1; second < documents.Count; second++)
            {
                RoomLayoutDocument a = documents[first];
                RoomLayoutDocument b = documents[second];
                if (a.Profile != b.Profile) continue;
                string signature = RoomLayoutFormat.TerrainSignature(a, false, false);
                foreach ((bool horizontal, bool vertical, string label) in new[]
                         {
                             (false, false, "the same terrain as"), (true, false, "a left-right mirror of"),
                             (false, true, "a top-bottom mirror of"), (true, true, "a half turn of"),
                         })
                {
                    if (signature != RoomLayoutFormat.TerrainSignature(b, horizontal, vertical)) continue;
                    report.Warnings.Add($"'{a.Id}' is {label} '{b.Id}'.");
                    break;
                }
            }
        }

        public static string ContentHash(RoomLayoutDocument document)
        {
            using SHA1 sha = SHA1.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{BuildRevision}\n{document.NormalizedText}"));
            return string.Concat(bytes.Select(value => value.ToString("x2")));
        }

        // The hash of the text a room was last built from is kept in the template asset's .meta user data.
        private static bool IsUpToDate(RoomLayoutDocument document)
        {
            AssetImporter importer = AssetImporter.GetAtPath(TemplatePath(document.Id));
            return importer != null && importer.userData == ContentHash(document) &&
                   AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(document.Id)) != null &&
                   AssetDatabase.LoadAssetAtPath<RoomTemplateDefinition>(TemplatePath(document.Id)) != null;
        }

        private static void StoreHash(RoomLayoutDocument document)
        {
            AssetImporter importer = AssetImporter.GetAtPath(TemplatePath(document.Id));
            if (importer == null)
                throw new InvalidOperationException($"Room '{document.Id}' has no template asset to record its import on.");
            importer.userData = ContentHash(document);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void Finish(Report report)
        {
            string text = report.Format();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, text + "\n", new UTF8Encoding(false));
            if (report.Rejected.Count > 0)
                throw new InvalidOperationException($"{report.Rejected.Count} room Layout(s) were rejected.\n{text}");
            Debug.Log(text);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
