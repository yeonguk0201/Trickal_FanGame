using UnityEngine;

namespace TrickalFanGame.Item
{
    // In-game presentation guide: the gift above a reward altar bobs up and down on its local Y only. The reward root
    // keeps the position, trigger and selection; the gift has no collider.
    [DisallowMultipleComponent]
    public sealed class RewardGiftFloat : MonoBehaviour
    {
        public const float DefaultAmplitude = 0.05f;
        public const float DefaultPeriod = 1.6f;

        [SerializeField] private float baseLocalY;
        [SerializeField, Min(0f)] private float amplitude = DefaultAmplitude;
        [SerializeField, Min(0.1f)] private float period = DefaultPeriod;

        public float BaseLocalY => baseLocalY;
        public float Amplitude => amplitude;
        public float Period => period;

        public void Configure(float configuredBaseLocalY, float configuredAmplitude = DefaultAmplitude,
            float configuredPeriod = DefaultPeriod)
        {
            baseLocalY = configuredBaseLocalY;
            amplitude = Mathf.Max(0f, configuredAmplitude);
            period = Mathf.Max(0.1f, configuredPeriod);
            Apply(0f);
        }

        public static float Offset(float time, float amplitude, float period) =>
            amplitude * Mathf.Sin(time * Mathf.PI * 2f / Mathf.Max(0.1f, period));

        private void Update() => Apply(Time.time);

        private void OnDisable() => Apply(0f);

        private void Apply(float time)
        {
            Vector3 position = transform.localPosition;
            position.y = baseLocalY + Offset(time, amplitude, period);
            transform.localPosition = position;
        }
    }
}
