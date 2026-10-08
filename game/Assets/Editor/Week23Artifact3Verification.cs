using System.Linq;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using static TrickalFanGame.Editor.ArtifactVerificationFixtures;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Artifact-3: 아이시아의 지갑 (golden chest exclusive, gold when acquired) and the game rule that the player's
    // current health and shield together stop at 15 hearts.
    public static class Week23Artifact3Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-7000f, 7000f);

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Artifact-3 Aisia Wallet")]
        public static void SetupAndVerifyBatch()
        {
            Week23Artifact3Setup.Setup();
            string path = ArtifactSetupUtility.ItemPath(Week23Artifact3Setup.WalletId);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            Week23Artifact3Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(guid) && guid == AssetDatabase.AssetPathToGUID(path) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath),
                "Artifact-3 setup changed or lost the item or chest table GUID.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Artifact-3 Aisia Wallet")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            ValidateContractAndPools();
            ValidateWallet();
            ValidateHealthAndShieldLimit();
            Debug.Log("Artifact-3 verification passed: effect type 61 is appended, 아이시아의 지갑 is a Rare one-stack " +
                      "artifact held only by the golden chest exclusive pool, it gives 100 gold cut at the wallet " +
                      "limit of 99 and is still acquired at a full wallet, and the player's current health and " +
                      "shield together stop at 15 hearts: extra shield and healing are dropped without cutting " +
                      "the other, a heart pickup that does not fit stays on the floor, and enemies have no limit.");
        }

        private static void ValidateContractAndPools()
        {
            Assert((int)ItemEffectType.KillFrenzyKnockbackPercent == 60 &&
                   (int)ItemEffectType.GainGoldOnAcquire == 61,
                "Artifact-3 must append effect type 61 without renumbering earlier effects.");
            Assert(!new ItemEffectEntry(ItemEffectType.GainGoldOnAcquire).TryValidate(out _) &&
                   new ItemEffectEntry(ItemEffectType.GainGoldOnAcquire, 100f).TryValidate(out _),
                "The gold on acquire effect needs a positive amount.");

            ItemDefinition wallet = LoadItem(Week23Artifact3Setup.WalletId);
            Assert(wallet.DisplayName == Week23Artifact3Setup.WalletName && wallet.Kind == ItemKind.Artifact &&
                   wallet.Rarity == ItemRarity.Rare && wallet.IsActive && wallet.MaxStacks == 1 &&
                   wallet.Effects.Count == 1 && wallet.Effects[0].EffectType == ItemEffectType.GainGoldOnAcquire &&
                   Mathf.Approximately(wallet.Effects[0].Magnitude, 100f),
                "아이시아의 지갑 must be a Rare one-stack artifact that gives 100 gold.");
            string description = ArtifactEffectDescription.Build(wallet);
            Assert(description == Week23Artifact3Setup.WalletDescription,
                $"아이시아의 지갑 must read '{Week23Artifact3Setup.WalletDescription}', but reads '{description}'.");

            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out _), "The chest content table is missing or invalid.");
            Assert(GoldenChestExclusivePool.IsExclusive(wallet) &&
                   table.GoldenExclusiveArtifacts.Count(artifact => artifact == wallet) == 1 &&
                   table.GoldenExclusiveArtifacts.All(GoldenChestExclusivePool.IsExclusive) &&
                   table.GoldenExclusiveArtifacts.Any(artifact =>
                       artifact != null && artifact.ItemId == Week22Flight0Setup.FakeWingsId),
                "The golden exclusive pool must hold the wallet once beside the fake wings, and only exclusives.");
            Assert(!LoadAssembler().SelectionRewardPool.Contains(wallet),
                "아이시아의 지갑 must stay out of the selection reward pool and the shop stock.");
        }

        private static void ValidateWallet()
        {
            ItemDefinition wallet = LoadItem(Week23Artifact3Setup.WalletId);
            foreach ((int held, int expected) in new[] { (0, 99), (50, 99), (99, 99), (0, 99) })
            {
                GameObject root = new("Artifact-3 Wallet Verification");
                try
                {
                    // The player's own RunProgress keeps the check away from the Game Scene's Run state.
                    GameObject player = CreatePlayer(root.transform, Origin, typeof(RunProgress));
                    RunProgress progress = player.GetComponent<RunProgress>();
                    PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                    if (held > 0) progress.TryAddResource(RunResourceType.Gold, held);
                    Assert(inventory.TryAcquire(wallet) &&
                           progress.GetResourceCount(RunResourceType.Gold) == expected &&
                           progress.GetResourceCount(RunResourceType.Key) == 0,
                        $"With {held} gold the wallet must be acquired and leave {expected} gold, but left " +
                        $"{progress.GetResourceCount(RunResourceType.Gold)}.");
                    Assert(!inventory.TryAcquire(wallet) &&
                           progress.GetResourceCount(RunResourceType.Gold) == expected,
                        "A second wallet must be rejected.");
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void ValidateHealthAndShieldLimit()
        {
            GameObject root = new("Artifact-3 Limit Verification");
            try
            {
                GameObject player = CreatePlayer(root.transform, Origin);
                PlayerStats stats = player.GetComponent<PlayerStats>();
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                Health health = player.GetComponent<Health>();
                float limit = HealthUnits.MaximumHealthAndShieldUnits;
                Assert(Near(limit, 30f) && Near(health.HealthAndShieldLimit, limit) && Near(health.CurrentHealth, 10f),
                    "The player's health and shield limit must be 15 hearts.");

                Assert(health.AddShield(100f) && Near(health.CurrentShield, 20f) && Near(health.CurrentHealth, 10f),
                    "Shield beyond the limit must be dropped without cutting health.");
                Assert(Near(health.GainShield(2f), 0f) && Near(health.CurrentShield, 20f),
                    "At the limit no more shield is gained.");

                // More maximum health at the limit: the added hearts stay empty and the shield is kept.
                ItemDefinition hairpin = LoadItem(Week23Artifact2Setup.HairpinId);
                Assert(inventory.TryAcquire(hairpin) && inventory.TryAcquire(hairpin) && inventory.TryAcquire(hairpin) &&
                       Near(stats.MaxHealth, 16f) && Near(health.MaxHealth, 16f) && Near(health.CurrentHealth, 10f) &&
                       Near(health.CurrentShield, 20f) && Near(health.MissingHealth, 0f),
                    "Maximum health gained at the limit must not heal past it or cut the shield.");
                Assert(Near(health.Heal(4f), 0f) && Near(health.CurrentHealth, 10f),
                    "Healing at the limit must be dropped.");

                GameObject heart = new("Artifact-3 Heart", typeof(Rigidbody2D), typeof(CircleCollider2D),
                    typeof(HealthPickup));
                heart.transform.SetParent(root.transform);
                heart.transform.position = Origin + Vector2.up * 5f;
                HealthPickup pickup = heart.GetComponent<HealthPickup>();
                Assert(!pickup.CanCollect(health) && !pickup.Collect(health) && Near(health.CurrentHealth, 10f),
                    "A heart pickup must stay on the floor while health and shield are at the limit.");

                health.TakeDamage(new DamageContext(null, DamageSourceType.EnemyContact, 4f));
                Assert(Near(health.CurrentShield, 16f) && Near(health.CurrentHealth, 10f) &&
                       Near(health.MissingHealth, 4f),
                    "A hit on the shield must make room under the limit.");
                Assert(pickup.CanCollect(health) && pickup.Collect(health) && Near(health.CurrentHealth, 12f),
                    "A heart pickup that fits under the limit must be collected.");
                Assert(Near(health.Heal(100f), 2f) && Near(health.CurrentHealth, 14f) &&
                       Near(health.CurrentShield, 16f),
                    "A heal must stop at the limit and keep the shield.");

                Health enemy = CreateEnemy(root.transform, Origin + Vector2.up * 10f, 100f);
                Assert(enemy.SetShield(500f) && Near(enemy.CurrentShield, 500f) && Near(enemy.CurrentHealth, 100f) &&
                       Near(enemy.HealthAndShieldLimit, 0f),
                    "Enemies must have no health and shield limit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
