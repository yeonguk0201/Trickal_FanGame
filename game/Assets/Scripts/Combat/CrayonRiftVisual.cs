using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Unconnected visual prototype. One explicit clock drives all layers; no damage, Update or gameplay RNG.
    public sealed class CrayonRiftVisual : MonoBehaviour
    {
        public const float WarningSeconds = 0.35f;
        public const float TravelSeconds = 0.17f;
        public const float ArrivalDelay = 0.1f;
        public const float StrikeTime = WarningSeconds + TravelSeconds + ArrivalDelay;
        public const float Duration = StrikeTime + 0.48f;
        public const float DefaultLength = 6.2f;
        public const float DefaultWidth = 2.4f;
        public const float DefaultWarningLengthRatio = 0.56f;
        public const float DefaultWarningWidthRatio = 0.20f;
        private readonly MeshRenderer[] layers = new MeshRenderer[5];
        private readonly Material[] materials = new Material[5];
        private Mesh mesh;

        public static CrayonRiftVisual Create(Texture2D artwork)
        {
            if (artwork == null) throw new System.ArgumentNullException(nameof(artwork));
            Shader shader = Resources.Load<Shader>("CombatEffects/CrayonRift");
            if (shader == null) throw new System.InvalidOperationException("Missing CrayonRift shader.");
            var root = new GameObject("Crayon rift visual prototype") { hideFlags = HideFlags.HideAndDontSave };
            var effect = root.AddComponent<CrayonRiftVisual>();
            effect.mesh = new Mesh { name = "Rift quad", hideFlags = HideFlags.HideAndDontSave };
            effect.mesh.vertices = new[] { new Vector3(-0.5f,-0.5f), new Vector3(0.5f,-0.5f),
                new Vector3(0.5f,0.5f), new Vector3(-0.5f,0.5f) };
            effect.mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            effect.mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            effect.mesh.RecalculateBounds();
            for (int i=0; i<effect.layers.Length; i++)
            {
                var child = new GameObject(new[] { "Small translucent red warning", "Thin black incision",
                    "Concept artwork silver blade", "Instant needle flash", "Straight silver splinters" }[i]);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
                effect.materials[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                effect.materials[i].SetFloat("_Layer", i);
                effect.materials[i].SetTexture("_BladeArtwork", artwork);
                effect.layers[i] = child.AddComponent<MeshRenderer>();
                effect.layers[i].sharedMaterial = effect.materials[i];
                effect.layers[i].sortingOrder = 100+i;
                effect.layers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                effect.layers[i].receiveShadows = false;
            }
            effect.RenderAt(0);
            return effect;
        }

        public void Configure(Vector2 center, float angle, float length, float width,
            float warningLengthRatio = DefaultWarningLengthRatio, float warningWidthRatio = DefaultWarningWidthRatio)
        {
            transform.SetPositionAndRotation(center, Quaternion.Euler(0,0,angle));
            transform.localScale = new Vector3(Mathf.Max(0.1f,length),Mathf.Max(0.1f,width),1);
            layers[0].transform.localScale = new Vector3(Mathf.Clamp(warningLengthRatio,0.1f,0.9f),
                Mathf.Clamp(warningWidthRatio,0.05f,0.6f),1);
        }

        public void RenderAt(float time, bool warning = true, bool body = true, bool light = true,
            bool flash = true, bool fragments = true)
        {
            layers[0].enabled = warning && time >= 0 && time < StrikeTime;
            layers[1].enabled = body && time >= StrikeTime && time < Duration;
            layers[2].enabled = light && time >= StrikeTime && time < Duration;
            layers[3].enabled = flash && time >= StrikeTime && time < Duration;
            layers[4].enabled = fragments && time >= StrikeTime && time < Duration;
            foreach (Material material in materials) material.SetFloat("_Age", Mathf.Max(0,time-StrikeTime));
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Release(material);
            Release(mesh);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
