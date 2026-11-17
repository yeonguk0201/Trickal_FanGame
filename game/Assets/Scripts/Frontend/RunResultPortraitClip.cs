using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class RunResultPortraitClip : BaseMeshEffect
    {
        private static Material portraitMaterial;
        private RectTransform circle;

        internal bool Configure(RectTransform clipCircle)
        {
            Shader shader = Resources.Load<Shader>("UiEffects/RunResultPortrait");
            if (shader == null || !shader.isSupported || graphic.canvas == null) return false;

            if (portraitMaterial == null)
                portraitMaterial = new Material(shader) { name = "Run Result Portrait", hideFlags = HideFlags.HideAndDontSave };
            circle = clipCircle;
            graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            graphic.material = portraitMaterial;
            Refresh();
            return true;
        }

        internal void Refresh() => graphic.SetVerticesDirty();

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || circle == null) return;

            Matrix4x4 toCircle = circle.worldToLocalMatrix * transform.localToWorldMatrix;
            Rect rect = circle.rect;
            Vector2 center = rect.center;
            float diameter = Mathf.Max(1f, Mathf.Min(rect.width, rect.height));
            var vertex = new UIVertex();
            for (int index = 0; index < vertices.currentVertCount; index++)
            {
                vertices.PopulateUIVertex(ref vertex, index);
                Vector2 point = toCircle.MultiplyPoint3x4(vertex.position);
                vertex.uv1 = (point - center) * (2f / diameter);
                vertices.SetUIVertex(vertex, index);
            }
        }
    }
}
