using UnityEngine;

namespace TrickalFanGame.Combat
{
    // A short freeze when the player's critical hit lands. It only ever owns the time scale while that scale
    // is its own value, so a pause or result screen that takes over in the middle is never overridden.
    public sealed class HitStop : MonoBehaviour
    {
        public const float Duration = 0.03f;
        // Not 0: pause checks across the game read a zero time scale as "paused".
        public const float StoppedTimeScale = 0.02f;
        // Rapid critical hits do not chain into a stutter.
        public const float MinimumInterval = 0.35f;

        private static HitStop instance;

        private float endsAt;
        private float nextAllowedAt;
        private bool stopped;

        public bool IsStopped => stopped;

        public static void Play()
        {
            if (!Application.isPlaying) return;
            if (instance == null)
            {
                var owner = new GameObject("Hit stop");
                DontDestroyOnLoad(owner);
                instance = owner.AddComponent<HitStop>();
            }

            instance.TryBegin(Time.unscaledTime);
        }

        // now: unscaled seconds. Public so Editor verification can drive the clock.
        public bool TryBegin(float now)
        {
            if (stopped || now < nextAllowedAt || Time.timeScale != 1f) return false;
            stopped = true;
            endsAt = now + Duration;
            nextAllowedAt = now + MinimumInterval;
            Time.timeScale = StoppedTimeScale;
            return true;
        }

        public void Tick(float now)
        {
            if (stopped && now >= endsAt) stopped = false;
            // Also covers a pause that opened during the stop and later restored the stopped scale it had saved.
            if (!stopped && Time.timeScale == StoppedTimeScale) Time.timeScale = 1f;
        }

        private void Update() => Tick(Time.unscaledTime);

        private void OnDestroy()
        {
            if (Time.timeScale == StoppedTimeScale) Time.timeScale = 1f;
        }
    }
}
