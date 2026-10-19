using System;
using System.IO;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Resource;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Placed bomb fuse flash, embers, shockwave and scorch (Motion-0). Bomb damage rules stay covered by
    // Week20Special2Verification.
    public static class BombBurstVerification
    {
        private static readonly float[] ExportTimes =
            { 0.02f, 0.06f, 0.1f, 0.14f, 0.2f, 0.28f, 0.4f, 0.55f, 0.7f, 1f, 1.8f, 2.3f };
        // Fuse frames are drawn larger than in game so the flash reads in the review sheet.
        private const float FuseFrameWidth = 2f;

        public static void VerifyAndExportBatch()
        {
            // Gameplay fixtures replace the active Scene and can unload cached render resources.
            ExportFrames();
            Verify();
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Bomb Burst With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            // The ember mode lives in the shader 폭발 머핀 shares.
            MuffinExplosionVerification.Verify();
            FairyKingdomArtworkVerification.Verify();
            Week20Special2Verification.Verify();
            Debug.Log("Bomb burst regressions passed: embers and ring, muffin explosion on the shared shader, Fairy Kingdom artwork bindings and bomb rules.");
        }

        [MenuItem("Trickal Fan Game/Effects/Verify Bomb Burst")]
        public static void Verify()
        {
            VerifyFuseFlash();
            Material material = CombatEffectArtwork.Material("MuffinExplosion");
            Require(material != null && !ShaderUtil.ShaderHasError(material.shader),
                "The shared explosion shader is missing or failed compilation.");
            var sorting = new GameObject("Bomb burst sorting source").AddComponent<SpriteRenderer>();
            BombBurstEffect effect = null, twin = null;
            try
            {
                sorting.sortingOrder = 10;
                Require(BombBurstEffect.Play(Vector2.zero, 2f, null, sorting) == null,
                    "Edit Mode must not spawn runtime bomb effects.");
                Vector2 center = new(-7100f, 7100f);
                float radius = PlacedBomb.DefaultExplosionRadius;
                UnityEngine.Random.InitState(5151);
                UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
                effect = BombBurstEffect.Create(center, radius, sorting, 2);
                Require(effect != null, "Bomb burst effect must be created.");
                Require(effect.Ring.sortingOrder == 8 &&
                        effect.Embers.GetComponent<ParticleSystemRenderer>().sortingOrder == 9,
                    "The ring and then the embers must sit under the explosion drawing.");
                Require(effect.Embers.main.simulationSpace == ParticleSystemSimulationSpace.World &&
                        !effect.Embers.useAutoRandomSeed && effect.Embers.main.maxParticles <= 24 &&
                        !effect.Embers.emission.enabled,
                    "Embers must be world-space, fixed-seed, bounded and emitted only by code.");

                bool ringReachedRadius = false;
                for (float t = 0f; t <= BombBurstEffect.Duration + 0.0001f; t += 0.005f)
                {
                    effect.RenderAt(t);
                    Require(effect.RingOuterRadius <= radius + 0.0001f,
                        $"The ring must never pass the damage radius ({effect.RingOuterRadius:0.000} at {t:0.000}s).");
                    ringReachedRadius |= effect.RingOuterRadius >= radius * 0.97f;
                }
                Require(ringReachedRadius, "The ring must widen to the real damage radius.");
                effect.RenderAt(PlacedBomb.ExplosionVisualDuration + 0.02f);
                Require(effect.Ring.enabled, "The ring must still widen once the explosion drawing is gone.");

                var particles = new ParticleSystem.Particle[24];
                for (float t = 0.02f; t < BombBurstEffect.Duration; t += 0.04f)
                {
                    effect.SimulateTo(t);
                    int count = effect.Embers.GetParticles(particles);
                    for (int i = 0; i < count; i++)
                        Require(Vector2.Distance(particles[i].position, center) <=
                                radius * BombBurstEffect.EmberReach + 0.02f,
                            $"Embers must settle inside the damage radius ({Vector2.Distance(particles[i].position, center):0.00} at {t:0.00}s).");
                }
                effect.SimulateTo(PlacedBomb.ExplosionVisualDuration + 0.1f);
                Require(effect.Embers.particleCount == BombBurstEffect.EmberCount,
                    "Every ember must outlast the 0.15 s explosion drawing.");
                effect.SimulateTo(BombBurstEffect.Duration);
                Require(effect.Embers.particleCount == 0 && !effect.Ring.enabled,
                    "The ring and embers may not outlive the bomb burst duration.");

                Require(effect.Scorch.sortingOrder == BombBurstEffect.ScorchSortingOrder &&
                        effect.Scorch.sortingOrder > -100 && effect.Scorch.sortingOrder < -50,
                    "The scorch must lie on the floor: above room floors, under ground shadows.");
                Require(Mathf.Abs(effect.Scorch.transform.localScale.x) <= radius + 0.0001f,
                    "The scorch must stay inside the damage radius.");
                effect.RenderAt(0f);
                Require(!effect.Scorch.enabled, "The scorch must not show before the explosion.");
                effect.RenderAt(PlacedBomb.ExplosionVisualDuration);
                Require(effect.ScorchOpacityNow >= BombBurstEffect.ScorchOpacity * 0.85f,
                    "The scorch must already be there when the explosion drawing disappears.");
                effect.RenderAt(BombBurstEffect.ScorchFadeStart);
                Require(effect.Scorch.enabled && effect.ScorchOpacityNow >= BombBurstEffect.ScorchOpacity * 0.99f,
                    "The scorch must stay at full strength until it starts to fade.");
                effect.RenderAt((BombBurstEffect.ScorchFadeStart + BombBurstEffect.Lifetime) * 0.5f);
                Require(effect.ScorchOpacityNow < BombBurstEffect.ScorchOpacity,
                    "The scorch must fade out.");
                effect.RenderAt(BombBurstEffect.Lifetime);
                Require(!effect.Scorch.enabled, "Nothing may outlive the scorch lifetime.");

                effect.SimulateTo(0.3f);
                twin = BombBurstEffect.Create(center, radius, sorting, 2);
                twin.SimulateTo(0.3f);
                var second = new ParticleSystem.Particle[24];
                int firstCount = effect.Embers.GetParticles(particles), secondCount = twin.Embers.GetParticles(second);
                Require(firstCount == secondCount, "The same seed must emit the same embers.");
                for (int i = 0; i < firstCount; i++)
                    Require(Vector3.Distance(particles[i].position, second[i].position) < 0.0001f,
                        "The same seed must replay the same ember paths.");
                Require(randomBefore.Equals(UnityEngine.Random.state),
                    "The bomb burst must not consume UnityEngine.Random.");
            }
            finally
            {
                if (effect != null) Object.DestroyImmediate(effect.gameObject);
                if (twin != null) Object.DestroyImmediate(twin.gameObject);
                Object.DestroyImmediate(sorting.gameObject);
            }
            Debug.Log("Bomb burst verification passed: fuse flash speeds up and builds to a glow, shader, ring and " +
                      "embers under the drawing, ring stops at the damage radius, embers outlast the drawing and " +
                      "settle inside the radius, scorch lies on the floor inside the radius and fades by " +
                      $"{BombBurstEffect.Lifetime}s, fixed-seed replay without UnityEngine.Random. Actual Play art review pending.");
        }

        private static void VerifyFuseFlash()
        {
            foreach (float fuse in new[] { PlacedBomb.DefaultFuseDuration, 0.5f })
            {
                Require(BombFuseFlash.Intensity(0f, fuse) < 0.01f,
                    $"A {fuse}s fuse must start dark, so placing the bomb does not read as a flash.");
                Require(BombFuseFlash.Intensity(fuse, fuse) == 0f && BombFuseFlash.Intensity(-0.1f, fuse) == 0f,
                    "The flash must exist only while the fuse burns.");
                var starts = new System.Collections.Generic.List<float>();
                bool on = false;
                float early = 0f, late = 0f;
                int earlyCount = 0, lateCount = 0;
                const float step = 0.0005f;
                for (float t = 0f; t < fuse; t += step)
                {
                    float value = BombFuseFlash.Intensity(t, fuse);
                    Require(value <= BombFuseFlash.MaxOpacity + 0.0001f, "The flash must not exceed its opacity.");
                    bool now = value >= BombFuseFlash.MaxOpacity * 0.5f;
                    if (now && !on) starts.Add(t);
                    on = now;
                    if (t < fuse * 0.5f) { early += value; earlyCount++; }
                    else if (t >= fuse - 0.1f) { late += value; lateCount++; }
                }
                int minimum = fuse > 0.6f ? 5 : 3;
                Require(starts.Count >= minimum && starts.Count <= 10,
                    $"A {fuse}s fuse must blink {minimum}~10 times (got {starts.Count}).");
                for (int i = 2; i < starts.Count; i++)
                    Require(starts[i] - starts[i - 1] < starts[i - 1] - starts[i - 2] + 0.001f,
                        $"Blinks must come faster and faster ({fuse}s fuse, blink {i + 1}).");
                Require(late / Mathf.Max(1, lateCount) > early / Mathf.Max(1, earlyCount) * 1.3f,
                    "The bomb must look redder just before it explodes than early in the fuse.");
            }

            var bomb = new GameObject("Fuse flash probe").AddComponent<SpriteRenderer>();
            try
            {
                Require(BombFuseFlash.Attach(bomb, 0f, 0f) == null, "A bomb with no fuse must not flash.");
                bomb.sprite = FairyKingdomArtwork.Fit("pickup-bomb", 0.8f);
                Require(bomb.sprite != null, "The bomb drawing is missing for the fuse flash probe.");
                bomb.sortingOrder = 10;
                BombFuseFlash flash = BombFuseFlash.Attach(bomb, 0f, PlacedBomb.DefaultFuseDuration);
                Require(flash != null && flash.Overlay != null &&
                        flash.Overlay.sharedMaterial == CombatEffectArtwork.Material("BombFuseFlash"),
                    "The fuse flash must draw a red-hot copy of the bomb with its own shader.");
                Require(!ShaderUtil.ShaderHasError(flash.Overlay.sharedMaterial.shader),
                    "The fuse flash shader failed compilation.");
                Require(BombFuseFlash.Attach(bomb, 0f, PlacedBomb.DefaultFuseDuration) == flash &&
                        bomb.GetComponentsInChildren<SpriteRenderer>(true).Length == 2,
                    "Attaching twice must reuse the same overlay.");
                float nearEnd = PlacedBomb.DefaultFuseDuration - 0.01f;
                flash.RenderAt(nearEnd);
                var block = new MaterialPropertyBlock();
                flash.Overlay.GetPropertyBlock(block);
                Require(Mathf.Abs(block.GetFloat("_Strength") - BombFuseFlash.Intensity(nearEnd, PlacedBomb.DefaultFuseDuration)) < 0.0001f,
                    "The flash strength must reach the shader through the renderer property block.");
                Require(flash.Overlay.enabled && flash.Overlay.sprite == bomb.sprite &&
                        flash.Overlay.sortingOrder == bomb.sortingOrder + 1 &&
                        flash.Overlay.transform.localPosition == Vector3.zero,
                    "The flash must cover the bomb drawing exactly, one order above it.");
                flash.RenderAt(PlacedBomb.DefaultFuseDuration);
                Require(!flash.Overlay.enabled, "The flash must be off once the bomb explodes.");
                flash.Stop();
                Require(bomb.GetComponent<BombFuseFlash>() == null &&
                        bomb.GetComponentsInChildren<SpriteRenderer>(true).Length == 1,
                    "Stopping the flash must remove its overlay.");
            }
            finally
            {
                Object.DestroyImmediate(bomb.gameObject);
            }
        }

        // Top row: the explosion drawing for its 0.15 s plus the burst layers. Middle row: the burst layers alone.
        // Bottom row: the lit bomb across its fuse, evenly spaced, drawn larger than in game.
        [MenuItem("Trickal Fan Game/Effects/Export Bomb Burst Frames")]
        public static void ExportFrames()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            float radius = PlacedBomb.DefaultExplosionRadius, cell = radius * 2.4f;
            int columns = ExportTimes.Length;
            const int rows = 3;
            var owners = new GameObject("Bomb burst render probe");
            Camera camera = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owners, scene);
                Sprite drawing = FairyKingdomArtwork.Fit("effect-bomb-explosion", radius * 2f);
                Sprite bombDrawing = FairyKingdomArtwork.Fit("pickup-bomb", FuseFrameWidth);
                for (int column = 0; column < columns; column++)
                {
                    float time = ExportTimes[column];
                    var lit = new GameObject("Lit bomb").AddComponent<SpriteRenderer>();
                    lit.transform.SetParent(owners.transform);
                    lit.transform.position = new Vector2((column - (columns - 1) * 0.5f) * cell, -cell);
                    lit.sprite = bombDrawing;
                    lit.sortingOrder = 5;
                    float fuse = PlacedBomb.DefaultFuseDuration;
                    BombFuseFlash flash = BombFuseFlash.Attach(lit, 0f, fuse);
                    Require(flash != null, "Fuse flash material is unavailable for the render probe.");
                    flash.RenderAt(fuse * column / columns);
                    for (int row = 0; row < 2; row++)
                    {
                        Vector2 center = new((column - (columns - 1) * 0.5f) * cell, row == 0 ? cell : 0f);
                        var art = new GameObject("Explosion drawing").AddComponent<SpriteRenderer>();
                        art.transform.SetParent(owners.transform);
                        art.transform.position = center;
                        art.sprite = drawing;
                        art.sortingOrder = 5;
                        art.enabled = row == 0 && time < PlacedBomb.ExplosionVisualDuration;
                        BombBurstEffect effect = BombBurstEffect.Create(center, radius, art, 4);
                        Require(effect != null, "Bomb burst material is unavailable for the render probe.");
                        effect.transform.SetParent(owners.transform, true);
                        effect.SimulateTo(time);
                    }
                }
                foreach (Transform child in owners.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                camera = new GameObject("Bomb burst render camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.orthographic = true;
                camera.orthographicSize = cell * rows * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.8f, 0.84f, 0.72f);
                int width = Mathf.RoundToInt(columns * cell * 50f), height = Mathf.RoundToInt(cell * rows * 50f);
                target = new RenderTexture(width, height, 24);
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../output/art-review/bomb-burst"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, "unity-frames.png"), image.EncodeToPNG());
                Debug.Log("Bomb burst frames exported: top with the explosion drawing, middle burst layers alone, at " +
                          string.Join(", ", ExportTimes) + $" s; bottom the {PlacedBomb.DefaultFuseDuration}s fuse in " +
                          $"{columns} even steps. Edit-mode probe; Play review separate.");
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
