using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class FairyKingdomStatusArtwork : MonoBehaviour
    {
        private EnemyStatusEffects status;
        private SpriteRenderer source;
        private Health health;
        private readonly SpriteRenderer[,] overlays = new SpriteRenderer[3, 3];
        private readonly bool[] wasActive = new bool[3];
        private readonly float[] startedAt = new float[3];
        private Material material;
        private TrickalFanGame.Enemy.BossMovementAnimator bossAnimator;
        private MeshRenderer movementMesh;
        private Sprite referenceSprite;
        public Renderer BodyRenderer { get; private set; }
        public Bounds StatusBounds { get; private set; }
        private void Awake()
        {
            status = GetComponent<EnemyStatusEffects>(); health = GetComponent<Health>();
            source = GetComponent<SpriteRenderer>();
            bossAnimator = GetComponent<TrickalFanGame.Enemy.BossMovementAnimator>();
            material = Resources.Load<Material>("FairyKingdomArtwork/PreviewUnlit");
        }
        private void LateUpdate() => RefreshAt(Time.time);

        // Follow the body, but size from its neutral artwork rather than expanded attack frames.
        public void RefreshAt(float time)
        {
            if (!isActiveAndEnabled) { Hide(); return; }
            if (status == null) status = GetComponent<EnemyStatusEffects>();
            if (bossAnimator == null) bossAnimator = GetComponent<TrickalFanGame.Enemy.BossMovementAnimator>();
            if (bossAnimator != null && bossAnimator.Source != null) source = bossAnimator.Source;
            if (source == null)
            {
                Transform visual = transform.Find("Visual");
                if (visual != null) source = visual.GetComponent<SpriteRenderer>();
            }
            if (source == null || source.sprite == null || !source.enabled)
            {
                float largest = 0f;
                foreach (SpriteRenderer candidate in GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!candidate.enabled || candidate.sprite == null || candidate.transform.name.StartsWith("Status ")) continue;
                    float area = candidate.bounds.size.x * candidate.bounds.size.y;
                    if (area <= largest) continue;
                    source = candidate; largest = area;
                }
            }
            if (status == null || source == null || source.sprite == null || !source.enabled ||
                (health != null && health.IsDead)) { Hide(); return; }
            BodyRenderer = source;
            if (bossAnimator != null && bossAnimator.Artwork != null && bossAnimator.Artwork.enabled)
                BodyRenderer = bossAnimator.Artwork;
            else if (source.forceRenderingOff)
            {
                if (movementMesh == null)
                {
                    Transform movement = transform.Find("Enemy Movement Artwork");
                    if (movement != null) movementMesh = movement.GetComponent<MeshRenderer>();
                }
                if (movementMesh != null && movementMesh.enabled) BodyRenderer = movementMesh;
            }
            Bounds bounds = StableBodyBounds();
            StatusBounds = bounds;
            var effects = StatusEffectAnimation.Effects;
            for (int index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                bool active = effect.id == "poison" ? status.IsPoisoned : effect.id == "burn" ? status.IsBurning : status.IsShocked;
                if (active && !wasActive[index]) startedAt[index] = time;
                wasActive[index] = active;
                float elapsed = Mathf.Max(0f, time - startedAt[index]);
                for (int piece = 0; piece < effect.pieces.Length; piece++)
                {
                    if (active && overlays[index, piece] == null)
                    {
                        GameObject child = new("Status " + effect.id + " " + piece);
                        child.transform.SetParent(transform, false);
                        var renderer = child.AddComponent<SpriteRenderer>();
                        if (material != null) renderer.sharedMaterial = material;
                        overlays[index, piece] = renderer;
                    }
                    SpriteRenderer current = overlays[index, piece];
                    if (current == null) continue;
                    var motion = StatusEffectAnimation.Sample(effect, piece, elapsed);
                    current.enabled = active && motion.alpha > 0f;
                    if (!current.enabled) continue;
                    int frame = StatusEffectAnimation.FrameAt(effect, elapsed, piece);
                    current.sprite = effect.sprites[frame, piece];
                    current.color = new Color(1f, 1f, 1f, motion.alpha);
                    current.sortingLayerID = BodyRenderer.sortingLayerID;
                    current.sortingOrder = BodyRenderer.sortingOrder + 3 + index;
                    var region = effect.pieces[piece];
                    current.transform.position = new Vector3(bounds.center.x + region.anchorX * bounds.size.x + motion.x * bounds.size.y,
                        bounds.min.y + (region.anchorY + motion.y) * bounds.size.y, BodyRenderer.transform.position.z);
                    current.transform.rotation = Quaternion.identity;
                    CombatEffectArtwork.Size(current, region.width * bounds.size.y * motion.scale,
                        region.height * bounds.size.y * motion.scale);
                }
            }
        }
        private Bounds StableBodyBounds()
        {
            var attack = GetComponent<TrickalFanGame.Enemy.EnemyAttackArtwork>();
            var playerWalk = GetComponent<TrickalFanGame.Player.PlayerWalkAnimator>();
            Sprite neutral = bossAnimator != null && bossAnimator.BodySprite != null ? bossAnimator.BodySprite :
                attack != null && attack.isActiveAndEnabled && attack.ReferenceSprite != null ? attack.ReferenceSprite :
                playerWalk != null && playerWalk.IdleSprite != null ? playerWalk.IdleSprite : null;
            if (neutral != null) referenceSprite = neutral;
            if (referenceSprite == null) referenceSprite = source.sprite;
            Matrix4x4 matrix = source.transform.localToWorldMatrix;
            var presentation = GetComponent<TrickalFanGame.Enemy.EnemyAttackPresentation>();
            if (presentation != null)
            {
                Vector2 pose = presentation.CurrentScaleMultiplier;
                Matrix4x4 withoutPose = transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(
                    1f / Mathf.Max(.001f, pose.x), 1f / Mathf.Max(.001f, pose.y), 1f));
                matrix = withoutPose * transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            }
            Bounds local = referenceSprite.bounds;
            Vector3 min = local.min, max = local.max;
            // Keep jump/visual movement, but do not include artwork squash/stretch in the size.
            Vector3 offset = BodyRenderer.transform.position - source.transform.position;
            Bounds result = new(matrix.MultiplyPoint3x4(min) + offset, Vector3.zero);
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x,min.y,0f)) + offset);
            result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x,max.y,0f)) + offset);
            result.Encapsulate(matrix.MultiplyPoint3x4(max) + offset);
            return result;
        }
        private void Hide()
        {
            foreach (SpriteRenderer overlay in overlays) if (overlay != null) overlay.enabled = false;
        }
        private void OnDisable() { Hide(); System.Array.Clear(wasActive, 0, wasActive.Length); }
    }
}
