namespace TrickalFanGame.Item
{
    // Runs the effect of a single-use spell or jjangsem spell. CanExecute must report every condition that would
    // make Execute fail, because the slot consumes the item between the two calls.
    public interface ISingleUseItemExecutor
    {
        bool CanExecute(ItemDefinition definition, out string reason);
        void Execute(ItemDefinition definition);
    }
}
