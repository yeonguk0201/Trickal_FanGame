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
                Require(effect != null, "Realtime water material is unavailable for the render probe.");
                foreach (Transform child in effect.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
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
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/art-review/shasha-water"));
                Directory.CreateDirectory(directory);
                void Capture(string file, Vector3 position, float size)
                {
                    camera.transform.position = position;
                    camera.orthographicSize = size;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 400), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(directory, file), image.EncodeToPNG());
                }
                Capture("unity-comparison.png", new Vector3(0, 0, -10), 1.1f);
                // All layers together: droplets and mist come from the shared fixed-step emission rules.
                original.enabled = false;
                effect.Begin(new Vector2(-2.75f, 0), new Vector2(2.75f, 0), 0.2f, 0, 0.12f, true);
                effect.ResetPreviewParticles();
                for (float t = 0; t < 0.06f; t += 1f / 60f)
                {
                    effect.EmitAt(t);
                    effect.Droplets.Simulate(1f / 60f, false, false, false);
                    effect.Mist.Simulate(1f / 60f, false, false, false);
                }
                effect.RenderAt(0.06f);
                Capture("unity-layers.png", new Vector3(0, 0, -10), 1.1f);
                Capture("unity-layers-nozzle.png", new Vector3(-2.1f, 0, -10), 0.4f);
                Capture("unity-layers-wall.png", new Vector3(2.1f, 0, -10), 0.4f);
                effect.SetPreviewLayers(false, false, false, true, false);
                Capture("unity-layer-nozzle-gush.png", new Vector3(-2.1f, 0, -10), 0.4f);
                effect.RenderAt(0.06f);
                effect.SetPreviewLayers(false, false, false, false, true);
                Capture("unity-layer-mist.png", new Vector3(0, 0, -10), 1.1f);
                Debug.Log("Shasha comparison exported: top original, bottom realtime body; unity-layers*.png add nozzle gush, " +
                    "droplets and mist at 0.06 s. Edit-mode probe; Play review separate.");
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
            // Gameplay fixtures replace the active Scene and can unload cached render resources.
            ExportComparison();
            Verify();
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
                Require(effect.Mist.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                    effect.Mist.main.maxParticles <= ProceduralWaterStream.MaxMist, "Mist must remain world-space and bounded.");
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
                        Require(Mathf.Abs(Vector2.Dot(center - from, side)) < 0.084f &&
                            along >= -0.003f && along <= length + 0.003f,
                            "Decorative waves must stay bounded around the hit path in every direction.");
                    }
                    Require(effect.SplashVisible, "Wall contact must display an independent foam fan.");
                    foreach (Vector3 vertex in effect.SplashMesh.vertices)
                        Require(Vector2.Dot((Vector2)vertex - effect.EndPoint, direction) <= 0.003f,
                            "Contact foam must stay on the incoming side of the wall plane.");
                    Require(effect.MuzzleVisible, "Jar streams must display an independent nozzle gush.");
                    foreach (Vector3 vertex in effect.MuzzleMesh.vertices)
                        Require(Vector2.Dot((Vector2)vertex - from, direction) >= -0.053f &&
                            Vector2.Dot((Vector2)vertex - effect.EndPoint, direction) <= 0.003f,
                            "Nozzle gush must stay between just behind the nozzle and the wall plane.");
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
                    Require(!effect.BodyVisible && !effect.SplashVisible && !effect.MuzzleVisible,
                        "Body, nozzle gush and contact foam must expire at the burst deadline.");
                }
                effect.Begin(from, from + Vector2.right * 3, 0.2f, 10, 0.12f, false);
                effect.RenderAt(10.04f);
                Require(!effect.SplashVisible, "Free endpoints must not pretend to hit a wall.");
                Vector3[] waveVertices = effect.BodyMesh.vertices;
                bool hasWave = false;
                for (int i = 2; i < waveVertices.Length - 2; i += 2)
                    hasWave |= Mathf.Abs(((waveVertices[i] + waveVertices[i + 1]) * 0.5f).y - from.y) > 0.01f;
                Require(hasWave, "The pumped stream must have a travelling center wave, not only changing width.");
                effect.Begin(from, from + Vector2.up * 3, 0.2f, 20, 0.12f, true);
                effect.ResetPreviewParticles();
                for (float t = 20; t < 20.12f; t += 1f / 60f)
                {
                    effect.EmitAt(t);
                    effect.Droplets.Simulate(1f / 60f, false, false, false);
                    effect.Mist.Simulate(1f / 60f, false, false, false);
                }
                Require(effect.Mist.particleCount > 0 && effect.Mist.particleCount <= ProceduralWaterStream.MaxMist &&
                    effect.Droplets.particleCount <= 96, "Wall bursts must shed bounded mist and droplets.");
                effect.Begin(from, from, 0.2f, 20, 0.12f, true);
                Require(!effect.BodyVisible, "Zero-length wall paths must not produce an inverted strip.");
                effect.Clear();
                Require(!effect.BodyVisible && !effect.SplashVisible && !effect.MuzzleVisible &&
                    effect.Droplets.particleCount == 0 && effect.Mist.particleCount == 0,
                    "Cancellation must clear body, nozzle gush, contact foam, droplets and mist.");
                Object.DestroyImmediate(effect.gameObject);
                effect = ProceduralWaterStream.Create(owner, true);
                effect.Begin(from, from + Vector2.right * 2, 0.2f, 30, 0.08f, false);
                effect.RenderAt(30.03f);
                Require(effect.BodyVisible && !effect.MuzzleVisible,
                    "Cutter/bounce links start on an enemy and must not show the jar's nozzle gush.");

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
            Debug.Log("Shasha realtime water prototype verification passed: shader, bounded travelling waves, contact fan, " +
                "nozzle gush, world droplets and mist, moved endpoints, cancellation, A/B cooldown preservation and existing stream rules. " +
                "Actual Play art comparison pending.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
