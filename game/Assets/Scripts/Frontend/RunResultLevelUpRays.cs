using UnityEngine;
using UnityEngine.UI;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    public sealed class RunResultLevelUpRays : MaskableGraphic
    {
        private static readonly float[] Angles = { -124f, -70f, -42f, -14f, 14f, 42f, 70f, 124f };
        private const int RadialSteps = 12;
        private const int AngularSteps = 8;
        private float halfAngle;
        private float feather;

        internal void Configure(float width, float edgeFeather, Color tint)
        {
            halfAngle = width;
            feather = edgeFeather;
            raycastTarget = false;
            color = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (float angle in Angles)
                AddRay(mesh, angle);
        }

        private void AddRay(VertexHelper mesh, float angle)
        {
            int start = mesh.currentVertCount;
            float spread = Mathf.Abs(angle);
            float length = Mathf.Lerp(176f, 135f, Mathf.InverseLerp(14f, 70f, spread));
            if (spread > 90f) length *= 0.52f;
            for (int row = 0; row <= RadialSteps; row++)
            {
                float distance = (float)row / RadialSteps;
                float fade = Mathf.SmoothStep(0f, 1f, distance / 0.26f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (distance - 0.36f) / 0.64f));
                for (int column = 0; column <= AngularSteps; column++)
                {
                    float side = 2f * column / AngularSteps - 1f;
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(side) - 1f + feather) / feather);
                    float radians = (angle + side * halfAngle) * Mathf.Deg2Rad;
                    Vector3 point = new(Mathf.Sin(radians) * distance * length,
                        Mathf.Cos(radians) * distance * length, 0f);
                    Color tint = color;
                    tint.a *= fade * edge;
                    mesh.AddVert(point, tint, Vector2.zero);
                }
            }

            const int stride = AngularSteps + 1;
            for (int row = 0; row < RadialSteps; row++)
            for (int column = 0; column < AngularSteps; column++)
            {
                int a = start + row * stride + column;
                mesh.AddTriangle(a, a + stride, a + 1);
                mesh.AddTriangle(a + 1, a + stride, a + stride + 1);
            }
        }
    }
}
