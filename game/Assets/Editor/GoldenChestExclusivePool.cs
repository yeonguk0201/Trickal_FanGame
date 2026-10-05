using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;

namespace TrickalFanGame.Editor
{
    // Contract-0 §2.1: a golden chest exclusive artifact is an ordinary Artifact whose acquisition path is limited by
    // pool membership. Setups that gather every active artifact (Reward-3) skip these IDs, so the selection reward pool
    // and the shop stock that shares it never offer them.
    public static class GoldenChestExclusivePool
    {
        public static readonly IReadOnlyList<string> ItemIds = new[]
        {
            "artifact-sist-fake-wings", // Flight-0: 시스트의 가짜 날개.
        };

        public static bool IsExclusive(ItemDefinition definition)
        {
            return definition != null && definition.Kind == ItemKind.Artifact &&
                   ItemIds.Contains(definition.ItemId, StringComparer.Ordinal);
        }
    }
}
