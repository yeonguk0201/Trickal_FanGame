using System;
using System.Collections.Generic;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEngine;

namespace TrickalFanGame.Frontend
{
    // User-reviewed values live in Resources, independent of shared prefab variants and object names.
    public static class GroundShadowProfiles
    {
        public const string SpellProfileId = "single-spell-afterimage";
        [Serializable] public sealed class Entry
        {
            public int number;
            public string id;
            public float widthMultiplier, thickness, offsetX, offsetY, opacity;
        }
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        private static Dictionary<string, Entry> entries;

        public static bool TryGet(string id, out Entry entry)
        {
            if (entries == null)
            {
                entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
                TextAsset asset = Resources.Load<TextAsset>("GroundShadowProfiles");
                if (asset != null)
                    foreach (Entry value in JsonUtility.FromJson<Catalog>(asset.text).entries)
                        entries.Add(value.id, value);
            }
            entry = null;
            return !string.IsNullOrEmpty(id) && entries.TryGetValue(id, out entry);
        }

        public static string ResolveId(GameObject owner, string explicitId)
        {
            if (owner.GetComponent<PlayerMovement>() != null) return "player-erpin";
            var item = owner.GetComponent<ItemPickup>();
            if (item != null) return ResolveItemId(item.Definition);
            var spell = owner.GetComponent<SingleUseItemPickup>();
            if (spell != null) return ResolveItemId(spell.Definition);
            var resource = owner.GetComponent<RunResourcePickup>();
            if (resource != null) return resource.ResourceType switch
            {
                RunResourceType.Key => "KeyPickup", RunResourceType.Bomb => "BombPickup", _ => "ElifPickup",
            };
            if (owner.GetComponent<HealthPickup>() != null) return "HealthPickup";
            if (owner.GetComponent<SPPickup>() != null) return "SPPickup";
            if (owner.GetComponent<PlacedBomb>() != null) return "PlacedBomb";
            var chest = owner.GetComponent<TreasureChest>();
            if (chest != null) return "chest-" + chest.Kind + "-" + chest.IsOpened;
            var obstacle = owner.GetComponent<DestructibleObstacle>();
            if (obstacle != null && string.IsNullOrEmpty(explicitId))
            {
                string id = obstacle.VariantId == "tree" ? "tree" : "obstacle-" + obstacle.VariantId;
                if (TryGet(id, out _)) return id;
            }
            var artwork = owner.GetComponent<FairyKingdomArtworkView>();
            if (artwork != null)
            {
                switch (artwork.ArtworkId)
                {
                    case "crumb-minion-pink": return "BuseureogiCrumbMinion";
                    case "crumb-minion-cream": return "crumb-cream";
                    case "crumb-minion-chocolate": return "crumb-chocolate";
                    case "terrain-pillar": return "pillar";
                }
            }
            var keeper = owner.GetComponent<ShopKeeper>();
            if (keeper != null && keeper.Portrait != null)
                return keeper.Portrait.sprite == UserArtwork.Load(ShopKeeper.GoldiArtworkKey) ? "npc-goldi" : "npc-sist";
            return explicitId;
        }

        public static string ResolveItemId(ItemDefinition definition) => definition == null ? null :
            definition.Kind == ItemKind.Artifact ? definition.ItemId : SpellProfileId;
    }
}
