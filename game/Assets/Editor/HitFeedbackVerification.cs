using System;
using TrickalFanGame.Combat;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class HitFeedbackVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Hit Feedback")]
        public static void Verify()
        {
            VerifySpark();
            VerifyHitStop();
            VerifyCameraShake();
            Week20DevPanelVerification.Verify();
            Debug.Log("Hit feedback verification passed: the spark sprays back from the shot inside its radius, " +
                      "scrubs deterministically and expires; the hit stop restores the time scale, keeps its " +
                      "interval and yields to a pause; the camera shake stays weak and ends.");
        }

        private static void VerifySpark()
        {
            foreach (Vector2 incoming in new[] { Vector2.right, Vector2.left, new Vector2(0.6f, -0.8f) })
            {
                HitSpark effect = HitSpark.Create(new Vector3(3f, -2f, 0f), incoming, HitSpark.DefaultRadius);
                try
                {
                    Assert(effect != null, "Hit spark shader must load from Resources.");
                    Assert(!ShaderUtil.ShaderHasError(effect.Visual.sharedMaterial.shader),
                        "Hit spark shader must compile.");
                    effect.RenderAt(HitSpark.Duration * 0.5f);
                    Vector3[] vertices = effect.Mesh.vertices;
                    float farthest = 0f;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        farthest = Mathf.Max(farthest, vertices[i].magnitude);
                        // Streak tips: the first and last vertex of each streak. The flash is not a streak.
                        if (i < HitSpark.StreakCount * 4 && i % 4 is 0 or 3)
                            Assert(Vector2.Dot(vertices[i], incoming) <= 0.001f,
                                "Streaks must spray back from the shot, never through the enemy.");
                    }
                    Assert(farthest > HitSpark.DefaultRadius * 0.3f && farthest <= HitSpark.DefaultRadius * 1.05f,
                        "A spark must be visible and stay inside its radius.");

                    Vector3 sample = vertices[3];
                    effect.RenderAt(HitSpark.Duration * 0.9f);
                    effect.RenderAt(HitSpark.Duration * 0.5f);
                    Assert(effect.Mesh.vertices[3] == sample, "Scrubbing backward must restore the same geometry.");
                    effect.RenderAt(HitSpark.LeadTime);
                    Assert(effect.Visual.enabled && effect.Mesh.bounds.size.sqrMagnitude > 0.0001f,
                        "A spark must already be visible on the frame it is born.");
                    effect.RenderAt(HitSpark.Duration);
                    Assert(!effect.Visual.enabled, "An expired spark must be invisible.");
                }
                finally
                {
                    if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
                }
            }
        }

        private static void VerifyHitStop()
        {
            float originalTimeScale = Time.timeScale;
            var owner = new GameObject("Hit stop verification");
            try
            {
                Time.timeScale = 1f;
                HitStop.Play();
                Assert(Time.timeScale == 1f, "Hit stop must not touch the time scale outside Play Mode.");

                HitStop stop = owner.AddComponent<HitStop>();
                Assert(stop.TryBegin(10f) && Time.timeScale == HitStop.StoppedTimeScale && stop.IsStopped,
                    "A hit stop must slow time to its stopped scale.");
                Assert(HitStop.StoppedTimeScale > 0f, "The stopped scale must not read as a pause.");
                stop.Tick(10f + HitStop.Duration * 0.5f);
                Assert(stop.IsStopped && Time.timeScale == HitStop.StoppedTimeScale,
                    "A hit stop must last its whole duration.");
                stop.Tick(10f + HitStop.Duration);
                Assert(!stop.IsStopped && Time.timeScale == 1f, "A finished hit stop must restore normal time.");
                Assert(!stop.TryBegin(10f + HitStop.MinimumInterval * 0.5f) && Time.timeScale == 1f,
                    "Rapid critical hits must not chain hit stops.");

                // A pause that opens during the stop owns the time scale until it lets go.
                float second = 10f + HitStop.MinimumInterval;
                Assert(stop.TryBegin(second), "A hit stop must be available again after its interval.");
                Time.timeScale = 0f;
                stop.Tick(second + HitStop.Duration);
                Assert(Time.timeScale == 0f, "A hit stop must not end a pause.");
                Assert(!stop.TryBegin(second + HitStop.MinimumInterval), "A hit stop must not start while paused.");
                Time.timeScale = HitStop.StoppedTimeScale;
                stop.Tick(second + HitStop.Duration + 1f);
                Assert(Time.timeScale == 1f, "A pause that restores the stopped scale must not leave time slowed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                Time.timeScale = originalTimeScale;
            }
        }

        private static void VerifyCameraShake()
        {
            Assert(CameraShake.OffsetAt(0f).sqrMagnitude > 0f, "A camera shake must move the camera at its start.");
            for (float seconds = 0f; seconds < CameraShake.Duration; seconds += 0.005f)
                Assert(CameraShake.OffsetAt(seconds).magnitude <= CameraShake.Amplitude,
                    "A camera shake must stay within its weak amplitude.");
            Assert(CameraShake.OffsetAt(CameraShake.Duration) == Vector2.zero &&
                   CameraShake.OffsetAt(-0.01f) == Vector2.zero,
                "A camera shake must leave no offset before it starts or after it ends.");
            Assert(CameraShake.Amplitude <= 0.1f && CameraShake.Duration <= 0.2f,
                "The critical hit shake must stay weak and short.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
