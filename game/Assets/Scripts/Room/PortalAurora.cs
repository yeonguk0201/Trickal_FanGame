using UnityEngine;

namespace TrickalFanGame.Room
{
    // Separate backdrop: no colliders, no changes to portal position or silhouette.
    public sealed class PortalAurora : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new(4.8f, 5.2f);
        [SerializeField, Range(0f, 1f)] private float brightness = 0.8f;
        [SerializeField, Range(0.1f, 2f)] private float speed = 1f;

        private Mesh mesh;
        private Material material;
        private MeshRenderer glow;
        public Renderer Glow => glow;

        public void Configure(SpriteRenderer portal)
        {
            if (glow != null) return;
            Shader shader = Resources.Load<Shader>("CombatEffects/PortalAurora");
            if (shader == null)
            {
                Debug.LogError("Portal aurora shader is missing.", this);
                return;
            }
            GameObject layer = new("Aurora backdrop");
            layer.transform.SetParent(transform, false);
            // Match the fixed first-frame artwork rather than the padded canvas center.
            Vector2 frameSize = portal.sprite.rect.size / portal.sprite.pixelsPerUnit;
            layer.transform.localPosition = new Vector3(frameSize.x * 0.025f, -frameSize.y * 0.015f, 0f);
            mesh = new Mesh { name = "Portal aurora quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0),
                new Vector3(0.5f,0.5f,0), new Vector3(-0.5f,0.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            layer.AddComponent<MeshFilter>().sharedMesh = mesh;
            glow = layer.AddComponent<MeshRenderer>();
            glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.sortingLayerID = portal.sortingLayerID;
            glow.sortingOrder = portal.sortingOrder - 1;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            glow.sharedMaterial = material;
            Texture2D sheet = portal.sprite.texture;
            Rect rect = portal.sprite.rect;
            material.SetTexture("_PortalTex", sheet);
            material.SetVector("_FrameRect", new Vector4(rect.x / sheet.width, rect.y / sheet.height,
                rect.width / sheet.width, rect.height / sheet.height));
            material.SetVector("_PortalRadius", new Vector4(frameSize.x * 0.275f, frameSize.y * 0.405f, 0f, 0f));
            Evaluate(0f, 0f);
        }

        public void Evaluate(float time, float opacity)
        {
            if (glow == null) return;
            glow.transform.localScale = new Vector3(Mathf.Max(0.1f,size.x), Mathf.Max(0.1f,size.y), 1f);
            glow.enabled = opacity > 0f && brightness > 0f;
            material.SetFloat("_Phase", time * speed * Mathf.PI / 6f);
            material.SetFloat("_Opacity", Mathf.Clamp01(opacity) * brightness);
            material.SetVector("_GlowSize", new Vector4(size.x, size.y, 0f, 0f));
        }

        private void OnDestroy()
        {
            Dispose(mesh);
            Dispose(material);
            if (glow != null) Dispose(glow.gameObject);
        }
        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
