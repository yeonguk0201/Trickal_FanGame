using System;
using System.IO;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class LivelyFairyMotionSetup
    {
        public const string AtlasPath = "Assets/Art/Enemies/FairyKingdom/Walking/LivelyFairy_Motion.png";
        [MenuItem("Trickal Fan Game/Artwork/Setup Lively Fairy Motion")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode.");
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture.width < 4 || texture.height < 2)
                throw new InvalidOperationException("Expected a 4x2 atlas.");
            Texture2D pixels = new(2, 2);
            Rect content;
            try
            {
                pixels.LoadImage(File.ReadAllBytes(AtlasPath));
                int width = pixels.width / 4, height = pixels.height / 2;
                int minX = width, minY = height, maxX = 0, maxY = 0;
                Color32[] colors = pixels.GetPixels32();
                // Neutral pose (bottom left) fixes scale and ground baseline for every frame.
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (colors[y * pixels.width + x].a < 20) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (maxX <= minX || maxY <= minY) throw new InvalidOperationException("Empty neutral pose.");
                content = new Rect(minX / (pixels.width / 4f), minY / (pixels.height / 2f),
                    (maxX - minX + 1f) / (pixels.width / 4f), (maxY - minY + 1f) / (pixels.height / 2f));
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            GameObject root = PrefabUtility.LoadPrefabContents(Week25Enemy8Setup.PrefabPath);
            try
            {
                var motion = root.GetComponent<LivelyFairyMotion>();
                if (motion == null) motion = root.AddComponent<LivelyFairyMotion>();
                motion.Configure(texture, content);
                // Keep the lively fairy's individually reviewed shadow placement across setup reruns.
                var shadow = root.GetComponent<GroundShadow>();
                if (shadow == null) shadow = root.AddComponent<GroundShadow>();
                shadow.SetProfileId("LivelyLowBloodSugarFairy");
                if (PrefabUtility.SaveAsPrefabAsset(root, Week25Enemy8Setup.PrefabPath) == null)
                    throw new InvalidOperationException("Could not save lively fairy motion.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void SetupAndVerifyBatch() { Setup(); Verify(); }

        [MenuItem("Trickal Fan Game/Artwork/Verify Lively Fairy Motion")]
        public static void Verify()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Week25Enemy8Setup.PrefabPath);
            try
            {
                var motion = root.GetComponent<LivelyFairyMotion>();
                if (motion == null || motion.Atlas == null) throw new InvalidOperationException("Motion missing.");
                var renderer = root.GetComponent<SpriteRenderer>();
                var collider = root.GetComponent<Collider2D>();
                Vector3 scale = root.transform.localScale;
                Bounds bounds = collider.bounds;
                for (int i = 0; i < 4; i++)
                {
                    motion.RenderPose(FourWayShooterState.Moving, i / 4f, true, 1);
                    if (motion.CurrentFrame != i || renderer.sprite == null)
                        throw new InvalidOperationException("Walk frame missing.");
                }
                motion.RenderPose(FourWayShooterState.Telegraph, 0, false, 1);
                if (motion.CurrentFrame != 5) throw new InvalidOperationException("Telegraph missing.");
                motion.RenderPose(FourWayShooterState.Recovering, 0, false, 0);
                if (motion.CurrentFrame != 6) throw new InvalidOperationException("Release missing.");
                motion.RenderPose(FourWayShooterState.Recovering, 0, false, 0.2f);
                if (motion.CurrentFrame != 7) throw new InvalidOperationException("Recovery missing.");
                motion.RenderPose(FourWayShooterState.Moving, 0, false, 1);
                if (motion.CurrentFrame != -1) throw new InvalidOperationException("Blocked walk must idle.");
                motion.RenderPose(FourWayShooterState.Telegraph, 0, false, 1, true);
                if (motion.CurrentFrame != -1) throw new InvalidOperationException("Suppression must idle.");
                if (root.transform.localScale != scale || collider.bounds != bounds)
                    throw new InvalidOperationException("Motion changed physics geometry.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("Lively fairy motion verification passed.");
        }
    }
}
