using System;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    public enum BossMovementStyle { BuseureogiHop, VaultHop, CrayonWalk }

    /// <summary>Visual-only motion. Existing boss runtimes still own movement, attack scale and jump altitude.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController), typeof(Rigidbody2D))]
    public sealed class BossMovementAnimator : MonoBehaviour
    {
        [SerializeField] private BossMovementStyle style;
        [SerializeField] private SpriteRenderer source;
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] hopFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] healFrames = Array.Empty<Sprite>();
        [SerializeField] private SpriteRenderer[] groundTreasures = Array.Empty<SpriteRenderer>();
        [SerializeField, Min(0.1f)] private float strideLength = 1.4f;
        [SerializeField, Min(0f)] private float hopHeight = 0.45f;

        private BossController boss;
        private Rigidbody2D body;
        private SaemaeumVaultBossPatternRuntime vault;
        private CrayonHeroBossPatternRuntime knight;
        private SpriteRenderer artwork;
        private Vector2 previousPosition;
        private Vector3[] treasurePositions;
        private Vector3[] treasureScales;
        private Quaternion[] treasureRotations;
        private bool originalRenderingOff;
        private float cycle;
        private bool moving;
        private float impactAge = 1f;
        private float impactStrength;
        private bool initialized;
        private Vector3 displayedHopPosition;
        private Vector3 displayedHopScale = Vector3.one;

        public BossMovementStyle Style => style;
        public SpriteRenderer Source => source;
        public SpriteRenderer Artwork => artwork;
        public Sprite BodySprite => bodySprite;
        public int WalkFrameCount => walkFrames.Length;
        public Sprite GetWalkFrame(int i) => walkFrames[i];
        public int HopFrameCount => hopFrames.Length;
        public Sprite GetHopFrame(int i) => hopFrames[i];
        public int HealFrameCount => healFrames.Length;
        public Sprite GetHealFrame(int i) => healFrames[i];
        public void ConfigureHealing(Sprite[] frames) => healFrames = frames ?? Array.Empty<Sprite>();
        public int TreasureCount => groundTreasures.Length;
        public SpriteRenderer GetTreasure(int i) => groundTreasures[i];
        public bool IsAnimating => moving;
        public bool HasLandingImpulse => impactAge < 0.24f && impactStrength > 0f;

        public void Configure(BossMovementStyle motion, SpriteRenderer renderer, Sprite separateBody,
            Sprite[] frames, SpriteRenderer[] treasures, float stride, float height, Sprite[] drawnHopFrames = null)
        {
            style = motion;
            source = renderer;
            bodySprite = separateBody;
            walkFrames = frames ?? Array.Empty<Sprite>();
            hopFrames = drawnHopFrames ?? Array.Empty<Sprite>();
            groundTreasures = treasures ?? Array.Empty<SpriteRenderer>();
            strideLength = Mathf.Max(0.1f, stride);
            hopHeight = Mathf.Max(0f, height);
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized || source == null) return;
            initialized = true;
            boss = GetComponent<BossController>();
            body = GetComponent<Rigidbody2D>();
            vault = GetComponent<SaemaeumVaultBossPatternRuntime>();
            knight = GetComponent<CrayonHeroBossPatternRuntime>();
            originalRenderingOff = source.forceRenderingOff;
            previousPosition = body.position;
            treasurePositions = new Vector3[groundTreasures.Length];
            treasureScales = new Vector3[groundTreasures.Length];
            treasureRotations = new Quaternion[groundTreasures.Length];
            for (int i = 0; i < groundTreasures.Length; i++)
            {
                treasurePositions[i] = groundTreasures[i].transform.localPosition;
                treasureScales[i] = groundTreasures[i].transform.localScale;
                treasureRotations[i] = groundTreasures[i].transform.localRotation;
            }
        }

        private void OnEnable()
        {
            Initialize();
            if (!initialized) return;
            previousPosition = body.position;
            cycle = 0f;
            moving = false;
            impactAge = 1f;
            impactStrength = 0f;
            displayedHopPosition = Vector3.zero;
            displayedHopScale = Vector3.one;
        }

        private void FixedUpdate()
        {
            Initialize();
            if (!initialized) return;
            Vector2 displacement = body.position - previousPosition;
            float distance = displacement.magnitude;
            previousPosition = body.position;
            // Face actual travel, including dash/jump travel, without mirroring the collider or pattern transform.
            if (!boss.IsActionSuppressed && boss.State != BossActionState.PhaseTransition &&
                boss.State != BossActionState.Defeated && distance < 0.7f && Mathf.Abs(displacement.x) > 0.001f)
                source.flipX = displacement.x > 0f;
            bool suppressed = boss.IsActionSuppressed || boss.State == BossActionState.PhaseTransition ||
                              boss.State == BossActionState.Defeated || (vault != null && vault.IsJumping) ||
                              (knight != null && knight.IsDashInMotion);
            moving = !suppressed && distance > 0.0001f && distance < 0.7f;
            float previousCycle = cycle;
            cycle = moving ? Mathf.Repeat(cycle + distance / strideLength, 1f) : 0f;
            // The ordinary vault hop lands at 86% of its stride. Its ground treasures react only on impact.
            if (style == BossMovementStyle.VaultHop && moving &&
                ((previousCycle < 0.86f && cycle >= 0.86f) || (cycle < previousCycle && previousCycle < 0.86f)))
                NotifyLanding(0.55f);
        }

        private void LateUpdate()
        {
            if (boss != null && boss.State == BossActionState.Defeated)
            {
                Restore();
                return;
            }
            bool animate = moving && (boss == null || (!boss.IsActionSuppressed && boss.State != BossActionState.PhaseTransition)) &&
                           (vault == null || !vault.IsJumping) && (knight == null || !knight.IsDashInMotion);
            RenderPose(cycle, animate);
            if (style != BossMovementStyle.CrayonWalk && artwork != null)
            {
                // FixedUpdate advances travel in steps; ease the display between them and into idle.
                bool attackJump = vault != null && vault.IsJumping;
                float blend = attackJump ? 1f : 1f - Mathf.Exp(-Time.deltaTime / 0.035f);
                displayedHopPosition = Vector3.Lerp(displayedHopPosition, artwork.transform.localPosition, blend);
                displayedHopScale = Vector3.Lerp(displayedHopScale, artwork.transform.localScale, blend);
                artwork.transform.localPosition = displayedHopPosition;
                artwork.transform.localScale = displayedHopScale;
            }
            TickLanding(Time.deltaTime);
        }

        private void EnsureArtwork()
        {
            if (artwork != null) return;
            GameObject child = new GameObject("Boss Movement Artwork");
            child.layer = source.gameObject.layer;
            child.transform.SetParent(source.transform, false);
            artwork = child.AddComponent<SpriteRenderer>();
        }

        public void RenderPose(float progress, bool animate)
        {
            Initialize();
            if (!initialized) return;
            EnsureArtwork();
            source.forceRenderingOff = true;
            artwork.enabled = source.enabled && !originalRenderingOff;
            artwork.sharedMaterial = source.sharedMaterial;
            artwork.color = source.color;
            artwork.flipX = source.flipX;
            artwork.flipY = source.flipY;
            artwork.sortingLayerID = source.sortingLayerID;
            artwork.sortingOrder = source.sortingOrder;
            artwork.maskInteraction = source.maskInteraction;
            artwork.sprite = bodySprite != null ? bodySprite : source.sprite;
            artwork.transform.localPosition = Vector3.zero;
            artwork.transform.localScale = Vector3.one;
            artwork.transform.localRotation = Quaternion.identity;
            foreach (SpriteRenderer treasure in groundTreasures)
            {
                treasure.enabled = source.enabled && !originalRenderingOff;
                treasure.flipX = source.flipX;
            }
            if (style == BossMovementStyle.CrayonWalk)
            {
                if (animate && walkFrames.Length > 0)
                    artwork.sprite = walkFrames[Mathf.FloorToInt(Mathf.Repeat(progress, 1f) * walkFrames.Length)];
                return;
            }
            if (style == BossMovementStyle.VaultHop && vault != null && vault.HealPoseIndex >= 0 &&
                healFrames.Length == 4 && healFrames[vault.HealPoseIndex] != null &&
                !boss.IsActionSuppressed && boss.State != BossActionState.PhaseTransition)
            {
                artwork.sprite = healFrames[vault.HealPoseIndex];
                return;
            }
            if (hopFrames.Length == 4)
            {
                float hopPhase = Mathf.Repeat(progress, 1f);
                int pose = !animate ? 0 : hopPhase < 0.12f ? 1 : hopPhase < 0.18f ? 0 :
                    hopPhase < 0.72f ? 2 : hopPhase < 0.86f ? 0 : hopPhase < 0.95f ? 3 : 0;
                if (vault != null && vault.JumpPoseIndex >= 0) pose = vault.JumpPoseIndex;
                artwork.sprite = hopFrames[pose];
            }
            if (!animate || artwork.sprite == null || (vault != null && vault.JumpPoseIndex >= 0)) return;
            float phase = Mathf.Repeat(progress, 1f);
            float lift = 0f;
            // Squared sine gives zero velocity at takeoff/contact and every segment boundary.
            if (phase >= 0.18f && phase < 0.86f) lift = SmoothPulse((phase - 0.18f) / 0.68f);
            artwork.transform.localPosition = new Vector3(0f,
                lift * hopHeight / Mathf.Max(0.001f, Mathf.Abs(source.transform.lossyScale.y)), 0f);
        }

        public void NotifyLanding(float strength = 1f)
        {
            impactAge = 0f;
            impactStrength = Mathf.Clamp01(strength);
        }

        private static float SmoothPulse(float progress)
        {
            float sine = Mathf.Sin(progress * Mathf.PI);
            return sine * sine;
        }

        public void TickLanding(float deltaTime)
        {
            Initialize();
            if (!initialized) return;
            impactAge += Mathf.Max(0f, deltaTime);
            for (int i = 0; i < groundTreasures.Length; i++)
            {
                Transform pile = groundTreasures[i].transform;
                float age = impactAge - i * 0.018f;
                float normalized = Mathf.Clamp01(age / 0.2f);
                float pulse = age >= 0f && normalized < 1f
                    ? Mathf.Sin(normalized * Mathf.PI * 2f) * (1f - normalized) * impactStrength : 0f;
                // First compress on contact, then the loose treasure hops and settles with a slight stagger.
                float lift = Mathf.Max(0f, -pulse) * 0.13f;
                pile.localPosition = treasurePositions[i] + Vector3.up * lift /
                    Mathf.Max(0.001f, Mathf.Abs(pile.parent.lossyScale.y));
                pile.localScale = Vector3.Scale(treasureScales[i], new Vector3(1f + pulse * 0.12f, 1f - pulse * 0.16f, 1f));
                pile.localRotation = treasureRotations[i] * Quaternion.Euler(0f, 0f, pulse * (i == 0 ? -2f : 2f));
                groundTreasures[i].color = source.color;
            }
        }

        private void Restore()
        {
            if (source != null && initialized) source.forceRenderingOff = originalRenderingOff;
            if (artwork != null) artwork.enabled = false;
            if (!initialized) return;
            for (int i = 0; i < groundTreasures.Length; i++)
            {
                if (groundTreasures[i] == null) continue;
                groundTreasures[i].enabled = false;
                groundTreasures[i].transform.localPosition = treasurePositions[i];
                groundTreasures[i].transform.localScale = treasureScales[i];
                groundTreasures[i].transform.localRotation = treasureRotations[i];
            }
        }

        private void OnDisable()
        {
            moving = false;
            cycle = 0f;
            impactAge = 1f;
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
            if (artwork == null) return;
            if (Application.isPlaying) Destroy(artwork.gameObject);
            else DestroyImmediate(artwork.gameObject);
        }
    }
}
