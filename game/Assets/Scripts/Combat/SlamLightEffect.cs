using UnityEngine;

namespace TrickalFanGame.Combat
{
    public sealed class SlamLightEffect : MonoBehaviour
    {
        public const float FallSeconds = 0.1f;
        public const float PeakHoldSeconds = 0.7f;
        public const float FadeSeconds = 0.22f;
        public const float Duration = FallSeconds + PeakHoldSeconds + FadeSeconds;
        public const float VerticalOffset = -0.25f;
        public const float RoomInset = 0.1f;
        public const float EdgeFadeDistance = 0.8f;
        private Transform source;
        private Vector3 previousSourcePosition;
        private float age;
        private Mesh mesh;
        private MeshRenderer visual;
        private MaterialPropertyBlock properties;

        public static float StrengthAt(float seconds)
        {
            if (seconds < 0f || seconds >= Duration) return 0f;
            if (seconds < FallSeconds) return Mathf.Clamp01(seconds / FallSeconds);
            return seconds <= FallSeconds + PeakHoldSeconds ? 1f :
                1f - (seconds - FallSeconds - PeakHoldSeconds) / FadeSeconds;
        }

        public static SlamLightEffect Play(Vector2 center, float length, float height, float angle,
            Transform source, Renderer sortingSource, Rect bounds, bool reverse = false)
        {
            if (!Application.isPlaying || source == null) return null;
            Material material = CombatEffectArtwork.Material("SlamLight");
            Texture2D artwork = Resources.Load<Texture2D>("CombatEffects/slam-light");
            if (material == null || artwork == null) return null;
            var owner = new GameObject("VFX slam light");
            owner.transform.SetPositionAndRotation(center, Quaternion.Euler(0, 0, angle));
            var effect = owner.AddComponent<SlamLightEffect>();
            effect.source = source;
            effect.previousSourcePosition = source.position;
            effect.mesh = new Mesh { name = "Independent slam light plane" };
            effect.mesh.vertices = new[] { new Vector3(-length/2,-height*0.16f), new Vector3(length/2,-height*0.16f),
                new Vector3(-length/2,height*0.84f), new Vector3(length/2,height*0.84f) };
            effect.mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            effect.mesh.triangles = new[] { 0,2,1, 2,3,1 };
            effect.mesh.RecalculateBounds();
            owner.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            effect.visual = owner.AddComponent<MeshRenderer>();
            effect.visual.sharedMaterial = material;
            effect.visual.sortingLayerID = sortingSource != null ? sortingSource.sortingLayerID : 0;
            effect.visual.sortingOrder = (sortingSource != null ? sortingSource.sortingOrder : 0) + 1;
            effect.properties = new MaterialPropertyBlock();
            effect.properties.SetTexture("_MainTex", artwork);
            effect.properties.SetVector("_ClipRect", new Vector4(bounds.xMin,bounds.yMin,bounds.xMax,bounds.yMax));
            effect.properties.SetFloat("_Reverse", reverse ? 1f : 0f);
            effect.properties.SetFloat("_EdgeFadeDistance", EdgeFadeDistance);
            effect.properties.SetFloat("_EndFade", Mathf.Min(0.3f, EdgeFadeDistance / Mathf.Max(0.001f,length)));
            effect.UpdateProperties();
            return effect;
        }

        public static SlamLightEffect PlayPath(LineRenderer warning, float height,
            Transform source, Renderer sortingSource, Rect bounds)
        {
            if (warning == null || warning.positionCount < 2) return null;
            Vector2 start = warning.GetPosition(0), end = warning.GetPosition(1);
            if (!TryGetFittedPathLayout(start, end, height, bounds, out Vector2 center, out float length,
                out float fittedHeight, out float angle, out bool reverse)) return null;
            return Play(center, length, fittedHeight, angle, source, sortingSource, bounds, reverse);
        }

        public static bool TryGetFittedPathLayout(Vector2 start, Vector2 end, float height, Rect bounds,
            out Vector2 center, out float length, out float fittedHeight, out float angle, out bool reverse)
        {
            fittedHeight = 0f;
            if (!TryGetPathLayout(start, end, out center, out length, out angle, out reverse) || height <= 0f)
                return false;
            Vector2 anchor = start + Vector2.up * VerticalOffset;
            Rect room = Rect.MinMaxRect(bounds.xMin+RoomInset, bounds.yMin+RoomInset,
                bounds.xMax-RoomInset, bounds.yMax-RoomInset);
            if (room.width <= 0f || room.height <= 0f || !room.Contains(anchor)) return false;
            // Fit only the ground path. Height stays dramatic even when the wall shortens the path.
            Vector2 direction = (end-start).normalized;
            if (direction.x > 0.00001f) length = Mathf.Min(length,(room.xMax-anchor.x)/direction.x);
            else if (direction.x < -0.00001f) length = Mathf.Min(length,(room.xMin-anchor.x)/direction.x);
            if (direction.y > 0.00001f) length = Mathf.Min(length,(room.yMax-anchor.y)/direction.y);
            else if (direction.y < -0.00001f) length = Mathf.Min(length,(room.yMin-anchor.y)/direction.y);
            fittedHeight = height;
            center = anchor + direction * length * 0.5f;
            return length > 0.001f && fittedHeight > 0.001f;
        }

        public static bool TryGetPathLayout(Vector2 start, Vector2 end, out Vector2 center,
            out float length, out float angle, out bool reverse)
        {
            Vector2 direction = end - start;
            center = (start + end) * 0.5f;
            length = direction.magnitude;
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            reverse = true;
            if (angle > 90f) { angle -= 180f; reverse = false; }
            else if (angle < -90f) { angle += 180f; reverse = false; }
            return length > 0.001f;
        }

        private void Update()
        {
            if (source == null || !source.gameObject.activeInHierarchy ||
                (source.position - previousSourcePosition).sqrMagnitude > 16f)
            { Destroy(gameObject); return; }
            previousSourcePosition = source.position;
            age += Time.deltaTime;
            if (age >= Duration) { Destroy(gameObject); return; }
            UpdateProperties();
        }

        private void UpdateProperties()
        {
            // Keep the fully expanded painted frame intact throughout the peak hold.
            properties.SetFloat("_Strength", age <= FallSeconds + PeakHoldSeconds ? 1f : StrengthAt(age));
            properties.SetFloat("_Frame", FrameAt(age));
            visual.SetPropertyBlock(properties);
        }

        public static int FrameAt(float seconds) => seconds < FallSeconds ?
            Mathf.Clamp((int)(seconds / FallSeconds * 2f), 0, 1) :
            seconds <= FallSeconds + PeakHoldSeconds ? 3 :
            Mathf.Clamp(6 + (int)((seconds-FallSeconds-PeakHoldSeconds)/FadeSeconds*2f), 6, 7);

        private void OnDestroy() => CombatEffectArtwork.Release(mesh);
    }
}
