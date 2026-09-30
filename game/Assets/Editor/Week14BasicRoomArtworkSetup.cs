using System;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class Week14BasicRoomArtworkSetup
    {
        public const string ArtworkPath = "Assets/Rooms/Artwork/fairy-village-basic-room.png";
        public const string BackgroundObjectName = "Temporary Fairy Village Background";
        public const int BackgroundSortingOrder = -100;

        // Alpha bounds measured from the source artwork. Keeping the transparent canvas lets us
        // preserve the original PNG while aligning its visible outer wall with the 16x9 room contract.
        private const float SourceWidth = 1672f;
        private const float SourceHeight = 941f;
        private const float VisibleWidth = 1452f;
        private const float VisibleHeight = 915f;
        private const float VisibleCenterX = 836.5f;
        private const float VisibleCenterYFromTop = 457f;
        private const float PixelsPerUnit = 100f;

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

        [MenuItem("Trickal Fan Game/Week 14/Setup Temporary Basic Room Artwork")]
        public static void Setup()
        {
            ConfigureTextureImporter();

            Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ArtworkPath);
            GameObject basicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week8GridFloorSetup.PrefabPath);
            if (artwork == null || basicPrefab == null)
            {
                Debug.LogError(
                    "Temporary Basic room artwork requires the imported fairy-village PNG and existing Basic room Prefab.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(Week8GridFloorSetup.PrefabPath);
            try
            {
                RoomPrefab roomPrefab = root.GetComponent<RoomPrefab>();
                Transform content = roomPrefab != null && roomPrefab.Node != null
                    ? roomPrefab.Node.ContentRoot.transform
                    : null;
                if (content == null)
                {
                    Debug.LogError("Temporary Basic room artwork could not find the Basic room Content root.");
                    return;
                }

                Transform background = content.Find(BackgroundObjectName);
                if (background == null)
                {
                    background = new GameObject(BackgroundObjectName).transform;
                    background.SetParent(content, false);
                }

                SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = background.gameObject.AddComponent<SpriteRenderer>();
                }

                renderer.sprite = artwork;
                renderer.color = Color.white;
                renderer.sortingOrder = BackgroundSortingOrder;
                background.localPosition = CalculateLocalPosition();
                background.localRotation = Quaternion.identity;
                background.localScale = CalculateLocalScale();

                foreach (string wallName in WallNames)
                {
                    Transform wall = content.Find(wallName);
                    SpriteRenderer wallRenderer = wall != null ? wall.GetComponent<SpriteRenderer>() : null;
                    BoxCollider2D wallCollider = wall != null ? wall.GetComponent<BoxCollider2D>() : null;
                    if (wallRenderer == null || wallCollider == null)
                    {
                        throw new InvalidOperationException(
                            $"Basic room is missing the visual or collider for wall '{wallName}'.");
                    }

                    wallRenderer.enabled = false;
                    wallCollider.enabled = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, Week8GridFloorSetup.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Temporary Fairy Village artwork is applied only to the Basic 16x9 room; " +
                "wall colliders and door runtime objects remain unchanged.");
        }

        public static Vector3 CalculateLocalScale()
        {
            return new Vector3(
                RoomLayout.Width / (VisibleWidth / PixelsPerUnit),
                RoomLayout.Height / (VisibleHeight / PixelsPerUnit),
                1f);
        }

        public static Vector3 CalculateLocalPosition()
        {
            Vector3 scale = CalculateLocalScale();
            float canvasCenterX = (SourceWidth - 1f) * 0.5f;
            float canvasCenterYFromTop = (SourceHeight - 1f) * 0.5f;
            float visibleOffsetX = (VisibleCenterX - canvasCenterX) / PixelsPerUnit;
            float visibleOffsetY = (canvasCenterYFromTop - VisibleCenterYFromTop) / PixelsPerUnit;
            return new Vector3(-visibleOffsetX * scale.x, -visibleOffsetY * scale.y, 0f);
        }

        private static void ConfigureTextureImporter()
        {
            AssetDatabase.ImportAsset(ArtworkPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(ArtworkPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
