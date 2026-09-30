using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Special3Verification
    {
        private const int SeedCount = 512;
        private static readonly RoomDoorDirection[] Directions =
            { RoomDoorDirection.Left, RoomDoorDirection.Right, RoomDoorDirection.Up, RoomDoorDirection.Down };

        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Special-3 Secret Rooms")]
        public static void SetupAndVerifyBatch()
        {
            Week20Special3Setup.Setup();
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week20Special3Setup.SecretPitPrefabPath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week20Special3Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(prefabGuid) &&
                   prefabGuid == AssetDatabase.AssetPathToGUID(Week20Special3Setup.SecretPitPrefabPath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Special-3 setup changed the secret pit Prefab or Game Scene GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Special-3 Secret Rooms")]
        public static void Verify()
        {
            SecretPit pitPrefab = ValidatePrefabAndTables();
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(generator != null && assembler != null && assembler.Progress != null,
                "Special-3 requires the configured Game Scene generator, assembler, and RunProgress.");
            Assert(generator.RoomContentVersion >= Week20Special3Setup.RoomContentVersion &&
                   generator.EncounterContentVersion >= Week20Special3Setup.EncounterContentVersion,
                "Special-3 must raise the Room and Encounter content versions for hidden-passage exits.");

            int runtimeSeed = ValidateGeneration(generator);
            ValidatePitDropRules(pitPrefab);
            ValidateBombWallAndReload(assembler, runtimeSeed);
            ValidatePitEntry(assembler, runtimeSeed, pitPrefab);
            Debug.Log("Special-3 verification passed: floors reproduce a 50% secret room (at most one) in the empty " +
                      "cell touching the most rooms but never the start or boss room, hidden passages never shorten " +
                      "the required route, a bomb opens only the wall it reaches, entering by door or pit opens " +
                      "every passage, the secret reward room is safe, pit drops re-weight away without a secret " +
                      "room, pits need a cleared room and a centered player, and walls, pits, and room state " +
                      "survive revisits and floor rebuilds.");
        }

        private static SecretPit ValidatePrefabAndTables()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week20Special3Setup.SecretPitPrefabPath);
            SecretPit pit = prefab != null ? prefab.GetComponent<SecretPit>() : null;
            Collider2D[] colliders = prefab != null ? prefab.GetComponents<Collider2D>() : Array.Empty<Collider2D>();
            Assert(pit != null && prefab.layer == LayerMask.NameToLayer("Pickup") && colliders.Length == 1 &&
                   colliders[0] is CircleCollider2D && colliders[0].isTrigger &&
                   prefab.GetComponents<Rigidbody2D>().Length == 0 &&
                   prefab.GetComponents<RunResourcePickup>().Length == 0,
                "SecretPit Prefab must be one Pickup-layer trigger without physics body or pickup behaviour.");

            foreach ((string path, (string dropId, int weight)[] weights) in new[]
                     {
                         (Week18Obstacle1Setup.DropTablePath, Week18Obstacle1Setup.DropWeights),
                         (Week20Obstacle4Setup.MarieDropTablePath, Week20Obstacle4Setup.MarieDropWeights),
                     })
            {
                ResourceDropTable table = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(path);
                Assert(table != null && table.TryValidate(out string error) &&
                       table.Entries.Select(entry => (entry.DropId, entry.Weight)).SequenceEqual(weights),
                    $"{path} must keep its drop weights.");
                ResourceDropEntry pitEntry = table.Entries.Single(entry => entry.DropId == "pit");
                Assert(pitEntry.Prefab == prefab && DestructibleObstacle.IsSecretPit(pitEntry),
                    $"{path} pit candidate must spawn the SecretPit Prefab.");
            }

            return pit;
        }

        private static int ValidateGeneration(FloorGenerator generator)
        {
            int floors = 0, secrets = 0, multiNeighbor = 0, runtimeSeed = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph first, out string error), error);
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph repeated, out error), error);
                foreach (GeneratedFloor floor in first.Floors)
                {
                    floors++;
                    GeneratedRoomNode[] secretRooms =
                        floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Secret).ToArray();
                    GeneratedFloor repeatedFloor = repeated.FindFloor(floor.FloorNumber);
                    Assert(secretRooms.Length <= 1 && Signature(floor) == Signature(repeatedFloor),
                        $"Seed {seed} floor {floor.FloorNumber} must reproduce at most one secret room.");
                    if (secretRooms.Length == 0) continue;

                    secrets++;
                    GeneratedRoomNode secret = secretRooms[0];
                    Dictionary<RoomGridPosition, GeneratedRoomNode> byPosition =
                        floor.Nodes.ToDictionary(node => node.GridPosition);
                    Assert(secret.RoomNumber == floor.Nodes.Count && secret.RoomType == RoomType.Reward &&
                           !secret.RequiresKey && secret.Encounter == null &&
                           secret.Template != null && secret.Template.SupportsRoomType(RoomType.Reward),
                        $"Seed {seed} floor {floor.FloorNumber} secret room must be the last, unlocked reward room.");

                    int expectedNeighbors = NeighborCount(byPosition, secret.GridPosition, out bool forbidden);
                    Assert(!forbidden && secret.DirectionalConnections.Count == expectedNeighbors &&
                           secret.DirectionalConnections.All(connection => connection.IsSecret),
                        $"Seed {seed} floor {floor.FloorNumber} secret room must link every touching room " +
                        "through hidden passages and never touch the start or boss room.");
                    Assert(expectedNeighbors == BestCandidateNeighborCount(floor, byPosition),
                        $"Seed {seed} floor {floor.FloorNumber} secret room must use a cell touching the most rooms.");
                    foreach (GeneratedRoomNode node in floor.Nodes)
                    foreach (GeneratedRoomConnection connection in node.DirectionalConnections)
                        Assert(connection.IsSecret == (node == secret || connection.DestinationRoomId == secret.RoomId),
                            $"Seed {seed} room {node.RoomId} marks a regular connection as secret or vice versa.");
                    Assert(RegularDistance(floor, floor.BossRoomId) >= generator.MinimumBossDistance,
                        $"Seed {seed} floor {floor.FloorNumber} boss distance must ignore hidden passages.");

                    multiNeighbor += expectedNeighbors >= 2 ? 1 : 0;
                    if (runtimeSeed == 0 && floor.FloorNumber == 1 && expectedNeighbors >= 2 &&
                        FindObstacleRoom(floor) != null &&
                        secret.ConnectedRoomIds.Any(id => floor.Nodes.Single(node => node.RoomId == id).Role ==
                                                          GeneratedRoomRole.Intermediate))
                        runtimeSeed = seed;
                }
            }

            float ratio = secrets / (float)floors;
            Assert(ratio >= 0.4f && ratio <= 0.6f,
                $"Secret rooms should appear on about 50% of floors; observed {ratio:P1} of {floors}.");
            Assert(multiNeighbor > 0 && runtimeSeed > 0,
                "Seeds must include a floor-1 secret room with several neighbors for runtime verification.");
            Debug.Log($"Special-3 generation: {secrets}/{floors} floors ({ratio:P1}) have a secret room, " +
                      $"{multiNeighbor} with two or more neighbors. Runtime seed {runtimeSeed}.");
            return runtimeSeed;
        }

        private static int NeighborCount(IReadOnlyDictionary<RoomGridPosition, GeneratedRoomNode> byPosition,
            RoomGridPosition cell, out bool forbidden)
        {
            int count = 0;
            forbidden = false;
            foreach (RoomDoorDirection direction in Directions)
            {
                if (!byPosition.TryGetValue(cell.Offset(direction), out GeneratedRoomNode neighbor) ||
                    neighbor.Role == GeneratedRoomRole.Secret) continue;
                count++;
                forbidden |= neighbor.Role is GeneratedRoomRole.Start or GeneratedRoomRole.Boss;
            }

            return count;
        }

        private static int BestCandidateNeighborCount(GeneratedFloor floor,
            IReadOnlyDictionary<RoomGridPosition, GeneratedRoomNode> byPosition)
        {
            int best = 0;
            foreach (GeneratedRoomNode node in floor.Nodes.Where(node => node.Role != GeneratedRoomRole.Secret))
            foreach (RoomDoorDirection direction in Directions)
            {
                RoomGridPosition cell = node.GridPosition.Offset(direction);
                if (byPosition.TryGetValue(cell, out GeneratedRoomNode occupant) &&
                    occupant.Role != GeneratedRoomRole.Secret) continue;
                int count = NeighborCount(byPosition, cell, out bool forbidden);
                if (!forbidden) best = Math.Max(best, count);
            }

            return best;
        }

        private static int RegularDistance(GeneratedFloor floor, string destination)
        {
            Dictionary<string, GeneratedRoomNode> byId = floor.Nodes.ToDictionary(node => node.RoomId,
                StringComparer.Ordinal);
            Dictionary<string, int> distance = new(StringComparer.Ordinal) { [floor.StartingRoomId] = 0 };
            Queue<string> queue = new();
            queue.Enqueue(floor.StartingRoomId);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                foreach (GeneratedRoomConnection connection in byId[current].DirectionalConnections)
                    if (!connection.IsSecret && distance.TryAdd(connection.DestinationRoomId, distance[current] + 1))
                        queue.Enqueue(connection.DestinationRoomId);
            }

            return distance.TryGetValue(destination, out int value) ? value : -1;
        }

        private static GeneratedRoomNode FindObstacleRoom(GeneratedFloor floor) =>
            floor.Nodes.FirstOrDefault(node => node.Role == GeneratedRoomRole.Intermediate &&
                                               node.Template?.RoomPrefabAsset != null &&
                                               node.Template.RoomPrefabAsset
                                                   .GetComponentsInChildren<DestructibleObstacle>(true).Length > 0);

        private static string Signature(GeneratedFloor floor) => string.Join("|", floor.Nodes.Select(node =>
            $"{node.RoomId}@{node.GridPosition}:{node.Role}:{node.ContentSeed}:{node.TemplateId}:{node.EncounterId}:" +
            string.Join(",", node.DirectionalConnections.Select(connection =>
                $"{connection.Direction}>{connection.DestinationRoomId}{(connection.IsSecret ? "*" : "")}"))));

        private static void ValidatePitDropRules(SecretPit pitPrefab)
        {
            GameObject root = new("Special-3 pit drop verification");
            ResourceDropTable table = ScriptableObject.CreateInstance<ResourceDropTable>();
            try
            {
                GameObject elif = Week17Resource1Setup.LoadPrefab(RunResourceType.Elif).gameObject;
                table.Configure(1f, new[]
                {
                    new ResourceDropEntry("elif", elif, 50),
                    new ResourceDropEntry("pit", pitPrefab.gameObject, 50),
                });
                int pitsWithLink = 0;
                for (int seed = 1; seed <= 256; seed++)
                {
                    Assert(table.TryRoll(seed, candidate => !DestructibleObstacle.IsSecretPit(candidate),
                               out ResourceDropEntry withoutSecret) && withoutSecret.DropId == "elif",
                        "Without a secret room the pit candidate must leave the roll and keep the drop chance.");
                    Assert(table.TryRoll(seed, out ResourceDropEntry withSecret), "A 100% table must always drop.");
                    pitsWithLink += withSecret.DropId == "pit" ? 1 : 0;
                }
                Assert(pitsWithLink > 64 && pitsWithLink < 192, "With a secret room the pit must stay a candidate.");

                SecretRoomLink link = new(null);
                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle broken = CreateObstacle(root.transform, "pit-obstacle", table);
                int pitSeed = Enumerable.Range(1, 256).First(seed =>
                    table.TryRoll(DestructibleObstacle.DeriveDropSeed(seed, "pit-obstacle"), out ResourceDropEntry entry) &&
                    entry.DropId == "pit");
                broken.Bind(state, pitSeed, root.transform, null, link);
                Assert(broken.TryDestroyByBomb() && broken.LastDrop != null &&
                       broken.LastDrop.GetComponent<SecretPit>() != null && state.IsObstacleDestroyed("pit-obstacle"),
                    "A pit roll on a secret-room floor must leave a SecretPit.");

                DestructibleObstacle rebuilt = CreateObstacle(root.transform, "pit-obstacle", table);
                rebuilt.Bind(state, pitSeed, root.transform, null, link);
                Assert(rebuilt.IsBroken && rebuilt.LastDrop != null && rebuilt.LastDrop.GetComponent<SecretPit>() != null,
                    "A rebuilt room must restore the pit left by its broken obstacle.");

                DestructibleObstacle noSecret = CreateObstacle(root.transform, "pit-obstacle", table);
                noSecret.Bind(new RoomRunState("floor-01-room-03"), pitSeed, root.transform, null);
                Assert(noSecret.TryDestroyByBomb() &&
                       (noSecret.LastDrop == null || noSecret.LastDrop.GetComponent<SecretPit>() == null),
                    "Without a secret room the same pit roll must re-weight to another candidate.");
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(root);
            }
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, string obstacleId, ResourceDropTable table)
        {
            GameObject obstacleObject = new(obstacleId, typeof(BoxCollider2D), typeof(SpriteRenderer),
                typeof(DestructibleObstacle));
            obstacleObject.transform.SetParent(parent);
            obstacleObject.layer = LayerMask.NameToLayer("Environment");
            DestructibleObstacle obstacle = obstacleObject.GetComponent<DestructibleObstacle>();
            obstacle.Configure(obstacleId, DestructibleObstacle.DefaultRequiredHits, table,
                obstacleObject.GetComponent<SpriteRenderer>());
            return obstacle;
        }

        private static void ValidateBombWallAndReload(RoomGraphAssembler assembler, int seed)
        {
            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode secret = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Secret);
            GeneratedRoomConnection toNeighbor = secret.DirectionalConnections.First(connection =>
                floor.Nodes.Single(node => node.RoomId == connection.DestinationRoomId).Role ==
                GeneratedRoomRole.Intermediate);
            string neighborId = toNeighbor.DestinationRoomId;
            RoomDoorDirection wallDirection = GeneratedFloorGraph.Opposite(toNeighbor.Direction);
            RoomRunState secretState = progress.GetRoomState(secret.RoomId);
            RoomPrefab neighborRoom = FindRuntimeRoom(assembler, neighborId);
            RoomPrefab secretRoom = FindRuntimeRoom(assembler, secret.RoomId);
            RoomDoorSlot wallSlot = neighborRoom.FindSlot(wallDirection);
            RoomDoorSlot insideSlot = secretRoom.FindSlot(toNeighbor.Direction);

            Assert(IsSealed(wallSlot) && IsSealed(insideSlot) &&
                   wallSlot.Seal.GetComponent<SecretPassageWall>()?.NeighborRoomId == neighborId &&
                   wallSlot.Doorway.Destination == secretRoom.Node && !secretState.IsSecretDiscovered,
                "An undiscovered hidden passage must look and act like a sealed wall on both sides.");
            Assert(secretRoom.RewardRoom != null && secretRoom.RewardRoom.gameObject.activeSelf &&
                   secretRoom.Controller.State == RoomState.Cleared,
                "The secret room must be a safe room with the treasure-style selection reward.");

            EnterRoomDirectly(assembler, neighborRoom);
            GameObject owner = CreateBombOwner();
            try
            {
                ExplodeAt(owner, (Vector2)neighborRoom.Node.CameraAnchor.position);
                Assert(IsSealed(wallSlot) && secretState.OpenedSecretPassages.Count == 0,
                    "A bomb out of reach must not open the hidden wall.");

                Vector2 inward = -RoomLayout.Direction(wallDirection);
                ExplodeAt(owner, (Vector2)wallSlot.Seal.transform.position + inward * 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }

            Assert(secretState.IsSecretPassageOpen(neighborId) && IsOpen(wallSlot) && IsOpen(insideSlot) &&
                   wallSlot.Blocker.VisualKind == DoorVisualKind.SecretPassage && secretState.IsSecretDiscovered,
                "A bomb within reach must open the hidden passage on both sides.");
            Assert(secret.ConnectedRoomIds.Where(id => id != neighborId).All(id =>
                    !secretState.IsSecretPassageOpen(id) &&
                    IsSealed(FindRuntimeRoom(assembler, id).FindSlot(GeneratedFloorGraph.Opposite(
                        secret.DirectionalConnections.Single(connection => connection.DestinationRoomId == id)
                            .Direction)))),
                "A bomb must open only the wall it reached.");

            ResetTransitionCooldown(assembler.Graph);
            Assert(wallSlot.Doorway.TryEnter(assembler.Graph.Player) &&
                   assembler.Graph.CurrentNode == secretRoom.Node && secretState.HasVisited &&
                   secret.ConnectedRoomIds.All(secretState.IsSecretPassageOpen) &&
                   secret.DirectionalConnections.All(connection => IsOpen(secretRoom.FindSlot(connection.Direction))),
                "Entering the secret room through the opened wall must open every hidden passage.");

            Assert(assembler.TryLoadFloor(2, null, out error) && assembler.TryLoadFloor(1, null, out error), error);
            RoomPrefab rebuiltSecret = FindRuntimeRoom(assembler, secret.RoomId);
            Assert(secret.DirectionalConnections.All(connection =>
                       IsOpen(rebuiltSecret.FindSlot(connection.Direction)) &&
                       IsOpen(FindRuntimeRoom(assembler, connection.DestinationRoomId)
                           .FindSlot(GeneratedFloorGraph.Opposite(connection.Direction)))) &&
                   rebuiltSecret.Controller.State == RoomState.Cleared,
                "Opened hidden passages and the secret room state must survive a floor rebuild.");
        }

        private static void ValidatePitEntry(RoomGraphAssembler assembler, int seed, SecretPit pitPrefab)
        {
            RunProgress progress = assembler.Progress;
            progress.ResetProgress();
            Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
            GeneratedFloor floor = assembler.GeneratedGraph.FindFloor(1);
            GeneratedRoomNode secret = floor.Nodes.Single(node => node.Role == GeneratedRoomRole.Secret);
            GeneratedRoomNode source = FindObstacleRoom(floor);
            RoomPrefab sourceRoom = FindRuntimeRoom(assembler, source.RoomId);
            RoomPrefab secretRoom = FindRuntimeRoom(assembler, secret.RoomId);
            RoomRunState secretState = progress.GetRoomState(secret.RoomId);

            ResourceDropTable pitOnly = ScriptableObject.CreateInstance<ResourceDropTable>();
            try
            {
                EnterRoomDirectly(assembler, sourceRoom, false);
                DestructibleObstacle obstacle = sourceRoom.GetComponentsInChildren<DestructibleObstacle>()
                    .First(candidate => !candidate.IsBroken && candidate.isActiveAndEnabled);
                pitOnly.Configure(1f, new[] { new ResourceDropEntry("pit", pitPrefab.gameObject, 1) });
                typeof(DestructibleObstacle).GetField("dropTable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(obstacle, pitOnly);
                Assert(obstacle.TryDestroyByBomb() && obstacle.LastDrop != null, "The forced pit roll must drop.");
                SecretPit pit = obstacle.LastDrop.GetComponent<SecretPit>();
                Assert(pit != null && pit.Target == secretRoom.Node && pit.SourceNode == sourceRoom.Node,
                    "A pit must lead from its own room to the floor's secret room.");

                PlayerMovementAt(assembler, pit.transform.position);
                ResetTransitionCooldown(assembler.Graph);
                Assert(sourceRoom.Controller.State != RoomState.Cleared && !pit.TryEnter(assembler.Graph.Player) &&
                       assembler.Graph.CurrentNode == sourceRoom.Node,
                    "A pit must not work before its room is cleared.");

                sourceRoom.Controller.BindRunState(progress.GetRoomState(source.RoomId), true);
                PlayerMovementAt(assembler, (Vector2)pit.transform.position + Vector2.right * 0.8f);
                Assert(!pit.TryEnter(assembler.Graph.Player), "Brushing the pit rim must not drop the player in.");

                PlayerMovementAt(assembler, pit.transform.position);
                Assert(pit.TryEnter(assembler.Graph.Player) && assembler.Graph.CurrentNode == secretRoom.Node &&
                       secretState.HasVisited && secret.ConnectedRoomIds.All(secretState.IsSecretPassageOpen) &&
                       ((Vector2)assembler.Graph.Player.transform.position - secretRoom.Node.InitialSpawnPosition)
                       .sqrMagnitude < 0.0001f,
                    "A cleared room's pit must drop the player into the secret room and open every passage.");

                EnterRoomDirectly(assembler, sourceRoom, false);
                PlayerMovementAt(assembler, pit.transform.position);
                ResetTransitionCooldown(assembler.Graph);
                Assert(pit.TryEnter(assembler.Graph.Player) && assembler.Graph.CurrentNode == secretRoom.Node,
                    "After discovery the pit must remain a shortcut into the secret room.");
            }
            finally
            {
                Object.DestroyImmediate(pitOnly);
            }
        }

        private static void EnterRoomDirectly(RoomGraphAssembler assembler, RoomPrefab room, bool markCleared = true)
        {
            if (markCleared) room.Controller.BindRunState(assembler.Progress.GetRoomState(room.Node.RoomId), true);
            Assert(assembler.Graph.TryReplaceFloor(assembler.Graph.Nodes.ToArray(), room.Node,
                assembler.Graph.Player, out string error), error);
            Physics2D.SyncTransforms();
        }

        private static void PlayerMovementAt(RoomGraphAssembler assembler, Vector2 position)
        {
            Transform player = assembler.Graph.Player.transform;
            player.position = new Vector3(position.x, position.y, player.position.z);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = position;
            Physics2D.SyncTransforms();
        }

        private static GameObject CreateBombOwner()
        {
            GameObject owner = new("Special-3 bomb owner", typeof(Health));
            owner.transform.position = new Vector3(-10000f, -10000f, 0f);
            owner.GetComponent<Health>().GetType()
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(owner.GetComponent<Health>(), null);
            return owner;
        }

        private static void ExplodeAt(GameObject owner, Vector2 position)
        {
            GameObject bombObject = new("Special-3 bomb", typeof(PlacedBomb));
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

        private static void ResetTransitionCooldown(RoomGraphController graph)
        {
            typeof(RoomGraphController).GetField("nextTransitionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(graph, 0f);
        }

        private static bool IsSealed(RoomDoorSlot slot) =>
            slot.Seal.activeSelf && !slot.Doorway.gameObject.activeSelf && !slot.Blocker.gameObject.activeSelf;

        private static bool IsOpen(RoomDoorSlot slot) =>
            !slot.Seal.activeSelf && slot.Doorway.gameObject.activeSelf && slot.Blocker.gameObject.activeSelf;

        private static RoomPrefab FindRuntimeRoom(RoomGraphAssembler assembler, string roomId)
        {
            foreach (RoomNode node in assembler.Graph.Nodes)
                if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
            throw new InvalidOperationException($"Runtime room {roomId} is missing.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
