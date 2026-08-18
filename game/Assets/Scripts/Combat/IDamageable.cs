namespace TrickalFanGame.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(int amount);
    }
}
