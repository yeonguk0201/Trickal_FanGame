using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;

namespace TrickalFanGame.Editor
{
    // Contract-0 §2.2: each legacy always-on spell leaves the selection reward pool (and the shop stock that shares
    // it) once its single-use replacement exists. The legacy asset, ID and Backend catalog entry stay active for
    // past Run records and the item test room.
    public static class LegacySpellRetirement
    {
        public static readonly IReadOnlyList<string> RetiredItemIds = new[]
        {
            "spell-catch-that-one", // Spell-1: replaced by single-spell-catch-that-one.
        };

        public static bool IsRetired(ItemDefinition definition)
        {
            return definition != null && definition.Kind == ItemKind.Spell &&
                   RetiredItemIds.Contains(definition.ItemId, StringComparer.Ordinal);
        }
    }
}
