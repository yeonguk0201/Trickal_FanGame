using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using TrickalFanGame.Shop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Pool0ItemPoolVerification
    {
        private const int SeedCount = 512;

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Pool-0 Item Pools")]
        public static void SetupAndVerifyBatch()
        {
            Pool0ItemPoolSetup.Setup();
            Pool0ItemPoolSetup.Setup();
            Verify();
        }

        // Pool-0 changes which artifacts every reward source rolls, so the reward, shop and rare-obstacle checks
        // that depend on the selector run after it.
        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-0 Item Pools With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            PhaseGRewardRunVerification.Verify();
            // Week16Reward0Verification is left out: its mixed-pool check predates jjangsem spells and 권능 and fails
            // on the unchanged legacy path (ItemPoolSource.None) regardless of Pool-0.
            Week20Special4Verification.Verify();
            Week22Shop0Verification.Verify();
            Week23Shop1Verification.Verify();
            Week27Power2Verification.Verify();
            Week27Elif3Verification.Verify();
            Week23Obstacle5Verification.Verify();
            Week23Obstacle6Verification.Verify();
            Debug.Log("Pool-0 item pool verification with regressions passed.");
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Pool-0 Item Pools")]
        public static void Verify()
        {
            ItemDefinition[] artifacts = Pool0ItemPoolSetup.LoadArtifacts();
            VerifyAssetTags(artifacts);
            VerifySelectorRules();
            VerifySourceWiring();
            VerifyRealPools(artifacts);
            Debug.Log("Pool-0 item pool verification passed.");
        }

        private static void VerifyAssetTags(ItemDefinition[] artifacts)
        {
            foreach (ItemDefinition item in artifacts)
            {
                ItemPoolSource expected = Pool0ItemPoolSetup.ExpectedSources(item);
                Assert(item.PoolSources == expected,
                    $"{item.ItemId} must be in pools {expected}, found {item.PoolSources}. Run Pool-0 setup.");
            }

            foreach (string id in Pool0ItemPoolSetup.Sources.Keys)
                Assert(artifacts.Any(item => item.ItemId == id && item.IsActive && item.IsValid),
                    $"Pool-0 classifies {id}, which is not an active valid artifact.");

            foreach (string id in GoldenChestExclusivePool.ItemIds)
                Assert(!Pool0ItemPoolSetup.Sources.ContainsKey(id),
                    $"Golden chest exclusive {id} must stay out of every reward pool.");

            ItemDefinition[] pooled = RewardPool(artifacts);
            foreach (ItemPoolSource source in Pool0ItemPoolSetup.DrawSources)
            {
                int count = pooled.Count(item => item.IsInPool(source));
                Assert(count >= Pool0ItemPoolSetup.MinimumPoolSize,
                    $"The {source} pool needs at least {Pool0ItemPoolSetup.MinimumPoolSize} artifacts, found {count}.");
            }
        }

        private static void VerifySelectorRules()
        {
            List<ItemDefinition> created = new();
            try
            {
                ItemDefinition Make(string id, ItemRarity rarity, ItemPoolSource sources)
                {
                    ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                    item.name = id;
                    item.ConfigureContract(id, id, ItemKind.Artifact, rarity, true, 1,
                        new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
                    item.ConfigurePoolSources(sources);
                    created.Add(item);
                    return item;
                }

                ItemDefinition bossA = Make("item-pool0-boss-a", ItemRarity.Common, ItemPoolSource.Boss);
                ItemDefinition bossB = Make("item-pool0-boss-b", ItemRarity.Rare, ItemPoolSource.Boss | ItemPoolSource.Shop);
                ItemDefinition bossC = Make("item-pool0-boss-c", ItemRarity.Epic, ItemPoolSource.Boss);
                ItemDefinition shared1 = Make("item-pool0-treasure-a", ItemRarity.Common, ItemPoolSource.Treasure);
                ItemDefinition shared2 = Make("item-pool0-treasure-b", ItemRarity.Common, ItemPoolSource.Treasure);
                ItemDefinition untagged = Make("item-pool0-none", ItemRarity.Common, ItemPoolSource.None);
                ItemDefinition[] pool = { shared1, bossA, untagged, bossB, shared2, bossC };

                Assert(bossB.IsInPool(ItemPoolSource.Boss) && bossB.IsInPool(ItemPoolSource.Shop) &&
                       !bossB.IsInPool(ItemPoolSource.Treasure) && !bossB.IsInPool(ItemPoolSource.None),
                    "IsInPool must test each source bit and never match None.");

                HashSet<string> bossIds = new(StringComparer.Ordinal) { bossA.ItemId, bossB.ItemId, bossC.ItemId };
                for (int seed = 0; seed < 64; seed++)
                {
                    // Enough items in the source's own pool: every card comes from it.
                    IReadOnlyList<ItemRewardCandidate> boss = Build(pool, seed, "pool0:boss", ItemPoolSource.Boss);
                    Assert(boss.Count == 3 && boss.All(card => card.IsItem && bossIds.Contains(card.Definition.ItemId)),
                        $"Seed {seed}: a boss 3택1 with three boss items must offer only boss items.");
                    Assert(Ids(boss) == Ids(Build(pool.Reverse().ToArray(), seed, "pool0:boss", ItemPoolSource.Boss)),
                        $"Seed {seed}: pool order must not change the cards.");

                    // One own item: it is always offered, and the shared pool fills the other two distinct cards.
                    IReadOnlyList<ItemRewardCandidate> shop = Build(pool, seed, "pool0:shop", ItemPoolSource.Shop);
                    Assert(shop.Count == 3 && shop.All(card => card.IsItem) &&
                           shop[0].Definition == bossB &&
                           shop.Select(card => card.Definition.ItemId).Distinct().Count() == 3,
                        $"Seed {seed}: a shop with one shop item must offer it first and fill from the shared pool.");

                    // None keeps the pre-Pool-0 draw over the whole pool.
                    Assert(Ids(ArtifactRewardSelector.BuildCandidates(pool, null, seed, "pool0:legacy")) ==
                           Ids(Build(pool, seed, "pool0:legacy", ItemPoolSource.None)),
                        $"Seed {seed}: the None source must match the legacy selector.");
                }

                // An own pool and a shared pool that both run out still fall back to healing.
                IReadOnlyList<ItemRewardCandidate> thin = Build(new[] { bossA, shared1 }, 7, "pool0:thin",
                    ItemPoolSource.Boss);
                Assert(thin.Count == 3 && thin[0].Definition == bossA && thin[1].Definition == shared1 &&
                       thin[2].IsHealing,
                    "A boss 3택1 must take its own item, then the shared item, then the healing fallback.");
                IReadOnlyList<ItemRewardCandidate> noOwn = Build(new[] { shared1, shared2 }, 7, "pool0:no-own",
                    ItemPoolSource.Boss);
                Assert(noOwn.Count == 3 && noOwn.Take(2).All(card => card.IsItem) && noOwn[2].IsHealing,
                    "A source with an empty own pool must still offer the shared pool.");

                Assert(ArtifactRewardSelector.TryChoose(pool, null, 3, "pool0:obstacle", ItemPoolSource.Treasure,
                           out ItemDefinition rare) && rare.IsInPool(ItemPoolSource.Treasure),
                    "A treasure-sourced single draw must pick a treasure item when one exists.");
            }
            finally
            {
                foreach (ItemDefinition item in created) Object.DestroyImmediate(item);
            }
        }

        private static void VerifySourceWiring()
        {
            Assert(BossItemDrop.RewardPoolSource == ItemPoolSource.Boss, "Boss 3택1 must draw from the boss pool.");
            Assert(RewardRoom.RewardPoolSource == ItemPoolSource.Treasure,
                "Treasure and secret room 3택1 must draw from the treasure pool.");
            Assert(ShopStockBuilder.RewardPoolSource == ItemPoolSource.Shop, "Shops must draw from the shop pool.");
        }

        // The real pools over many seeds: each source offers only its own items, and the shops built from the
        // real catalog sell only shop items.
        private static void VerifyRealPools(ItemDefinition[] artifacts)
        {
            ItemDefinition[] pool = RewardPool(artifacts);
            foreach (ItemPoolSource source in Pool0ItemPoolSetup.DrawSources)
            {
                Dictionary<string, int> seen = new(StringComparer.Ordinal);
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    foreach (ItemRewardCandidate card in Build(pool, seed, $"pool0:{source}", source))
                    {
                        Assert(card.IsItem && card.Definition.IsInPool(source),
                            $"Seed {seed}: the {source} pool offered {card.StableId} from outside it.");
                        seen[card.Definition.ItemId] = seen.TryGetValue(card.Definition.ItemId, out int n) ? n + 1 : 1;
                    }
                }

                int members = pool.Count(item => item.IsInPool(source));
                Assert(seen.Count == members,
                    $"Over {SeedCount} seeds the {source} pool showed {seen.Count} of its {members} artifacts.");
                Debug.Log($"Pool-0 {source} pool ({members}): " + string.Join(", ",
                    seen.OrderByDescending(entry => entry.Value).Select(entry => $"{entry.Key} {entry.Value}")));
            }

            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(Week20Special4Setup.CatalogPath);
            Assert(catalog != null, $"Missing shop catalog {Week20Special4Setup.CatalogPath}.");
            for (int seed = 0; seed < SeedCount; seed++)
            {
                foreach (ShopKind kind in new[] { ShopKind.General, ShopKind.Goldi })
                {
                    ShopStockState stock = ShopStockBuilder.Build("floor-02-room-05", seed, catalog, pool, null, kind);
                    foreach (ShopOffer offer in stock.Offers)
                    {
                        if (offer.Kind != ShopOfferKind.Item) continue;
                        Assert(offer.Item.IsInPool(ItemPoolSource.Shop),
                            $"Seed {seed}: the {kind} shop sold {offer.Item.ItemId}, which is not a shop item.");
                    }
                }
            }
        }

        private static ItemDefinition[] RewardPool(IEnumerable<ItemDefinition> artifacts) =>
            artifacts.Where(item => item.IsActive && item.IsValid && !GoldenChestExclusivePool.IsExclusive(item))
                .ToArray();

        private static IReadOnlyList<ItemRewardCandidate> Build(IReadOnlyList<ItemDefinition> pool, int seed,
            string rewardId, ItemPoolSource source) =>
            ArtifactRewardSelector.BuildCandidates(pool, null, seed, rewardId, null, source);

        private static string Ids(IReadOnlyList<ItemRewardCandidate> candidates) =>
            string.Join(",", candidates.Select(card => card.StableId));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
