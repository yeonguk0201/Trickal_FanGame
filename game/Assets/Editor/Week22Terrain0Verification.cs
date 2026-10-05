using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Terrain-0: an ordinary pit blocks the player, enemies and pickups but lets every projectile and line of fire
    // pass. Pit Layouts use new template IDs, never cover door passages, SpawnPoints or chest positions, enemies path
    // around pits, the pit stays separate from SecretPit, and the same seed selects the same Layouts.
    public static class Week22Terrain0Verification
    {
        private const int SelectionSeedCount = 512;
        private static readonly Vector2 TestOrigin = new(7000f, 7000f);

        [MenuItem("Trickal Fan Game/Week 22/Setup and Verify Terrain-0 Pit Layouts")]
        public static void SetupAndVerifyBatch()
        {
            Week22Terrain0Setup.Setup();
            Dictionary<string, string> guids = Week22Terrain0Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week22Terrain0Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Terrain-0 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        // Re-runs the Layout, Encounter and chest placement checks that now see pits, and the obstacle Layouts whose
        // footprint contract was extended.
        [MenuItem("Trickal Fan Game/Week 22/Verify Terrain-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week18Obstacle2Verification.Verify();
            Week19Difficulty1Verification.Verify();
            Week19Encounter4Verification.Verify();
            Week20Obstacle4Verification.Verify();
            Week22Chest0Verification.Verify();
            Week22Chest1Verification.Verify();
            Debug.Log("Terrain-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Terrain-0 Pit Layouts")]
        public static void Verify()
        {
            int pitLayer = ValidateLayerContract();
            Week18Obstacle2Setup.OpenGameScene();
            // Opening the scene unloads unused assets, so Prefabs are loaded after it.
            GameObject pitPrefab = ValidatePitPrefab(pitLayer);
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week22Terrain0Setup.RoomContentVersion,
                "Run Terrain-0 setup before verification.");

            ValidateLayouts(generator, pitPrefab);
            ValidateLayoutRules(generator);
            ValidatePhysics(pitPrefab);
            ValidateEnemyNavigation(pitPrefab);
            ValidateChestPlacement(generator);
            ValidateSelection(generator);
            Debug.Log("Terrain-0 verification passed: the Pit layer blocks the player, enemies and pickups while " +
                      "player and enemy projectiles and lines of fire pass, enemies path around pits, " +
                      "basic-central-pit and large-pit-lanes keep doors, SpawnPoints and chest positions clear, " +
                      "SecretPit stays separate, and seeded selection reproduces both Layouts on every floor.");
        }

        private static int ValidateLayerContract()
        {
            int pitLayer = RoomPit.Layer;
            Assert(pitLayer >= 8, "The Pit user layer is missing.");
            HashSet<int> colliding = new(Week22Terrain0Setup.PitCollisionLayers.Select(LayerMask.NameToLayer));
            for (int layer = 0; layer < 32; layer++)
            {
                Assert(Physics2D.GetIgnoreLayerCollision(pitLayer, layer) != colliding.Contains(layer),
                    $"Pit must {(colliding.Contains(layer) ? "collide with" : "ignore")} layer " +
                    $"'{LayerMask.LayerToName(layer)}'.");
            }

            int pitBit = 1 << pitLayer;
            int environmentBit = 1 << LayerMask.NameToLayer("Environment");
            Assert((RoomPit.MovementBlockMask & pitBit) != 0 && (RoomPit.MovementBlockMask & environmentBit) != 0,
                "The movement block mask must cover Environment and Pit.");
            Assert(EnemyObstacleNavigator.ObstacleMask == RoomPit.MovementBlockMask,
                "Enemy walking must avoid pits.");
            Assert((EnemyObstacleNavigator.LineOfFireMask & pitBit) == 0 &&
                   (EnemyObstacleNavigator.LineOfFireMask & environmentBit) != 0,
                "Enemy lines of fire must ignore pits but not walls.");
            Assert((TreasureChest.PushBlockMask & pitBit) != 0, "A pushed chest must stop at a pit.");
            return pitLayer;
        }

        private static GameObject ValidatePitPrefab(int pitLayer)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            Assert(prefab != null, "The Room Pit Prefab is missing.");
            RoomPit pit = prefab.GetComponent<RoomPit>();
            Assert(pit != null && pit.TryValidate(out string error), "The Room Pit Prefab is invalid.");
            Assert(prefab.layer == pitLayer && !prefab.GetComponent<BoxCollider2D>().isTrigger,
                "A pit must be a solid collider on the Pit layer.");
            Assert(prefab.GetComponentsInChildren<SpriteRenderer>(true).All(renderer =>
                    renderer.sortingOrder < 0 && renderer.sortingOrder > -100),
                "Pit visuals must draw above the floor artwork and below actors.");

            GameObject secretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special3Setup.SecretPitPrefabPath);
            Assert(secretPrefab != null && secretPrefab.GetComponent<SecretPit>() != null &&
                   secretPrefab.GetComponent<RoomPit>() == null && secretPrefab.layer != pitLayer,
                "SecretPit must stay a separate trigger outside the Pit layer.");
            Assert(prefab.GetComponent<SecretPit>() == null, "An ordinary pit must not lead to the secret room.");

            GameObject broken = Object.Instantiate(prefab);
            try
            {
                broken.GetComponent<BoxCollider2D>().isTrigger = true;
                Assert(!broken.GetComponent<RoomPit>().TryValidate(out error) && error.Contains("solid"),
                    "A trigger pit must fail validation.");
                broken.GetComponent<BoxCollider2D>().isTrigger = false;
                broken.layer = LayerMask.NameToLayer("Environment");
                Assert(!broken.GetComponent<RoomPit>().TryValidate(out error) && error.Contains("Pit layer"),
                    "A pit outside the Pit layer must fail validation.");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }

            return prefab;
        }

        private static void ValidateLayouts(FloorGenerator generator, GameObject pitPrefab)
        {
            ObstacleVariantTable variants = AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(
                Week20Obstacle4Setup.FairyKingdomVariantTablePath);
            Dictionary<string, int> spawnCounts = new(StringComparer.Ordinal)
            {
                [Week14Room1Setup.BasicProfileId] = 5,
                [Week14Room6Setup.LargeProfileId] = 6,
            };
            foreach (Week22Terrain0Setup.LayoutSpec spec in Week22Terrain0Setup.Layouts)
            {
                RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                    candidate != null && candidate.TemplateId == spec.TemplateId);
                string error = null;
                Assert(template != null && template.TryValidate(out error) && template.TryValidateLayout(out error),
                    $"Pit Layout '{spec.TemplateId}' is missing or invalid. {error}");
                Assert(template.Profile.ProfileId == spec.ProfileId &&
                       template.AllowedRoomTypes.SequenceEqual(new[] { RoomType.Normal }) &&
                       template.MinimumFloor == 1 && template.MaximumFloor >= 3,
                    $"Pit Layout '{spec.TemplateId}' must be a Normal room on every floor of its profile.");
                Assert(template.LayoutDifficultyModifier == Week19Difficulty1Setup.ObstacleLayoutModifier,
                    $"Pit Layout '{spec.TemplateId}' must keep the +1 Layout difficulty modifier.");
                Assert(template.SpawnPoints.Count == spawnCounts[spec.ProfileId] &&
                       template.SpawnPoints.Take(Week19Encounter4Setup.BaseSpawnPointCount)
                           .SequenceEqual(spec.SpawnPoints),
                    $"Pit Layout '{spec.TemplateId}' must keep its authored SpawnPoints plus Encounter-4's.");

                GameObject prefab = template.RoomPrefabAsset;
                RoomPit[] pits = prefab.GetComponentsInChildren<RoomPit>(true);
                Assert(pits.Length == spec.Pits.Length, $"Pit Layout '{spec.TemplateId}' has the wrong pit count.");
                for (int index = 0; index < pits.Length; index++)
                {
                    Assert(pits[index].TryValidate(out error) && pits[index].PitId == Week22Terrain0Setup.PitId(index),
                        $"Pit Layout '{spec.TemplateId}' pit {index + 1} is invalid. {error}");
                    Assert(PrefabUtility.GetCorrespondingObjectFromSource(pits[index].gameObject) == pitPrefab,
                        $"Pit Layout '{spec.TemplateId}' must place the shared Room Pit Prefab.");
                }

                Assert(prefab.GetComponentsInChildren<SecretPit>(true).Length == 0,
                    $"Pit Layout '{spec.TemplateId}' must not author a SecretPit.");
                DestructibleObstacle[] obstacles = prefab.GetComponentsInChildren<DestructibleObstacle>(true);
                Assert(obstacles.Length == spec.ObstacleCells.Length &&
                       obstacles.All(obstacle => obstacle.TryGetComponent(out RoomObstacleVariantSlot slot) &&
                                                 slot.VariantTable == variants),
                    $"Pit Layout '{spec.TemplateId}' must place its obstacles as special candidate slots.");

                Assert(RoomObstacleLayout.TryCollectFootprints(prefab, out List<RoomObstacleFootprint> footprints,
                    out error), error);
                Rect[] pitBounds = footprints.Where(footprint => footprint.IsPit)
                    .Select(footprint => footprint.Bounds).ToArray();
                Assert(pitBounds.Length == spec.Pits.Length &&
                       spec.Pits.All(pit => pitBounds.Any(bounds => Approximately(bounds, pit.Bounds))) &&
                       footprints.Where(footprint => footprint.IsPit).All(footprint => !footprint.BlocksProjectiles &&
                           !footprint.IsDestructible),
                    $"Pit Layout '{spec.TemplateId}' pit footprints do not match the authored pits.");

                List<RoomObstacleFootprint> solidOnly = footprints.Where(footprint => !footprint.IsPit).ToList();
                List<Vector2> samples = CombatSamples(template.Profile.EncounterBounds);
                foreach (Vector2 spawn in template.SpawnPoints)
                    Assert(Mathf.Approximately(RoomObstacleLayout.SightCoverage(spawn, samples, footprints),
                            RoomObstacleLayout.SightCoverage(spawn, samples, solidOnly)),
                        $"Pit Layout '{spec.TemplateId}' pits must not block sightlines from {spawn}.");
            }
        }

        // Pits block reachability like obstacles but never sightlines; the Layout validator reports them as pits.
        private static void ValidateLayoutRules(FloorGenerator generator)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                candidate != null && candidate.TemplateId == Week22Terrain0Setup.BasicCentralPit.TemplateId);
            Assert(RoomObstacleLayout.TryCollectFootprints(template.RoomPrefabAsset,
                out List<RoomObstacleFootprint> valid, out string error), error);

            ExpectFailure(template, valid, "door-blocking pit", "pit 'pit-bad' blocks the Up",
                Pit("pit-bad", Rect.MinMaxRect(-1f, 2.5f, 1f, 3.5f)));
            ExpectFailure(template, valid, "SpawnPoint pit", "overlaps pit",
                Pit("pit-bad", Rect.MinMaxRect(-5f, 2f, -4f, 3f)));
            ExpectFailure(template, valid, "off-grid pit", "edges on the",
                Pit("pit-bad", Rect.MinMaxRect(-5.7f, -3f, -4.7f, -2f)));
            ExpectFailure(template, valid, "thin pit", "at least 1x1",
                Pit("pit-bad", Rect.MinMaxRect(-6f, -3.5f, -5.5f, -2f)));
            ExpectFailure(template, valid, "pit on an obstacle", "overlap",
                new RoomObstacleFootprint("obstacle-bad", new Rect(-0.5f, -0.5f, 1f, 1f), true));
            ExpectFailure(template, valid, "out-of-bounds pit", "leaves the movement bounds",
                Pit("pit-bad", Rect.MinMaxRect(-8f, -2f, -6.5f, -1f)));

            // A full-height pit just inside the right door passage cuts that door off from the others.
            List<RoomObstacleFootprint> sealing = valid
                .Append(Pit("pit-wall", Rect.MinMaxRect(4f, template.Profile.MovementBounds.yMin, 5f,
                    template.Profile.MovementBounds.yMax))).ToList();
            Assert(!RoomObstacleLayout.TryValidate(template.TemplateId, template.Profile.MovementBounds,
                       template.Profile.EncounterBounds, template.DoorSlots, Array.Empty<Vector2>(), sealing,
                       out error) && error.Contains("cut the", StringComparison.Ordinal),
                $"A sealing pit must cut a door off, but got: {error}");

            // The same rectangle as a solid obstacle hides part of the room; as a pit it hides nothing.
            Rect screen = Rect.MinMaxRect(-6f, -1f, -5f, 1f);
            List<Vector2> samples = CombatSamples(template.Profile.EncounterBounds);
            Vector2 sniper = new(-7f, 0f);
            float open = RoomObstacleLayout.SightCoverage(sniper, samples, Array.Empty<RoomObstacleFootprint>());
            Assert(Mathf.Approximately(RoomObstacleLayout.SightCoverage(sniper, samples, new[] { Pit("pit-screen", screen) }),
                    open) &&
                   RoomObstacleLayout.SightCoverage(sniper, samples,
                       new[] { new RoomObstacleFootprint("obstacle-screen", screen, false) }) < open,
                "Only solid obstacles may block Layout sightlines.");
        }

        private static void ValidatePhysics(GameObject pitPrefab)
        {
            GameObject pit = Object.Instantiate(pitPrefab, TestOrigin, Quaternion.identity);
            GameObject playerProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerProjectile.prefab");
            Assert(playerProjectilePrefab != null && playerProjectilePrefab.GetComponent<Projectile>() != null,
                "The player projectile Prefab is missing.");
            try
            {
                Week22Terrain0Setup.ApplySize(pit, new Vector2(2f, 2f));
                foreach (string layer in Week22Terrain0Setup.PitCollisionLayers)
                {
                    float reached = Push(CreateBody(layer, false), 0.3f);
                    Assert(reached < -1f - 0.3f + 0.05f, $"A {layer} body crossed into the pit (x={reached:F2}).");
                }

                float projectile = Push(Object.Instantiate(playerProjectilePrefab), 0f);
                Assert(projectile > 1.5f, $"A player projectile stopped at the pit (x={projectile:F2}).");
                float enemyShot = Push(CreateBody("Default", true), 0f);
                Assert(enemyShot > 1.5f, $"An enemy projectile body stopped at the pit (x={enemyShot:F2}).");

                GameObject shooter = new("Terrain-0 shooter");
                try
                {
                    EnemyProjectile shot = EnemyProjectile.Create(TestOrigin + Vector2.left * 3f, Vector2.right, shooter,
                        EnemyDamageTier.Light, 6f, 2f, null);
                    Assert(!shot.TryHit(pit.GetComponent<Collider2D>()) && shot.IsLaunched,
                        "An enemy projectile must fly over a pit instead of stopping.");
                    Object.DestroyImmediate(shot.gameObject);
                }
                finally
                {
                    Object.DestroyImmediate(shooter);
                }
            }
            finally
            {
                Object.DestroyImmediate(pit);
            }
        }

        // Drives a body at the 2x2 pit centered on the test origin and returns its final room-local x.
        private static float Push(GameObject body, float radius)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                body.transform.position = TestOrigin + Vector2.left * (2f + radius);
                Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
                rigidbody.gravityScale = 0f;
                Physics2D.SyncTransforms();
                for (int step = 0; step < 40; step++)
                {
                    rigidbody.linearVelocity = Vector2.right * 6f;
                    Physics2D.Simulate(0.02f);
                }

                return rigidbody.position.x - TestOrigin.x;
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(body);
            }
        }

        private static GameObject CreateBody(string layer, bool trigger)
        {
            GameObject body = new($"Terrain-0 {layer} body", typeof(Rigidbody2D), typeof(CircleCollider2D));
            body.layer = LayerMask.NameToLayer(layer);
            CircleCollider2D collider = body.GetComponent<CircleCollider2D>();
            collider.radius = trigger ? 0.15f : 0.3f;
            collider.isTrigger = trigger;
            Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
            rigidbody.bodyType = trigger ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            rigidbody.freezeRotation = true;
            return body;
        }

        private static void ValidateEnemyNavigation(GameObject pitPrefab)
        {
            GameObject pit = Object.Instantiate(pitPrefab, TestOrigin + new Vector2(0f, 100f), Quaternion.identity);
            try
            {
                Week22Terrain0Setup.ApplySize(pit, new Vector2(2f, 4f));
                Physics2D.SyncTransforms();
                Vector2 center = pit.transform.position;
                Vector2 from = center + Vector2.left * 3f;
                Vector2 goal = center + Vector2.right * 3f;
                const float bodyRadius = 0.4f;
                Assert(!EnemyObstacleNavigator.HasClearPath(from, goal, bodyRadius),
                    "A walking enemy must not treat a pit as a clear path.");
                Assert(EnemyObstacleNavigator.HasLineOfFire(from, goal),
                    "A ranged enemy must be able to fire across a pit.");

                List<Vector2> path = new();
                Assert(EnemyObstacleNavigator.TryFindPath(from, goal, bodyRadius, path) && path.Count > 1,
                    "An enemy must find a path around a pit.");
                Rect bounds = new(center - new Vector2(1f, 2f), new Vector2(2f, 4f));
                Assert(path.All(point => ChestPlacement.IsClear(point,
                        new[] { Pit("pit-nav", bounds) }, bodyRadius * 0.9f - 0.01f)),
                    "An enemy path must keep its body off the pit.");
                Vector2 step = new EnemyObstacleNavigator().GetMoveDirection(from, goal, bodyRadius, 0f);
                Assert(Mathf.Abs(step.y) > 0.1f, "A chasing enemy must turn around the pit instead of walking into it.");
            }
            finally
            {
                Object.DestroyImmediate(pit);
            }
        }

        // Chests and their contents never land on a pit, even when the preferred point is the pit itself.
        private static void ValidateChestPlacement(FloorGenerator generator)
        {
            foreach (Week22Terrain0Setup.LayoutSpec spec in Week22Terrain0Setup.Layouts)
            {
                RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                    candidate != null && candidate.TemplateId == spec.TemplateId);
                Assert(RoomObstacleLayout.TryCollectFootprints(template.RoomPrefabAsset,
                    out List<RoomObstacleFootprint> footprints, out string error), error);
                RoomObstacleFootprint[] pits = footprints.Where(footprint => footprint.IsPit).ToArray();
                foreach (Week22Terrain0Setup.PitSpec pit in spec.Pits)
                {
                    Assert(ChestPlacement.TryFindSafeLocalPosition(template, template.RoomPrefabAsset, pit.Center,
                        null, out Vector2 chest, out error), error);
                    // Same clearance the placement gives obstacles: an actor-sized circle around the chest center.
                    Assert(ChestPlacement.IsClear(chest, pits, RoomObstacleLayout.ActorRadius - 0.001f),
                        $"Layout '{spec.TemplateId}' placed a chest on a pit at {chest}.");
                    ChestPlacement.TryFindContentPositions(template.Profile.MovementBounds, footprints, chest, null,
                        ChestPlacement.MaximumContentCount, out List<Vector2> contents);
                    Assert(contents.All(point => ChestPlacement.IsClear(point, pits, ChestPlacement.MaximumPickupRadius)),
                        $"Layout '{spec.TemplateId}' dropped chest contents on a pit.");
                }
            }
        }

        private static void ValidateSelection(FloorGenerator generator)
        {
            HashSet<string> ids = new(Week22Terrain0Setup.Layouts.Select(spec => spec.TemplateId), StringComparer.Ordinal);
            Dictionary<string, HashSet<int>> floors = ids.ToDictionary(id => id, _ => new HashSet<int>(),
                StringComparer.Ordinal);
            for (int seed = 1; seed <= SelectionSeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph second, out error), error);
                string[] firstTemplates = first.Nodes.Select(node => node.Template?.TemplateId).ToArray();
                Assert(firstTemplates.SequenceEqual(second.Nodes.Select(node => node.Template?.TemplateId)),
                    $"Seed {seed} selected different Layouts on a repeat.");
                foreach (GeneratedRoomNode node in first.Nodes)
                {
                    if (node.Template == null || !ids.Contains(node.Template.TemplateId)) continue;
                    Assert(node.RoomType == RoomType.Normal && node.Role != GeneratedRoomRole.Start,
                        $"Seed {seed} placed a pit Layout in a {node.RoomType} {node.Role} room.");
                    floors[node.Template.TemplateId].Add(node.FloorNumber);
                }
            }

            foreach (KeyValuePair<string, HashSet<int>> entry in floors)
                Assert(entry.Value.SetEquals(new[] { 1, 2, 3 }),
                    $"Layout '{entry.Key}' must appear on floors 1~3 across {SelectionSeedCount} seeds " +
                    $"(saw {string.Join(", ", entry.Value.OrderBy(floor => floor))}).");
        }

        private static void ExpectFailure(RoomTemplateDefinition template, IEnumerable<RoomObstacleFootprint> valid,
            string label, string expected, RoomObstacleFootprint added)
        {
            List<RoomObstacleFootprint> footprints = valid.Append(added).ToList();
            bool passed = RoomObstacleLayout.TryValidate(template.TemplateId, template.Profile.MovementBounds,
                template.Profile.EncounterBounds, template.DoorSlots, template.SpawnPoints, footprints, out string error);
            Assert(!passed && error != null && error.Contains(expected, StringComparison.Ordinal),
                $"A {label} Layout must fail with '{expected}', but got: {(passed ? "pass" : error)}");
        }

        private static List<Vector2> CombatSamples(Rect encounter)
        {
            List<Vector2> samples = new();
            for (float x = encounter.xMin + 0.5f; x < encounter.xMax; x += 1f)
            for (float y = encounter.yMin + 0.5f; y < encounter.yMax; y += 1f)
                samples.Add(new Vector2(x, y));
            return samples;
        }

        private static RoomObstacleFootprint Pit(string id, Rect bounds) => new(id, bounds, false, true);

        private static bool Approximately(Rect first, Rect second) =>
            (first.min - second.min).sqrMagnitude < 0.0001f && (first.max - second.max).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
