using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Trap-1: the three trap Layouts (room text files with trap symbols) and the placement rules of a floor trap.
    // A floor trap never stands within 1.5 of a door's safe entry, on a SpawnPoint, an obstacle, a pit or a chest,
    // every pair of doors keeps a path whose feet circle touches no trap cell, and a chest never lands on a trap.
    public static class Week25Trap1Verification
    {
        public const string SpikeFieldId = "basic-spike-field";
        public const string FireLaneId = "wide-fire-lane";
        public const string WebGardenId = "large-web-garden";
        public const int RoomContentVersion = 13;

        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-6300f, 6300f);
        private const int GeneratedSeedCount = 400;
        private const int DeterminismSeedCount = 32;

        private static readonly (string id, string profileId, RoomTrapKind[] kinds)[] Layouts =
        {
            (SpikeFieldId, Week14Room1Setup.BasicProfileId,
                new[] { RoomTrapKind.FloorSpikes, RoomTrapKind.PeriodicSpikes }),
            (FireLaneId, Week14Room3Setup.WideProfileId, new[] { RoomTrapKind.Fire, RoomTrapKind.SpikedRock }),
            (WebGardenId, Week14Room6Setup.LargeProfileId,
                new[] { RoomTrapKind.DamagePuddle, RoomTrapKind.Spiderweb, RoomTrapKind.PoisonMushroom }),
        };

        // Builds the rooms whose text changed, then proves a second import leaves the trap Layouts untouched.
        [MenuItem("Trickal Fan Game/Week 25/Import and Verify Trap-1 Layouts")]
        public static void ImportAndVerifyBatch()
        {
            RoomLayoutImporter.Report first = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
            Assert(first.Rejected.Count == 0, first.Format());
            string[] paths = Layouts.SelectMany(layout => new[]
                { RoomLayoutImporter.TemplatePath(layout.id), RoomLayoutImporter.PrefabPath(layout.id) }).ToArray();
            Dictionary<string, string> guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Dictionary<string, string> contents = paths.ToDictionary(path => path, System.IO.File.ReadAllText,
                StringComparer.Ordinal);
            RoomLayoutImporter.Report second = RoomLayoutImporter.Run(RoomLayoutImporter.LayoutFolderPath, true);
            Assert(second.Rejected.Count == 0 && second.Removed.Count == 0 && second.Imported.All(room => !room.Rebuilt),
                $"A second import of unchanged files must rebuild and remove nothing.\n{second.Format()}");
            foreach (string path in paths)
            {
                Assert(!string.IsNullOrWhiteSpace(guids[path]) && guids[path] == AssetDatabase.AssetPathToGUID(path) &&
                       contents[path] == System.IO.File.ReadAllText(path),
                    $"A second import of unchanged files changed {path}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Trap-1 Layouts")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Trap-1 verification needs the Game Scene generator.");
            Assert(RoomLayoutImporter.RoomContentVersion == RoomContentVersion &&
                   generator.RoomContentVersion >= RoomContentVersion,
                $"Trap-1 raises the Room content version to {RoomContentVersion}; the Game Scene has " +
                $"{generator.RoomContentVersion}. Import the room Layouts.");

            ValidatePlacementRules();
            RoomLayoutImporter.Report report = new();
            List<RoomLayoutDocument> documents = RoomLayoutImporter.ReadDocuments(
                RoomLayoutImporter.LayoutFolderPath, generator, report);
            Assert(report.Rejected.Count == 0, report.Format());
            foreach ((string id, string profileId, RoomTrapKind[] kinds) in Layouts)
            {
                RoomLayoutDocument document = documents.FirstOrDefault(candidate => candidate.Id == id);
                Assert(document != null, $"Trap Layout '{id}' has no room file.");
                RoomTemplateDefinition template = generator.RoomTemplates.FirstOrDefault(candidate =>
                    candidate != null && candidate.TemplateId == id);
                Assert(template != null && RoomLayoutImporter.IsImportedTemplate(template),
                    $"Trap Layout '{id}' is not in the Game Scene catalog. Import the room Layouts.");
                ValidateLayout(document, template, profileId, kinds, generator);
                ValidateChestPositions(template);
                ValidateRuntime(template);
            }

            // Layouts that hold no trap keep their chest positions: the trap rule only removes trap cells.
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                if (Layouts.Any(layout => layout.id == template.TemplateId)) continue;
                Assert(template.RoomPrefabAsset.GetComponentsInChildren<RoomTrap>(true).Length == 0,
                    $"Room '{template.TemplateId}' holds a trap but is not a Trap-1 Layout.");
            }

            ValidateGeneration(generator);
            Debug.Log("Trap-1 verification passed: floor traps are rejected off the grid, on an obstacle, a pit, a " +
                      "chest or a SpawnPoint, within 1.5 of a door's safe entry and when they cut the trap-free path " +
                      $"between the doors; {SpikeFieldId} (floor and periodic spikes in both phases), {FireLaneId} " +
                      $"(fire and spiked rocks) and {WebGardenId} (webs, poison mushrooms and puddles) match their " +
                      "files with position-based trap IDs and Layout difficulty +1, pass the obstacle and trap " +
                      "placement rules as built Prefabs, keep chests off every trap cell, bind with unique IDs and " +
                      "keep a bombed web and an extinguished fire gone on a rebuild; and " +
                      $"{GeneratedSeedCount} seeds generate with each trap Layout in use and repeat for the same seed " +
                      $"at Room content version {generator.RoomContentVersion}.");
        }

        [MenuItem("Trickal Fan Game/Week 25/Verify Trap-1 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // The room Layout importer and everything that reads its rooms, then the traps themselves.
            RoomLayoutImporterVerification.VerifyWithRegressionsBatch();
            Week25Trap0Verification.Verify();
            Debug.Log("Trap-1 regression verification passed.");
        }

        // The floor trap rules on hand-built cells of the Basic profile, each with the reason it must give.
        private static void ValidatePlacementRules()
        {
            RoomLayoutProfile profile = RoomLayoutFormat.FindProfile(Week14Room1Setup.BasicProfileId);
            RoomTemplateDoor[] doors = Week14Room3Setup.BuildDoorContracts(profile.RoomSize);
            Vector2[] spawnPoints = { profile.CellCenter(6, 3) };
            RoomObstacleFootprint Trap(string id, int column, int row) => new(id, profile.CellRect(column, row), false);
            RoomObstacleFootprint[] solid =
            {
                new("obstacle-01", profile.CellRect(3, 1), true),
                new("pit-01", profile.CellRect(9, 1), false, true),
            };
            bool Validate(out string error, params RoomObstacleFootprint[] traps) =>
                RoomObstacleLayout.TryValidateFloorTraps("trap-rules", profile.Floor, doors, spawnPoints, solid, traps,
                    out error);

            Assert(RoomObstacleLayout.FloorTrapDoorClearance == 1.5f && PlayerFeet.Radius == 0.3f,
                "Floor traps keep 1.5 from a safe entry and the trap-free path is judged by the 0.3 feet circle.");
            Assert(Validate(out string reason) && Validate(out reason, Trap("trap-a", 3, 3), Trap("trap-b", 4, 4)),
                $"Floor traps on free cells must be accepted. {reason}");
            (string label, RoomObstacleFootprint[] traps, string expected)[] rejected =
            {
                ("a repeated trap ID", new[] { Trap("trap-a", 3, 3), Trap("trap-a", 4, 4) }, "duplicates"),
                ("two traps on one cell", new[] { Trap("trap-a", 3, 3), Trap("trap-b", 3, 3) }, "overlap"),
                ("a trap on an obstacle", new[] { Trap("trap-a", 3, 1) }, "overlaps obstacle"),
                ("a trap on a pit", new[] { Trap("trap-a", 9, 1) }, "overlaps pit"),
                ("a trap on a SpawnPoint", new[] { Trap("trap-a", 6, 3) }, "SpawnPoint 1"),
                ("a trap off the grid", new[]
                {
                    new RoomObstacleFootprint("trap-a", new Rect(-2.25f, -2.25f, 1f, 1f), false),
                }, "grid"),
                ("a trap larger than a cell", new[]
                {
                    new RoomObstacleFootprint("trap-a", profile.CellRect(3, 4, 2, 1), false),
                }, "1x1"),
                ("a trap outside the floor", new[]
                {
                    new RoomObstacleFootprint("trap-a", profile.CellRect(-1, 5), false),
                }, "movement bounds"),
                ("a trap beside the Left safe entry", new[] { Trap("trap-a", 2, 2) }, "Left safe entry"),
                ("a trap beside the Up safe entry", new[] { Trap("trap-a", 7, 2) }, "Up safe entry"),
                ("a trap wall between the doors",
                    Enumerable.Range(0, 7).Select(row => Trap($"trap-{row}", 4, row)).ToArray(),
                    "avoids every floor trap"),
            };
            foreach ((string label, RoomObstacleFootprint[] traps, string expected) in rejected)
            {
                Assert(!Validate(out reason, traps) && reason.Contains(expected, StringComparison.Ordinal),
                    $"Floor trap placement must reject {label} with its reason. Got: {reason}");
            }

            // The cells just outside the 1.5 safe entry radius are free.
            Assert(Validate(out reason, Trap("trap-a", 3, 3), Trap("trap-b", 0, 1), Trap("trap-c", 4, 0),
                    Trap("trap-d", 6, 4)) == false && reason.Contains("Down safe entry", StringComparison.Ordinal),
                $"A trap under the room center is inside the Down safe entry radius. Got: {reason}");
            Assert(Validate(out reason, Trap("trap-a", 3, 3), Trap("trap-b", 0, 1), Trap("trap-c", 4, 0)),
                $"Traps just outside every safe entry radius must be accepted. {reason}");
        }

        private static void ValidateLayout(RoomLayoutDocument document, RoomTemplateDefinition template,
            string profileId, RoomTrapKind[] kinds, FloorGenerator generator)
        {
            string label = $"Trap Layout '{document.Id}'";
            Assert(document.Profile.ProfileId == profileId && template.Profile.ProfileId == profileId &&
                   template.AllowedRoomTypes.SequenceEqual(new[] { RoomType.Normal }) &&
                   document.Difficulty == Week19Difficulty1Setup.ObstacleLayoutModifier &&
                   template.LayoutDifficultyModifier == Week19Difficulty1Setup.ObstacleLayoutModifier,
                $"{label} must be a Normal '{profileId}' room with the obstacle Layout difficulty +1.");
            Assert(RoomLayoutImporter.TryValidateGeometry(document, out string error), $"{label}: {error}");
            Assert(RoomLayoutImporter.TryValidateBuilt(template, generator, out error), $"{label}: {error}");

            GameObject prefab = template.RoomPrefabAsset;
            RoomTrap[] traps = prefab.GetComponentsInChildren<RoomTrap>(true);
            RoomLayoutCell[] cells = document.Traps.ToArray();
            Assert(traps.Length == cells.Length && cells.Length > 0 &&
                   traps.Select(trap => trap.Kind).Distinct().OrderBy(kind => kind)
                       .SequenceEqual(kinds.OrderBy(kind => kind)),
                $"{label} must hold exactly {string.Join(", ", kinds)}; it holds " +
                $"{string.Join(", ", traps.Select(trap => trap.Kind).Distinct())}.");
            foreach (RoomLayoutCell cell in cells)
            {
                string id = cell.Id(RoomLayoutImporter.TrapIdPrefix);
                RoomTrap trap = traps.SingleOrDefault(candidate => candidate.TrapId == id);
                Assert(trap != null && trap.Kind == cell.Symbol.TrapKind && trap.SecondPhase == cell.Symbol.SecondPhase &&
                       (Vector2)prefab.transform.InverseTransformPoint(trap.transform.position) == cell.Center &&
                       trap.TryValidate(out error),
                    $"{label} trap {id} does not match its symbol '{cell.Symbol.Symbol}'. {error}");
                DestructibleObstacle obstacle = trap.GetComponent<DestructibleObstacle>();
                Assert(trap.IsFloorTrap
                        ? obstacle == null
                        : obstacle != null && obstacle.ObstacleId == id &&
                          obstacle.GetComponent<RoomObstacleVariantSlot>() == null,
                    $"{label} trap {id} must be an obstacle with the same ID only when its kind is one, and never " +
                    "a seeded special obstacle candidate.");
            }

            if (kinds.Contains(RoomTrapKind.PeriodicSpikes))
            {
                RoomTrap[] periodic = traps.Where(trap => trap.Kind == RoomTrapKind.PeriodicSpikes).ToArray();
                Assert(periodic.Any(trap => trap.SecondPhase) && periodic.Any(trap => !trap.SecondPhase),
                    $"{label} must use both periodic spike phases.");
            }

            // The built Prefab against the rules, read back from its components instead of the text.
            Assert(RoomObstacleLayout.TryCollectFootprints(prefab, out List<RoomObstacleFootprint> footprints,
                out error), error);
            List<RoomObstacleFootprint> floorTraps = RoomObstacleLayout.CollectFloorTraps(prefab);
            Assert(floorTraps.Count == cells.Count(cell => cell.Symbol.Kind == RoomLayoutCellKind.FloorTrap),
                $"{label} Prefab does not hold one floor trap cell per floor trap symbol.");
            Assert(RoomObstacleLayout.TryValidateFloorTraps(document.Id, template.Profile.MovementBounds,
                    template.DoorSlots, template.SpawnPoints, footprints, floorTraps, out error),
                $"{label}: {error}");
            foreach (RoomObstacleFootprint trap in floorTraps)
            {
                foreach (RoomTemplateDoor door in template.DoorSlots)
                {
                    Vector2 nearest = new(Mathf.Clamp(door.SafeEntryPosition.x, trap.Bounds.xMin, trap.Bounds.xMax),
                        Mathf.Clamp(door.SafeEntryPosition.y, trap.Bounds.yMin, trap.Bounds.yMax));
                    Assert(Vector2.Distance(nearest, door.SafeEntryPosition) >=
                           RoomObstacleLayout.FloorTrapDoorClearance - 0.001f,
                        $"{label} floor trap {trap.ObstacleId} is within 1.5 of the {door.Direction} safe entry.");
                }

                Assert(template.SpawnPoints.All(point => !trap.Bounds.Contains(point)) &&
                       footprints.All(footprint => !RoomObstacleLayout.Overlaps(trap.Bounds, footprint.Bounds)),
                    $"{label} floor trap {trap.ObstacleId} overlaps a SpawnPoint, an obstacle or a pit.");
            }

            // One more trap across the room breaks the trap-free path, so the check reads the real cells.
            if (floorTraps.Count == 0) return;
            Rect floor = document.Profile.GridRect;
            List<RoomObstacleFootprint> walled = new(floorTraps);
            for (float y = floor.yMin; y < floor.yMax - 0.5f; y += 1f)
            {
                Rect cell = new(floor.xMin + 4f, y, 1f, 1f);
                if (walled.Any(trap => RoomObstacleLayout.Overlaps(trap.Bounds, cell)) ||
                    footprints.Any(footprint => RoomObstacleLayout.Overlaps(footprint.Bounds, cell)))
                {
                    continue;
                }

                walled.Add(new RoomObstacleFootprint($"wall-{walled.Count:00}", cell, false));
            }

            Assert(!RoomObstacleLayout.TryValidateFloorTraps(document.Id, document.Profile.Floor,
                       template.DoorSlots, Array.Empty<Vector2>(), footprints, walled, out error) &&
                   error.Contains("avoids every floor trap", StringComparison.Ordinal),
                $"{label} with a trap column added must lose its trap-free path. Got: {error}");
        }

        // Wherever a chest is asked for, including on top of each trap, it lands clear of every floor trap cell.
        private static void ValidateChestPositions(RoomTemplateDefinition template)
        {
            GameObject prefab = template.RoomPrefabAsset;
            List<RoomObstacleFootprint> floorTraps = RoomObstacleLayout.CollectFloorTraps(prefab);
            List<Vector2> preferred = new() { Vector2.zero };
            preferred.AddRange(floorTraps.Select(trap => trap.Bounds.center));
            preferred.AddRange(template.DoorSlots.Select(door => door.SafeEntryPosition));
            foreach (Vector2 point in preferred)
            {
                Assert(ChestPlacement.TryFindSafeLocalPosition(template, prefab, point, null, out Vector2 position,
                    out string error), $"Trap Layout '{template.TemplateId}' has no chest position near {point}. {error}");
                Assert(!ChestPlacement.OverlapsFloorTrap(position, floorTraps),
                    $"Trap Layout '{template.TemplateId}' puts a chest asked for at {point} on a floor trap at " +
                    $"{position}.");
            }

            if (floorTraps.Count == 0) return;
            Vector2 onTrap = floorTraps[0].Bounds.center;
            Assert(ChestPlacement.OverlapsFloorTrap(onTrap, floorTraps) &&
                   ChestPlacement.OverlapsFloorTrap(onTrap + Vector2.right * 0.5f, floorTraps) &&
                   !ChestPlacement.OverlapsFloorTrap(onTrap, null),
                "A chest body on or across the edge of a trap cell must count as on the trap.");
        }

        // The built room bound the way the assembler binds it: unique IDs, both spike phases, and a rebuild that
        // keeps a bombed web and an extinguished fire gone.
        private static void ValidateRuntime(RoomTemplateDefinition template)
        {
            List<GameObject> roots = new();
            try
            {
                RoomRunState state = new($"trap-1-{template.TemplateId}");
                RoomTrap[] traps = Bind(template, state, roots);
                Assert(traps.Select(trap => trap.TrapId).Distinct(StringComparer.Ordinal).Count() == traps.Length,
                    $"Trap Layout '{template.TemplateId}' repeats a trap ID.");
                float time = Time.time;
                foreach (RoomTrap trap in traps.Where(trap => trap.Kind == RoomTrapKind.PeriodicSpikes))
                {
                    Assert(trap.StateAt(time) ==
                           (trap.SecondPhase ? PeriodicSpikeState.Raised : PeriodicSpikeState.Lowered) &&
                           trap.StateAt(time + RoomTrap.PeriodicLoweredSeconds) ==
                           (trap.SecondPhase ? PeriodicSpikeState.Lowered : PeriodicSpikeState.Raised),
                        $"Periodic spikes {trap.TrapId} must start {(trap.SecondPhase ? "raised" : "lowered")} and " +
                        "swap after half a cycle.");
                }

                RoomTrap web = traps.FirstOrDefault(trap => trap.Kind == RoomTrapKind.Spiderweb);
                RoomTrap fire = traps.FirstOrDefault(trap => trap.Kind == RoomTrapKind.Fire);
                if (web != null)
                {
                    Assert(RoomTrap.RemoveWebsByBombInCircle(web.transform.position, 0.2f) == 1 && web.IsRemoved &&
                           state.IsTrapRemoved(web.TrapId),
                        $"A bomb must remove web {web.TrapId} and record it on the room.");
                }

                if (fire != null)
                {
                    Assert(fire.TryExtinguish() && state.IsObstacleDestroyed(fire.TrapId),
                        $"Fire {fire.TrapId} must go out and be recorded on the room.");
                }

                if (web == null && fire == null) return;
                RoomTrap[] rebuilt = Bind(template, state, roots);
                foreach (RoomTrap trap in rebuilt)
                {
                    bool gone = (web != null && trap.TrapId == web.TrapId) || (fire != null && trap.TrapId == fire.TrapId);
                    Assert(trap.gameObject.activeSelf != gone,
                        $"A rebuilt '{template.TemplateId}' must keep only the removed web and the extinguished " +
                        $"fire gone; trap {trap.TrapId} is {(trap.gameObject.activeSelf ? "present" : "gone")}.");
                }
            }
            finally
            {
                foreach (GameObject root in roots)
                    if (root != null) Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static RoomTrap[] Bind(RoomTemplateDefinition template, RoomRunState state, List<GameObject> roots)
        {
            GameObject instance = Object.Instantiate(template.RoomPrefabAsset, Origin, Quaternion.identity);
            roots.Add(instance);
            RoomPrefab room = instance.GetComponent<RoomPrefab>();
            // Room content is switched off until the player enters; bombs and water only reach an active room.
            if (room.Node != null && room.Node.ContentRoot != null) room.Node.ContentRoot.SetActive(true);
            Assert(RoomObstacleVariantSlot.TryResolveForRoom(room, 7, out string error), error);
            HashSet<string> obstacleIds = new(StringComparer.Ordinal);
            foreach (DestructibleObstacle obstacle in instance.GetComponentsInChildren<DestructibleObstacle>(true))
            {
                Assert(obstacle.TryValidate(out error) && obstacleIds.Add(obstacle.ObstacleId),
                    $"Trap Layout '{template.TemplateId}' has an invalid or duplicate obstacle " +
                    $"'{obstacle.ObstacleId}'. {error}");
                obstacle.Bind(state, 7, instance.transform, null);
            }

            RoomTrap[] traps = instance.GetComponentsInChildren<RoomTrap>(true);
            foreach (RoomTrap trap in traps)
            {
                Assert(trap.TryValidate(out error), error);
                trap.Bind(state, null, Time.time);
            }

            Physics2D.SyncTransforms();
            return traps;
        }

        private static void ValidateGeneration(FloorGenerator generator)
        {
            Dictionary<string, int> uses = Layouts.ToDictionary(layout => layout.id, _ => 0, StringComparer.Ordinal);
            for (int seed = 1; seed <= GeneratedSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error),
                    $"Seed {seed} failed to generate with the trap Layouts. {error}");
                if (seed <= DeterminismSeedCount)
                {
                    Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                    Assert(Signature(graph) == Signature(repeat),
                        $"Seed {seed} changed its room templates or content seeds on regeneration.");
                }

                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (node.TemplateId == null || !uses.ContainsKey(node.TemplateId)) continue;
                    Assert(node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null,
                        $"Seed {seed} put trap Layout '{node.TemplateId}' into {node.Role} room {node.RoomId}.");
                    uses[node.TemplateId]++;
                }
            }

            foreach (KeyValuePair<string, int> entry in uses)
                Assert(entry.Value > 0, $"Trap Layout '{entry.Key}' never appeared in {GeneratedSeedCount} seeds.");
        }

        private static string Signature(GeneratedFloorGraph graph) =>
            string.Join("|", graph.Nodes.Select(node => $"{node.RoomId}:{node.TemplateId}:{node.ContentSeed}"));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
