namespace TrickalFanGame.Room
{
    // Chest-0: the three chest kinds. Stored by value in Run room state; keep the numbers and append new kinds.
    public enum ChestKind
    {
        // Opens when the player touches it.
        Normal = 0,
        // Opens when the player touches it while holding a key, spending one key.
        Golden = 1,
        // Opens only from a player bomb explosion.
        Diamond = 2,
    }

    public enum ChestOpenMethod
    {
        Touch = 0,
        TouchWithKey = 1,
        Bomb = 2,
    }
}
