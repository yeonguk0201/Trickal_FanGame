using UnityEngine;

namespace TrickalFanGame.Enemy
{
    /// <summary>
    /// How long the presentation of a boss's death still runs (황금 용사's truck, 에르핀's victory jump). A cleared
    /// run waits for it before the result screen opens. Presentation only: rewards and the run record do not wait.
    /// </summary>
    public static class BossDefeatPresentation
    {
        private static float busyUntil = float.NegativeInfinity;

        public static float Remaining => Mathf.Max(0f, busyUntil - Time.time);

        public static void Hold(float seconds)
        {
            if (!Application.isPlaying) return;
            busyUntil = Mathf.Max(busyUntil, Time.time + Mathf.Max(0f, seconds));
        }

        public static void Clear() => busyUntil = float.NegativeInfinity;

        // Play Mode without a domain reload restarts Time.time, so an old hold must not carry over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
