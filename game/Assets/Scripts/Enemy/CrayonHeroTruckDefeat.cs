using System;
using TrickalFanGame.Combat;
using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// Presentation only (2026-11-19 user drawings, timing tuned in output/crayon-hero-truck-defeat). When 황금 용사
    /// dies a truck drives in from outside the room, hits it with the drawn crash frames and drives on out of the
    /// room. The boss itself is still removed at once, so room clear, rewards and collisions are unchanged.
    /// Every size and offset is a share of the boss's visible height, so it follows the boss's scale.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CrayonHeroTruckDefeat : MonoBehaviour
    {
        // The truck's two wheel frames and the crash frames in play order. Each rect is the picture's place in
        // boss heights: x forward from the truck anchor, y up from the boss's feet.
        [SerializeField] private Sprite[] driveFrames = Array.Empty<Sprite>();
        [SerializeField] private Rect[] driveRects = Array.Empty<Rect>();
        [SerializeField] private Sprite[] impactFrames = Array.Empty<Sprite>();
        [SerializeField] private Rect[] impactRects = Array.Empty<Rect>();
        [SerializeField] private float[] impactSeconds = Array.Empty<float>();
        // Where the truck's bottom centre stands when it hits, from the boss's feet, in boss heights.
        [SerializeField] private Vector2 anchorFromFeet = new(-1.592f, 0f);
        // The boss drawing's visible height and feet as shares of its sprite, measured by the Editor setup.
        [SerializeField, Range(0.01f, 1f)] private float bodyVisibleShare = 1f;
        [SerializeField, Range(0f, 1f)] private float bodyBottomShare;
        [SerializeField, Min(0f)] private float waitSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float approachSeconds = 0.55f;
        // World units, so the truck starts and ends outside the room whatever the boss's size.
        [SerializeField, Min(0f)] private float approachDistance = 16f;
        [SerializeField, Min(0f)] private float impactTravel = 0.3618f;
        [SerializeField, Min(0.01f)] private float exitSeconds = 0.9f;
        [SerializeField, Min(0f)] private float exitDistance = 30f;
        [SerializeField, Range(0f, 1f)] private float exitAcceleration = 0.3f;
        [SerializeField, Min(1f)] private float wheelFramesPerSecond = 16f;
        [SerializeField, Min(0f)] private float shakePeak = 0.25f;
        [SerializeField, Min(0f)] private float shakeSeconds = 0.3f;
        // 에르핀 jumps for joy this long after the truck hits.
        [SerializeField, Min(0f)] private float victoryAfterImpact = 0.6f;

        private Health health;

        public Sprite[] DriveFrames => driveFrames;
        public Rect[] DriveRects => driveRects;
        public Sprite[] ImpactFrames => impactFrames;
        public Rect[] ImpactRects => impactRects;
        public float[] ImpactSeconds => impactSeconds;
        public Vector2 AnchorFromFeet => anchorFromFeet;
        public float BodyVisibleShare => bodyVisibleShare;
        public float BodyBottomShare => bodyBottomShare;
        public float WaitSeconds => waitSeconds;
        public float ApproachSeconds => approachSeconds;
        public float ApproachDistance => approachDistance;
        public float ImpactTravel => impactTravel;
        public float ExitSeconds => exitSeconds;
        public float ExitDistance => exitDistance;
        public float ExitAcceleration => exitAcceleration;
        public float WheelFramesPerSecond => wheelFramesPerSecond;
        public float ShakePeak => shakePeak;
        public float ShakeSeconds => shakeSeconds;
        public float ImpactStart => waitSeconds + approachSeconds;
        public float ImpactDuration
        {
            get
            {
                float total = 0f;
                foreach (float seconds in impactSeconds) total += Mathf.Max(0.001f, seconds);
                return total;
            }
        }
        public float Duration => ImpactStart + ImpactDuration + exitSeconds;
        public float VictoryDelay => ImpactStart + victoryAfterImpact;

        public bool HasFrames =>
            driveFrames != null && driveFrames.Length > 0 && driveRects != null && driveRects.Length == driveFrames.Length &&
            impactFrames != null && impactFrames.Length > 0 && impactRects != null &&
            impactRects.Length == impactFrames.Length && impactSeconds != null &&
            impactSeconds.Length == impactFrames.Length && Array.TrueForAll(driveFrames, s => s != null) &&
            Array.TrueForAll(impactFrames, s => s != null);

        public void Configure(Sprite[] drive, Rect[] driveAt, Sprite[] impact, Rect[] impactAt, float[] impactTimes,
            Vector2 anchor, float visibleShare, float bottomShare)
        {
            driveFrames = drive;
            driveRects = driveAt;
            impactFrames = impact;
            impactRects = impactAt;
            impactSeconds = impactTimes;
            anchorFromFeet = anchor;
            bodyVisibleShare = Mathf.Clamp(visibleShare, 0.01f, 1f);
            bodyBottomShare = Mathf.Clamp01(bottomShare);
        }

        public void ConfigureTiming(float wait, float approach, float approachUnits, float travel, float exit,
            float exitUnits, float acceleration, float wheelFps, float shake, float shakeTime, float victoryAfter)
        {
            waitSeconds = Mathf.Max(0f, wait);
            approachSeconds = Mathf.Max(0.01f, approach);
            approachDistance = Mathf.Max(0f, approachUnits);
            impactTravel = Mathf.Max(0f, travel);
            exitSeconds = Mathf.Max(0.01f, exit);
            exitDistance = Mathf.Max(0f, exitUnits);
            exitAcceleration = Mathf.Clamp01(acceleration);
            wheelFramesPerSecond = Mathf.Max(1f, wheelFps);
            shakePeak = Mathf.Max(0f, shake);
            shakeSeconds = Mathf.Max(0f, shakeTime);
            victoryAfterImpact = Mathf.Max(0f, victoryAfter);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (Application.isPlaying) SpawnView();
        }

        // Leaves the truck scene where the boss stood. Also used by the Editor verifier.
        public CrayonHeroTruckDefeatView SpawnView()
        {
            if (!HasFrames) return null;
            SpriteRenderer body = EnemyDefeatAnimation.FindBody(gameObject);
            return body != null ? CrayonHeroTruckDefeatView.Spawn(this, body) : null;
        }
    }
}
