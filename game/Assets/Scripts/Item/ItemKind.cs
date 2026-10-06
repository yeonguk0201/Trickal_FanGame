namespace TrickalFanGame.Item
{
    // Values are serialized by number. Append new kinds only; 4 (Trinket) and 5 (Active) are reserved (Contract-0).
    public enum ItemKind
    {
        Artifact = 0,
        // Legacy always-on spell. Kept for past Run records and test loadouts, excluded from new reward pools.
        Spell = 1,
        SingleUseSpell = 2,
        JjangsemSpell = 3,
    }

    public static class ItemKindText
    {
        public static bool IsSingleUse(ItemKind kind) =>
            kind == ItemKind.SingleUseSpell || kind == ItemKind.JjangsemSpell;

        public static string GetDisplayName(ItemKind kind) => kind switch
        {
            ItemKind.Artifact => "아티팩트",
            ItemKind.Spell => "스펠",
            ItemKind.SingleUseSpell => "스펠",
            ItemKind.JjangsemSpell => "짱셈스펠",
            _ => string.Empty,
        };
    }
}
