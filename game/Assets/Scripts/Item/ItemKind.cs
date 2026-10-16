namespace TrickalFanGame.Item
{
    // Values are serialized by number. Append new kinds only; 4 (Trinket) stays reserved (Contract-0). 5 is the
    // 교주의 권능 (Power-0, contract 23 §9).
    public enum ItemKind
    {
        Artifact = 0,
        // Legacy always-on spell. Kept for past Run records and test loadouts, excluded from new reward pools.
        Spell = 1,
        SingleUseSpell = 2,
        JjangsemSpell = 3,
        Active = 5,
    }

    public static class ItemKindText
    {
        public static bool IsSingleUse(ItemKind kind) =>
            kind == ItemKind.SingleUseSpell || kind == ItemKind.JjangsemSpell;

        public static bool IsActivePower(ItemKind kind) => kind == ItemKind.Active;

        public static string GetDisplayName(ItemKind kind) => kind switch
        {
            ItemKind.Artifact => "아티팩트",
            ItemKind.Spell => "스펠",
            ItemKind.SingleUseSpell => "스펠",
            ItemKind.JjangsemSpell => "짱셈스펠",
            ItemKind.Active => "교주의 권능",
            _ => string.Empty,
        };
    }
}
