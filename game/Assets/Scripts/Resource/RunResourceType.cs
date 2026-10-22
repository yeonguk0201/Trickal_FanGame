namespace TrickalFanGame.Resource
{
    // Serialized by value in pickup prefabs; keep existing numbers stable and append new resources.
    // Gold-0: value 0 is the Run-only currency, renamed from Elif. Elif-2: the permanent elif is appended as 3
    // (contract 27 §6.3). It is not held in the Run wallet: RunProgress counts it against the server balance.
    public enum RunResourceType
    {
        Gold = 0,
        Key = 1,
        Bomb = 2,
        Elif = 3,
    }
}
