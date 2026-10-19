using System;
using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Crumb minion death blast pink burst (Motion-0). Blast damage rules stay covered by Week25Enemy7Verification.
    public static class CrumbDeathBlastVerification
    {
        // The crumb minion's picture is one unit wide; EnemyDeathBlast shows the drawing at 1.2 x its width.
        public const float ArtWidth = 1.2f;
        private const string FormerSheetGuidPath = "Assets/Resources/CombatEffects/circular-explosion.png";
        private static readonly float[] ExportTimes = { 0.01f, 0.04f, 0.08f, 0.13f, 0.19f, 0.26f, 0.34f, 0.44f, 0.55f };

        public static void VerifyAndExportBatch()
        {
            // Gameplay fixtures replace the active Scene and can unload cached render resources.
            ExportFrames();
            Verify();
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Crumb Death Blast With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            FairyKingdomArtworkVerification.Verify();
            Week25Enemy7Verification.Verify();
            Debug.Log("Crumb death blast regressions passed: pink burst, Fairy Kingdom artwork bindings and Enemy-7 blast rules.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Crumb Death Blast")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("CrumbDeathBlast");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "The crumb death blast shader is missing or failed compilation.");
            Require(AssetDatabase.LoadAssetAtPath<Texture2D>(FormerSheetGuidPath) != null,
                "circular-explosion.png must stay for the homing projectile.");
            Require(FairyKingdomArtwork.Fit(EnemyDeathBlast.BodyBurstArtwork, ArtWidth) != null,
                "The pink effect-crumb-death drawing the burst surrounds must stay available.");
            var sorting = new GameObject("Crumb blast sorting source").AddComponent<SpriteRenderer>();
            CrumbDeathBlastEffect effect = null, twin = null;
            try
            {
                sorting.sortingOrder = 10;
                Require(CrumbDeathBlastEffect.Play(Vector2.zero, 1.2f, null, sorting) == null,
                    "Edit Mode must not spawn runtime crumb blast effects.");
                Vector2 center = new(-7300f, 7300f);
                float radius = EnemyDeathExplosion.DefaultRadius;
                UnityEngine.Random.InitState(5353);
                UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
                effect = CrumbDeathBlastEffect.Create(center, radius, sorting, 2);
                Require(effect != null, "Crumb death blast effect must be created.");
                // The drawing sits at body + 3 (FairyKingdomSpriteEffect).
                Require(effect.Smoke.GetComponent<ParticleSystemRenderer>().sortingOrder == 10 &&
                        effect.Ring.sortingOrder == 11 && effect.Puff.sortingOrder == 12 &&
                        effect.Crumbs.GetComponent<ParticleSystemRenderer>().sortingOrder == 14 &&
                        effect.Droplets.GetComponent<ParticleSystemRenderer>().sortingOrder == 15 &&
                        effect.Flash.sortingOrder == 16,
                    "Smoke, ring and cloud must sit under the pink drawing; crumbs, droplets and the flash over it.");
                foreach (ParticleSystem system in new[] { effect.Droplets, effect.Crumbs, effect.Smoke })
                    Require(system.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                            !system.useAutoRandomSeed && system.main.maxParticles <= 16 && !system.emission.enabled,
                        $"{system.name} must be world-space, fixed-seed, bounded and emitted only by code.");

                bool ringReachedRadius = false, hollowedEarly = false;
                for (float t = 0f; t <= CrumbDeathBlastEffect.Duration + 0.0001f; t += 0.005f)
                {
                    effect.RenderAt(t);
                    Require(effect.RingOuterRadius <= radius + 0.0001f,
                        $"The ring must never pass the damage radius ({effect.RingOuterRadius:0.000} at {t:0.000}s).");
                    Require(effect.PuffHalfSize <= radius * CrumbDeathBlastEffect.PuffReach + 0.0001f,
                        "The pink cloud must stay well inside the damage radius.");
                    ringReachedRadius |= effect.RingOuterRadius >= radius * 0.97f;
                    if (t >= 0.24f && t < 0.25f) hollowedEarly = effect.Puff.enabled && effect.HoleRadius >= 0.4f;
                }
                Require(ringReachedRadius, "The ring must widen to the real damage radius.");
                Require(hollowedEarly, "The pink cloud must open from the center well before it ends.");
                effect.RenderAt(EnemyDeathBlast.BodyBurstDuration + 0.02f);
                Require(effect.Ring.enabled && effect.Puff.enabled,
                    "The ring and cloud must still show once the 0.24 s drawing is gone.");

                var particles = new ParticleSystem.Particle[16];
                for (float t = 0.02f; t < CrumbDeathBlastEffect.Duration; t += 0.03f)
                {
                    effect.SimulateTo(t);
                    foreach (ParticleSystem system in new[] { effect.Droplets, effect.Crumbs })
                    {
                        int count = system.GetParticles(particles);
                        for (int i = 0; i < count; i++)
                            Require(Vector2.Distance(particles[i].position, center) <=
                                    radius * CrumbDeathBlastEffect.ParticleReach + 0.02f,
                                $"{system.name} must stay inside the damage radius " +
                                $"({Vector2.Distance(particles[i].position, center):0.00} at {t:0.00}s).");
                    }
                }
                effect.SimulateTo(EnemyDeathBlast.BodyBurstDuration + 0.04f);
                Require(effect.Droplets.particleCount == CrumbDeathBlastEffect.DropletCount &&
                        effect.Crumbs.particleCount == CrumbDeathBlastEffect.CrumbCount &&
                        effect.Smoke.particleCount == CrumbDeathBlastEffect.SmokeCount,
                    "Droplets, crumbs and smoke must outlast the pink drawing.");
                effect.SimulateTo(CrumbDeathBlastEffect.Duration);
                Require(effect.Droplets.particleCount == 0 && effect.Crumbs.particleCount == 0 &&
                        effect.Smoke.particleCount == 0 && !effect.Ring.enabled && !effect.Puff.enabled &&
                        !effect.Flash.enabled,
                    "Nothing may outlive the crumb death blast duration.");

                effect.SimulateTo(0.3f);
                twin = CrumbDeathBlastEffect.Create(center, radius, sorting, 2);
                twin.SimulateTo(0.3f);
                var second = new ParticleSystem.Particle[16];
                int firstCount = effect.Droplets.GetParticles(particles), secondCount = twin.Droplets.GetParticles(second);
                Require(firstCount == secondCount, "The same seed must emit the same droplets.");
                for (int i = 0; i < firstCount; i++)
                    Require(Vector3.Distance(particles[i].position, second[i].position) < 0.0001f,
                        "The same seed must replay the same droplet paths.");
                Require(randomBefore.Equals(UnityEngine.Random.state),
                    "The crumb death blast must not consume UnityEngine.Random.");
            }
            finally
            {
                if (effect != null) Object.DestroyImmediate(effect.gameObject);
                if (twin != null) Object.DestroyImmediate(twin.gameObject);
                Object.DestroyImmediate(sorting.gameObject);
            }
            Debug.Log("Crumb death blast verification passed: shader, layers around the pink drawing, ring stops at " +
                      "the damage radius, cloud opens from the center, droplets and crumbs stay inside the radius and " +
                      "outlast the drawing, nothing outlives 0.6s, fixed-seed replay without UnityEngine.Random, " +
                      "circular-explosion kept. Actual Play art review pending.");
        }

        // Mirrors FairyKingdomSpriteEffect: the drawing fades over the last 40% of its 0.24 s.
        public static void ShowDrawing(SpriteRenderer drawing, bool show, float seconds)
        {
            float lifetime = EnemyDeathBlast.BodyBurstDuration;
            drawing.enabled = show && seconds < lifetime;
            drawing.color = new Color(1f, 1f, 1f, Mathf.Clamp01((lifetime - seconds) / (lifetime * 0.4f)));
        }

        // Top row: the pink drawing for its 0.24 s plus the new layers. Bottom row: the new layers alone.
        [MenuItem("Trickal Fan Game/Effects/Export Crumb Death Blast Frames")]
        public static void ExportFrames()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            float radius = EnemyDeathExplosion.DefaultRadius, cell = radius * 2.4f;
            int columns = ExportTimes.Length;
            var owners = new GameObject("Crumb death blast render probe");
            Camera camera = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owners, scene);
                Sprite art = FairyKingdomArtwork.Fit(EnemyDeathBlast.BodyBurstArtwork, ArtWidth);
                for (int column = 0; column < columns; column++)
                {
                    float time = ExportTimes[column];
                    for (int row = 0; row < 2; row++)
                    {
                        Vector2 center = new((column - (columns - 1) * 0.5f) * cell, row == 0 ? cell * 0.5f : -cell * 0.5f);
                        var body = new GameObject("Body sorting").AddComponent<SpriteRenderer>();
                        body.transform.SetParent(owners.transform);
                        body.sortingOrder = 5;
                        var drawing = new GameObject("Pink drawing").AddComponent<SpriteRenderer>();
                        drawing.transform.SetParent(owners.transform);
                        drawing.transform.position = center;
                        drawing.sprite = art;
                        drawing.sortingOrder = body.sortingOrder + 3;
                        ShowDrawing(drawing, row == 0, time);
                        CrumbDeathBlastEffect effect = CrumbDeathBlastEffect.Create(center, radius, body, 4);
                        Require(effect != null, "Crumb death blast material is unavailable for the render probe.");
                        effect.transform.SetParent(owners.transform, true);
                        effect.SimulateTo(time);
                    }
                }
                foreach (Transform child in owners.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                camera = new GameObject("Crumb death blast render camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = cell;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.8f, 0.84f, 0.72f);
                int width = Mathf.RoundToInt(columns * cell * 100f), height = Mathf.RoundToInt(cell * 2f * 100f);
                target = new RenderTexture(width, height, 24);
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../output/art-review/crumb-death-blast"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "unity-frames.png"), image.EncodeToPNG());
                Debug.Log("Crumb death blast frames exported: top with the pink drawing, bottom new layers alone, at " +
                          string.Join(", ", ExportTimes) + " s. Edit-mode probe; Play review separate.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (image != null) Object.DestroyImmediate(image);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(owners);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
