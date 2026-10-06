using UnityEngine;

namespace TrickalFanGame.Combat
{
    [DefaultExecutionOrder(10000)]
    public sealed class ChargeWarningVisual : MonoBehaviour
    {
        private LineRenderer path;
        private SpriteRenderer visual;
        private float width;

        public static void Bind(LineRenderer line, float width, bool show)
        {
            if (!Application.isPlaying || line == null) return;
            var warning = line.GetComponent<ChargeWarningVisual>();
            if (warning == null && show) warning = line.gameObject.AddComponent<ChargeWarningVisual>();
            if (warning == null) return;
            warning.path = line;
            warning.width = Mathf.Max(0.1f, width);
            warning.enabled = show;
            line.forceRenderingOff = show;
        }

        private void LateUpdate()
        {
            if (path == null || !path.enabled || path.positionCount < 2)
            { if (visual != null) visual.enabled = false; return; }
            if (visual == null)
            {
                Sprite[] frames = CombatEffectArtwork.Frames("warning-chevron");
                Material material = CombatEffectArtwork.Material("ChargeWarning");
                if (frames.Length == 0 || material == null) { path.forceRenderingOff = false; return; }
                var owner = new GameObject("VFX charge warning");
                owner.transform.SetParent(transform, false);
                visual = CombatEffectArtwork.Renderer(owner, frames[0], path, -1);
                visual.sharedMaterial = material;
            }
            Vector3 start = path.GetPosition(0), end = path.GetPosition(1);
            Vector3 delta = end - start;
            visual.enabled = delta.sqrMagnitude > 0.001f;
            visual.transform.SetPositionAndRotation((start + end) * 0.5f,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
            CombatEffectArtwork.Size(visual, delta.magnitude, width);
        }

        private void OnDisable()
        {
            if (visual != null) visual.enabled = false;
            if (path != null) path.forceRenderingOff = false;
        }
    }
}
