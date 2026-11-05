using System;

namespace TrickalFanGame.Item
{
    // Pool-0: which reward sources draw an artifact first. Values are serialized as bit flags; append new bits only.
    // None as a draw source means the whole pool with no preference (the pre-Pool-0 behavior).
    [Flags]
    public enum ItemPoolSource
    {
        None = 0,
        Boss = 1 << 0,
        Treasure = 1 << 1,
        Shop = 1 << 2,
    }
}
