using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week18Obstacle2Verification
    {
        // Far from every authored room so only the verification instance takes part in physics queries.
        private static readonly Vector2 Origin = new(-5000f, 5000f);
        private const int SelectionSeedLimit = 1024;

        [MenuItem("Trickal Fan Game/Week 18/Setup and Verify Obstacle-2 Obstacle Layouts")]
        public static void SetupAndVerifyBatch()
        {
            Week18Obstacle2Setup.Setup();
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Dictionary<string, string> guids = Week18Obstacle2Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week18Obstacle2Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Obstacle-2 setup changed the Game Scene GUID.");
            foreach (KeyValuePair<string, string> entry in guids)
            {
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Obstacle-2 setup changed or lost the GUID for {entry.Key}.");
            }

            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 18/Verify Obstacle-2 Obstacle Layouts")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week18Obstacle2Setup.RoomContentVersion,
                "Run Obstacle-2 setup before verification.");

            RoomTemplateDefinition empty = FindTemplate(generator, Week14Room6Setup.LargeTemplateId);
            RoomTemplateDefinition pillar = FindTemplate(generator, Week14Room7Setup.TemplateId);
            RoomTemplateDefinition[] layouts = Week18Obstacle2Setup.Layouts
                .Select(layout => FindTemplate(generator, layout.TemplateId))
                .ToArray();

            ValidateSharedRoomSize(empty, pillar, layouts);
            for (int index = 0; index < layouts.Length; index++)
            {
                ValidateAuthoredLayout(Week18Obstacle2Setup.Layouts[index], layouts[index], empty);
                ValidateEncounterCompatibility(generator, empty, layouts[index]);
                ValidatePhysicsRoutes(layouts[index]);
            }

            ValidateViolationsFail(layouts[0]);
            ValidateCatalogRejectsInvalidLayout(generator, layouts[0]);
            ValidateSelection(generator, empty, pillar, layouts);
            Debug.Log("Obstacle-2 verification passed: the empty Large room, the fixed pillar room and three " +
                      "destructible obstacle Layouts share the Large profile; each Layout owns its SpawnPoints and " +
                      "passes the overlap, required door passage, reachability and ranged sightline checks with real " +
                      "physics routes; every Encounter valid for the empty room stays valid; overlap, blocked " +
                      "passage, covered SpawnPoint, sealed entry, blind SpawnPoint, off-grid, out-of-bounds and " +
                      "duplicate-ID violations fail explicitly, including through the Room catalog and generator.");
        }

        private static void ValidateSharedRoomSize(RoomTemplateDefinition empty, RoomTemplateDefinition pillar,
            IEnumerable<RoomTemplateDefinition> layouts)
        {
            RoomProfile large = empty.Profile;
            Assert(large != null && large.ProfileId == Week14Room6Setup.LargeProfileId && pillar.Profile == large,
                "The empty and pillar rooms must use the Large profile.");
            Assert(RoomObstacleLayout.TryCollectFootprints(empty.RoomPrefabAsset, out var emptyFootprints, out string error) &&
                   emptyFootprints.Count == 0, $"The empty Large room must stay free of obstacles. {error}");
            Assert(pillar.TryValidateLayout(out error), $"The fixed pillar room must pass the Layout checks. {error}");
            foreach (RoomTemplateDefinition layout in layouts)
            {
                Assert(layout.Profile == large, $"Layout '{layout.TemplateId}' must share the Large profile.");
                Assert(layout.SupportsRoomType(RoomType.Normal) && layout.AllowedRoomTypes.Count == 1,
                    $"Layout '{layout.TemplateId}' must only be used for Normal combat rooms.");
            }
        }

        private static void ValidateAuthoredLayout(Week18Obstacle2Setup.LayoutSpec spec, RoomTemplateDefinition template,
            RoomTemplateDefinition empty)
        {
            Assert(template.TryValidate(out string error), error);
            Assert(template.TryValidateLayout(out error), error);
            // Encounter-4 appends SpawnPoints after the Obstacle-2 authored ones.
            Assert(template.SpawnPoints.Take(spec.SpawnPoints.Length).SequenceEqual(spec.SpawnPoints),
                $"Layout '{spec.TemplateId}' must own its authored SpawnPoints.");
            Assert(!template.SpawnPoints.SequenceEqual(empty.SpawnPoints),
                $"Layout '{spec.TemplateId}' must not reuse the empty room SpawnPoints.");

            GameObject obstaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(Week18Obstacle1Setup.DropTablePath);
            DestructibleObstacle[] obstacles = template.RoomPrefabAsset.GetComponentsInChildren<DestructibleObstacle>(true);
            Assert(obstacles.Length == spec.Cells.Length &&
                   template.RoomPrefabAsset.GetComponentsInChildren<RoomStaticObstacle>(true).Length == 0,
                $"Layout '{spec.TemplateId}' must contain exactly its {spec.Cells.Length} destructible obstacles.");
            RoomPrefab room = template.RoomPrefabAsset.GetComponent<RoomPrefab>();
            for (int index = 0; index < spec.Cells.Length; index++)
            {
                string id = Week18Obstacle2Setup.ObstacleId(index);
                DestructibleObstacle obstacle = obstacles.SingleOrDefault(candidate => candidate.ObstacleId == id);
                Assert(obstacle != null, $"Layout '{spec.TemplateId}' is missing stable obstacle ID '{id}'.");
                Vector2 local = room.transform.InverseTransformPoint(obstacle.transform.position);
                Assert((local - spec.Cells[index]).sqrMagnitude < 0.0001f,
                    $"Layout '{spec.TemplateId}' obstacle '{id}' is not at its authored cell.");
                Assert(PrefabUtility.GetCorrespondingObjectFromSource(obstacle.gameObject) == obstaclePrefab &&
                       obstacle.RequiredHits == DestructibleObstacle.DefaultRequiredHits && obstacle.DropTable == table &&
                       obstacle.TryValidate(out error),
                    $"Layout '{spec.TemplateId}' obstacle '{id}' must be the 5-hit Obstacle-1 prefab. {error}");
            }
        }

        // An obstacle Layout must never lose an Encounter that the empty room of the same size accepts.
        private static void ValidateEncounterCompatibility(FloorGenerator generator, RoomTemplateDefinition empty,
            RoomTemplateDefinition layout)
        {
            int accepted = 0;
            foreach (EncounterDefinition encounter in generator.EncounterDefinitions)
            {
                for (int floor = 1; floor <= generator.FloorCount; floor++)
                {
                    if (!encounter.TryValidateFor(empty, floor, out _)) continue;
                    Assert(encounter.TryValidateFor(layout, floor, out string error),
                        $"Layout '{layout.TemplateId}' rejects Encounter '{encounter.EncounterId}' on floor {floor}. {error}");
                    accepted++;
                }
            }

            Assert(accepted > 0, $"Layout '{layout.TemplateId}' has no compatible Encounter.");
        }

        // Cross-checks the static grid with real colliders and the enemy A* navigator.
        private static void ValidatePhysicsRoutes(RoomTemplateDefinition template)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template.RoomPrefabAsset);
            try
            {
                instance.transform.position = Origin;
                Physics2D.SyncTransforms();
                List<Vector2> path = new();
                Vector2[] entries = template.DoorSlots.Select(door => Origin + door.SafeEntryPosition).ToArray();
                Vector2[] spawns = template.SpawnPoints.Select(point => Origin + point).ToArray();
                foreach (Vector2 spawn in spawns)
                {
                    Assert(Physics2D.OverlapCircle(spawn, RoomObstacleLayout.ActorRadius,
                            EnemyObstacleNavigator.ObstacleMask) == null,
                        $"Layout '{template.TemplateId}' SpawnPoint {spawn - Origin} overlaps a collider.");
                }

                foreach (Vector2 from in entries)
                foreach (Vector2 to in entries.Concat(spawns))
                {
                    if (from == to) continue;
                    Assert(EnemyObstacleNavigator.TryFindPath(from, to, RoomObstacleLayout.ActorRadius, path) &&
                           path.Count > 0,
                        $"Layout '{template.TemplateId}' has no physics route from {from - Origin} to {to - Origin}.");
                }

                int blockedLines = 0;
                foreach (Vector2 spawn in spawns)
                foreach (Vector2 entry in entries)
                {
                    if (!EnemyObstacleNavigator.HasLineOfFire(spawn, entry)) blockedLines++;
                }

                Debug.Log($"Obstacle-2 '{template.TemplateId}': {blockedLines} of {spawns.Length * entries.Length} " +
                          "SpawnPoint-to-entry lines of fire are blocked by obstacles.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateViolationsFail(RoomTemplateDefinition template)
        {
            Assert(RoomObstacleLayout.TryCollectFootprints(template.RoomPrefabAsset,
                    out List<RoomObstacleFootprint> valid, out string error), error);
            RoomProfile profile = template.Profile;
            RoomTemplateDoor left = template.DoorSlots.Single(door => door.Direction == RoomDoorDirection.Left);
            Vector2 spawn = template.SpawnPoints[0];
            // The last Obstacle-2 authored point; Encounter-4 points after it can sit next to a door passage.
            Vector2 lastSpawn = template.SpawnPoints[Math.Min(2, template.SpawnPoints.Count - 1)];
            Rect movement = profile.MovementBounds;

            ExpectFailure(template, valid, "overlap", "overlap",
                Static("obstacle-90", valid[0].Bounds));
            ExpectFailure(template, valid, "blocked door passage", "required door passage",
                Unit("obstacle-90", new Vector2(Mathf.Round(left.SafeEntryPosition.x) - 0.5f, 0.5f)));
            ExpectFailure(template, valid, "covered SpawnPoint", "overlaps obstacle",
                Static("obstacle-90", new Rect(spawn - Vector2.one * 0.5f, Vector2.one)));
            ExpectFailure(template, valid, "sealed entry", "cut the",
                Static("obstacle-90", Rect.MinMaxRect(-7f, movement.yMin, -6f, movement.yMax)));
            ExpectFailure(template, valid, "blind SpawnPoint", "sees only",
                Static("obstacle-90", Rect.MinMaxRect(lastSpawn.x - 1.5f, lastSpawn.y - 1f, lastSpawn.x - 1f, lastSpawn.y + 1.5f)),
                Static("obstacle-91", Rect.MinMaxRect(lastSpawn.x + 1f, lastSpawn.y - 1f, lastSpawn.x + 1.5f, lastSpawn.y + 1.5f)),
                Static("obstacle-92", Rect.MinMaxRect(lastSpawn.x - 1.5f, lastSpawn.y + 1.5f, lastSpawn.x + 1.5f, lastSpawn.y + 2f)));
            ExpectFailure(template, valid, "off-grid obstacle", "1x1 unit",
                Unit("obstacle-90", new Vector2(8.25f, 1.5f)));
            ExpectFailure(template, valid, "out-of-bounds obstacle", "leaves the movement bounds",
                Unit("obstacle-90", new Vector2(movement.xMax, 5f)));
            ExpectFailure(template, valid, "duplicate obstacle ID", "duplicates obstacle ID",
                Unit(valid[0].ObstacleId, new Vector2(8.5f, 1.5f)));
        }

        private static void ValidateCatalogRejectsInvalidLayout(FloorGenerator generator, RoomTemplateDefinition source)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source.RoomPrefabAsset);
            RoomTemplateDefinition invalid = ScriptableObject.CreateInstance<RoomTemplateDefinition>();
            GameObject holder = new("Obstacle-2 Invalid Catalog Verification");
            try
            {
                instance.transform.position = Origin;
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                RoomPrefab room = instance.GetComponent<RoomPrefab>();
                RoomTemplateDoor left = source.DoorSlots.Single(door => door.Direction == RoomDoorDirection.Left);
                DestructibleObstacle blocker = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<DestructibleObstacle>(Week18Obstacle1Setup.PrefabPath),
                    room.Node.ContentRoot.transform);
                blocker.transform.localPosition = new Vector2(Mathf.Round(left.SafeEntryPosition.x) - 0.5f, 0.5f);
                blocker.Configure("obstacle-90", DestructibleObstacle.DefaultRequiredHits, null, null);

                invalid.Configure("large-invalid-blocked-door", source.Profile, instance, new[] { RoomType.Normal },
                    source.DoorSlots.ToArray(), source.SpawnPoints.ToArray());
                Assert(invalid.TryValidate(out string error), $"The invalid Layout must pass the basic contract. {error}");
                Assert(!RoomContractCatalog.TryValidate(new[] { source.Profile }, new[] { invalid }, out error) &&
                       error.Contains("required door passage", StringComparison.Ordinal),
                    $"The Room catalog must reject a Layout that blocks a door passage. {error}");

                FloorGenerator candidate = holder.AddComponent<FloorGenerator>();
                candidate.Configure(generator.FloorCount, generator.MinimumRoomsPerFloor, generator.MaximumRoomsPerFloor,
                    generator.MinimumBossDistance, generator.GenerationRetryLimit, generator.RoomDefinitions.ToArray());
                candidate.ConfigureTemplates(generator.RoomContentVersion,
                    generator.RoomTemplates.Append(invalid).ToArray());
                candidate.ConfigureEncounters(generator.EncounterContentVersion, generator.EncounterDefinitions.ToArray());
                Assert(!candidate.TryGenerateForSeed(Week8RandomRoomSetup.FixedVerificationSeed, out _, out error) &&
                       error.Contains("required door passage", StringComparison.Ordinal),
                    $"Floor generation must fail explicitly with an invalid obstacle Layout. {error}");
            }
            finally
            {
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(invalid);
                Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateSelection(FloorGenerator generator, RoomTemplateDefinition empty,
            RoomTemplateDefinition pillar, IReadOnlyList<RoomTemplateDefinition> layouts)
        {
            HashSet<string> required = new(layouts.Select(layout => layout.TemplateId), StringComparer.Ordinal)
            {
                empty.TemplateId,
                pillar.TemplateId,
            };
            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= SelectionSeedLimit && !required.IsSubsetOf(seen); seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeat, out error), error);
                Assert(graph.Nodes.Select(node => node.TemplateId).SequenceEqual(repeat.Nodes.Select(node => node.TemplateId)),
                    $"Seed {seed} must select the same Layouts every time.");
                foreach (GeneratedRoomNode node in graph.Nodes)
                {
                    if (!required.Contains(node.TemplateId) || node.Role != GeneratedRoomRole.Intermediate) continue;
                    Assert(node.RoomType == RoomType.Normal && node.Encounter != null &&
                           node.Encounter.TryValidateFor(node.Template, node.FloorNumber, node.DirectionalConnections,
                               out error),
                        $"Seed {seed} room {node.RoomId} Layout '{node.TemplateId}' has no valid Encounter. {error}");
                    seen.Add(node.TemplateId);
                }
            }

            Assert(required.IsSubsetOf(seen),
                $"{SelectionSeedLimit} seeds must select the empty, pillar and every obstacle Layout. Missing: " +
                string.Join(", ", required.Except(seen)));
        }

        private static void ExpectFailure(RoomTemplateDefinition template, IEnumerable<RoomObstacleFootprint> valid,
            string label, string expected, params RoomObstacleFootprint[] added)
        {
            List<RoomObstacleFootprint> footprints = valid.Concat(added).ToList();
            bool passed = RoomObstacleLayout.TryValidate(template.TemplateId, template.Profile.MovementBounds,
                template.Profile.EncounterBounds, template.DoorSlots, template.SpawnPoints, footprints, out string error);
            Assert(!passed && error != null && error.Contains(expected, StringComparison.Ordinal),
                $"A {label} Layout must fail with '{expected}', but got: {(passed ? "pass" : error)}");
        }

        private static RoomObstacleFootprint Unit(string id, Vector2 center) =>
            new(id, new Rect(center - Vector2.one * 0.5f, Vector2.one), true);

        private static RoomObstacleFootprint Static(string id, Rect bounds) => new(id, bounds, false);

        private static RoomTemplateDefinition FindTemplate(FloorGenerator generator, string templateId)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                candidate != null && candidate.TemplateId == templateId);
            Assert(template != null, $"The Game Scene catalog is missing Room Template '{templateId}'.");
            return template;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
