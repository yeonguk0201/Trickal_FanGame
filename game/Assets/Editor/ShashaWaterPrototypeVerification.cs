using System;
using System.Reflection;
using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class ShashaWaterPrototypeVerification
    {
        // An isolated render probe, not a replacement for movement and art review in SampleScene.
        [MenuItem("Trickal Fan Game/Effects/Export Shasha Water Comparison")]
        public static void ExportComparison()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject owner = new("Water render probe");
            ProceduralWaterStream effect = null;
            Camera camera = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
                owner.layer = 31;
                SpriteRenderer original = owner.AddComponent<SpriteRenderer>();
                original.sprite = FairyKingdomArtwork.Fit("effect-water-stream", 1);
                Require(original.sprite != null, "Original water artwork is missing.");
                owner.transform.position = new Vector3(0, 0.5f, 0);
                Vector2 size = original.sprite.bounds.size;
                owner.transform.localScale = new Vector3(5.5f / size.x, 0.2f / size.y, 1);
                effect = ProceduralWaterStream.Create(owner);
                effect.gameObject.layer = 31;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect.gameObject, scene);
                effect.Begin(new Vector2(-2.75f, -0.5f), new Vector2(2.75f, -0.5f), 0.2f, 10, 0.12f, true);
                effect.RenderAt(10.045f);
                camera = new GameObject("Water render camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = 1.1f;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.8f, 0.84f, 0.72f);
                target = new RenderTexture(1280, 400, 24);
                image = new Texture2D(1280, 400, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 400), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/art-review/shasha-water"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "unity-comparison.png"), image.EncodeToPNG());
                Debug.Log("Shasha comparison exported: top original, bottom realtime body. Edit-mode probe; droplets and Play review separate.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (effect != null) Object.DestroyImmediate(effect.gameObject);
                if (image != null) Object.DestroyImmediate(image);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        public static void VerifyAndExportBatch()
        {
            Verify();
            ExportComparison();
        }

        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            FairyKingdomArtworkVerification.Verify();
            Debug.Log("Shasha realtime water regressions passed: prototype, stream gameplay and original artwork bindings.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Shasha Realtime Water Prototype")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("ProceduralWaterStream");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "Realtime water shader is missing or failed compilation.");
            var owner = new GameObject("Water prototype verification owner");
            ProceduralWaterStream effect = null;
            try
            {
                owner.transform.position = new Vector3(-7500, 7500);
                owner.transform.localScale = new Vector3(-1.7f, 2.3f, 1);
                owner.AddComponent<SpriteRenderer>().sortingOrder = 17;
                effect = ProceduralWaterStream.Create(owner);
                Require(effect != null && effect.GetComponent<MeshRenderer>().sortingOrder == 16,
                    "Water must sort below the player and remain independent of its transform.");
                Require(effect.Droplets.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                    effect.Droplets.main.maxParticles <= 96, "Droplets must remain world-space and bounded.");
                Vector2 from = owner.transform.position;
                foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up,
                    Vector2.down, new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized })
                foreach (float length in new[] { 0.02f, 0.2f, 3f, 40f })
                {
                    effect.Begin(from, from + direction * length, 0.2f, 10, 0.12f, true);
                    effect.RenderAt(10.04f);
                    Require(effect.BodyVisible, "Every nonzero path must display.");
                    Vector3[] before = effect.BodyMesh.vertices;
                    Require(before.Length == 162, "Strip geometry must remain bounded for long paths.");
                    for (int i = 0; i < before.Length; i += 2)
                    {
                        Vector2 center = (before[i] + before[i + 1]) * 0.5f;
                        float along = Vector2.Dot(center - from, direction);
                        Vector2 side = new(-direction.y, direction.x);
                        Require(Mathf.Abs(Vector2.Dot(center - from, side)) < 0.003f &&
                            along >= -0.003f && along <= length + 0.003f,
                            "Visual centerline must stay straight and inside the path in every direction.");
                    }
                    effect.RenderAt(10.07f);
                    Require((effect.BodyMesh.vertices[35] - before[35]).sqrMagnitude > 0.0000001f,
                        "Water contour must animate instead of remaining a static stretched image.");
                    Vector2 moved = from + new Vector2(0.4f, 0.1f);
                    effect.SetPath(moved, moved + direction * length * 0.5f, true);
                    effect.RenderAt(10.08f);
                    Vector3[] after = effect.BodyMesh.vertices;
                    Require(Vector2.Distance((after[0] + after[1]) * 0.5f, moved) < 0.003f &&
                        Vector2.Distance((after[^2] + after[^1]) * 0.5f, effect.EndPoint) < 0.003f,
                        "Moving start and wall endpoint must update together without parent scale distortion.");
                    effect.RenderAt(10.13f);
                    Require(!effect.BodyVisible, "Body must expire at the burst deadline.");
                }
                effect.Begin(from, from, 0.2f, 20, 0.12f, true);
                Require(!effect.BodyVisible, "Zero-length wall paths must not produce an inverted strip.");
                effect.Clear();
                Require(!effect.BodyVisible && effect.Droplets.particleCount == 0,
                    "Cancellation must clear body and droplets.");

                PlayerWaterStream stream = owner.AddComponent<PlayerWaterStream>();
                var nextFire = typeof(PlayerWaterStream).GetField("nextFireTime", BindingFlags.NonPublic | BindingFlags.Instance);
                nextFire.SetValue(stream, 123f);
                stream.SetVisualMode(false);
                stream.SetVisualMode(true);
                Require((float)nextFire.GetValue(stream) == 123 && stream.BurstCount == 0,
                    "Comparison mode must not fire attacks or reset the cooldown.");
            }
            finally
            {
                if (effect != null) Object.DestroyImmediate(effect.gameObject);
                Object.DestroyImmediate(owner);
            }
            // Existing gameplay fixture checks damage, cooldown, walls, pass-through and item synergies.
            Week23Passive4Verification.Verify();
            Debug.Log("Shasha realtime water prototype verification passed: shader, animated straight geometry, " +
                "world droplets, moved endpoints, cancellation, A/B cooldown preservation and existing stream rules. " +
                "Actual Play art comparison pending.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
