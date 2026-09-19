namespace TrickalFanGame.Enemy
{
    public interface IBossPatternRuntime
    {
        void BeginCombat(UnityEngine.Transform target, int seed, float now);
        bool CanSelect(BossPatternExecution execution);
        void OnPatternStateChanged(BossActionState state, BossPatternExecution execution, float stateEndsAt);
        bool TryExecute(BossPatternExecution execution);
        void TickPattern(BossActionState state, BossPatternExecution execution, float now);
    }
}
