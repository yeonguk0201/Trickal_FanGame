namespace TrickalFanGame.Combat
{
    public readonly struct DamageResult
    {
        public DamageResult(float finalDamage, bool isCritical)
        {
            FinalDamage = finalDamage;
            IsCritical = isCritical;
        }

        public float FinalDamage { get; }
        public bool IsCritical { get; }
    }
}
