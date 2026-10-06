using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum EnemyMovementMotion { Walk, Hop }

    /// <summary>Plays drawn walking frames or a visual hop without changing gameplay transforms.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
    public sealed class EnemyMovementAnimator : MonoBehaviour
    {
        private const int Columns = 24;
        private const int Rows = 32;
        [SerializeField] private EnemyMovementMotion motion;
        [SerializeField] private Material motionMaterial;
        [SerializeField, Min(0.1f)] private float strideLength = 0.65f;
        [SerializeField, Range(0f, 0.15f)] private float amplitude = 0.045f;
        [SerializeField] private Sprite[] walkFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite walkReferenceSprite;

        private SpriteRenderer source;
        private Rigidbody2D body;
        private Health health;
        private KnockbackReceiver knockback;
        private EnemyAttackPresentation attack;
        private EnemyAttackArtwork attackArtwork;
        private MeshRenderer visual;
        private Mesh mesh;
        private Sprite builtSprite;
        private Sprite idleSprite;
        private Vector3[] restVertices;
        private Vector3[] animatedVertices;
        private MaterialPropertyBlock properties;
        private Vector2 previousPosition;
        private float phase;
        private bool moving;
        private bool previousForceRenderingOff;

        public EnemyMovementMotion Motion => motion;
        public Material MotionMaterial => motionMaterial;
        public bool IsAnimating => moving;
        public int WalkFrameCount => walkFrames.Length;
        public Sprite GetWalkFrame(int index) => walkFrames[index];

        public void Configure(EnemyMovementMotion style, Material material, Sprite[] frames, float stride, float strength,
            Sprite referenceSprite = null)
        {
            motion = style;
            motionMaterial = material;
            walkFrames = frames ?? System.Array.Empty<Sprite>();
            walkReferenceSprite = referenceSprite;
            strideLength = Mathf.Max(0.1f, stride);
            amplitude = Mathf.Clamp(strength, 0f, 0.15f);
        }

        private void Awake()
        {
            source = GetComponent<SpriteRenderer>();
            if (idleSprite == null) idleSprite = source.sprite;
            // Boss minions inherit normal enemy prefabs but replace the artwork. Do not show another species' frames.
            if (motion == EnemyMovementMotion.Walk && walkReferenceSprite != null && idleSprite != walkReferenceSprite)
                walkFrames = System.Array.Empty<Sprite>();
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            previousPosition = GetComponent<Rigidbody2D>().position;
            phase = 0f;
            moving = false;
            if (source != null) previousForceRenderingOff = source.forceRenderingOff;
        }

        private void FixedUpdate()
        {
            Vector2 position = body.position;
            Vector2 displacement = position - previousPosition;
            float distance = displacement.magnitude;
            previousPosition = position;
            if (attack == null) attack = GetComponent<EnemyAttackPresentation>();
            bool suppressed = (health != null && health.IsDead) || (knockback != null && knockback.IsActive) ||
                              (attack != null && attack.Phase != EnemyAttackPhase.Idle);
            // Actual displacement stops the feet at walls. Ignore room placement/teleport discontinuities.
            moving = !suppressed && body.linearVelocity.sqrMagnitude > 0.0025f &&
                     distance > 0.0001f && distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f);
            // These sprites are drawn facing left. Retain facing when blocked or moving vertically.
            if (moving && Mathf.Abs(displacement.x) > 0.001f) source.flipX = displacement.x > 0f;
            phase = moving ? Mathf.Repeat(phase + distance / strideLength, 1f) : 0f;
        }

        private void LateUpdate()
        {
            if (RenderAttack()) return;
            if (motion == EnemyMovementMotion.Hop) source.sprite = idleSprite;
            if (motion == EnemyMovementMotion.Walk)
            {
                RenderPose(phase, moving);
                return;
            }
            if (source.sprite == null || motionMaterial == null)
            {
                RestoreSource();
                return;
            }
            if (builtSprite != source.sprite) BuildMesh();
            if (visual == null) return;
            source.forceRenderingOff = true;
            visual.enabled = source.enabled && !previousForceRenderingOff;
            visual.sortingLayerID = source.sortingLayerID;
            visual.sortingOrder = source.sortingOrder;
            visual.sharedMaterial = motionMaterial;
            source.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", source.sprite.texture);
            properties.SetColor("_Tint", source.color);
            visual.SetPropertyBlock(properties);
            RenderPose(moving ? phase : 0f, moving);
        }

        private void BuildMesh()
        {
            if (visual == null)
            {
                GameObject child = new GameObject("Enemy Movement Artwork");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>();
                visual = child.AddComponent<MeshRenderer>();
                visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                visual.receiveShadows = false;
            }
            if (mesh != null) Release(mesh);
            mesh = new Mesh { name = "Enemy movement mesh" };
            mesh.MarkDynamic();
            builtSprite = source.sprite;
            Rect rect = builtSprite.rect;
            Vector2 pivot = builtSprite.pivot;
            float ppu = builtSprite.pixelsPerUnit;
            restVertices = new Vector3[(Columns + 1) * (Rows + 1)];
            animatedVertices = new Vector3[restVertices.Length];
            Vector2[] uv = new Vector2[restVertices.Length];
            int[] triangles = new int[Columns * Rows * 6];
            // The hopping sprite is an unpacked single-sprite texture; preserve its original UVs.
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                float px = rect.width * x / Columns, py = rect.height * y / Rows;
                restVertices[i] = new Vector3((px - pivot.x) / ppu, (py - pivot.y) / ppu, 0f);
                uv[i] = new Vector2((rect.x + px) / builtSprite.texture.width,
                    (rect.y + py) / builtSprite.texture.height);
            }
            int index = 0;
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                triangles[index++] = i; triangles[index++] = i + Columns + 1; triangles[index++] = i + 1;
                triangles[index++] = i + 1; triangles[index++] = i + Columns + 1; triangles[index++] = i + Columns + 2;
            }
            mesh.vertices = restVertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        // Public so Editor verification can inspect the artwork without running AI or physics.
        public void RenderPose(float cycle, bool animate)
        {
            if (source == null) Awake();
            if (RenderAttack()) return;
            if (motion == EnemyMovementMotion.Hop) source.sprite = idleSprite;
            if (motion == EnemyMovementMotion.Walk)
            {
                RestoreSource();
                int frame = Mathf.FloorToInt(Mathf.Repeat(cycle, 1f) * walkFrames.Length);
                source.sprite = animate && walkFrames.Length > 0 && walkFrames[frame] != null
                    ? walkFrames[frame] : idleSprite;
                return;
            }
            if (source.sprite == null || motionMaterial == null) return;
            if (builtSprite != source.sprite) BuildMesh();
            float height = builtSprite.rect.height / builtSprite.pixelsPerUnit;
            float bottom = -builtSprite.pivot.y / builtSprite.pixelsPerUnit;
            for (int i = 0; i < restVertices.Length; i++)
            {
                Vector3 point = Deform(restVertices[i], height, bottom, cycle, animate);
                point.x *= source.flipX ? -1f : 1f;
                point.y *= source.flipY ? -1f : 1f;
                animatedVertices[i] = point;
            }
            mesh.vertices = animatedVertices;
            mesh.RecalculateBounds();
        }

        private Vector3 Deform(Vector3 point, float height, float bottom, float cycle, bool animate)
        {
            if (!animate) return point;
            float wave = Mathf.Sin(cycle * Mathf.PI * 2f);
            float lift = Mathf.Max(0f, wave);
            float squash = Mathf.Min(0f, wave);
            float verticalScale = 1f + lift * amplitude * 1.8f + squash * amplitude * 2.8f;
            point.x *= 1f - lift * amplitude + -squash * amplitude * 1.8f;
            point.y = bottom + (point.y - bottom) * verticalScale + lift * height * amplitude * 2f;
            return point;
        }

        private void RestoreSource()
        {
            if (source != null) source.forceRenderingOff = previousForceRenderingOff;
            if (visual != null) visual.enabled = false;
        }

        private bool RenderAttack()
        {
            if (attackArtwork == null) attackArtwork = GetComponent<EnemyAttackArtwork>();
            if (attackArtwork == null || !attackArtwork.TryRender()) return false;
            RestoreSource();
            return true;
        }

        private void OnDisable()
        {
            moving = false;
            phase = 0f;
            RestoreSource();
            if (source != null && idleSprite != null) source.sprite = idleSprite;
        }

        private void OnDestroy()
        {
            RestoreSource();
            if (mesh != null) Release(mesh);
            if (visual != null) Release(visual.gameObject);
        }

        private static void Release(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
