using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;

namespace TrickalFanGame.Editor
{
    // Contract-0 §2.1: a golden chest exclusive artifact is an ordinary Artifact whose acquisition path is limited by
    // pool membership. Setups that gather every active artifact (Reward-3) skip these IDs, so the selection reward pool
    // and the shop stock that shares it never offer them.
    public static class GoldenChestExclusivePool
    {
        public const string ArtifactPickupPrefabPath = "Assets/Prefabs/ItemPickup.prefab";

        public static readonly IReadOnlyList<string> ItemIds = new[]
        {
            "artifact-sist-fake-wings", // Flight-0: 시스트의 가짜 날개.
            "artifact-aisia-wallet", // Artifact-3: 아이시아의 지갑.
        };

        public static bool IsExclusive(ItemDefinition definition)
        {
            return definition != null && definition.Kind == ItemKind.Artifact &&
                   ItemIds.Contains(definition.ItemId, StringComparer.Ordinal);
        }

        // Puts every exclusive artifact whose asset exists into the chest content table, in ItemIds order. Each
        // setup that creates one calls this, so re-running an earlier setup keeps the later artifacts in the pool.
        public static void ConfigureTable(string piece)
        {
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            if (table == null)
                throw new InvalidOperationException($"Run Chest-1 setup first: {Week22Chest1Setup.ChestContentTablePath}.");
            ItemPickup pickup = AssetDatabase.LoadAssetAtPath<GameObject>(ArtifactPickupPrefabPath)
                ?.GetComponent<ItemPickup>();
            if (pickup == null)
                throw new InvalidOperationException($"The artifact pickup Prefab is missing: {ArtifactPickupPrefabPath}.");

            ItemDefinition[] artifacts = ItemIds
                .Select(itemId => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Items/{itemId}.asset"))
                .Where(definition => definition != null).ToArray();
            if (table.GoldenExclusiveArtifacts.SequenceEqual(artifacts) && table.ArtifactPickupPrefab == pickup)
                return;

            Undo.RecordObject(table, "Configure golden exclusive pool");
            table.ConfigureGoldenExclusivePool(artifacts, pickup);
            if (!table.TryValidate(out string error))
                throw new InvalidOperationException($"{piece} built an invalid chest content table. {error}");
            EditorUtility.SetDirty(table);
        }
    }
}
