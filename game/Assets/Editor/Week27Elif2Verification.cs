using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Network;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TrickalFanGame.Editor.ArtifactVerificationFixtures;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Elif-2 (contract 27 §7.1): the permanent elif on the Unity side. Checks the stable enum values, the 7% gold
    // conversion and its seed replay, 아이시아의 지갑's one-time roll, the Run's elif accounting, the Run save request,
    // the chest drop path and that 멜룬카드 does not copy elif. The HUD cell is checked by UX-1 verification.
    public static class Week27Elif2Verification
    {
        private const int ConversionSamples = 200000;
        private const int WalletSamples = 100000;

        [MenuItem("Trickal Fan Game/Week 27/Verify Elif-2 Permanent Elif")]
        public static void Verify()
        {
            ValidateContract();
            ValidateConversionRoll();
            ValidateWalletRoll();
            ValidateRunAccounting();
            ValidatePickups();
            ValidateWalletAcquire();
            ValidateRunRequest();
            ValidateChestPath();
            Debug.Log("Week 27 Elif-2 verification passed: Elif = 3 after gold/key/bomb, gold pickups turn into " +
                      "elif at 7% from a separate seeded hash (same seed, same result; drop rolls untouched), the " +
                      "wallet rolls 0-4 once at 40/30/15/10/5%, the Run counts elif from its starting balance with " +
                      "no 99 cap, development elif is usable but not saved, the save request carries gained/spent " +
                      "(capped at 50) on both request shapes, chest gold follows the hash and 멜룬카드 skips elif.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week27Elif2Setup.Setup();
            Verify();
            UserArtworkVerification.Verify();
            Week27Ux1Verification.Verify();
        }

        // The wallet, the HUD layout, resource pickups, chests, obstacles and 멜룬카드 share the changed paths.
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week23Artifact3Verification.Verify();
            Week17Resource1Verification.Verify();
            Week17Resource3Verification.Verify();
            Week18Obstacle1Verification.Verify();
            Week21Gold0Verification.Verify();
            Week22Chest0Verification.Verify();
            Week22Chest1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week23Obstacle5Verification.Verify();
            Week20DevPanelVerification.Verify();
            Verify();
            Debug.Log("Week 27 Elif-2 regression verification passed.");
        }

        private static void ValidateContract()
        {
            Assert((int)RunResourceType.Gold == 0 && (int)RunResourceType.Key == 1 && (int)RunResourceType.Bomb == 2 &&
                   (int)RunResourceType.Elif == 3,
                "RunResourceType must keep gold 0, key 1, bomb 2 and append Elif as 3 (contract 23 §6, 27 §6.3).");
            Assert((int)ItemEffectType.RandomStatShift == 85 && (int)ItemEffectType.GainPermanentElifRollOnAcquire == 86,
                "Elif-2 must append effect type 86 without renumbering earlier effects.");
            Assert(new ItemEffectEntry(ItemEffectType.GainPermanentElifRollOnAcquire).TryValidate(out string error),
                "The wallet's elif roll needs no value fields. " + error);
            RunResourceWallet wallet = new();
            Assert(!RunResourceWallet.IsDefined(RunResourceType.Elif) && !wallet.CanAccept(RunResourceType.Elif) &&
                   wallet.Add(RunResourceType.Elif, 1) == 0,
                "The Run wallet must not hold the permanent elif.");
            Assert(Mathf.Approximately(PermanentElif.ConversionChance, 0.07f) &&
                   PermanentElif.WalletWeights.SequenceEqual(new[] { 40, 30, 15, 10, 5 }) &&
                   PermanentElif.RunGainMax == 50 && PermanentElif.BalanceMax == 9999,
                "Elif-2 must use the contract 27 values: 7%, wallet 40/30/15/10/5, Run gain 50, balance 9999.");
            Assert(GameResourceHudView.LabelOf(RunResourceType.Elif) == "엘리프" &&
                   GameResourceHudView.FormatCount(RunResourceType.Elif, 7) == "07" &&
                   GameResourceHudView.FormatCount(RunResourceType.Elif, 150) == "150" &&
                   GameResourceHudView.FormatCount(RunResourceType.Gold, 150) == "99",
                "The HUD must show elif as two or more digits without the 99 cap.");
        }

        private static void ValidateConversionRoll()
        {
            int converted = 0;
            for (int sample = 0; sample < ConversionSamples; sample++)
            {
                int seed = sample / 40 * 7919 - 1000003;
                string room = $"floor-{sample % 3 + 1}-room-{sample / 3 % 8 + 1:00}";
                if (PermanentElif.ShouldConvert(seed, room, $"obstacle-{sample % 40:00}", sample % 4)) converted++;
            }

            float rate = converted / (float)ConversionSamples;
            Assert(Mathf.Abs(rate - PermanentElif.ConversionChance) <= 0.003f,
                $"Gold pickups must turn into elif near 7% over many seeds; got {rate:P2}.");

            for (int sample = 0; sample < 1000; sample++)
            {
                float roll = PermanentElif.ConversionRoll(sample, "floor-1-room-02", "clear-chest", sample % 5);
                Assert(roll >= 0f && roll < 1f &&
                       Mathf.Approximately(roll,
                           PermanentElif.ConversionRoll(sample, "floor-1-room-02", "clear-chest", sample % 5)),
                    "The same Run seed and drop must replay the same conversion roll.");
            }

            int differentIndex = 0, differentSource = 0, differentRoom = 0;
            for (int seed = 0; seed < 1000; seed++)
            {
                float roll = PermanentElif.ConversionRoll(seed, "floor-1-room-02", "obstacle-03", 0);
                if (!Mathf.Approximately(roll, PermanentElif.ConversionRoll(seed, "floor-1-room-02", "obstacle-03", 1)))
                    differentIndex++;
                if (!Mathf.Approximately(roll, PermanentElif.ConversionRoll(seed, "floor-1-room-02", "obstacle-04", 0)))
                    differentSource++;
                if (!Mathf.Approximately(roll, PermanentElif.ConversionRoll(seed, "floor-1-room-03", "obstacle-03", 0)))
                    differentRoom++;
            }

            Assert(differentIndex > 990 && differentSource > 990 && differentRoom > 990,
                "Each pickup of a drop source, each source and each room must roll on its own.");
        }

        private static void ValidateWalletRoll()
        {
            int[] counts = new int[PermanentElif.WalletWeights.Length];
            long total = 0;
            for (int seed = 0; seed < WalletSamples; seed++)
            {
                int amount = PermanentElif.RollWallet(seed * 31 - 50000, Week23Artifact3Setup.WalletId);
                Assert(amount >= 0 && amount < counts.Length, $"The wallet rolled {amount} elif.");
                Assert(amount == PermanentElif.RollWallet(seed * 31 - 50000, Week23Artifact3Setup.WalletId),
                    "The wallet roll must replay from the Run seed.");
                counts[amount]++;
                total += amount;
            }

            for (int amount = 0; amount < counts.Length; amount++)
            {
                float share = counts[amount] / (float)WalletSamples;
                float expected = PermanentElif.WalletWeights[amount] / 100f;
                Assert(Mathf.Abs(share - expected) <= 0.01f,
                    $"The wallet must roll {amount} elif {expected:P0} of the time; got {share:P2}.");
            }

            float average = total / (float)WalletSamples;
            Assert(Mathf.Abs(average - 1.1f) <= 0.03f, $"The wallet must average 1.1 elif; got {average:F3}.");
        }

        private static void ValidateRunAccounting()
        {
            GameObject holder = new("Elif-2 Accounting");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                int reported = -1;
                progress.ResourceChanged += (type, count) =>
                {
                    if (type == RunResourceType.Elif) reported = count;
                };
                Assert(progress.GetResourceCount(RunResourceType.Elif) == 0 && progress.AvailableElif == 0,
                    "A new Run must start with no elif before its balance is set.");
                progress.SetElifStartingBalance(5);
                Assert(progress.AvailableElif == 5 && reported == 5, "The starting balance must be usable and shown.");
                Assert(progress.CanAcceptResource(RunResourceType.Elif) &&
                       progress.TryAddResource(RunResourceType.Elif, 150) == 150 &&
                       progress.ElifGained == 150 && progress.AvailableElif == 155 && reported == 155 &&
                       progress.GetResourceCount(RunResourceType.Gold) == 0,
                    "Elif gained in a Run has no 99 cap and never adds gold.");
                Assert(!progress.TrySpendResource(RunResourceType.Elif, 156) && progress.ElifSpent == 0 &&
                       progress.TrySpendResource(RunResourceType.Elif, 4) && progress.ElifSpent == 4 &&
                       progress.AvailableElif == 151,
                    "Spending must fail without enough elif and leave nothing spent; a spend must count once.");
                progress.AddDevelopmentElif(5);
                Assert(progress.DevelopmentElif == 5 && progress.ElifGained == 150 && progress.AvailableElif == 156,
                    "Development elif must be usable without counting as the Run's gain.");
                RunElifDto request = RunSession.BuildRunElif(progress);
                Assert(request.gained == PermanentElif.RunGainMax && request.spent == 4 &&
                       request.upgradePurchases != null && request.upgradePurchases.Length == 0,
                    "The save request must cap the gain at 50, keep the spending and leave development elif out.");

                progress.StopProgression();
                Assert(!progress.CanAcceptResource(RunResourceType.Elif) &&
                       progress.TryAddResource(RunResourceType.Elif, 1) == 0 &&
                       !progress.TrySpendResource(RunResourceType.Elif, 1) && progress.ElifGained == 150,
                    "No elif may be gained or spent after the Run has ended.");
                progress.ResetProgress();
                Assert(progress.AvailableElif == 0 && progress.ElifGained == 0 && progress.ElifSpent == 0 &&
                       progress.DevelopmentElif == 0 && progress.ElifStartingBalance == 0 && reported == 0,
                    "Resetting the Run must clear its elif record.");
                progress.SetElifStartingBalance(20000);
                Assert(progress.ElifStartingBalance == PermanentElif.BalanceMax,
                    "The starting balance must stay within the server cap.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static void ValidatePickups()
        {
            RunResourcePickup goldPrefab = Week17Resource1Setup.LoadPrefab(RunResourceType.Gold);
            RunResourcePickup keyPrefab = Week17Resource1Setup.LoadPrefab(RunResourceType.Key);
            Assert(goldPrefab != null && keyPrefab != null, "Run Resource-1 setup first: the pickup prefabs are missing.");
            Assert(goldPrefab.ResourceType == RunResourceType.Gold && goldPrefab.Amount == 1 &&
                   AssetDatabase.GetAssetPath(goldPrefab) == Week17Resource1Setup.ElifPrefabPath,
                "ElifPickup.prefab must stay the one-gold pickup (contract 27 §8).");
            RunResourcePickup elifPrefab = Week17Resource1Setup.LoadPrefab(RunResourceType.Elif);
            Assert(elifPrefab != null && elifPrefab.ResourceType == RunResourceType.Elif && elifPrefab.Amount == 1 &&
                   AssetDatabase.GetAssetPath(elifPrefab) == Week17Resource1Setup.PermanentElifPrefabPath,
                "Run Elif-2 setup first: PermanentElifPickup.prefab must grant one permanent elif.");
            GameObject holder = new("Elif-2 Pickups");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                Assert(progress.TryInitializeRunSeed(12345, out string seedError), seedError);
                (int convertIndex, int keepIndex) = FindIndices(12345, "floor-1-room-02", "obstacle-01");

                GameObject unseeded = Spawn(goldPrefab, holder.transform, "Obstacle Drop elif - obstacle-01");
                Assert(!PermanentElif.TryConvertSpawnedGold(unseeded, 12345, false, "floor-1-room-02", "obstacle-01",
                           convertIndex) && unseeded.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Gold,
                    "A Run without a seed must not convert gold.");
                GameObject key = Spawn(keyPrefab, holder.transform, "Obstacle Drop key - obstacle-01");
                Assert(!PermanentElif.TryConvertSpawnedGold(key, 12345, true, "floor-1-room-02", "obstacle-01",
                        convertIndex) && key.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Key,
                    "Only gold pickups may turn into elif.");
                GameObject kept = Spawn(goldPrefab, holder.transform, "Obstacle Drop elif - obstacle-01");
                Assert(!PermanentElif.TryConvertSpawnedGold(kept, 12345, true, "floor-1-room-02", "obstacle-01",
                        keepIndex) && kept.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Gold,
                    "A gold pickup whose roll misses must stay gold.");

                GameObject elif = Spawn(goldPrefab, holder.transform, "Obstacle Drop elif - obstacle-01");
                Assert(PermanentElif.TryConvertSpawnedGold(elif, 12345, true, "floor-1-room-02", "obstacle-01",
                        convertIndex),
                    "A gold pickup whose roll hits must turn into elif.");
                RunResourcePickup elifPickup = elif.GetComponent<RunResourcePickup>();
                SpriteRenderer display = elif.GetComponentInChildren<SpriteRenderer>();
                Assert(elifPickup.ResourceType == RunResourceType.Elif && elifPickup.Amount == 1 &&
                       !elifPickup.IsDevelopmentGrant && elif.name == "Obstacle Drop permanent-elif - obstacle-01" &&
                       display != null && display.sprite == UserArtwork.Load(PermanentElif.ArtworkKey),
                    "A converted pickup must be one elif with the permanent elif artwork and drop ID.");
                Assert(!PermanentElif.TryConvertSpawnedGold(elif, 12345, true, "floor-1-room-02", "obstacle-01",
                        convertIndex),
                    "An elif pickup must not convert again.");
                Assert(!RoomDuplication.IsFloorConsumable(elif) && RoomDuplication.IsFloorConsumable(kept),
                    "멜룬카드 must copy floor gold but not a permanent elif (contract 27 §10).");

                Assert(elifPickup.Collect(progress) && progress.ElifGained == 1 &&
                       progress.GetResourceCount(RunResourceType.Gold) == 0,
                    "Collecting an elif pickup must add one elif and no gold.");
                progress.TryAddResource(RunResourceType.Gold, RunResourceWallet.MaxCount);
                GameObject full = Spawn(goldPrefab, holder.transform, "Obstacle Drop elif - obstacle-01");
                full.GetComponent<RunResourcePickup>().ConvertToPermanentElif();
                Assert(full.GetComponent<RunResourcePickup>().Collect(progress) && progress.ElifGained == 2,
                    "An elif pickup must be collected with a full gold wallet.");

                PermanentElif.DevelopmentForceNextConversion = true;
                GameObject forced = Spawn(goldPrefab, holder.transform, "Obstacle Drop elif - obstacle-01");
                Assert(PermanentElif.TryConvertSpawnedGold(forced, 12345, true, "floor-1-room-02", "obstacle-01",
                           keepIndex) && !PermanentElif.DevelopmentForceNextConversion &&
                       forced.GetComponent<RunResourcePickup>().IsDevelopmentGrant,
                    "The development toggle must turn the next gold drop into a development elif and clear itself.");
                Assert(forced.GetComponent<RunResourcePickup>().Collect(progress) && progress.ElifGained == 2 &&
                       progress.DevelopmentElif == 1,
                    "A development elif pickup must not count as the Run's gain.");
            }
            finally
            {
                PermanentElif.DevelopmentForceNextConversion = false;
                Object.DestroyImmediate(holder);
            }
        }

        private static void ValidateWalletAcquire()
        {
            ItemDefinition wallet = LoadItem(Week23Artifact3Setup.WalletId);
            Assert(wallet.Effects.Any(effect => effect.EffectType == ItemEffectType.GainPermanentElifRollOnAcquire),
                "Run Elif-2 setup first: 아이시아의 지갑 must roll permanent elif.");
            int[] seen = new int[PermanentElif.WalletWeights.Length];
            for (int seed = 1; seed <= 40; seed++)
            {
                GameObject root = new("Elif-2 Wallet");
                try
                {
                    GameObject player = CreatePlayer(root.transform, Vector2.zero, typeof(RunProgress));
                    RunProgress progress = player.GetComponent<RunProgress>();
                    PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                    Assert(progress.TryInitializeRunSeed(seed, out string error), error);
                    int expected = PermanentElif.RollWallet(seed, Week23Artifact3Setup.WalletId);
                    Assert(inventory.TryAcquire(wallet) && progress.ElifGained == expected &&
                           inventory.LastPermanentElifRoll == expected &&
                           progress.GetResourceCount(RunResourceType.Gold) == RunResourceWallet.MaxCount,
                        $"With seed {seed} the wallet must add {expected} elif and its gold; added {progress.ElifGained}.");
                    Assert(!inventory.TryAcquire(wallet) && progress.ElifGained == expected,
                        "A second wallet must not roll again.");
                    seen[expected]++;
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }

            Assert(seen[0] > 0 && seen.Skip(1).Sum() > 0, "Forty seeds must show both empty and paying wallets.");
        }

        private static void ValidateRunRequest()
        {
            CreateRunRequest request = new()
            {
                clientRunId = "00000000-0000-4000-8000-0000000000e2",
                userId = "00000000-0000-4000-8000-000000000001",
                characterId = "erpin",
                isCleared = true,
                items = Array.Empty<RunItemDto>(),
                elif = new RunElifDto { gained = 3, spent = 1 }
            };
            MethodInfo serialize = typeof(ApiClient).GetMethod("SerializeRunRequest",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert(serialize != null, "ApiClient.SerializeRunRequest was not found.");
            const string expected = "\"elif\":{\"gained\":3,\"spent\":1,\"upgradePurchases\":[]}";
            string clear = (string)serialize.Invoke(null, new object[] { request });
            request.isCleared = false;
            request.deathReason = "ENEMY_CONTACT";
            string death = (string)serialize.Invoke(null, new object[] { request });
            Assert(clear.Contains(expected) && !clear.Contains("deathReason") && death.Contains(expected),
                "Clear and death save requests must both carry the Run's elif.");

            CreateRunRequest legacy = JsonUtility.FromJson<CreateRunRequest>(
                "{\"clientRunId\":\"a\",\"userId\":\"b\",\"characterId\":\"erpin\"}");
            Assert(legacy.elif != null && legacy.elif.gained == 0 && legacy.elif.spent == 0 &&
                   JsonUtility.ToJson(legacy).Contains("\"elif\":{\"gained\":0,\"spent\":0,\"upgradePurchases\":[]}"),
                "A pending request saved before Elif-2 must resend all-zero elif, the Backend's pre-Elif fingerprint.");
            UserProfileResponse profile = JsonUtility.FromJson<UserProfileResponse>(
                "{\"success\":true,\"data\":{\"id\":\"u\",\"nickname\":\"n\",\"meta\":{\"elifBalance\":7," +
                "\"shopUpgrades\":[{\"upgradeId\":\"shop-upgrade-discount\",\"level\":1}]}}}");
            Assert(profile.data.meta.elifBalance == 7 && profile.data.meta.shopUpgrades.Length == 1 &&
                   profile.data.meta.shopUpgrades[0].upgradeId == "shop-upgrade-discount",
                "The profile lookup must read meta.elifBalance and meta.shopUpgrades (API 07 §9.4).");
        }

        // A chest's gold drops follow the conversion hash of the Run seed, room, chest and drop index.
        private static void ValidateChestPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            try
            {
                RoomGraphAssembler assembler = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();
                ChestContentTable table = assembler.ChestContentTable;
                ResourceDropEntry gold = table != null
                    ? table.Consumables.FirstOrDefault(entry => entry?.Prefab != null &&
                                                                entry.Prefab.TryGetComponent(out RunResourcePickup pickup) &&
                                                                pickup.ResourceType == RunResourceType.Gold)
                    : null;
                Assert(gold != null, "The chest content table must hold the gold pickup.");

                int graphSeed = Enumerable.Range(1, 64)
                    .FirstOrDefault(seed => assembler.TryApplyGeneratedGraphForVerification(seed, out _));
                Assert(graphSeed > 0, "No seed built the first floor for the Elif-2 chest check.");
                GeneratedRoomNode start = assembler.GeneratedGraph.FindFloor(1).Nodes
                    .Single(node => node.Role == GeneratedRoomRole.Start);
                const string chestId = "dev-chest-01";
                const int dropCount = 4;
                // A Run seed whose chest has both an elif and a gold drop, so both outcomes go through the path.
                int runSeed = assembler.Progress.HasRunSeed
                    ? assembler.Progress.RunSeed
                    : Enumerable.Range(1, 10000).First(seed =>
                    {
                        int hits = Enumerable.Range(0, dropCount)
                            .Count(index => PermanentElif.ShouldConvert(seed, start.RoomId, chestId, index));
                        return hits > 0 && hits < dropCount;
                    });
                if (!assembler.Progress.HasRunSeed)
                    Assert(assembler.Progress.TryInitializeRunSeed(runSeed, out string seedError), seedError);
                RoomPrefab room = assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                    .Single(candidate => candidate.Node.RoomId == start.RoomId);
                RoomChestSite site = new(room, start.Template, assembler.Progress.GetRoomState(start.RoomId),
                    assembler.Progress);
                TreasureChest chest = RoomChestSpawner.SpawnDevelopmentChest(table, site, start.ContentSeed,
                    ChestKind.Normal, out string error);
                Assert(chest != null && chest.ChestId == chestId, error);
                ResourceDropEntry[] drops = Enumerable.Repeat(gold, dropCount).ToArray();
                GameObject[] spawned = RoomChestSpawner.SpawnContents(table, chest,
                    new ChestContents(ChestKind.Normal, drops, null), site).ToArray();
                Assert(spawned.Length == drops.Length, "The chest must drop every gold pickup.");
                int converted = 0;
                for (int index = 0; index < spawned.Length; index++)
                {
                    bool expected = PermanentElif.ShouldConvert(runSeed, start.RoomId, chestId, index);
                    RunResourceType type = spawned[index].GetComponent<RunResourcePickup>().ResourceType;
                    Assert(type == (expected ? RunResourceType.Elif : RunResourceType.Gold),
                        $"Chest gold {index} in {start.RoomId} must {(expected ? "" : "not ")}turn into elif.");
                    if (expected) converted++;
                }

                GameObject[] replay = RoomChestSpawner.SpawnContents(table, chest,
                    new ChestContents(ChestKind.Normal, drops, null), site).ToArray();
                Assert(replay.Select(drop => drop.GetComponent<RunResourcePickup>().ResourceType)
                        .SequenceEqual(spawned.Select(drop => drop.GetComponent<RunResourcePickup>().ResourceType)),
                    "The same Run seed and chest must replay the same elif drops.");
                Assert(converted > 0 && converted < dropCount, "The chest check must see both elif and gold drops.");
            }
            finally
            {
                // The Run seed set above stays on the scene's RunProgress, so the scene is reloaded from disk.
                EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            }
        }

        private static (int convert, int keep) FindIndices(int seed, string room, string source)
        {
            int convert = -1, keep = -1;
            for (int index = 0; index < 1000 && (convert < 0 || keep < 0); index++)
            {
                if (PermanentElif.ShouldConvert(seed, room, source, index)) { if (convert < 0) convert = index; }
                else if (keep < 0) keep = index;
            }

            Assert(convert >= 0 && keep >= 0, "No converting and non-converting drop index was found.");
            return (convert, keep);
        }

        private static GameObject Spawn(RunResourcePickup prefab, Transform parent, string name)
        {
            GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            PrefabUtility.UnpackPrefabInstance(spawned, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            spawned.name = name;
            // Awake does not run in Edit Mode, so the artwork a converted pickup shows is applied by the conversion.
            return spawned;
        }
    }
}
