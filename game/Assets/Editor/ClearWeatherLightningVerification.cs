using System;
using TrickalFanGame.Combat;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    public static class ClearWeatherLightningVerification
    {
        [MenuItem("Trickal Fan Game/Effects/Verify Clear Weather Lightning")]
        public static void Verify()
        {
            var cameraObject = new GameObject("Lightning verification camera");
            ClearWeatherLightning effect = null;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.transform.position = new Vector3(2, 3, -10);
                foreach (Vector3 target in new[] { new Vector3(-2, -1, 0), new Vector3(4, 7.8f, 0) })
                {
                    Vector3 top = ClearWeatherLightning.ScreenTop(camera, target);
                    Assert(camera.WorldToViewportPoint(top).y > 1 && Mathf.Abs(top.x - target.x) < 0.001f,
                        "Lightning must start above the viewport, aligned with its target.");
                    effect = ClearWeatherLightning.Create(top, target, ClearWeatherLightning.DefaultWidth);
                    Assert(effect != null, "Lightning shader must load from Resources.");
                    Assert(!ShaderUtil.ShaderHasError(effect.Bolt.sharedMaterial.shader), "Lightning shader must compile.");
                    effect.RenderAt(0.009f);
                    Assert(effect.Bolt.GetPosition(effect.Bolt.positionCount - 1).y > target.y,
                        "Reveal must descend from the top, not grow upward from the target.");
                    effect.RenderAt(0.04f);
                    Assert(Vector3.Distance(effect.Bolt.GetPosition(0), top) < 0.001f &&
                        Vector3.Distance(effect.Bolt.GetPosition(effect.Bolt.positionCount - 1), target) < 0.001f,
                        "Full strike must connect screen top to the exact impact point.");
                    Vector3 point = effect.Bolt.GetPosition(1);
                    effect.RenderAt(0.15f);
                    effect.RenderAt(0.04f);
                    Assert(effect.Bolt.GetPosition(1) == point, "Scrubbing backward must restore the same geometry.");
                    effect.RenderAt(ClearWeatherLightning.Duration);
                    Assert(!effect.Bolt.enabled, "Expired lightning must be invisible.");
                    UnityEngine.Object.DestroyImmediate(effect.gameObject);
                    effect = null;
                }
                Week16Artifact1Verification.Verify();
                Debug.Log("Clear Weather Lightning verification passed: viewport origin, downward reveal, impact, deterministic scrub, expiry and card damage rules.");
            }
            finally
            {
                if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
