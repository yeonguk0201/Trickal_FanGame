using System;
using System.Linq;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14BasicRoomArtworkVerification
    {
        private static readonly string[] WallNames =
        {
            "Top Left Wall",
            "Top Right Wall",
            "Bottom Left Wall",
            "Bottom Right Wall",
            "Left Upper Wall",
            "Left Lower Wall",
            "Right Upper Wall",
            "Right Lower Wall",
        };

        [MenuItem("Trickal Fan Game/Week 14/Verify Temporary Basic Room Artwork")]
        public static void Verify()
        {
            Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(Week14BasicRoomArtworkSetup.ArtworkPath);
            Assert(artwork != null, "The temporary Basic room artwork is not imported as a Sprite.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            RoomPrefab roomPrefab = prefab != null ? prefab.GetComponent<RoomPrefab>() : null;
            Transform content = roomPrefab != null && roomPrefab.Node != null
                ? roomPrefab.Node.ContentRoot.transform
                : null;
            Assert(content != null, "The Basic room Prefab or Content root is missing.");

            Transform background = content.Find(Week14BasicRoomArtworkSetup.BackgroundObjectName);
            SpriteRenderer backgroundRenderer = background != null
                ? background.GetComponent<SpriteRenderer>()
                : null;
            Assert(backgroundRenderer != null && backgroundRenderer.sprite == artwork,
                "The Basic room does not reference the temporary Fairy Village artwork.");
            Assert(backgroundRenderer.enabled &&
                   backgroundRenderer.sortingOrder == Week14BasicRoomArtworkSetup.BackgroundSortingOrder,
                "The temporary Basic room artwork must render behind gameplay objects.");
            Assert(Approximately(background.localScale, Week14BasicRoomArtworkSetup.CalculateLocalScale()) &&
                   Approximately(background.localPosition, Week14BasicRoomArtworkSetup.CalculateLocalPosition()),
                "The temporary Basic room artwork is not aligned to its measured 16x9 visible bounds.");

            foreach (string wallName in WallNames)
            {
                Transform wall = content.Find(wallName);
                SpriteRenderer renderer = wall != null ? wall.GetComponent<SpriteRenderer>() : null;
                BoxCollider2D collider = wall != null ? wall.GetComponent<BoxCollider2D>() : null;
                Assert(renderer != null && !renderer.enabled && collider != null && collider.enabled,
                    $"Wall '{wallName}' must hide only its placeholder visual and preserve collision.");
            }

            string[] nonBasicPaths =
            {
                Week14Room3Setup.SmallPrefabPath,
                Week14Room3Setup.WidePrefabPath,
                Week14Room6Setup.TallPrefabPath,
                Week14Room6Setup.LargePrefabPath,
            };
            foreach (string path in nonBasicPaths)
            {
                GameObject otherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RoomPrefab otherRoom = otherPrefab != null ? otherPrefab.GetComponent<RoomPrefab>() : null;
                Transform otherContent = otherRoom != null && otherRoom.Node != null
                    ? otherRoom.Node.ContentRoot.transform
                    : null;
                Assert(otherContent == null ||
                       otherContent.Find(Week14BasicRoomArtworkSetup.BackgroundObjectName) == null,
                    $"Temporary Basic artwork leaked into non-Basic Prefab '{path}'.");
            }

            Assert(prefab.GetComponentsInChildren<Collider2D>(true).All(collider => collider != null),
                "The Basic room collider hierarchy became invalid.");
            Debug.Log(
                "Temporary Basic room artwork verification passed: the Fairy Village image is aligned behind " +
                "the Basic 16x9 room while wall collision remains active and other room sizes stay unchanged.");
        }

        public static void SetupAndVerifyBatch()
        {
            string prefabGuid = AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath);
            string artworkGuid = AssetDatabase.AssetPathToGUID(Week14BasicRoomArtworkSetup.ArtworkPath);
            Week14BasicRoomArtworkSetup.Setup();
            Week14BasicRoomArtworkSetup.Setup();
            Assert(prefabGuid == AssetDatabase.AssetPathToGUID(Week8GridFloorSetup.PrefabPath),
                "Repeated artwork setup changed the Basic Prefab GUID.");
            Assert(!string.IsNullOrWhiteSpace(artworkGuid) &&
                   artworkGuid == AssetDatabase.AssetPathToGUID(Week14BasicRoomArtworkSetup.ArtworkPath),
                "Repeated artwork setup changed the source artwork GUID.");
            Verify();
            Week14Room0RegressionVerification.Verify();
        }

        private static bool Approximately(Vector3 first, Vector3 second) =>
            (first - second).sqrMagnitude < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
