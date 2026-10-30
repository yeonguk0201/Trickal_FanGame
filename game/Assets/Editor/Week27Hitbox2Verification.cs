using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrickalFanGame.Enemy;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week27Hitbox2Verification
    {
        private const float MoveSpeed = 3f;
        private const int SimulationSteps = 80;
        private const int WalkStepLimit = 1500;
        private const float Tolerance = 0.04f;
        private const float BodyRadius = 0.5f;
        // On the world 0.5 lattice like every room, far from authored scenes.
        private static readonly Vector2 Origin = new(6000f, 6000f);

        // Normal enemies that must walk on feet, by Prefab file name. 불효자손 is TestEnemy (Enemy-2).
        private static readonly string[] FeetPrefabs =
        {
            "TestEnemy", "SansamoEnemy", "RangedEnemy", "HighBloodSugarFairy", "QuickRangedFairy",
            "LivelyLowBloodSugarFairy", "ChargingEnemy", "NururingFairyWarrior", "CrayonArcherMinion",
            "CrayonAxeMinion", "CrayonMageMinion", "CrayonShieldMinion", "BuseureogiCrumbMinion",
            "SelfDestructFairy", "LobbingFairy", "AmbushFairy",
        };

        // Bosses, stationary boss props, fliers (쥬비, 대왕쥬비) and bodies no wider than the feet keep one circle.
        private static readonly string[] BodyOnlyPrefabs =
        {
            "CrayonHeroBoss", "SaemaeumVaultBoss", "TestBoss", "JyubiEnemy", "JyubiKing", "NururingSplit",
            "BuseureogiCreamObstacle", "BuseureogiDoughObstacle",
        };

        // Walk probes: 불효자손, 산사모 and a ranged enemy, the three named in the bug report.
        private static readonly string[] WalkingPrefabs = { "TestEnemy", "SansamoEnemy", "RangedEnemy" };

        [MenuItem("Trickal Fan Game/Week 27/Setup and Verify Hitbox-2 Enemy Feet Collider")]
        public static void SetupAndVerifyBatch()
        {
            Week27Hitbox2Setup.Setup();
            // A rerun must not add a second feet child or move the layer.
            int feetLayer = EnemyFeet.Layer;
            Week27Hitbox2Setup.Setup();
            Assert(feetLayer >= 0 && feetLayer == EnemyFeet.Layer, "Hitbox-2 setup rerun moved the feet layer.");
            Verify();
        }

        // The navigator is shared by every walking enemy, so the enemy, obstacle, pit and flight checks rerun too.
        [MenuItem("Trickal Fan Game/Week 27/Verify Hitbox-2 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Week18Obstacle0Verification.VerifyWithEnemyRegressionsBatch();
            Action[] emptySceneChecks =
            {
                Verify, Week18Obstacle1Verification.Verify, Week18Obstacle2Verification.Verify,
                Week22Terrain0Verification.Verify, Week22Flight0Verification.Verify,
                Week22MeleeLungeVerification.Verify, Week23Enemy6Verification.Verify,
                Week25Enemy8Verification.Verify, Week23Hitbox0Verification.Verify,
            };
            foreach (Action check in emptySceneChecks)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                check();
            }

            Debug.Log("Hitbox-2 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 27/Verify Hitbox-2 Enemy Feet Collider")]
        public static void Verify()
        {
            ValidateLayers();
            ValidatePrefabs();
            ValidateRoomsOnLattice();
            ValidateTerrainUsesFeet();
            ValidateBossBodyStillMeetsTerrain();
            ValidateLanePlanning();
            ValidateLaneWalks();
            Debug.Log($"Hitbox-2 verification passed: the {EnemyFeet.LayerName} layer collides with terrain only, " +
                      $"{FeetPrefabs.Length} normal enemy Prefabs have one feet circle of radius {EnemyFeet.Radius} " +
                      $"under an unchanged body of radius {BodyRadius} that ignores terrain, bosses keep one circle, " +
                      "walls stop the feet, and enemies plan and walk through one-cell lanes between obstacles and " +
                      "beside a wall from any start offset.");
        }

        private static void ValidateLayers()
        {
            int feet = EnemyFeet.Layer;
            Assert(feet >= 0, $"Run Hitbox-2 setup to create the {EnemyFeet.LayerName} layer.");
            HashSet<int> terrain = new(EnemyFeet.CollisionLayers.Select(LayerMask.NameToLayer));
            Assert(!terrain.Contains(-1), "Hitbox-2 needs the Environment and Pit layers.");
            for (int layer = 0; layer < 32; layer++)
            {
                Assert(Physics2D.GetIgnoreLayerCollision(feet, layer) != terrain.Contains(layer),
                    $"The enemy feet layer must collide with terrain only, but layer " +
                    $"'{LayerMask.LayerToName(layer)}' differs.");
            }

            // Bosses still meet terrain with the body on the Enemy layer.
            int enemy = LayerMask.NameToLayer("Enemy");
            foreach (int layer in terrain)
            {
                Assert(!Physics2D.GetIgnoreLayerCollision(enemy, layer),
                    $"The Enemy layer must still collide with '{LayerMask.LayerToName(layer)}' for bosses.");
            }

            Assert(Week22Terrain0Setup.PitCollisionLayers.Contains(EnemyFeet.LayerName),
                "Terrain-0 must keep the enemy feet among the pit collision layers on a rerun.");
        }

        private static void ValidatePrefabs()
        {
            string[] paths = Week27Hitbox2Setup.EnemyPrefabPaths();
            Dictionary<string, string> byName = paths.ToDictionary(Path.GetFileNameWithoutExtension, path => path);
            foreach (string name in FeetPrefabs.Concat(BodyOnlyPrefabs))
                Assert(byName.ContainsKey(name), $"Hitbox-2 expects the enemy Prefab '{name}'.");

            int terrainMask = EnemyFeet.TerrainMask;
            foreach (string path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                bool expectsFeet = FeetPrefabs.Contains(name);
                Assert(expectsFeet || BodyOnlyPrefabs.Contains(name),
                    $"{path} is a new enemy Prefab. Add it to the Hitbox-2 feet or body-only list.");

                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Assert(EnemyFeet.Needs(contents) == expectsFeet,
                        $"{name} must {(expectsFeet ? "" : "not ")}be a normal enemy that walks on feet.");
                    Assert(EnemyFeet.TryGetBody(contents, out CircleCollider2D body), $"{name} has no body circle.");
                    EnemyFeet[] feet = contents.GetComponentsInChildren<EnemyFeet>(true);
                    if (!expectsFeet)
                    {
                        Assert(feet.All(found => !found.gameObject.activeSelf),
                            $"{name} keeps one circle and must have no active feet.");
                        Assert((body.excludeLayers.value & terrainMask) == 0,
                            $"{name} must still meet terrain with its body.");
                        EnemyObstacleNavigator.TerrainFootprint bodyOnly =
                            EnemyObstacleNavigator.ResolveFootprint(contents);
                        Assert(bodyOnly.Offset == Vector2.zero && bodyOnly.Collider == body,
                            $"{name} must plan with its body circle.");
                        continue;
                    }

                    Assert(feet.Length == 1 && feet[0].transform.parent == contents.transform &&
                           feet[0].gameObject.activeSelf,
                        $"{name} must have exactly one active feet child, found {feet.Length}.");
                    Assert(!EnemyFeet.Ensure(contents, out _),
                        $"{name} feet differ from the Hitbox-2 layer, place or size. Run the setup.");
                    Assert(feet[0].GetComponents<Collider2D>().Length == 1, $"{name} feet must hold one collider.");
                    Assert(Mathf.Approximately(body.radius, BodyRadius) && body.offset == Vector2.zero,
                        $"{name} body (hurtbox and contact) must stay a circle of radius {BodyRadius}.");
                    Assert((body.excludeLayers.value & terrainMask) == terrainMask,
                        $"{name} body must leave terrain to the feet.");

                    float bodyWorld = EnemyFeet.WorldRadiusOf(body);
                    Vector2 root = contents.transform.position;
                    Assert(Mathf.Abs(feet[0].WorldRadius - EnemyFeet.Radius) < 0.001f,
                        $"{name} feet must be {EnemyFeet.Radius} wide in world units, found {feet[0].WorldRadius:F3}.");
                    float feetBottom = feet[0].WorldCenter.y - feet[0].WorldRadius;
                    Assert(Mathf.Abs(feetBottom - (root.y - bodyWorld)) < 0.001f &&
                           Mathf.Abs(feet[0].WorldCenter.x - root.x) < 0.001f,
                        $"{name} feet must sit centered at the bottom of the body.");

                    EnemyObstacleNavigator.TerrainFootprint footprint =
                        EnemyObstacleNavigator.ResolveFootprint(contents);
                    Assert(footprint.Collider == feet[0].Collider &&
                           Mathf.Abs(footprint.Radius - EnemyFeet.Radius) < 0.001f &&
                           Vector2.Distance(footprint.Offset, feet[0].WorldCenter - root) < 0.001f,
                        $"{name} must plan its path with the feet circle.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        // The navigator puts its cell centers on the world 0.5 lattice, which is only the middle of a one-cell lane if
        // rooms sit on whole units and every obstacle and pit is centered on that lattice.
        private static void ValidateRoomsOnLattice()
        {
            Assert(OnLattice(RoomLayout.RoomSpacingX) && OnLattice(RoomLayout.RoomSpacingY),
                "Room spacing must stay on the 0.5 lattice.");
            int checkedColliders = 0;
            foreach (string path in FairyVillageArtworkSetup.PrefabPaths())
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Rect floor = FairyVillageArtworkSetup.WalkableFloor(FairyVillageArtworkSetup.RoomSize(
                        contents.GetComponent<RoomPrefab>().Node.ContentRoot.transform));
                    Assert(OnLattice(floor.xMin) && OnLattice(floor.xMax) && OnLattice(floor.yMin) &&
                           OnLattice(floor.yMax), $"{path} walkable floor {floor} must sit on the 0.5 lattice.");
                    foreach (Collider2D collider in contents.GetComponentsInChildren<Collider2D>(true))
                    {
                        if (collider.isTrigger || (collider.GetComponent<DestructibleObstacle>() == null &&
                                                   collider.GetComponent<RoomStaticObstacle>() == null &&
                                                   collider.GetComponent<RoomPit>() == null)) continue;
                        Vector2 center = collider.transform.TransformPoint(collider.offset);
                        Assert(OnLattice(center.x) && OnLattice(center.y),
                            $"{path} '{collider.name}' is centered at {center}, off the 0.5 lattice.");
                        checkedColliders++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            Assert(checkedColliders > 0, "The room lattice check found no obstacles or pits.");
        }

        private static bool OnLattice(float value) => Mathf.Abs(value * 2f - Mathf.Round(value * 2f)) < 0.01f;

        // Walls stop the feet, so the body overlaps a wall above and keeps its old distance from a wall below.
        private static void ValidateTerrainUsesFeet()
        {
            float feetTop = EnemyFeet.Radius - BodyRadius + EnemyFeet.Radius;
            foreach (string layerName in EnemyFeet.CollisionLayers)
            {
                Run(layerName + " above", root =>
                {
                    Box(root, layerName, Origin + new Vector2(0f, 2f), new Vector2(6f, 1f));
                    Rigidbody2D body = Enemy(root, "TestEnemy", Origin);
                    Drive(body, Vector2.up);
                    float expected = Origin.y + 1.5f - feetTop;
                    Assert(Mathf.Abs(body.position.y - expected) < Tolerance,
                        $"An enemy walking up into {layerName} must stop at the feet (y {expected:F2}), but stopped " +
                        $"at {body.position.y:F2}.");
                });
                Run(layerName + " below", root =>
                {
                    Box(root, layerName, Origin + new Vector2(0f, -2f), new Vector2(6f, 1f));
                    Rigidbody2D body = Enemy(root, "TestEnemy", Origin);
                    Drive(body, Vector2.down);
                    float expected = Origin.y - 1.5f + BodyRadius;
                    Assert(Mathf.Abs(body.position.y - expected) < Tolerance,
                        $"An enemy walking down into {layerName} must stop where it did before (y {expected:F2}), " +
                        $"but stopped at {body.position.y:F2}.");
                });
                Run(layerName + " beside", root =>
                {
                    Box(root, layerName, Origin + new Vector2(2f, 0f), new Vector2(1f, 6f));
                    Rigidbody2D body = Enemy(root, "TestEnemy", Origin);
                    Drive(body, Vector2.right);
                    float expected = Origin.x + 1.5f - EnemyFeet.Radius;
                    Assert(Mathf.Abs(body.position.x - expected) < Tolerance,
                        $"An enemy walking sideways into {layerName} must stop at the feet (x {expected:F2}), but " +
                        $"stopped at {body.position.x:F2}.");
                });
            }
        }

        private static void ValidateBossBodyStillMeetsTerrain()
        {
            Run("boss body", root =>
            {
                Box(root, "Environment", Origin + new Vector2(0f, 2f), new Vector2(6f, 1f));
                GameObject boss = new("Hitbox-2 Boss", typeof(Rigidbody2D), typeof(CircleCollider2D));
                boss.transform.SetParent(root.transform);
                boss.transform.position = Origin;
                boss.layer = LayerMask.NameToLayer("Enemy");
                boss.GetComponent<CircleCollider2D>().radius = BodyRadius;
                Rigidbody2D body = boss.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                Drive(body, Vector2.up);
                float expected = Origin.y + 1.5f - BodyRadius;
                Assert(Mathf.Abs(body.position.y - expected) < Tolerance,
                    $"An enemy body without feet must still stop at walls (y {expected:F2}), but stopped at " +
                    $"{body.position.y:F2}.");
            });
        }

        // A column of 1x1 obstacles with one open cell, and a column ending one cell below a wall. Before Hitbox-2 the
        // grid followed the enemy, so the lane's middle cell moved with it and was usually tested too close to an edge.
        private static void ValidateLanePlanning()
        {
            foreach (Lane lane in Lanes())
            {
                Run(lane.Label, root =>
                {
                    lane.Build(root);
                    Physics2D.SyncTransforms();
                    for (int sample = 0; sample < 16; sample++)
                    {
                        Vector2 shift = new((sample % 4) * 0.13f, (sample / 4) * 0.11f);
                        List<Vector2> path = new();
                        Assert(EnemyObstacleNavigator.TryFindPath(lane.Start + shift, lane.Goal + shift * 0.5f,
                                EnemyFeet.Radius, path),
                            $"Feet must find the {lane.Label} from start offset {shift}.");
                        Assert(path.Any(point => Vector2.Distance(point, lane.Middle) < 0.001f),
                            $"The {lane.Label} path from start offset {shift} must pass the middle of the lane.");
                    }

                    // The lane is the only way through, so plugging it seals the goal off.
                    Box(root, "Environment", lane.Middle, Vector2.one);
                    Physics2D.SyncTransforms();
                    Assert(!EnemyObstacleNavigator.TryFindPath(lane.Start, lane.Goal, EnemyFeet.Radius,
                            new List<Vector2>()),
                        $"The {lane.Label} probe must have no way around the lane.");
                });
            }
        }

        // The Prefab bodies walk the planned path under physics and come out the other side.
        private static void ValidateLaneWalks()
        {
            foreach (Lane lane in Lanes())
            foreach (string prefab in WalkingPrefabs)
            foreach (float shift in new[] { 0f, 0.21f, -0.37f })
            {
                Run($"{prefab} {lane.Label} {shift:F2}", root =>
                {
                    lane.Build(root);
                    Vector2 start = lane.Start + new Vector2(shift, shift * 0.5f);
                    Rigidbody2D body = Enemy(root, prefab, start);
                    EnemyObstacleNavigator.TerrainFootprint footprint =
                        EnemyObstacleNavigator.ResolveFootprint(body.gameObject);
                    EnemyObstacleNavigator navigator = new();
                    Physics2D.SyncTransforms();
                    for (int step = 0; step < WalkStepLimit; step++)
                    {
                        if (Vector2.Distance(body.position, lane.Goal) <= 0.2f) return;
                        body.linearVelocity = navigator.GetMoveDirection(body.position, lane.Goal, footprint,
                            step * Time.fixedDeltaTime) * MoveSpeed;
                        Physics2D.Simulate(Time.fixedDeltaTime);
                    }

                    throw new InvalidOperationException(
                        $"{prefab} did not walk through the {lane.Label} from {start}; it stopped at {body.position}.");
                });
            }
        }

        private sealed class Lane
        {
            public string Label;
            public Vector2 Start;
            public Vector2 Goal;
            public Vector2 Middle;
            public Action<GameObject> Build;
        }

        private static IEnumerable<Lane> Lanes()
        {
            // Obstacle column at x+2 from y-8 to y+8 with the cell at y open.
            yield return new Lane
            {
                Label = "lane between obstacles",
                Start = Origin + new Vector2(-1f, 2f),
                Goal = Origin + new Vector2(5f, -2f),
                Middle = Origin + new Vector2(2f, 0f),
                Build = root =>
                {
                    for (int y = -8; y <= 8; y++)
                        if (y != 0) Box(root, "Environment", Origin + new Vector2(2f, y), Vector2.one);
                },
            };

            // A wall from y+0.5 up and an obstacle column at x+2 from y-1 down to y-10: the one cell between the
            // column's top and the wall is the way across.
            yield return new Lane
            {
                Label = "lane beside a wall",
                Start = Origin + new Vector2(-1f, -3f),
                Goal = Origin + new Vector2(5f, -3f),
                Middle = Origin + new Vector2(2f, 0f),
                Build = root =>
                {
                    Box(root, "Environment", Origin + new Vector2(2f, 5.5f), new Vector2(40f, 10f));
                    for (int y = -10; y <= -1; y++)
                        Box(root, "Environment", Origin + new Vector2(2f, y), Vector2.one);
                },
            };
        }

        private static Rigidbody2D Enemy(GameObject root, string prefabName, Vector2 position)
        {
            string path = Week27Hitbox2Setup.EnemyPrefabPaths()
                .Single(found => Path.GetFileNameWithoutExtension(found) == prefabName);
            GameObject enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            enemy.transform.position = position;
            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            Assert(body != null, $"{prefabName} needs a Rigidbody2D.");
            body.position = position;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.bodyType = RigidbodyType2D.Dynamic;
            // Behaviour scripts do not run under Physics2D.Simulate in edit mode; only the colliders take part.
            foreach (Collider2D collider in enemy.GetComponentsInChildren<Collider2D>(true))
                if (collider.isTrigger) collider.enabled = false;
            return body;
        }

        private static void Box(GameObject root, string layerName, Vector2 position, Vector2 size)
        {
            GameObject box = new($"Hitbox-2 {layerName}", typeof(BoxCollider2D));
            box.transform.SetParent(root.transform);
            box.transform.position = position;
            box.layer = LayerMask.NameToLayer(layerName);
            box.GetComponent<BoxCollider2D>().size = size;
        }

        private static void Drive(Rigidbody2D body, Vector2 direction)
        {
            Physics2D.SyncTransforms();
            for (int index = 0; index < SimulationSteps; index++)
            {
                body.linearVelocity = direction * MoveSpeed;
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
        }

        private static void Run(string label, Action<GameObject> probe)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject root = new($"Hitbox-2 {label}");
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                probe(root);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                Object.DestroyImmediate(root);
                Physics2D.SyncTransforms();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
