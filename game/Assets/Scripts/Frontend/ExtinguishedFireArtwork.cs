using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // A room-owned visual only: the original trap/obstacle is disabled by the existing break path.
    // No collider, damage component or shadow is copied into this floor decoration.
    [DisallowMultipleComponent]
    public sealed class ExtinguishedFireArtwork : MonoBehaviour
    {
        public const int SortingOrder = -8;
        private SpriteRenderer visual;
        private float startedAt;
        private float width;
        public SpriteRenderer Visual => visual;

        public static ExtinguishedFireArtwork Spawn(Transform trap, SpriteRenderer source, float width,
            float currentTime, bool alreadyCold)
        {
            GameObject owner = new("Extinguished fire - " + trap.name);
            owner.transform.SetParent(trap.parent, false);
            owner.transform.localPosition = trap.localPosition;
            owner.transform.localRotation = trap.localRotation;
            owner.transform.localScale = trap.localScale;
            var result = owner.AddComponent<ExtinguishedFireArtwork>();
            result.width = width;
            result.startedAt = alreadyCold ? currentTime - EnvironmentHazardArtwork.ExtinguishSeconds : currentTime;
            var child = new GameObject("Visual");
            child.transform.SetParent(owner.transform, false);
            result.visual = child.AddComponent<SpriteRenderer>();
            if (source != null)
            {
                child.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                Vector3 parentScale = owner.transform.lossyScale;
                Vector3 sourceScale = source.transform.lossyScale;
                child.transform.localScale = new Vector3(sourceScale.x / parentScale.x,
                    sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
                result.visual.sharedMaterial = source.sharedMaterial;
                result.visual.sortingLayerID = source.sortingLayerID;
                result.visual.flipX = source.flipX;
                result.visual.flipY = source.flipY;
            }
            result.visual.sortingOrder = SortingOrder;
            result.visual.color = Color.white;
            result.Tick(currentTime);
            return result;
        }

        private void Update() => Tick(Time.time);

        public void Tick(float currentTime)
        {
            visual.sprite = EnvironmentHazardArtwork.Fit(EnvironmentHazardArtwork.FireExtinguish,
                currentTime - startedAt, width);
        }
    }
}
