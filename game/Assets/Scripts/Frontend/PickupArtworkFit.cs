using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // Keeps a floor pickup's artwork at one size on screen. It measures what the renderer really draws in the
    // world and corrects the sliced size from that, so the pickup's own scale, its parent's scale and the
    // artwork's import size cannot make one kind of pickup larger than another.
    [DisallowMultipleComponent]
    public sealed class PickupArtworkFit : MonoBehaviour
    {
        private const float Tolerance = 0.01f;

        // A correction that does not change what is drawn must not repeat forever.
        private const int AttemptsPerChange = 3;

        private SpriteRenderer target;
        private float worldSize;
        private Vector3 fittedScale;
        private int attemptsLeft;
        private Material ownedMaterial;

        public float WorldSize => worldSize;

        public void Configure(SpriteRenderer configuredTarget, float configuredWorldSize)
        {
            target = configuredTarget;
            worldSize = Mathf.Max(0f, configuredWorldSize);
            attemptsLeft = AttemptsPerChange;
            Fit();
        }

        // The pickup's own copy of the artwork material; it goes away with the pickup or when it is replaced.
        public void OwnMaterial(Material material)
        {
            if (ownedMaterial != null && ownedMaterial != material) Release(ownedMaterial);
            ownedMaterial = material;
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null) Release(ownedMaterial);
        }

        private static void Release(Material material)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private void LateUpdate()
        {
            if (target != null && target.transform.lossyScale != fittedScale)
            {
                attemptsLeft = AttemptsPerChange;
            }

            Fit();
        }

        // The longer side the renderer draws in the world, measured along its own axes so a rotation does not
        // change the result.
        public static float DrawnWorldSize(SpriteRenderer renderer)
        {
            Vector2 local = renderer.localBounds.size;
            Vector3 scale = renderer.transform.lossyScale;
            return Mathf.Max(Mathf.Abs(local.x * scale.x), Mathf.Abs(local.y * scale.y));
        }

        private void Fit()
        {
            if (target == null || worldSize <= 0f || target.sprite == null ||
                target.drawMode != SpriteDrawMode.Sliced || attemptsLeft <= 0)
            {
                return;
            }

            fittedScale = target.transform.lossyScale;
            float drawn = DrawnWorldSize(target);
            if (drawn <= 0.0001f || Mathf.Abs(drawn - worldSize) <= Tolerance)
            {
                return;
            }

            attemptsLeft--;
            target.size *= worldSize / drawn;
        }
    }
}
