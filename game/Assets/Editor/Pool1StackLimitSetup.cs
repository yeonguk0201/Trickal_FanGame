using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Pool-1: pure-stat artifacts (a number and nothing else) stack without limit; artifacts with special effects keep
    // their stack limit. PlayerStats caps the stats that would break if they grew forever, and the reward selector
    // stops offering an artifact once every stat it raises is at its cap.
    public static class Pool1StackLimitSetup
    {
        // The Pool-0 pure-stat group (boss and shop pools). The older setups that write these assets use 0 too.
        public static readonly IReadOnlyList<string> UnlimitedItemIds = new[]
        {
            "item-01", // 급조한 목검: 공격력
            "item-12", // 녹슨 송곳: 치명 확률
            "item-04", // 낡은 화살: 공격 속도
            "artifact-pork-cutlet-hairpin", // 돈까스 모양 머리핀: 최대 체력
            "artifact-sylla-wind-arrow", // 실라의 바람살: 공속·이속·탄속
            "artifact-kanna-cannon", // 칸나의 대포: 투사체 크기
            // Pool-2: pure-stat artifacts (priority 1 and 2).
            Pool2PureStatArtifactSetup.TowelHeadbandId,
            Pool2PureStatArtifactSetup.OldTreeDaggerId,
            Pool2PureStatArtifactSetup.PatchedStaffId,
            Pool2PureStatArtifactSetup.MistDaggerId,
            Pool2PureStatArtifactSetup.ObsidianShurikenId,
            Pool2PureStatArtifactSetup.FoxDaggerId,
            Pool2PureStatArtifactSetup.CombatManualId,
            Pool2PureStatArtifactSetup.JadeGrimoireId,
            Pool2PureStatArtifactSetup.SharpStaffId,
            Pool2PureStatArtifactSetup.GoldenCrownId,
            Pool2PureStatArtifactSetup.BelitaStaffId,
            Pool2PureStatArtifactSetup.WeightedVestId,
            Pool2PureStatArtifactSetup.FairyRobeId,
            Pool2PureStatArtifactSetup.ScaleArmorId,
            Pool2PureStatArtifactSetup.JadeFountainPenId,
            Pool2PureStatArtifactSetup.ShiningTiaraId,
            Pool2PureStatArtifactSetup.PriestCenserId,
            Pool2PureStatArtifactSetup.BlanchetBluebirdId,
        };

        public static bool IsUnlimited(string itemId) => UnlimitedItemIds.Contains(itemId, StringComparer.Ordinal);

        [MenuItem("Trickal Fan Game/Week 28/Setup Pool-1 Stack Limits")]
        public static void Setup()
        {
            ItemDefinition[] artifacts = Pool0ItemPoolSetup.LoadArtifacts();
            string[] missing = UnlimitedItemIds.Where(id => artifacts.All(item => item.ItemId != id)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Pool-1 lists artifacts that have no asset: " +
                                                    string.Join(", ", missing));

            int changed = 0;
            foreach (ItemDefinition item in artifacts.Where(item => IsUnlimited(item.ItemId)))
            {
                if (item.MaxStacks == 0) continue;
                Undo.RecordObject(item, "Configure Pool-1 stack limits");
                item.ConfigureMaxStacks(0);
                EditorUtility.SetDirty(item);
                changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            Debug.Log($"Pool-1 stack limits ready ({changed} artifact(s) updated).");
        }
    }
}
