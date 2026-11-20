using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// How long the presentation of a boss's death still runs (황금 용사's truck, 에르핀's victory jump). A cleared
    /// run waits for it before the result screen opens. Presentation only: rewards and the run record do not wait.
    /// A dying boss also marks when 에르핀 may start her victory jump (after its truck has hit, when it has one); only
    /// the last boss's clear plays it, through the run's VICTORY sequence.
    /// </summary>
    public static class BossDefeatPresentation
    {
        private static float busyUntil = float.NegativeInfinity;
        private static float victoryCueAt = float.NegativeInfinity;

        public static float Remaining => Mathf.Max(0f, busyUntil - Time.time);
        // Seconds until the victory jump may start; 0 once the cue has passed or when no boss set one.
        public static float VictoryCueRemaining => Mathf.Max(0f, victoryCueAt - Time.time);

        public static void CueVictory(float delay)
        {
            if (!Application.isPlaying) return;
            victoryCueAt = Time.time + Mathf.Max(0f, delay);
        }

        public static void Hold(float seconds)
        {
            if (!Application.isPlaying) return;
            busyUntil = Mathf.Max(busyUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public static void Clear()
        {
            busyUntil = float.NegativeInfinity;
            victoryCueAt = float.NegativeInfinity;
        }

        // Play Mode without a domain reload restarts Time.time, so an old hold must not carry over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
