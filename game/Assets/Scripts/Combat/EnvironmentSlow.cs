using UnityEngine;

namespace TrickalFanGame.Combat
{
    // Trap-0 (거미줄): a movement slow that terrain puts on a body while it stands there. The terrain renews it every
    // tick, so it ends shortly after the body leaves. Several sources do not stack; the strongest slow wins.
    // It scales ordinary movement only, the same movement the shock slow scales.
    [DisallowMultipleComponent]
    public sealed class EnvironmentSlow : MonoBehaviour
    {
        private float multiplier = 1f;
        private float endTime = float.NegativeInfinity;

        public float MultiplierAt(float currentTime) => currentTime < endTime ? multiplier : 1f;

        public static float MultiplierOf(Component body)
        {
            return body != null && body.TryGetComponent(out EnvironmentSlow slow)
                ? slow.MultiplierAt(Time.time)
                : 1f;
        }

        public static void Apply(GameObject body, float configuredMultiplier, float currentTime, float untilTime)
        {
            if (body == null) return;
            if (!body.TryGetComponent(out EnvironmentSlow slow)) slow = body.AddComponent<EnvironmentSlow>();
            float next = Mathf.Clamp01(configuredMultiplier);
            slow.multiplier = currentTime < slow.endTime ? Mathf.Min(slow.multiplier, next) : next;
            slow.endTime = Mathf.Max(slow.endTime, untilTime);
        }
    }
}
