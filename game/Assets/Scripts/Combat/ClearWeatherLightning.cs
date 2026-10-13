using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Combat
{
    // Visual only: deterministic geometry never consumes gameplay Random or schedules damage.
    public sealed class ClearWeatherLightning : MonoBehaviour
    {
        public const float Duration = 0.24f;
        public const float DefaultWidth = 0.075f;
        private LineRenderer bolt;
        private Material material;
        private Vector3 start, end;
        private float age, width;
        private GameObject source;
        private Health sourceHealth;
        private bool runtime;
        public Vector3 StartPoint => start;
        public Vector3 EndPoint => end;
        public LineRenderer Bolt => bolt;

        public static Vector3 ScreenTop(Camera camera, Vector3 target)
        {
            if (camera == null) return target + Vector3.up * 12f;
            Vector3 viewport = camera.WorldToViewportPoint(target);
            return camera.ViewportToWorldPoint(new Vector3(viewport.x, 1.03f, viewport.z));
        }

        public static void Play(Health target, GameObject owner)
        {
            if (!Application.isPlaying || target == null) return;
            Vector3 impact = target.transform.position + Vector3.up * 0.35f;
            var effect = Create(ScreenTop(Camera.main, impact), impact, DefaultWidth,
                target.GetComponentInChildren<SpriteRenderer>());
            if (effect == null) return;
            SceneManager.MoveGameObjectToScene(effect.gameObject, target.gameObject.scene);
            effect.source = owner;
            effect.sourceHealth = owner != null ? owner.GetComponent<Health>() : null;
            effect.runtime = true;
        }

        public static ClearWeatherLightning Create(Vector3 from, Vector3 to, float thickness,
            Renderer sorting = null)
        {
            Shader shader = Resources.Load<Shader>("CombatEffects/ClearWeatherLightning");
            if (shader == null) return null;
            var root = new GameObject("Clear weather lightning");
            var effect = root.AddComponent<ClearWeatherLightning>();
            effect.material = new Material(shader);
            effect.bolt = root.AddComponent<LineRenderer>();
            effect.bolt.sharedMaterial = effect.material;
            effect.bolt.useWorldSpace = true;
            effect.bolt.alignment = LineAlignment.View;
            effect.bolt.textureMode = LineTextureMode.Stretch;
            effect.bolt.numCornerVertices = 2;
            effect.bolt.sortingLayerID = sorting != null ? sorting.sortingLayerID : 0;
            effect.bolt.sortingOrder = (sorting != null ? sorting.sortingOrder : 0) + 3;
            effect.Configure(from, to, thickness);
            effect.RenderAt(0.04f);
            return effect;
        }

        public void Configure(Vector3 from, Vector3 to, float thickness)
        {
            start = from; end = to; width = Mathf.Max(0.005f, thickness);
        }

        public void RenderAt(float seconds)
        {
            bolt.enabled = seconds >= 0 && seconds < Duration;
            if (!bolt.enabled) return;
            // Reveal from the screen edge in 18ms; length never scales the thin core width.
            float reveal = Mathf.Clamp01(seconds / 0.018f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, Duration, seconds));
            float pulse = seconds < 0.085f ? 1f : 0.72f + 0.28f * Mathf.Cos(seconds * 110f);
            material.SetFloat("_Opacity", fade * pulse);
            bolt.widthMultiplier = width;
            int count = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, end) / 0.3f), 3, 80);
            bolt.positionCount = count + 1;
            Vector3 sideways = Vector3.Cross((end - start).normalized, Vector3.forward);
            int phase = Mathf.FloorToInt(seconds / 0.045f);
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count * reveal;
                float hash = Mathf.Sin(i * 73.17f + phase * 19.31f) * 437.13f;
                float zig = (hash - Mathf.Floor(hash) - 0.5f) * 0.32f;
                float taper = Mathf.Sin(t * Mathf.PI);
                bolt.SetPosition(i, Vector3.Lerp(start, end, t) + sideways * (zig * taper));
            }
        }

        private void Update()
        {
            if (!runtime) return;
            age += Time.deltaTime;
            if (age >= Duration || source == null || !source.activeInHierarchy ||
                (sourceHealth != null && sourceHealth.IsDead))
            { Destroy(gameObject); return; }
            RenderAt(age);
        }

        private void OnDestroy() => CombatEffectArtwork.Release(material);
    }
}
