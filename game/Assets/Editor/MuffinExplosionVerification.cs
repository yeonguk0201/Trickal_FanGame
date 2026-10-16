using System;
using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // 폭발 머핀's realtime explosion (Motion-0). Damage rules stay covered by Week23Artifact2Verification.
    public static class MuffinExplosionVerification
    {
        private const string OriginalSheetGuid = "6e7841888404471bb27795882e5e39b3";
        private static readonly float[] ExportTimes = { 0.01f, 0.03f, 0.06f, 0.1f, 0.15f, 0.2f, 0.26f, 0.32f, 0.38f };

        public static void VerifyAndExportBatch()
        {
            // Gameplay fixtures replace the active Scene and can unload cached render resources.
            ExportFrames();
            Verify();
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Muffin Explosion With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            ReviewedCombatEffectsVerification.Verify();
            Week23Artifact2Verification.Verify();
            Debug.Log("Muffin explosion regressions passed: realtime effect, reviewed effect sheets and 폭발 머핀 damage rules.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Muffin Explosion")]
        public static void Verify()
        {
            Material material = CombatEffectArtwork.Material("MuffinExplosion");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "Muffin explosion shader is missing or failed compilation.");
            Require(AssetDatabase.AssetPathToGUID("Assets/Resources/CombatEffects/circular-explosion.png") ==
                    OriginalSheetGuid && CombatEffectArtwork.Frames("circular-explosion", 4, 2).Length == 8,
                "circular-explosion.png and its GUID must stay for the other explosions that still use it.");

            var sorting = new GameObject("Muffin explosion sorting source").AddComponent<SpriteRenderer>();
            MuffinExplosionEffect effect = null, twin = null;
            try
            {
                sorting.sortingOrder = 10;
                Require(MuffinExplosionEffect.Play(Vector2.zero, 1.5f, sorting.transform, sorting) == null,
                    "Edit Mode must not spawn runtime explosions.");
                Vector2 center = new(-7300f, 7300f);
                const float radius = 1.5f;
                UnityEngine.Random.InitState(4242);
                UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
                effect = MuffinExplosionEffect.Create(center, radius, sorting, 3);
                Require(effect != null, "Muffin explosion effect must be created.");
                Require(effect.Smoke.GetComponent<ParticleSystemRenderer>().sortingOrder == 11 &&
                        effect.Ring.sortingOrder == 12 && effect.Fireball.sortingOrder == 13 &&
                        effect.Sparks.GetComponent<ParticleSystemRenderer>().sortingOrder == 14 &&
                        effect.Flash.sortingOrder == 15,
                    "Layers must sort above the source: smoke, ring, fireball, sparks, then the flash.");
                foreach (ParticleSystem system in new[] { effect.Sparks, effect.Smoke })
                    Require(system.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                            !system.useAutoRandomSeed && system.main.maxParticles <= 16 && !system.emission.enabled,
                        "Sparks and smoke must be world-space, fixed-seed, bounded and emitted only by code.");

                bool ringReachedRadius = false;
                for (float t = 0f; t <= MuffinExplosionEffect.Duration + 0.0001f; t += 0.005f)
                {
                    effect.RenderAt(t);
                    Require(effect.RingOuterRadius <= radius + 0.0001f,
                        $"The ring must never pass the damage radius ({effect.RingOuterRadius:0.000} at {t:0.000}s).");
                    Require(effect.FireballHalfSize <= radius * MuffinExplosionEffect.FireballReach + 0.0001f,
                        "The fireball must stay a small core inside the damage radius.");
                    ringReachedRadius |= effect.RingOuterRadius >= radius * 0.97f;
                }
                Require(ringReachedRadius, "The ring must expand to the real damage radius.");
                effect.RenderAt(0.02f);
                Require(effect.Flash.enabled && effect.Fireball.enabled && effect.Ring.enabled,
                    "Flash, fireball and ring must all show at the moment of impact.");
                effect.RenderAt(MuffinExplosionEffect.FlashEnd);
                Require(!effect.Flash.enabled, "The core flash must stay a short pop.");
                effect.RenderAt(0.2f);
                Require(effect.HoleRadius >= 0.4f,
                    "The fireball must hollow from the center well before the end so the enemy stays readable.");
                effect.RenderAt(MuffinExplosionEffect.FireballEnd);
                Require(!effect.Fireball.enabled, "The fireball must be gone before the smoke tail.");

                var particles = new ParticleSystem.Particle[16];
                for (float t = 0.02f; t < MuffinExplosionEffect.Duration; t += 0.02f)
                {
                    effect.SimulateTo(t);
                    int count = effect.Sparks.GetParticles(particles);
                    for (int i = 0; i < count; i++)
                        Require(Vector2.Distance(particles[i].position, center) <=
                                radius * MuffinExplosionEffect.SparkReach + 0.01f,
                            "Sparks must stay inside the damage radius so decoration is not read as range.");
                }
                effect.SimulateTo(0.1f);
                Require(effect.Sparks.particleCount == MuffinExplosionEffect.SparkCount &&
                        effect.Smoke.particleCount == MuffinExplosionEffect.SmokeCount,
                    "Sparks burst at impact and the smoke follows the fireball: " +
                    $"sparks {effect.Sparks.particleCount}, smoke {effect.Smoke.particleCount}.");
                effect.SimulateTo(MuffinExplosionEffect.Duration);
                Require(effect.Sparks.particleCount == 0 && effect.Smoke.particleCount == 0 &&
                        !effect.Flash.enabled && !effect.Fireball.enabled && !effect.Ring.enabled,
                    "Nothing may outlive the 0.4 second play time.");

                effect.SimulateTo(0.12f);
                twin = MuffinExplosionEffect.Create(center, radius, sorting, 3);
                twin.SimulateTo(0.12f);
                var first = new ParticleSystem.Particle[16];
                var second = new ParticleSystem.Particle[16];
                int firstCount = effect.Sparks.GetParticles(first), secondCount = twin.Sparks.GetParticles(second);
                Require(firstCount == secondCount, "The same seed must emit the same sparks.");
                for (int i = 0; i < firstCount; i++)
                    Require(Vector3.Distance(first[i].position, second[i].position) < 0.0001f,
                        "The same seed must replay the same spark paths.");
                Require(randomBefore.Equals(UnityEngine.Random.state), "The explosion must not consume UnityEngine.Random.");
            }
            finally
            {
                if (effect != null) Object.DestroyImmediate(effect.gameObject);
                if (twin != null) Object.DestroyImmediate(twin.gameObject);
                Object.DestroyImmediate(sorting.gameObject);
            }
            Debug.Log("Muffin explosion verification passed: shader, layer order, ring stops at the damage radius, " +
                      "small fireball hollows by 0.2s, sparks stay inside the radius, nothing outlives 0.4s, " +
                      "fixed-seed replay without UnityEngine.Random, original sheet GUID kept. Actual Play art review pending.");
        }

        // A still-frame sheet for art review: top row the new effect, bottom row the former sheet, same times and size.
        [MenuItem("Trickal Fan Game/Effects/Export Muffin Explosion Frames")]
        public static void ExportFrames()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            const float cell = 3.6f, radius = 1.5f;
            int columns = ExportTimes.Length;
            var owners = new GameObject("Muffin explosion render probe");
            Camera camera = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owners, scene);
                Sprite enemySprite = FairyKingdomArtwork.Fit("crumb-minion-cream", 1f);
                Sprite[] originalFrames = CombatEffectArtwork.Frames("circular-explosion", 4, 2);
                for (int column = 0; column < columns; column++)
                {
                    float x = (column - (columns - 1) * 0.5f) * cell;
                    float time = ExportTimes[column];
                    for (int row = 0; row < 2; row++)
                    {
                        Vector2 center = new(x, row == 0 ? cell * 0.5f : -cell * 0.5f);
                        var enemy = new GameObject("Enemy").AddComponent<SpriteRenderer>();
                        enemy.transform.SetParent(owners.transform);
                        enemy.transform.position = center;
                        enemy.sprite = enemySprite;
                        if (row == 0)
                        {
                            MuffinExplosionEffect effect = MuffinExplosionEffect.Create(center, radius, enemy, 7);
                            Require(effect != null, "Muffin explosion material is unavailable for the render probe.");
                            effect.transform.SetParent(owners.transform, true);
                            effect.SimulateTo(time);
                        }
                        else if (originalFrames.Length > 0)
                        {
                            var original = new GameObject("Original").AddComponent<SpriteRenderer>();
                            original.transform.SetParent(owners.transform);
                            original.transform.position = center;
                            original.sortingOrder = 2;
                            original.sprite = originalFrames[Mathf.Min(originalFrames.Length - 1,
                                (int)(time / MuffinExplosionEffect.Duration * originalFrames.Length))];
                            CombatEffectArtwork.Size(original, radius * 2.2f, radius * 2.2f);
                        }
                    }
                }
                foreach (Transform child in owners.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                camera = new GameObject("Muffin explosion render camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = cell;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.8f, 0.84f, 0.72f);
                int width = Mathf.RoundToInt(columns * cell * 70f), height = Mathf.RoundToInt(cell * 2f * 70f);
                target = new RenderTexture(width, height, 24);
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../output/art-review/muffin-explosion"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "unity-frames.png"), image.EncodeToPNG());
                Debug.Log("Muffin explosion frames exported: top realtime, bottom former sheet, at " +
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
