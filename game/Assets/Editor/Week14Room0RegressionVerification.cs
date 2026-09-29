using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week14Room0RegressionVerification
    {
        private const string BasicRoomPrefabGuid = "4c3637cd1a19e3f4883f17715d95367f";
        private const string ExpectedSeedSignatureHash =
            "821297a860878f50bc7f318dc8bdb05aa846a22720e3a1b524456502521f98f3";

        [MenuItem("Trickal Fan Game/Week 14/Verify Room-0 Basic Regression")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null, "Room-0 requires the configured FloorGenerator.");
            Assert(generator.FloorCount == 3 && generator.MinimumRoomsPerFloor == 6 &&
                   generator.MaximumRoomsPerFloor == 8 && generator.MinimumBossDistance == 3 &&
                   generator.GenerationRetryLimit == 32,
                "The existing three-floor generator settings changed.");
            Assert(AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath) == BasicRoomPrefabGuid,
                "The existing Basic room prefab GUID changed.");

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            RoomPrefab prefab = prefabAsset != null ? prefabAsset.GetComponent<RoomPrefab>() : null;
            string error = null;
            Assert(prefab != null && prefab.TryValidate(out error),
                error ?? "The existing Basic room prefab is missing.");
            ValidateBasicLayout(prefab);

            RoomGraphAssembler assembler = generator.GetComponent<RoomGraphAssembler>();
            Camera roomCamera = assembler != null && assembler.Graph != null && assembler.Graph.RoomCamera != null
                ? assembler.Graph.RoomCamera.RoomCamera
                : null;
            Assert(roomCamera != null && roomCamera.orthographic &&
                   Mathf.Approximately(roomCamera.orthographicSize, RoomLayout.CameraOrthographicSize),
                "The Basic room camera must keep its fixed orthographic framing.");
            Assert(generator.TryGenerateForSeed(Week8RandomRoomSetup.FixedVerificationSeed,
                out GeneratedFloorGraph generated, out error), error);

            string signature = BuildSignature(generated);
            string signatureHash = BuildSha256(signature);
            Assert(signatureHash == ExpectedSeedSignatureHash,
                $"The existing Basic-room seed result changed. Expected hash {ExpectedSeedSignatureHash}, " +
                $"actual hash {signatureHash}. Actual signature: {signature}");

            Week7RoomGraphVerification.Verify();
            Debug.Log(
                "Week 14 Room-0 verification passed: the existing Basic room prefab GUID, 16x9 bounds, " +
                "20x13 grid spacing, fixed camera framing, four door slots, safe entry points, spawn layout, " +
                "fixed-seed topology/content, bidirectional movement, combat clear and revisit state are unchanged.");
        }

        public static void SetupAndVerifyBatch()
        {
            Verify();
        }

        private static string BuildSignature(GeneratedFloorGraph graph)
        {
            StringBuilder signature = new();
            foreach (GeneratedFloor floor in graph.Floors.OrderBy(candidate => candidate.FloorNumber))
            {
                signature.Append("F").Append(floor.FloorNumber)
                    .Append('[').Append(floor.StartingRoomId)
                    .Append('>').Append(floor.BossRoomId).Append("]{");

                foreach (GeneratedRoomNode node in floor.Nodes.OrderBy(candidate => candidate.RoomNumber))
                {
                    signature.Append(node.RoomId).Append('@')
                        .Append(node.GridPosition.X).Append(',').Append(node.GridPosition.Y).Append(':')
                        .Append(node.Role).Append(':')
                        .Append(node.Definition.RoomDefinitionId).Append(':')
                        .Append(node.ContentSeed).Append('(');

                    foreach (GeneratedRoomConnection connection in node.DirectionalConnections
                                 .OrderBy(candidate => candidate.Direction))
                    {
                        signature.Append(connection.Direction).Append('>')
                            .Append(connection.DestinationRoomId).Append(',');
                    }

                    signature.Append(");");
                }

                signature.Append('}');
            }

            return signature.ToString();
        }

        private static void ValidateBasicLayout(RoomPrefab prefab)
        {
            Assert(Mathf.Approximately(RoomLayout.Width, 16f) && Mathf.Approximately(RoomLayout.Height, 9f) &&
                   Mathf.Approximately(RoomLayout.RoomSpacingX, 20f) &&
                   Mathf.Approximately(RoomLayout.RoomSpacingY, 13f) &&
                   Mathf.Approximately(RoomLayout.CameraOrthographicSize, 5.25f),
                "The Basic room size, grid spacing, or camera framing changed.");
            Assert(Mathf.Approximately(RoomLayout.TransitionInnerEdgeInset, 0.35f) &&
                   Mathf.Approximately(RoomLayout.SafeEntryInsetFromWall, 1.9f),
                "The Basic room transition or safe-entry depth changed.");

            BoxCollider2D encounter = prefab.Controller.GetComponent<BoxCollider2D>();
            Assert(encounter != null && Approximately(encounter.size, RoomLayout.EncounterSize),
                "The Basic room Encounter bounds changed.");
            Transform content = prefab.Node.ContentRoot.transform;
            Vector2 horizontalWallSize = new(RoomLayout.HorizontalWallSegmentLength, RoomLayout.WallThickness);
            Vector2 verticalWallSize = new(RoomLayout.WallThickness, RoomLayout.VerticalWallSegmentLength);
            AssertWall(content, "Top Left Wall",
                new Vector2(-RoomLayout.HorizontalWallSegmentCenter, RoomLayout.VerticalWallCenter),
                horizontalWallSize);
            AssertWall(content, "Top Right Wall",
                new Vector2(RoomLayout.HorizontalWallSegmentCenter, RoomLayout.VerticalWallCenter),
                horizontalWallSize);
            AssertWall(content, "Bottom Left Wall",
                new Vector2(-RoomLayout.HorizontalWallSegmentCenter, -RoomLayout.VerticalWallCenter),
                horizontalWallSize);
            AssertWall(content, "Bottom Right Wall",
                new Vector2(RoomLayout.HorizontalWallSegmentCenter, -RoomLayout.VerticalWallCenter),
                horizontalWallSize);
            AssertWall(content, "Left Upper Wall",
                new Vector2(-RoomLayout.HorizontalWallCenter, RoomLayout.VerticalWallSegmentCenter),
                verticalWallSize);
            AssertWall(content, "Left Lower Wall",
                new Vector2(-RoomLayout.HorizontalWallCenter, -RoomLayout.VerticalWallSegmentCenter),
                verticalWallSize);
            AssertWall(content, "Right Upper Wall",
                new Vector2(RoomLayout.HorizontalWallCenter, RoomLayout.VerticalWallSegmentCenter),
                verticalWallSize);
            AssertWall(content, "Right Lower Wall",
                new Vector2(RoomLayout.HorizontalWallCenter, -RoomLayout.VerticalWallSegmentCenter),
                verticalWallSize);
            // Encounter-4 appends SpawnPoints; the original three must stay in place.
            Assert(prefab.Controller.SpawnPoints.Count >= 3,
                "The Basic room must keep its three existing SpawnPoints.");
            for (int index = 0; index < 3; index++)
            {
                Assert(Approximately(prefab.Controller.SpawnPoints[index].localPosition,
                        RoomLayout.SpawnPosition(index, 3)),
                    $"Basic room SpawnPoint {index + 1} changed.");
            }

            foreach (RoomDoorDirection direction in Enum.GetValues(typeof(RoomDoorDirection)))
            {
                RoomDoorSlot slot = prefab.FindSlot(direction);
                Assert(slot != null, $"The Basic room lost its {direction} door slot.");
                Vector2 normal = RoomLayout.Direction(direction);
                Vector2 expectedSlot = normal * RoomLayout.TransitionCenter(direction);
                Vector2 expectedWallCenter = normal * (RoomLayout.IsSideDoor(direction)
                    ? RoomLayout.HorizontalWallCenter
                    : RoomLayout.VerticalWallCenter);
                Vector2 expectedEntry = normal *
                    (RoomLayout.TransitionCenter(direction) - RoomLayout.EntryInsetFromTransition);
                Assert(Approximately(slot.transform.localPosition, expectedSlot),
                    $"The Basic room {direction} transition position changed.");
                Assert(Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.Blocker.transform.localPosition, expectedWallCenter) &&
                       Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.Seal.transform.localPosition, expectedWallCenter),
                    $"The Basic room {direction} door or seal left the wall center.");
                Assert(Approximately((Vector2)slot.transform.localPosition +
                                     (Vector2)slot.EntryPoint.localPosition, expectedEntry),
                    $"The Basic room {direction} safe entry point changed.");
            }

            Assert(Approximately(prefab.Node.CameraAnchor.localPosition, Vector2.zero) &&
                   prefab.Node.InitialSpawnPoint != null &&
                   Approximately(prefab.Node.InitialSpawnPosition, prefab.transform.position) &&
                   prefab.Node.DefaultEntryPoint == prefab.FindSlot(RoomDoorDirection.Left).EntryPoint,
                "The Basic room camera anchor, initial spawn, or default door entry changed.");
        }

        private static void AssertWall(
            Transform content,
            string wallName,
            Vector2 expectedPosition,
            Vector2 expectedSize)
        {
            Transform wall = content.Find(wallName);
            BoxCollider2D collider = wall != null ? wall.GetComponent<BoxCollider2D>() : null;
            Assert(wall != null && collider != null && Approximately(wall.localPosition, expectedPosition) &&
                   Approximately(Vector2.Scale(collider.size, wall.localScale), expectedSize),
                $"The Basic room {wallName} bounds changed.");
        }

        private static string BuildSha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Approximately(Vector2 first, Vector2 second)
        {
            return (first - second).sqrMagnitude < 0.0001f;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
