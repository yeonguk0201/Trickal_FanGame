using UnityEngine;

namespace TrickalFanGame.Combat
{
    public readonly struct DamageContext
    {
        public DamageContext(
            GameObject source,
            DamageSourceType sourceType,
            int baseDamage,
            float multiplier = 1f)
        {
            Source = source;
            SourceType = sourceType;
            BaseDamage = Mathf.Max(0, baseDamage);
            Multiplier = Mathf.Max(0f, multiplier);
        }

        public GameObject Source { get; }
        public DamageSourceType SourceType { get; }
        public int BaseDamage { get; }
        public float Multiplier { get; }
    }
}
