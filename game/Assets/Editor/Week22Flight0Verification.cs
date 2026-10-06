using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Flight-0: 시스트의 가짜 날개 is an Artifact that only the golden chest exclusive pool holds. Acquiring it makes the
    // player fly until the Run ends: pits and low obstacles no longer stop the player, while walls, doors and chests
    // still do, and damage (enemy shots, bomb self-damage) is unchanged. D3 (2026-10-03): a flying player still drops
    // into a SecretPit automatically, a bomb cannot be placed over a pit and lands on the nearest walkable point when
    // placed over an obstacle, and enemies that cannot reach the player walk to the nearest reachable point and hold.
    public static class Week22Flight0Verification
    {
        private static readonly Vector2 TestOrigin = new(9000f, 9000f);
        private const float BodyRadius = 0.3f;
        private const float EnemyRadius = 0.4f;

        [MenuItem("Trickal Fan Game/Week 22/Setup and Verify Flight-0 Fake Wings")]
        public static void SetupAndVerifyBatch()
        {
            Week22Flight0Setup.Setup();
            string itemGuid = AssetDatabase.AssetPathToGUID(Week22Flight0Setup.FakeWingsPath);
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week22Flight0Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(itemGuid) &&
                   itemGuid == AssetDatabase.AssetPathToGUID(Week22Flight0Setup.FakeWingsPath) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Flight-0 setup changed an item, chest table or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the checks that share the movement layers, the enemy navigator, bombs and the chest table.
        [MenuItem("Trickal Fan Game/Week 22/Verify Flight-0 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week22Terrain0Verification.Verify();
            Week22Chest0Verification.Verify();
            Week22Chest1Verification.Verify();
            Week20Special2Verification.Verify();
            Week20Special3Verification.Verify();
            Week16Reward3Verification.Verify();
            Week13Hud3BVerification.Verify();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Week18Obstacle0Verification.Verify();
            Debug.Log("Flight-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Flight-0 Fake Wings")]
        public static void Verify()
        {
            // Opening a scene unloads unused assets, so the item is loaded again after each scene change.
            ValidateContract();
            ValidateAcquisitionPath();
            ValidateRunReset();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateClassification();
            ValidateMovement();
            ValidateInventoryFlight(LoadWings());
            ValidateDamage();
            ValidateBombPlacement();
            ValidateEnemyResponse();
            Debug.Log("Flight-0 verification passed: 시스트의 가짜 날개 (Epic Artifact, effect 35) is only in the golden " +
                      "chest exclusive pool, flight starts on pickup and lasts until the Run ends, the flying player " +
                      "passes over pits and low obstacles but not walls or chests, keeps taking enemy and bomb " +
                      "damage, still drops into SecretPits, cannot place bombs over pits, places them beside " +
                      "obstacles, and enemies that cannot reach the player hold at the nearest reachable point.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.Flight == 35 && (int)ItemEffectType.ProjectileLifetimePercent == 34 &&
                   (int)ItemEffectType.EscapeToFloorStartRoom == 31,
                "Flight-0 must add effect type 35 after the existing effects without renumbering them.");
            Assert(new ItemEffectEntry(ItemEffectType.Flight).TryValidate(out string error),
                $"The Flight effect must need no value fields. {error}");

            ItemDefinition wings = LoadWings();
            Assert(wings.ItemId == "artifact-sist-fake-wings" && wings.DisplayName == "시스트의 가짜 날개" &&
                   wings.Kind == ItemKind.Artifact && wings.Rarity == ItemRarity.Epic && wings.IsActive &&
                   wings.MaxStacks == 1 && wings.IsValid && !wings.IsSingleUse,
                "artifact-sist-fake-wings must be a valid active Epic Artifact named 시스트의 가짜 날개 with one stack.");
            Assert(wings.Effects.Count == 1 && wings.Effects[0].EffectType == ItemEffectType.Flight,
                "The fake wings must carry exactly the Flight effect.");
            Assert(ItemDefinition.IsItemIdValidForKind(wings.ItemId, ItemKind.Artifact) &&
                   !ItemDefinition.IsItemIdValidForKind(wings.ItemId, ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(wings.ItemId, ItemKind.Spell),
                "The fake wings ID must be an Artifact ID only (Contract-0 §2.1).");

            string description = ArtifactEffectDescription.Build(wings);
            Assert(!string.IsNullOrWhiteSpace(description) && !description.Contains("\n") &&
                   description.Contains("비행", StringComparison.Ordinal),
                "The fake wings description must be one line that names flight.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(wings.DisplayName + description),
                "The Frontend font is missing a fake wings glyph.");
        }

        private static ItemDefinition LoadWings()
        {
            ItemDefinition wings = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Flight0Setup.FakeWingsPath);
            Assert(wings != null, $"Run Flight-0 setup first: {Week22Flight0Setup.FakeWingsPath} is missing.");
            return wings;
        }

        private static void ValidateAcquisitionPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ItemDefinition wings = LoadWings();
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out string error), "The chest content table is missing or invalid.");
            Assert(table.GoldenExclusiveArtifacts.SequenceEqual(new[] { wings }) && table.IsGoldenExclusive(wings) &&
                   table.ArtifactPickupPrefab != null,
                "The golden exclusive pool must hold exactly the fake wings with the artifact pickup Prefab.");
            Assert(!table.Spells.Contains(wings), "The fake wings must not be a chest spell.");
            Assert(GoldenChestExclusivePool.IsExclusive(wings), "Reward pool setups must treat the fake wings as exclusive.");

            ChestContentTable copy = Object.Instantiate(table);
            try
            {
                ItemDefinition spell = table.Spells.First();
                copy.ConfigureGoldenExclusivePool(new[] { spell }, table.ArtifactPickupPrefab);
                Assert(!copy.TryValidate(out error) && error.Contains("golden exclusive", StringComparison.Ordinal),
                    "A single-use spell in the golden exclusive pool must fail validation.");
                copy.ConfigureGoldenExclusivePool(new[] { wings, wings }, table.ArtifactPickupPrefab);
                Assert(!copy.TryValidate(out _), "A duplicated golden exclusive artifact must fail validation.");
                copy.ConfigureGoldenExclusivePool(new[] { wings }, null);
                Assert(!copy.TryValidate(out _), "A golden exclusive pool without a pickup Prefab must fail validation.");
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }

            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            Assert(!assembler.SelectionRewardPool.Contains(wings) &&
                   assembler.SelectionRewardPool.All(item => !GoldenChestExclusivePool.IsExclusive(item)),
                "The selection reward pool (and the shop stock that shares it) must not offer the fake wings.");
            Assert(assembler.ChestContentTable == table, "The Game Scene must roll chests from the configured table.");

            // Every other pool (boss drops, item drop sources, shop catalogs, reward rooms) is serialized in a scene,
            // Prefab or asset, so the only file that may reference the wings is the chest content table.
            string guid = AssetDatabase.AssetPathToGUID(Week22Flight0Setup.FakeWingsPath);
            string[] referencing = Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".unity", StringComparison.Ordinal) ||
                               path.EndsWith(".prefab", StringComparison.Ordinal) ||
                               path.EndsWith(".asset", StringComparison.Ordinal))
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path != Week22Flight0Setup.FakeWingsPath &&
                               File.ReadAllText(path).Contains(guid, StringComparison.Ordinal))
                .ToArray();
            Assert(referencing.SequenceEqual(new[] { Week22Chest1Setup.ChestContentTablePath }),
                "Only the golden exclusive pool may reference the fake wings, but found: " +
                string.Join(", ", referencing));
        }

        // The Game Scene reloads for every Run, so a Run starts on foot as long as the scene player is serialized
        // without flight and PlayerFlight keeps no static state.
        private static void ValidateRunReset()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PlayerMovement player = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerMovement>(true)).Single();
            Assert(player.GetComponent<PlayerFlight>() == null && player.transform.Find(PlayerFlight.ShadowObjectName) == null,
                "The scene player must not be serialized with flight.");
            Assert((player.GetComponent<Rigidbody2D>().excludeLayers.value & (1 << RoomPit.Layer)) == 0,
                "The scene player must collide with pits until flight starts.");
            FieldInfo[] state = typeof(PlayerFlight)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsLiteral && field.FieldType != typeof(Sprite))
                .ToArray();
            Assert(state.Length == 0, "PlayerFlight must not keep static flight state across Runs: " +
                                      string.Join(", ", state.Select(field => field.Name)));
        }

        private static void ValidateClassification()
        {
            GameObject root = new("Flight-0 classification");
            try
            {
                Collider2D rock = Solid(root, "Rock", Vector2.zero, typeof(DestructibleObstacle));
                Collider2D pillar = Solid(root, "Pillar", Vector2.right * 3f, typeof(RoomStaticObstacle));
                Collider2D wall = Solid(root, "Wall", Vector2.right * 6f);
                Collider2D door = Solid(root, "Door", Vector2.right * 9f, typeof(DoorController));
                Collider2D chest = Object.Instantiate(LoadChestPrefab(), root.transform).GetComponent<Collider2D>();
                Collider2D secretWall = Solid(root, "Secret wall", Vector2.right * 12f, typeof(SecretPassageWall));
                Assert(RoomMovementClass.IsLowObstacle(rock) && RoomMovementClass.IsLowObstacle(pillar),
                    "Destructible and static room obstacles must be low obstacles.");
                Assert(!RoomMovementClass.IsLowObstacle(wall) && !RoomMovementClass.IsLowObstacle(door) &&
                       !RoomMovementClass.IsLowObstacle(chest) && !RoomMovementClass.IsLowObstacle(secretWall),
                    "Walls, doors, chests and secret passage walls must not be low obstacles.");
                rock.isTrigger = true;
                Assert(!RoomMovementClass.IsLowObstacle(rock), "A trigger is never a movement block.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateMovement()
        {
            GameObject root = new("Flight-0 movement");
            GameObject pitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            Assert(pitPrefab != null, "Run Terrain-0 setup first: the Room Pit Prefab is missing.");
            try
            {
                // Each lane holds one 2x2 blocker centered 2 units right of the lane start.
                GameObject pit = Object.Instantiate(pitPrefab, Lane(0) + Vector2.right * 2f, Quaternion.identity,
                    root.transform);
                Week22Terrain0Setup.ApplySize(pit, new Vector2(2f, 2f));
                GameObject chest = Object.Instantiate(LoadChestPrefab(), Lane(4) + Vector2.right * 2f,
                    Quaternion.identity, root.transform);
                chest.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                Collider2D[] blockers =
                {
                    pit.GetComponent<BoxCollider2D>(),
                    Blocker(root, Lane(1), typeof(DestructibleObstacle)),
                    Blocker(root, Lane(2), typeof(RoomStaticObstacle)),
                    Blocker(root, Lane(3)),
                    chest.GetComponent<Collider2D>(),
                };
                Physics2D.SyncTransforms();

                string[] labels = { "pit", "destructible obstacle", "static obstacle", "wall", "chest" };
                bool[] passable = { true, true, true, false, false };
                for (int lane = 0; lane < labels.Length; lane++)
                {
                    float near = blockers[lane].bounds.min.x - Lane(lane).x;
                    float far = blockers[lane].bounds.max.x - Lane(lane).x;
                    float walking = Drive(CreateBody(root, PlayerFeet.LayerName, false), Lane(lane));
                    Assert(walking < near - BodyRadius + 0.05f,
                        $"A walking player crossed into the {labels[lane]} (x={walking:F2}).");
                    float flying = Drive(CreateBody(root, PlayerFeet.LayerName, true), Lane(lane));
                    if (passable[lane])
                        Assert(flying > far + BodyRadius, $"A flying player must pass over the {labels[lane]} " +
                                                          $"(x={flying:F2}).");
                    else
                        Assert(flying < near - BodyRadius + 0.05f,
                            $"A flying player crossed into the {labels[lane]} (x={flying:F2}).");
                }

                float enemy = Drive(CreateBody(root, "Enemy", false), Lane(0));
                Assert(enemy < blockers[0].bounds.min.x - Lane(0).x - BodyRadius + 0.05f,
                    "Enemies must keep walking around pits while the player flies.");

                // A flying player starting inside a pit or obstacle (for example after a room rebuild) is not pushed
                // out, and the shadow and draw order show the flight.
                GameObject hovering = CreateBody(root, PlayerFeet.LayerName, true);
                hovering.transform.position = Lane(1) + Vector2.right * 2f;
                PlayerFlight flight = hovering.GetComponent<PlayerFlight>();
                SpriteRenderer sprite = hovering.GetComponent<SpriteRenderer>();
                Assert(flight.IsFlying && sprite.sortingOrder == PlayerFlight.FlyingSortingOrderBoost &&
                       flight.Shadow != null && flight.Shadow.sortingOrder == sprite.sortingOrder - 1 &&
                       flight.Shadow.sortingOrder > 0,
                    "Flight must draw the player above obstacles with a shadow underneath.");
                Assert(!flight.TryStartFlying() && sprite.sortingOrder == PlayerFlight.FlyingSortingOrderBoost &&
                       hovering.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                    "Starting flight twice must not stack the draw order or shadows.");
                Physics2D.SyncTransforms();
                Vector2 before = hovering.GetComponent<Rigidbody2D>().position;
                Simulate(hovering, Vector2.zero, 10);
                Assert((hovering.GetComponent<Rigidbody2D>().position - before).sqrMagnitude < 0.0001f,
                    "A flying player over an obstacle must not be pushed out of it.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateInventoryFlight(ItemDefinition wings)
        {
            GameObject progressObject = new("Flight-0 progress", typeof(RunProgress));
            GameObject player = new("Flight-0 player", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(Health), typeof(PlayerSP), typeof(PlayerStats), typeof(PlayerInventory));
            try
            {
                player.layer = LayerMask.NameToLayer("Player");
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                SetField(inventory, "runProgress", progressObject.GetComponent<RunProgress>());
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
                InvokeLifecycle(inventory, "Awake");
                Assert(player.GetComponent<PlayerFlight>() == null, "A player must not fly before the wings.");
                Assert(inventory.TryAcquire(wings), "The player must acquire the fake wings.");
                PlayerFlight flight = player.GetComponent<PlayerFlight>();
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                Assert(flight != null && flight.IsFlying && (body.excludeLayers.value & (1 << RoomPit.Layer)) != 0,
                    "Acquiring the fake wings must start flight over pits.");
                Assert((body.excludeLayers.value & RoomMovementClass.EnvironmentMask) == 0,
                    "Flight must not turn the whole Environment layer off.");
                Assert(!inventory.TryAcquire(wings) && inventory.GetStackCount(wings.ItemId) == 1 && flight.IsFlying,
                    "A second fake wings must be refused at one stack while flight continues.");
                Assert(inventory.AcquiredItems.Count(item => item.ItemId == wings.ItemId) == 1,
                    "The fake wings must be recorded once for the Run result.");
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(progressObject);
            }
        }

        // Flight is not invulnerability: an enemy shot and the player's own bomb still hurt a flying player.
        private static void ValidateDamage()
        {
            GameObject root = new("Flight-0 damage");
            GameObject shooter = new("Flight-0 shooter");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                GameObject shotTarget = CreatePlayer(root, Lane(0), out Health shotHealth);
                float before = shotHealth.CurrentHealth;
                EnemyProjectile shot = EnemyProjectile.Create(Lane(0) + Vector2.left * 2f, Vector2.right, shooter,
                    EnemyDamageTier.Light, 6f, 2f, null);
                Assert(shot.TryHit(shotTarget.GetComponent<CircleCollider2D>()) && shotHealth.CurrentHealth < before,
                    "An enemy projectile must still damage a flying player.");
                if (shot != null) Object.DestroyImmediate(shot.gameObject);

                GameObject bomber = CreatePlayer(root, Lane(1), out Health bomberHealth);
                RunProgress progress = root.AddComponent<RunProgress>();
                PlayerBombController controller = bomber.AddComponent<PlayerBombController>();
                controller.Configure(progress, LoadBombPrefab());
                Physics2D.SyncTransforms();
                Assert(progress.TryAddResource(RunResourceType.Bomb, 1) == 1 && controller.TryPlaceBomb(10f),
                    "A flying player over open floor must place a bomb.");
                PlacedBomb bomb = controller.PendingBomb;
                Assert(((Vector2)bomb.transform.position - Lane(1)).sqrMagnitude < 0.0001f,
                    "A bomb over open floor stays at the player position.");
                before = bomberHealth.CurrentHealth;
                Assert(bomb.Tick(10f + bomb.FuseDuration) &&
                       Mathf.Approximately(bomberHealth.CurrentHealth, before - bomb.SelfDamage),
                    "A bomb must still hurt the flying player who placed it.");
                Object.DestroyImmediate(bomb.gameObject);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(shooter);
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateBombPlacement()
        {
            GameObject root = new("Flight-0 bombs");
            GameObject pitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                Vector2 pitCenter = Lane(0) + Vector2.right * 2f;
                GameObject pit = Object.Instantiate(pitPrefab, pitCenter, Quaternion.identity, root.transform);
                Week22Terrain0Setup.ApplySize(pit, new Vector2(2f, 2f));
                Vector2 rockCenter = Lane(1) + Vector2.right * 2f;
                Collider2D rock = Solid(root, "Rock", rockCenter, typeof(DestructibleObstacle));
                Vector2 pillarCenter = Lane(2) + Vector2.right * 2f;
                Collider2D pillar = Solid(root, "Pillar", pillarCenter, typeof(RoomStaticObstacle));
                ((BoxCollider2D)pillar).size = new Vector2(2f, 2f);
                // A pit right of the rock: the nearest walkable point must avoid it too.
                GameObject sidePit = Object.Instantiate(pitPrefab, rockCenter + Vector2.right * 1f, Quaternion.identity,
                    root.transform);
                Week22Terrain0Setup.ApplySize(sidePit, new Vector2(1f, 1f));
                Physics2D.SyncTransforms();

                Assert(!PlayerBombController.TryResolvePlacement(pitCenter, out _),
                    "A bomb must not be placed over a pit.");
                Assert(PlayerBombController.TryResolvePlacement(Lane(0), out Vector2 open) && open == Lane(0),
                    "A bomb over open floor must stay at the player position.");
                foreach ((Vector2 center, Collider2D obstacle, string label) in new[]
                         {
                             (rockCenter, rock, "destructible obstacle"), (pillarCenter, pillar, "static obstacle"),
                         })
                {
                    Assert(PlayerBombController.TryResolvePlacement(center, out Vector2 placed),
                        $"A bomb over a {label} must find a walkable point.");
                    Assert(!obstacle.OverlapPoint(placed) && !RoomMovementClass.IsOverPit(placed) &&
                           RoomMovementClass.IsWalkable(placed, PlayerBombController.BombPlacementClearance),
                        $"A bomb over a {label} must land on walkable floor, not at {placed - center}.");
                    float edge = obstacle.bounds.extents.x + PlayerBombController.BombPlacementClearance;
                    Assert((placed - center).magnitude <= edge + RoomMovementClass.NearestWalkableStep + 0.01f,
                        $"A bomb over a {label} must land at the nearest walkable point, not {placed - center}.");
                }

                // The full controller path: refused over a pit without spending, moved beside an obstacle.
                GameObject bomber = CreatePlayer(root, pitCenter, out _);
                RunProgress progress = root.AddComponent<RunProgress>();
                PlayerBombController controller = bomber.AddComponent<PlayerBombController>();
                controller.Configure(progress, LoadBombPrefab());
                Assert(progress.TryAddResource(RunResourceType.Bomb, 2) == 2, "The test wallet must take bombs.");
                Assert(!controller.TryPlaceBomb(10f) && controller.PendingBomb == null &&
                       progress.GetResourceCount(RunResourceType.Bomb) == 2,
                    "A flying player over a pit must not place or spend a bomb.");
                bomber.transform.position = rockCenter;
                Physics2D.SyncTransforms();
                Assert(controller.TryPlaceBomb(10f) && progress.GetResourceCount(RunResourceType.Bomb) == 1 &&
                       !rock.OverlapPoint(controller.PendingBomb.transform.position),
                    "A flying player over an obstacle must place one bomb beside it.");
                Object.DestroyImmediate(controller.PendingBomb.gameObject);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        private static void ValidateEnemyResponse()
        {
            GameObject root = new("Flight-0 enemies");
            GameObject pitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Terrain0Setup.PitPrefabPath);
            try
            {
                Vector2 pitCenter = Lane(0) + Vector2.right * 4f;
                GameObject pit = Object.Instantiate(pitPrefab, pitCenter, Quaternion.identity, root.transform);
                Week22Terrain0Setup.ApplySize(pit, new Vector2(4f, 2f));
                Vector2 rockCenter = Lane(2) + Vector2.right * 4f;
                Solid(root, "Rock", rockCenter, typeof(DestructibleObstacle));
                Physics2D.SyncTransforms();

                WalkToHold(Lane(0), pitCenter, new Rect(pitCenter - new Vector2(2f, 1f), new Vector2(4f, 2f)), "pit");
                WalkToHold(Lane(2), rockCenter, new Rect(rockCenter - new Vector2(0.5f, 0.5f), Vector2.one), "obstacle");

                Assert(EnemyObstacleNavigator.HasLineOfFire(Lane(0), pitCenter),
                    "A ranged enemy must keep firing at a player over a pit.");
                List<Vector2> path = new();
                Assert(!EnemyObstacleNavigator.TryFindPath(Lane(0), pitCenter, EnemyRadius, path) && path.Count == 0,
                    "TryFindPath must keep reporting an unreachable goal as no path.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Walks an enemy toward a player hovering over the blocked area: it must stop beside it, never step into it,
        // and keep holding instead of pushing.
        private static void WalkToHold(Vector2 start, Vector2 goal, Rect blocked, string label)
        {
            EnemyObstacleNavigator navigator = new();
            Vector2 position = start;
            int mask = EnemyObstacleNavigator.ObstacleMask;
            int held = 0;
            for (int step = 0; step < 2000 && held < 20; step++)
            {
                Vector2 direction = navigator.GetMoveDirection(position, goal, EnemyRadius, step * 0.02f);
                if (direction.sqrMagnitude < 0.0001f)
                {
                    held++;
                    continue;
                }

                held = 0;
                position += direction * 0.05f;
                Assert(Physics2D.OverlapCircle(position, EnemyRadius * 0.85f, mask) == null,
                    $"An enemy walked into the {label} under a flying player at {position - goal}.");
            }

            Assert(held >= 20 && !navigator.IsGoalReachable,
                $"An enemy must hold near a flying player over the {label} instead of pushing into it " +
                $"(held {held}, reachable {navigator.IsGoalReachable}, at {position - goal}).");
            float gap = Mathf.Max(Mathf.Abs(position.x - blocked.center.x) - blocked.width * 0.5f,
                Mathf.Abs(position.y - blocked.center.y) - blocked.height * 0.5f);
            Assert(gap < EnemyRadius + EnemyObstacleNavigator.CellSize + 0.05f,
                $"An enemy must come up to the {label} edge, but held {gap:F2} away.");
        }

        private static Vector2 Lane(int index) => TestOrigin + Vector2.up * (index * 6f);

        private static GameObject CreateBody(GameObject root, string layer, bool flying)
        {
            GameObject body = new($"Flight-0 {layer} body", typeof(Rigidbody2D), typeof(CircleCollider2D),
                typeof(SpriteRenderer));
            body.transform.SetParent(root.transform);
            body.layer = LayerMask.NameToLayer(layer);
            body.GetComponent<CircleCollider2D>().radius = BodyRadius;
            Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            if (flying)
            {
                PlayerFlight flight = body.AddComponent<PlayerFlight>();
                Assert(flight.TryStartFlying(), "The test body must start flying.");
            }

            return body;
        }

        // Drives a body right from the lane start toward the blocker 2 units ahead and returns its final local x.
        private static float Drive(GameObject body, Vector2 laneStart)
        {
            try
            {
                body.transform.position = laneStart;
                Simulate(body, Vector2.right * 6f, 60);
                return body.GetComponent<Rigidbody2D>().position.x - laneStart.x;
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        private static void Simulate(GameObject body, Vector2 velocity, int steps)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Rigidbody2D rigidbody = body.GetComponent<Rigidbody2D>();
                PlayerFlight flight = body.GetComponent<PlayerFlight>();
                Physics2D.SyncTransforms();
                for (int step = 0; step < steps; step++)
                {
                    // Edit mode does not run FixedUpdate, so the per-step obstacle scan is called here.
                    flight?.IgnoreNearbyLowObstacles();
                    rigidbody.linearVelocity = velocity;
                    Physics2D.Simulate(0.02f);
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
            }
        }

        private static GameObject CreatePlayer(GameObject root, Vector2 position, out Health health)
        {
            GameObject player = new("Flight-0 player", typeof(PlayerMovement), typeof(CircleCollider2D));
            player.transform.SetParent(root.transform);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            PlayerFeet.Ensure(player, out _);
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            health = player.GetComponent<Health>();
            InvokeLifecycle(health, "Awake");
            health.EnableHealthUnits();
            Assert(player.AddComponent<PlayerFlight>().TryStartFlying(), "The test player must start flying.");
            return player;
        }

        private static Collider2D Blocker(GameObject root, Vector2 laneStart, params Type[] components)
        {
            Collider2D blocker = Solid(root, "Blocker", laneStart + Vector2.right * 2f, components);
            ((BoxCollider2D)blocker).size = new Vector2(2f, 2f);
            return blocker;
        }

        private static Collider2D Solid(GameObject root, string label, Vector2 position, params Type[] components)
        {
            GameObject solid = new($"Flight-0 {label}", new[] { typeof(BoxCollider2D) }.Concat(components).ToArray());
            solid.transform.SetParent(root.transform);
            solid.transform.position = position;
            solid.layer = LayerMask.NameToLayer(RoomMovementClass.EnvironmentLayerName);
            return solid.GetComponent<BoxCollider2D>();
        }

        private static GameObject LoadChestPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Chest0Setup.ChestPrefabPath);
            Assert(prefab != null && prefab.GetComponent<TreasureChest>() != null,
                "Run Chest-0 setup first: the chest Prefab is missing.");
            return prefab;
        }

        private static PlacedBomb LoadBombPrefab()
        {
            PlacedBomb prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special2Setup.PlacedBombPrefabPath)
                ?.GetComponent<PlacedBomb>();
            Assert(prefab != null, "Run Special-2 setup first: the placed bomb Prefab is missing.");
            return prefab;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Field {fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
