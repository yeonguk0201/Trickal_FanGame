using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
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
    // Chest-0: three chest kinds with Run room state. A normal chest opens on touch, a golden chest opens on touch by
    // spending one key (nothing changes without one) and a diamond chest opens only from a bomb explosion. Repeated
    // contacts and overlapping explosions open a chest once, revisits keep its state, leaving the floor discards every
    // unopened chest, and the placement search keeps chests reachable and off obstacles and door passages.
    // No contents are generated: Chest-0 records the open state only.
    public static class Week22Chest0Verification
    {
        private static readonly Vector2 TestOrigin = new(5000f, 5000f);

        public static void SetupAndVerifyBatch()
        {
            Week22Chest0Setup.Setup();
            string guid = AssetDatabase.AssetPathToGUID(Week22Chest0Setup.ChestPrefabPath);
            Week22Chest0Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) &&
                   guid == AssetDatabase.AssetPathToGUID(Week22Chest0Setup.ChestPrefabPath),
                "Chest-0 setup changed the chest Prefab GUID.");
            Verify();
        }

        // Also re-runs the features that share the explosion path (obstacles, secret walls) and the room entry and
        // floor loading that now discard chests.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // Room-8 walks doorways with the real transition cooldown, so it runs before verifiers that leave one set.
            Week14Room8Verification.Verify();
            Week18Obstacle1Verification.Verify();
            Week20Special3Verification.Verify();
            Week21Spell2Verification.Verify();
            Debug.Log("Chest-0 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Chest-0 Chests")]
        public static void Verify()
        {
            ValidateContract();
            ValidateRoomState();
            // Opening the scene unloads unused assets, so the Prefab is loaded after it.
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
            TreasureChest prefab = ValidatePrefab();
            ValidateOpening(prefab, assembler.Graph.Player);
            ValidatePush(prefab);
            ValidatePlacement(assembler.Generator);
            ValidateFloorRun(prefab, assembler);
            Debug.Log("Chest-0 verification passed: a normal chest opens on touch, a golden chest opens on touch by " +
                      "spending one key and does nothing without one, a diamond chest opens only from a bomb; " +
                      "repeated contacts and overlapping explosions open each chest once, revisits keep the open " +
                      "state, leaving the floor discards unopened chests, and chests are placed reachable and clear " +
                      "of obstacles, door passages and each other.");
        }

        private static void ValidateContract()
        {
            Assert((int)ChestKind.Normal == 0 && (int)ChestKind.Golden == 1 && (int)ChestKind.Diamond == 2 &&
                   Enum.GetValues(typeof(ChestKind)).Length == 3,
                "ChestKind must be Normal 0, Golden 1, Diamond 2.");
            Assert(TreasureChest.OpenMethodFor(ChestKind.Normal) == ChestOpenMethod.Touch &&
                   TreasureChest.OpenMethodFor(ChestKind.Golden) == ChestOpenMethod.TouchWithKey &&
                   TreasureChest.OpenMethodFor(ChestKind.Diamond) == ChestOpenMethod.Bomb,
                "Chest open methods must follow the §3.1 decision (touch / touch with a key / bomb).");
            Assert((int)RunResourceType.Gold == 0 && (int)RunResourceType.Key == 1 && (int)RunResourceType.Bomb == 2,
                "Chest-0 must not renumber Run resources.");
        }

        private static TreasureChest ValidatePrefab()
        {
            TreasureChest prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week22Chest0Setup.ChestPrefabPath)
                ?.GetComponent<TreasureChest>();
            Assert(prefab != null, $"Run Chest-0 setup first: {Week22Chest0Setup.ChestPrefabPath} is missing.");
            Assert(prefab.TryValidate(out string error), error);
            BoxCollider2D collider = prefab.GetComponent<BoxCollider2D>();
            Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
            Assert(collider != null && !collider.isTrigger && prefab.GetComponents<Collider2D>().Length == 1 &&
                   body != null && body.bodyType == RigidbodyType2D.Kinematic && body.freezeRotation &&
                   body.useFullKinematicContacts,
                "The chest Prefab needs one solid BoxCollider2D on a kinematic Rigidbody2D with full contacts.");
            Assert(prefab.Visual != null && prefab.Visual.gameObject == prefab.gameObject &&
                   prefab.Visual.sprite != null && prefab.Visual.color == TreasureChest.NormalColor,
                "The chest Prefab must show its own normal-chest sprite.");
            float worldSize = prefab.Visual.sprite.bounds.size.x * prefab.transform.localScale.x;
            Assert(Mathf.Abs(worldSize - ChestPlacement.ChestWorldSize) < 0.001f,
                "The chest Prefab must match the placement chest size.");

            int chestLayer = LayerMask.NameToLayer(TreasureChest.LayerName);
            Assert(chestLayer == LayerMask.NameToLayer("Environment") && prefab.gameObject.layer == chestLayer,
                "Chests must use the Environment layer so they block like obstacles.");
            foreach (string other in new[] { PlayerFeet.LayerName, "Enemy", "PlayerProjectile", "Environment", "Pickup" })
                Assert(!Physics2D.GetIgnoreLayerCollision(chestLayer, LayerMask.NameToLayer(other)),
                    $"Chests must collide with the {other} layer.");
            return prefab;
        }

        private static void ValidateRoomState()
        {
            RoomRunState state = new("floor-1-room-02");
            int changes = 0;
            state.Changed += () => changes++;

            ChestRunState normal = state.RegisterChest("chest-01", ChestKind.Normal);
            Assert(normal.IsClosed && !normal.IsOpened && !normal.IsDiscarded && changes == 1 &&
                   ReferenceEquals(state.RegisterChest("chest-01", ChestKind.Normal), normal) && changes == 1 &&
                   ReferenceEquals(state.GetChest("chest-01"), normal) && state.Chests.Count == 1,
                "Registering a chest twice must return the same record without a second change.");
            Throws<InvalidOperationException>(() => state.RegisterChest("chest-01", ChestKind.Golden),
                "A chest ID must keep its kind for the Run.");
            Throws<ArgumentException>(() => state.RegisterChest("Chest 01", ChestKind.Normal),
                "A chest ID must be a stable lowercase ID.");
            Throws<ArgumentOutOfRangeException>(() => state.RegisterChest("chest-09", (ChestKind)7),
                "An undefined chest kind must be rejected.");
            Assert(state.Chests.Count == 1, "Rejected chests must not be recorded.");

            Assert(state.TryOpenChest("chest-01") && normal.IsOpened && !normal.IsClosed && changes == 2 &&
                   !state.TryOpenChest("chest-01") && changes == 2 && !state.TryOpenChest("chest-missing"),
                "A chest must open exactly once and a missing chest never opens.");

            ChestRunState golden = state.RegisterChest("chest-02", ChestKind.Golden);
            ChestRunState diamond = state.RegisterChest("chest-03", ChestKind.Diamond);
            int before = changes;
            Assert(state.DiscardClosedChests() == 2 && golden.IsDiscarded && diamond.IsDiscarded &&
                   normal.IsOpened && !normal.IsDiscarded && changes == before + 1,
                "Discarding must remove only unopened chests in one change.");
            Assert(!state.TryOpenChest("chest-02") && !golden.IsOpened && state.DiscardClosedChests() == 0 &&
                   changes == before + 1,
                "A discarded chest never opens and discarding twice changes nothing.");
        }

        private static void ValidateOpening(TreasureChest prefab, PlayerMovement player)
        {
            Assert(player != null, "The Game Scene needs its player for chest contact.");
            List<Object> created = new();
            try
            {
                RunProgress progress = Track(created, new GameObject("Chest-0 progress")).AddComponent<RunProgress>();
                RoomRunState state = new("floor-1-room-03");
                GameObject bombOwner = Track(created, CreateBombOwner());

                TreasureChest normal = Spawn(created, prefab, "chest-01", ChestKind.Normal, Vector2.zero);
                TreasureChest golden = Spawn(created, prefab, "chest-02", ChestKind.Golden, new Vector2(10f, 0f));
                TreasureChest diamond = Spawn(created, prefab, "chest-03", ChestKind.Diamond, new Vector2(20f, 0f));
                Assert(!normal.TryOpenByTouch(player) && !normal.IsBound,
                    "An unbound chest must not open.");
                Assert(normal.Bind(state, progress) && golden.Bind(state, progress) && diamond.Bind(state, progress) &&
                       state.Chests.Count == 3 && state.Chests.All(chest => chest.IsClosed),
                    "Binding must record each closed chest in the room state.");
                Throws<InvalidOperationException>(() => normal.Configure("chest-04", ChestKind.Normal),
                    "A bound chest must not change its ID or kind.");
                Dictionary<TreasureChest, List<ChestOpenMethod>> opened = new();
                foreach (TreasureChest chest in new[] { normal, golden, diamond })
                {
                    List<ChestOpenMethod> methods = new();
                    opened.Add(chest, methods);
                    chest.Opened += (source, method) =>
                    {
                        Assert(source == chest, "The Opened event must name its chest.");
                        methods.Add(method);
                    };
                }

                // Normal: touch opens it once; only the player counts.
                Assert(!normal.TryOpenByTouch(null) && normal.IsClosed, "A non-player contact must not open a chest.");
                Assert(normal.TryOpenByTouch(player) && normal.IsOpened && state.GetChest("chest-01").IsOpened &&
                       !normal.TryOpenByTouch(player) && !normal.TryOpenByBomb() &&
                       opened[normal].SequenceEqual(new[] { ChestOpenMethod.Touch }),
                    "A normal chest must open once on touch.");
                Assert(normal.Visual.color == TreasureChest.NormalColor * TreasureChest.OpenedTint,
                    "An opened chest must look opened.");

                // Golden: no key changes nothing; with keys one key is spent once.
                Assert(!golden.TryOpenByTouch(player) && golden.IsClosed &&
                       progress.GetResourceCount(RunResourceType.Key) == 0,
                    "A golden chest touched without a key must stay closed.");
                Assert(progress.TryAddResource(RunResourceType.Key, 2) == 2 && !golden.TryOpenByBomb() &&
                       golden.IsClosed && progress.GetResourceCount(RunResourceType.Key) == 2,
                    "A bomb must not open a golden chest or spend a key.");
                Assert(golden.TryOpenByTouch(player) && golden.IsOpened &&
                       progress.GetResourceCount(RunResourceType.Key) == 1 && !golden.TryOpenByTouch(player) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1 &&
                       opened[golden].SequenceEqual(new[] { ChestOpenMethod.TouchWithKey }),
                    "A golden chest must spend exactly one key and open once.");

                // Diamond: touch never opens it; overlapping explosions open it once.
                Assert(progress.TryAddResource(RunResourceType.Bomb, 3) == 3 && !diamond.TryOpenByTouch(player) &&
                       diamond.IsClosed && progress.GetResourceCount(RunResourceType.Key) == 1 &&
                       progress.GetResourceCount(RunResourceType.Bomb) == 3,
                    "Touching a diamond chest must not open it or spend resources.");
                float outOfReach = PlacedBomb.DefaultExplosionRadius + 1f;
                ExplodeAt(bombOwner, diamond.transform.position + new Vector3(outOfReach, 0f));
                Assert(diamond.IsClosed, "An explosion out of reach must not open a diamond chest.");
                ExplodeAt(bombOwner, diamond.transform.position + new Vector3(1f, 0f));
                ExplodeAt(bombOwner, diamond.transform.position + new Vector3(-1f, 0f));
                Assert(diamond.IsOpened && opened[diamond].SequenceEqual(new[] { ChestOpenMethod.Bomb }),
                    "Overlapping explosions must open a diamond chest once.");
                Assert(progress.GetResourceCount(RunResourceType.Key) == 1 &&
                       progress.GetResourceCount(RunResourceType.Bomb) == 3,
                    "Opening a diamond chest must not spend keys or wallet bombs.");

                // An explosion over normal and golden chests leaves them closed.
                TreasureChest bombedNormal = Spawn(created, prefab, "chest-05", ChestKind.Normal, new Vector2(30f, 0f));
                TreasureChest bombedGolden = Spawn(created, prefab, "chest-06", ChestKind.Golden, new Vector2(31f, 0f));
                bombedNormal.Bind(state, progress);
                bombedGolden.Bind(state, progress);
                ExplodeAt(bombOwner, bombedNormal.transform.position);
                Assert(bombedNormal.IsClosed && bombedGolden.IsClosed &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "A bomb must open only diamond chests.");

                // A rebuilt room keeps the stored state.
                TreasureChest rebuilt = Spawn(created, prefab, "chest-01", ChestKind.Normal, new Vector2(40f, 0f));
                Assert(!rebuilt.Bind(state, progress) && rebuilt.IsOpened && rebuilt.gameObject.activeSelf &&
                       !rebuilt.TryOpenByTouch(player) && state.Chests.Count == 5 &&
                       rebuilt.Visual.color == TreasureChest.NormalColor * TreasureChest.OpenedTint,
                    "A rebuilt opened chest must stay opened and never open again.");
                TreasureChest wrongKind = Spawn(created, prefab, "chest-01", ChestKind.Golden, new Vector2(41f, 0f));
                Throws<InvalidOperationException>(() => wrongKind.Bind(state, progress),
                    "A rebuilt chest must keep the kind recorded for its ID.");

                // Leaving the floor discards the unopened ones.
                Assert(state.DiscardClosedChests() == 2, "The two bombed chests were the only unopened ones.");
                TreasureChest discarded = Spawn(created, prefab, "chest-05", ChestKind.Normal, new Vector2(50f, 0f));
                Assert(!discarded.Bind(state, progress) && !discarded.gameObject.activeSelf &&
                       !discarded.TryOpenByTouch(player) && !state.GetChest("chest-05").IsOpened,
                    "A discarded chest must not come back on a rebuild.");

                // A stopped Run opens nothing and spends nothing.
                RoomRunState stoppedState = new("floor-1-room-04");
                TreasureChest late = Spawn(created, prefab, "chest-01", ChestKind.Normal, new Vector2(60f, 0f));
                TreasureChest lateGolden = Spawn(created, prefab, "chest-02", ChestKind.Golden, new Vector2(70f, 0f));
                late.Bind(stoppedState, progress);
                lateGolden.Bind(stoppedState, progress);
                progress.StopProgression();
                Assert(!late.TryOpenByTouch(player) && !lateGolden.TryOpenByTouch(player) && late.IsClosed &&
                       lateGolden.IsClosed && progress.GetResourceCount(RunResourceType.Key) == 1,
                    "Chests must not open after the Run has ended.");
            }
            finally
            {
                for (int index = created.Count - 1; index >= 0; index--)
                    if (created[index] != null) Object.DestroyImmediate(created[index]);
            }
        }

        // A player walking into a chest for 1 s at 5 units/s moves it about PushSpeed (1.2), far less than the same
        // walk moves a heart; walking away or past it does nothing; a wall in front stops it short.
        private static void ValidatePush(TreasureChest prefab)
        {
            GameObject heartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HealthPickup.prefab");
            Assert(heartPrefab != null, "The heart pickup Prefab is missing.");
            float heartMoved = HeartPushDistance(heartPrefab, TestOrigin + new Vector2(0f, 220f));
            float chestMoved = ChestPushDistance(prefab, TestOrigin + new Vector2(0f, 200f), Vector2.right, false);
            Debug.Log($"Chest-0 push over 1 s at 5 units/s: chest {chestMoved:F2}, heart {heartMoved:F2}.");
            Assert(Mathf.Abs(chestMoved - TreasureChest.PushSpeed) < 0.1f,
                $"Walking into a chest for 1 s must push it about {TreasureChest.PushSpeed} (moved {chestMoved:F2}).");
            Assert(chestMoved < heartMoved * 0.5f,
                $"A chest must move much less than a heart (chest {chestMoved:F2}, heart {heartMoved:F2}).");
            Assert(ChestPushDistance(prefab, TestOrigin + new Vector2(0f, 240f), Vector2.left, false) < 0.001f &&
                   ChestPushDistance(prefab, TestOrigin + new Vector2(0f, 260f), Vector2.up, false) < 0.001f,
                "Walking away from or past a chest must not push it.");
            float blocked = ChestPushDistance(prefab, TestOrigin + new Vector2(0f, 280f), Vector2.right, true);
            Assert(blocked > 0.1f && blocked < 0.5f,
                $"A wall 0.5 ahead must stop the chest before it touches (moved {blocked:F2}).");
        }

        private static float ChestPushDistance(TreasureChest prefab, Vector2 start, Vector2 intent, bool wallAhead)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject chestObject = null;
            GameObject wall = null;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                chestObject = Object.Instantiate(prefab.gameObject, start, Quaternion.identity);
                TreasureChest chest = chestObject.GetComponent<TreasureChest>();
                if (wallAhead)
                {
                    wall = new GameObject("Chest-0 wall", typeof(BoxCollider2D));
                    wall.layer = LayerMask.NameToLayer("Environment");
                    wall.transform.position = start + Vector2.right * (ChestPlacement.ChestHalfSize + 0.5f + 0.5f);
                    wall.GetComponent<BoxCollider2D>().size = Vector2.one;
                }

                Physics2D.SyncTransforms();
                for (int step = 0; step < 50; step++)
                {
                    // The pusher stands against the chest's left face.
                    Vector2 pusher = (Vector2)chest.transform.position +
                                     Vector2.left * (ChestPlacement.ChestHalfSize + 0.3f);
                    chest.RegisterPush(pusher, intent);
                    chest.StepPush(0.02f);
                    Physics2D.Simulate(0.02f);
                }

                return Vector2.Distance(chestObject.GetComponent<Rigidbody2D>().position, start);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                if (chestObject != null) Object.DestroyImmediate(chestObject);
                if (wall != null) Object.DestroyImmediate(wall);
            }
        }

        private static float HeartPushDistance(GameObject heartPrefab, Vector2 start)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject target = null;
            GameObject pusher = null;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                target = Object.Instantiate(heartPrefab, start, Quaternion.identity);
                pusher = new GameObject("Chest-0 pusher", typeof(Rigidbody2D), typeof(BoxCollider2D));
                pusher.layer = LayerMask.NameToLayer("Player");
                pusher.transform.position = start + Vector2.left * 1.2f;
                pusher.GetComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
                Rigidbody2D body = pusher.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.mass = 1f;
                Physics2D.SyncTransforms();
                for (int step = 0; step < 50; step++)
                {
                    body.linearVelocity = Vector2.right * 5f;
                    Physics2D.Simulate(0.02f);
                }

                return Vector2.Distance(target.GetComponent<Rigidbody2D>().position, start);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                if (target != null) Object.DestroyImmediate(target);
                if (pusher != null) Object.DestroyImmediate(pusher);
            }
        }

        private static void ValidatePlacement(FloorGenerator generator)
        {
            Assert(generator != null && generator.RoomTemplates.Count > 0, "The Game Scene needs its room templates.");
            Assert(!ChestPlacement.TryFindSafeLocalPosition(null, null, Vector2.zero, null, out _, out _),
                "Placement without a template must fail.");
            bool exercisedBlockedCenter = false;
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                GameObject room = template.RoomPrefabAsset;
                Assert(RoomObstacleLayout.TryCollectFootprints(room, out List<RoomObstacleFootprint> footprints,
                    out string error), error);
                Assert(ChestPlacement.TryFindSafeLocalPosition(template, room, Vector2.zero, null,
                        out Vector2 first, out error), $"Template '{template.TemplateId}': {error}");
                Assert(ChestPlacement.TryFindSafeLocalPosition(template, room, Vector2.zero, null,
                        out Vector2 again, out _) && again == first,
                    $"Template '{template.TemplateId}' must place its chest at the same point every time.");
                AssertSafe(template, footprints, first);
                Assert(ChestPlacement.TryFindContentPositions(template.Profile.MovementBounds, footprints, first, null,
                        ChestPlacement.MaximumContentCount, out List<Vector2> contentPoints) &&
                       contentPoints.All(point => ChestPlacement.IsClear(point, footprints,
                                                      ChestPlacement.MaximumPickupRadius) &&
                                                  Vector2.Distance(point, first) >=
                                                  ChestPlacement.InnerContentRadius - 0.001f) &&
                       contentPoints.Distinct().Count() == contentPoints.Count,
                    $"Template '{template.TemplateId}' has no room for a full chest's contents around its chest.");
                if (!IsClearOfObstacles(Vector2.zero, footprints))
                {
                    exercisedBlockedCenter = true;
                    Assert(first != Vector2.zero, $"Template '{template.TemplateId}' placed a chest in its pillar.");
                }

                List<Vector2> placed = new() { first };
                // The clear chest plus one more (a later duplicate) fits in every room.
                for (int count = 1; count < 2; count++)
                {
                    Assert(ChestPlacement.TryFindSafeLocalPosition(template, room, Vector2.zero, placed,
                        out Vector2 next, out error), $"Template '{template.TemplateId}' chest {count + 1}: {error}");
                    AssertSafe(template, footprints, next);
                    Assert(placed.All(other => Vector2.Distance(other, next) >= ChestPlacement.MinimumChestSpacing),
                        $"Template '{template.TemplateId}' stacked chests on each other.");
                    placed.Add(next);
                }

                // A new chest never appears on top of the player.
                Assert(ChestPlacement.TryFindSafeLocalPosition(template, room, Vector2.zero, null, first,
                        out Vector2 avoided, out error) &&
                       Vector2.Distance(avoided, first) >= ChestPlacement.AvoidClearance - 0.001f,
                    $"Template '{template.TemplateId}' placed a chest on the player. {error}");
                AssertSafe(template, footprints, avoided);

                // A preferred point inside a door passage moves out of it.
                RoomTemplateDoor door = template.DoorSlots[0];
                Assert(ChestPlacement.TryFindSafeLocalPosition(template, room, door.SafeEntryPosition, null,
                        out Vector2 nearDoor, out error) && nearDoor != door.SafeEntryPosition,
                    $"Template '{template.TemplateId}' placed a chest on a door's safe entry. {error}");
                AssertSafe(template, footprints, nearDoor);
            }

            Assert(exercisedBlockedCenter, "No template blocks its center, so obstacle avoidance was not exercised.");
        }

        private static void ValidateFloorRun(TreasureChest prefab, RoomGraphAssembler assembler)
        {
            RunProgress progress = assembler.Progress;
            List<Object> created = new();
            try
            {
                progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(FindSeed(assembler.Generator),
                    out string error), error);
                GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
                GeneratedRoomNode[] rooms = floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate)
                    .Take(2).ToArray();
                Assert(rooms.Length == 2, "Floor 1 needs two combat rooms.");

                // A chest placed in a real built room overlaps no wall, seal or obstacle collider.
                RoomPrefab instance = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                    .Single(room => room.Node.RoomId == rooms[0].RoomId);
                Assert(ChestPlacement.TryFindSafeLocalPosition(rooms[0].Template, instance.gameObject, Vector2.zero,
                    null, out Vector2 local, out error), error);
                TreasureChest placed = Track(created, Object.Instantiate(prefab.gameObject,
                    instance.transform.TransformPoint(local), Quaternion.identity,
                    instance.Node.ContentRoot.transform)).GetComponent<TreasureChest>();
                placed.Configure("chest-01", ChestKind.Normal);
                Physics2D.SyncTransforms();
                // The chest is on the Environment layer itself, so only other colliders count.
                Assert(Physics2D.OverlapBoxAll(placed.transform.position, Vector2.one * ChestPlacement.ChestWorldSize,
                        0f, DestructibleObstacle.ObstacleMask).All(hit => hit.gameObject == placed.gameObject),
                    $"The chest placed in {rooms[0].RoomId} overlaps an Environment collider.");
                Assert(RoomObstacleLayout.TryCollectFootprints(instance.gameObject,
                    out List<RoomObstacleFootprint> roomFootprints, out error), error);
                Assert(ChestPlacement.TryFindContentPositions(rooms[0].Template.Profile.MovementBounds, roomFootprints,
                    local, null, ChestPlacement.MaximumContentCount, out List<Vector2> drops), "No room for contents.");
                foreach (Vector2 drop in drops)
                    Assert(Physics2D.OverlapCircle(instance.transform.TransformPoint(drop),
                            ChestPlacement.MaximumPickupRadius, DestructibleObstacle.ObstacleMask) == null,
                        $"A content point of the chest in {rooms[0].RoomId} reaches into an Environment collider.");

                RoomRunState firstState = progress.GetRoomState(rooms[0].RoomId);
                RoomRunState secondState = progress.GetRoomState(rooms[1].RoomId);
                Assert(placed.Bind(firstState, progress), "The placed chest must start closed.");
                ChestRunState placedChest = placed.Record;
                ChestRunState openedChest = firstState.RegisterChest("chest-02", ChestKind.Normal);
                Assert(firstState.TryOpenChest("chest-02"), "The second chest must open.");
                ChestRunState closedChest = secondState.RegisterChest("chest-01", ChestKind.Golden);

                GeneratedFloor nextFloor = assembler.GeneratedGraph.FindFloor(2);
                Assert(nextFloor != null, "The verification Run needs a second floor.");
                RoomRunState nextFloorState = progress.GetRoomState(nextFloor.Nodes[0].RoomId);
                ChestRunState nextFloorChest = nextFloorState.RegisterChest("chest-01", ChestKind.Diamond);

                progress.RecordRoomEntry(1, rooms[1].RoomNumber);
                Assert(placedChest.IsClosed && closedChest.IsClosed,
                    "Moving between rooms of the same floor must keep unopened chests.");

                Assert(assembler.TryLoadFloor(2, assembler.Graph.Player, out error), error);
                Assert(progress.CurrentFloor == 2 && placedChest.IsDiscarded && closedChest.IsDiscarded &&
                       openedChest.IsOpened && !openedChest.IsDiscarded && nextFloorChest.IsClosed,
                    "Leaving floor 1 must discard its unopened chests only.");
                Assert(placed == null || !placed.gameObject.activeInHierarchy,
                    "The previous floor's chest objects must leave with the floor.");
                Assert(!firstState.TryOpenChest("chest-01") && !secondState.TryOpenChest("chest-01"),
                    "A discarded chest must never open later in the Run.");
            }
            finally
            {
                for (int index = created.Count - 1; index >= 0; index--)
                    if (created[index] != null) Object.DestroyImmediate(created[index]);
                progress.ResetProgress();
            }
        }

        private static int FindSeed(FloorGenerator generator)
        {
            for (int seed = 1; seed <= 4096; seed++)
            {
                if (generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _) &&
                    graph.FindFloor(2) != null &&
                    graph.FindFloor(1).Nodes.Count(node => node.Role == GeneratedRoomRole.Intermediate) >= 2)
                    return seed;
            }

            throw new InvalidOperationException("No seed produced two floor-1 combat rooms and a second floor.");
        }

        private static void AssertSafe(RoomTemplateDefinition template, IReadOnlyList<RoomObstacleFootprint> footprints,
            Vector2 position)
        {
            Rect inner = RoomObstacleLayout.Expand(template.Profile.MovementBounds, -RoomObstacleLayout.ActorRadius);
            Assert(inner.Contains(position), $"Template '{template.TemplateId}' chest {position} is outside the room.");
            Assert(IsClearOfObstacles(position, footprints),
                $"Template '{template.TemplateId}' chest {position} touches an obstacle.");
            foreach (RoomTemplateDoor door in template.DoorSlots)
            {
                Rect passage = RoomObstacleLayout.Expand(RoomTemplateGeometry.RequiredDoorPassageBounds(door),
                    RoomObstacleLayout.ActorRadius + ChestPlacement.ChestHalfSize);
                Assert(!passage.Contains(position),
                    $"Template '{template.TemplateId}' chest {position} sits in the {door.Direction} door passage.");
            }

            Func<Vector2, bool> reachable = RoomObstacleLayout.CreateReachability(template.Profile.MovementBounds,
                template.DoorSlots[0].SafeEntryPosition, footprints);
            foreach (RoomTemplateDoor door in template.DoorSlots)
                Assert(reachable(door.SafeEntryPosition) && reachable(position),
                    $"Template '{template.TemplateId}' chest {position} cannot be reached from its doors.");
        }

        private static bool IsClearOfObstacles(Vector2 position, IReadOnlyList<RoomObstacleFootprint> footprints)
        {
            foreach (RoomObstacleFootprint footprint in footprints)
            {
                Rect rect = footprint.Bounds;
                float dx = Mathf.Max(rect.xMin - position.x, 0f, position.x - rect.xMax);
                float dy = Mathf.Max(rect.yMin - position.y, 0f, position.y - rect.yMax);
                if (dx * dx + dy * dy < RoomObstacleLayout.ActorRadius * RoomObstacleLayout.ActorRadius - 0.0001f)
                    return false;
            }

            return true;
        }

        private static TreasureChest Spawn(List<Object> created, TreasureChest prefab, string chestId, ChestKind kind,
            Vector2 offset)
        {
            GameObject instance = Track(created, Object.Instantiate(prefab.gameObject));
            instance.transform.position = TestOrigin + offset;
            TreasureChest chest = instance.GetComponent<TreasureChest>();
            chest.Configure(chestId, kind);
            Assert(chest.Visual != null && chest.Visual.color == TreasureChest.ColorFor(kind),
                $"A {kind} chest must show its kind color.");
            Physics2D.SyncTransforms();
            return chest;
        }

        private static GameObject Track(List<Object> created, GameObject instance)
        {
            created.Add(instance);
            return instance;
        }

        private static GameObject CreateBombOwner()
        {
            GameObject owner = new("Chest-0 bomb owner", typeof(Health));
            owner.transform.position = new Vector3(-10000f, -10000f, 0f);
            typeof(Health).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(owner.GetComponent<Health>(), null);
            return owner;
        }

        private static void ExplodeAt(GameObject owner, Vector2 position)
        {
            GameObject bombObject = new("Chest-0 bomb", typeof(PlacedBomb));
            try
            {
                bombObject.transform.position = position;
                PlacedBomb bomb = bombObject.GetComponent<PlacedBomb>();
                bomb.ConfigureValues(PlacedBomb.DefaultFuseDuration, PlacedBomb.DefaultExplosionRadius,
                    PlacedBomb.DefaultEnemyDamage, PlacedBomb.DefaultSelfDamage, LayerMask.GetMask("Enemy"), null);
                bomb.Configure(owner, owner.GetComponent<Health>(), 0f);
                Physics2D.SyncTransforms();
                Assert(bomb.ApplyExplosion(), "The verification bomb must explode once.");
            }
            finally
            {
                Object.DestroyImmediate(bombObject);
            }
        }

        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
