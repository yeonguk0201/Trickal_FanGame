using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    // The walk clock follows distance; the flower recoil clock follows shots. Neither changes physics.
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class SansamoPureMotion : MonoBehaviour
    {
        public const float RecoilDuration = 0.24f;
        private const int Columns = 20, Rows = 32;
        [SerializeField] private Texture2D walkAtlas;
        [SerializeField] private Material material;
        [SerializeField] private Rect contentRect;
        [SerializeField] private Vector2 flowerUv = new(0.42f, 0.91f);
        [SerializeField, Min(0.1f)] private float strideLength = 1.05f;
        private SpriteRenderer source;
        private MobileRangedEnemyController controller;
        private Health health;
        private KnockbackReceiver knockback;
        private Rigidbody2D body;
        private Sprite idle;
        private readonly Sprite[] frames = new Sprite[4];
        private Mesh mesh;
        private MeshRenderer visual;
        private Vector3[] rest, vertices;
        private Vector2[] uvs;
        private MaterialPropertyBlock properties;
        private Sprite built;
        private Vector2 previousPosition, recoilDirection;
        private float cycle, firedAt = float.NegativeInfinity;
        private bool moving, originalHidden;
        public Texture2D WalkAtlas => walkAtlas;
        public Mesh PoseMesh => mesh;
        public Material MotionMaterial => material;
        public Sprite PoseSprite => built;
        public int CurrentFrame { get; private set; } = -1;
        public bool IsMoving => moving;

        public void Configure(Texture2D atlas, Material sharedMaterial, Rect normalizedContent)
        { walkAtlas = atlas; material = sharedMaterial; contentRect = normalizedContent; }

        private void OnEnable()
        {
            controller = GetComponent<MobileRangedEnemyController>();
            if (controller != null) controller.ProjectileFired += OnShot;
            body = GetComponent<Rigidbody2D>();
            previousPosition = body != null ? body.position : (Vector2)transform.position;
            source = GetComponent<SpriteRenderer>();
            originalHidden = source != null && source.forceRenderingOff;
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            cycle = 0f; moving = false; firedAt = float.NegativeInfinity;
        }

        private bool Suppressed => (health != null && health.IsDead) ||
            (knockback != null && knockback.IsActive) || (controller != null && controller.IsActionSuppressed);

        private void FixedUpdate()
        {
            if (body == null) return;
            Vector2 delta = body.position - previousPosition;
            previousPosition = body.position;
            float distance = delta.magnitude;
            moving = !Suppressed && body.linearVelocity.sqrMagnitude > 0.0025f && distance > 0.0001f &&
                distance < Mathf.Max(0.5f, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3f);
            cycle = moving ? Mathf.Repeat(cycle + distance / strideLength, 1f) : 0f;
        }

        private void LateUpdate()
        {
            if (Suppressed) firedAt = float.NegativeInfinity;
            RenderPose(cycle, moving && !Suppressed, Time.time - firedAt, recoilDirection);
        }

        private void OnShot(EnemyProjectile projectile)
        {
            firedAt = Time.time;
            recoilDirection = projectile.Velocity.normalized;
        }

        private bool Initialize()
        {
            if (mesh != null) return true;
            if (source == null) source = GetComponent<SpriteRenderer>();
            var artwork = GetComponent<FairyKingdomArtworkView>();
            idle = FairyKingdomArtwork.Fit("sansamo-pure", artwork != null ? artwork.LocalWidth : 0.7f);
            if (source == null || idle == null || walkAtlas == null || material == null || contentRect.height <= 0f)
                return false;
            float cell = walkAtlas.width / 2f;
            float ppu = contentRect.height * cell / idle.bounds.size.y;
            Vector2 pivot = contentRect.center;
            for (int i = 0; i < 4; i++)
                frames[i] = Sprite.Create(walkAtlas, new Rect(i % 2 * cell, (1 - i / 2) * cell, cell, cell),
                    pivot, ppu, 0, SpriteMeshType.FullRect);
            GameObject child = new("Sansamo pure walk and flower") { layer = gameObject.layer };
            child.transform.SetParent(source.transform, false);
            mesh = new Mesh { name = "Sansamo pure flower recoil" };
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual = child.AddComponent<MeshRenderer>();
            visual.sharedMaterial = material;
            visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visual.receiveShadows = false;
            rest = new Vector3[(Columns + 1) * (Rows + 1)];
            vertices = new Vector3[rest.Length]; uvs = new Vector2[rest.Length];
            int[] triangles = new int[Columns * Rows * 6];
            int n = 0;
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                triangles[n++] = i; triangles[n++] = i + Columns + 1; triangles[n++] = i + 1;
                triangles[n++] = i + 1; triangles[n++] = i + Columns + 1; triangles[n++] = i + Columns + 2;
            }
            mesh.vertices = vertices; mesh.triangles = triangles;
            properties = new MaterialPropertyBlock();
            return true;
        }

        // Shared by gameplay, the Scene preview and verification. Attack never selects a different body pose.
        public void RenderPose(float walkCycle, bool walking, float shotAge, Vector2 direction)
        {
            if (!Initialize()) return;
            CurrentFrame = walking ? Mathf.FloorToInt(Mathf.Repeat(walkCycle, 1f) * 4f) : -1;
            Sprite sprite = CurrentFrame >= 0 ? frames[CurrentFrame] : idle;
            if (built != sprite)
            {
                built = sprite;
                for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Columns; x++)
                {
                    int i = y * (Columns + 1) + x;
                    Vector2 pixel = new(sprite.rect.width * x / Columns, sprite.rect.height * y / Rows);
                    rest[i] = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
                    uvs[i] = new Vector2((sprite.rect.x + pixel.x) / sprite.texture.width,
                        (sprite.rect.y + pixel.y) / sprite.texture.height);
                }
                mesh.uv = uvs;
            }
            float pulse = shotAge >= 0f && shotAge < RecoilDuration
                ? Mathf.Sin(Mathf.PI * Mathf.Sqrt(shotAge / RecoilDuration)) : 0f;
            Vector2 localDirection = source.transform.InverseTransformVector(direction.normalized);
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                // Only the stalk above the leaves bends; feet, face and handbag remain on the walk clock.
                float upper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                    idle.bounds.min.y + idle.bounds.size.y * 0.63f,
                    idle.bounds.min.y + idle.bounds.size.y * 0.91f, p.y));
                if (source.flipX) p.x = -p.x;
                p -= (Vector3)localDirection * (pulse * upper * idle.bounds.size.y * 0.065f);
                vertices[i] = p;
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
            source.forceRenderingOff = true;
            visual.enabled = source.enabled && !originalHidden;
            visual.sortingLayerID = source.sortingLayerID; visual.sortingOrder = source.sortingOrder;
            // CombatVisualFeedback writes the hit strength to this renderer; retain it between poses.
            visual.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", sprite.texture); properties.SetColor("_Tint", source.color);
            visual.SetPropertyBlock(properties);
        }

        public Vector2 LaunchPosition
        {
            get
            {
                if (!Initialize()) return transform.position;
                Vector2 p = idle.bounds.min + Vector3.Scale(idle.bounds.size, new Vector3(flowerUv.x, flowerUv.y, 0f));
                if (source.flipX) p.x = -p.x;
                return source.transform.TransformPoint(p);
            }
        }

        private void OnDisable()
        {
            if (controller != null) controller.ProjectileFired -= OnShot;
            if (source != null) source.forceRenderingOff = originalHidden;
            if (visual != null) visual.enabled = false;
            moving = false; firedAt = float.NegativeInfinity;
        }

        private void OnDestroy()
        {
            if (mesh != null) Release(mesh);
            if (visual != null) Release(visual.gameObject);
            foreach (Sprite frame in frames) if (frame != null) Release(frame);
        }

        private static void Release(Object value)
        { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
