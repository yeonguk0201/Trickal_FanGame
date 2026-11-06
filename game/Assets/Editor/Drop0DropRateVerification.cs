using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Drop-0: the drop rates, the per-floor targets over 512 seeds, the pity counter and the champion kill drop.
    public static class Drop0DropRateVerification
    {
        private static readonly Vector2 Origin = new(-9800f, -9800f);
        // Expected permanent elif per Run before Drop-0 (512 seeds, 7% of 7.97 floor gold pickups).
        private const double ElifPerRunBefore = 0.558;

        [MenuItem("Trickal Fan Game/Week 28/Setup and Verify Drop-0 Drop Rates")]
        public static void SetupAndVerifyBatch()
        {
            string[] stable =
            {
                Week22Chest1Setup.ChestContentTablePath, Week27Variant0Setup.EliteDropTablePath,
                Week17Resource1Setup.ElifPrefabPath, Week13FrontendSetup.GameScenePath,
            };
            Dictionary<string, string> before = stable.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Drop0DropRateSetup.Setup();
            string championGuid = AssetDatabase.AssetPathToGUID(Drop0DropRateSetup.ChampionDropTablePath);
            Drop0DropRateSetup.Setup();
            foreach (KeyValuePair<string, string> entry in before)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Drop-0 setup changed or lost the GUID for {entry.Key}.");
            Assert(!string.IsNullOrWhiteSpace(championGuid) &&
                   championGuid == AssetDatabase.AssetPathToGUID(Drop0DropRateSetup.ChampionDropTablePath),
                "Rerunning Drop-0 setup must keep the champion drop table.");
            Verify();
        }

        // Drop-0 changes values that Chest-1/2, Variant-0/1, Resource-1, Gold-0, Elif-2 and Jjangsem-1 checked, and the
        // clear reward and enemy death paths they run through.
        [MenuItem("Trickal Fan Game/Week 28/Verify Drop-0 Drop Rates With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            SetupAndVerifyBatch();
            Week17Resource1Verification.Verify();
            Week17Resource3Verification.Verify();
            Week21Gold0Verification.Verify();
            Week22Chest1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Jjangsem1Verification.Verify();
            Week27Elif2Verification.Verify();
            Week27Variant0Verification.Verify();
            Week27Variant1Verification.Verify();
            Week27Variant2Verification.Verify();
            Debug.Log("Drop-0 drop rate verification with regressions passed.");
        }

        [MenuItem("Trickal Fan Game/Week 28/Verify Drop-0 Drop Rates")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null, "Drop-0 requires the Game Scene RoomGraphAssembler.");
            VerifyAssets(assembler);
            VerifyPityRules();
            string targets = VerifyTargets();
            VerifyClearPityDrop(assembler);
            VerifyGeneratedRoomPity(assembler);
            VerifyChampionRule();
            VerifyChampionRoom(assembler);
            Debug.Log($"Drop-0 drop rate verification passed. {targets}");
        }

        private static void VerifyAssets(RoomGraphAssembler assembler)
        {
            ChestContentTable chests = assembler.ChestContentTable;
            Assert(chests != null && Mathf.Approximately(chests.ChestChance, Drop0DropRateSetup.ClearChestChance),
                "Clear chests must appear on half of the clears. Run Drop-0 setup.");
            Assert(chests.FindRule(ChestKind.Normal).CountWeights.SequenceEqual(Drop0DropRateSetup.NormalChestCountWeights),
                "Normal chests must hold 1~3 at 40/40/20.");
            foreach ((string id, int weight) in Drop0DropRateSetup.ChestConsumableWeights)
                Assert(chests.Consumables.Any(entry => entry.DropId == id && entry.Weight == weight),
                    $"Chest consumable '{id}' must weigh {weight}.");
            Assert(chests.Consumables.Count == Drop0DropRateSetup.ChestConsumableWeights.Length,
                "Drop-0 must not add or remove chest consumables.");

            foreach (ResourceDropTable table in new[] { assembler.EliteKillDropTable, assembler.ChampionKillDropTable })
            {
                Assert(table != null && table.TryValidate(out _) && Mathf.Approximately(table.DropChance, 1f),
                    "The elite and champion kill drop tables must be valid and always pick once.");
                Assert(table.Entries.Select(entry => (entry.DropId, entry.Weight))
                        .SequenceEqual(Drop0DropRateSetup.KillDropWeights),
                    $"{table.name} must use the Drop-0 kill drop weights.");
                Assert(table.Entries.All(entry => entry.Prefab != null), $"{table.name} needs every pickup Prefab.");
            }

            Assert(AssetDatabase.GetAssetPath(assembler.ChampionKillDropTable) == Drop0DropRateSetup.ChampionDropTablePath,
                $"The Game Scene must use {Drop0DropRateSetup.ChampionDropTablePath}.");
            RunResourcePickup gold = Week17Resource1Setup.LoadPrefab(RunResourceType.Gold);
            Assert(gold != null && gold.Amount == Drop0DropRateSetup.GoldPickupAmount,
                $"One gold pickup must be worth {Drop0DropRateSetup.GoldPickupAmount} gold.");
            foreach (RunResourceType type in new[] { RunResourceType.Key, RunResourceType.Bomb })
                Assert(Week17Resource1Setup.LoadPrefab(type).Amount == 1, $"A {type} pickup must stay one {type}.");
        }

        private static void VerifyPityRules()
        {
            ResourcePity pity = new();
            Assert(ResourcePity.KeyThreshold == 5 && ResourcePity.BombThreshold == 4,
                "Pity must guarantee a key after 5 dry clears and a bomb after 4.");
            int keyAt = -1, bombAt = -1;
            for (int clear = 1; clear <= 12; clear++)
            {
                pity.RecordCombatClear(out bool key, out bool bomb);
                if (key && keyAt < 0) keyAt = clear;
                if (bomb && bombAt < 0) bombAt = clear;
            }

            Assert(keyAt == ResourcePity.KeyThreshold + 1 && bombAt == ResourcePity.BombThreshold + 1,
                $"Without gains the guarantee must come on clear {ResourcePity.KeyThreshold + 1} (key) and " +
                $"{ResourcePity.BombThreshold + 1} (bomb); got {keyAt} and {bombAt}.");

            pity.Clear();
            for (int clear = 0; clear < ResourcePity.BombThreshold; clear++) pity.RecordCombatClear(out _, out _);
            pity.RecordGain(RunResourceType.Key, 1);
            pity.RecordGain(RunResourceType.Gold, 3);
            Assert(pity.KeyDroughtClears == 0 && pity.BombDroughtClears == ResourcePity.BombThreshold,
                "Gaining a key must reset only the key count; gold must reset nothing.");
            pity.RecordGain(RunResourceType.Bomb, 0);
            Assert(pity.BombDroughtClears == ResourcePity.BombThreshold, "A gain of nothing must not reset the count.");

            GameObject holder = new("Drop-0 Pity Progress");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                progress.Pity.SetDroughtForDevelopment(RunResourceType.Key, 3);
                progress.Pity.SetDroughtForDevelopment(RunResourceType.Bomb, 3);
                Assert(progress.TryAddResource(RunResourceType.Bomb, 1) == 1 && progress.Pity.BombDroughtClears == 0 &&
                       progress.Pity.KeyDroughtClears == 3,
                    "A bomb added to the Run must reset the bomb count only.");
                progress.ResetProgress();
                Assert(progress.Pity.KeyDroughtClears == 0, "A new Run must start the pity counter over.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private static string VerifyTargets()
        {
            Drop0EconomyReport.BuildReport(out Dictionary<int, Drop0EconomyReport.FloorTotals> totals);
            Assert(totals.ContainsKey(1) && totals.ContainsKey(2), "The report must cover floors 1 and 2.");
            double keys = 0, bombs = 0, gold = 0;
            foreach (int floor in new[] { 1, 2 })
            {
                Drop0EconomyReport.FloorTotals t = totals[floor];
                keys += t.PerFloor(t.FreeKeys + t.PityKeys) / 2;
                bombs += t.PerFloor(t.FreeBombs + t.PityBombs) / 2;
                gold += t.PerFloor(t.FreeGold) / 2;
            }

            Assert(keys >= 1.3 && keys <= 1.8, $"Floors 1-2 must give about 1.5 keys each ({keys:0.00}).");
            Assert(bombs >= 1.3 && bombs <= 1.8, $"Floors 1-2 must give about 1.5 bombs each ({bombs:0.00}).");
            Assert(gold >= 5.5 && gold <= 8.5, $"Floors 1-2 must give about 6-8 gold each before locked chests ({gold:0.0}).");

            double elif = totals.Values.Sum(t =>
                t.PerFloor(t.GoldPickups + t.GoldenChestPickups + t.DiamondChestPickups)) * PermanentElif.ConversionChance;
            Assert(Math.Abs(elif - ElifPerRunBefore) <= ElifPerRunBefore * 0.08,
                $"The Run's expected elif must stay near {ElifPerRunBefore:0.000} ({elif:0.000}).");
            return $"Floors 1-2: keys {keys:0.00}, bombs {bombs:0.00}, gold {gold:0.0}; elif per Run {elif:0.000}.";
        }

        // Drop mode: the guaranteed key and bomb fall beside the clear point even when the roll drops nothing, once.
        private static void VerifyClearPityDrop(RoomGraphAssembler assembler)
        {
            GameObject root = new("Drop-0 Clear Verification");
            root.transform.position = Origin;
            try
            {
                RunProgress progress = root.AddComponent<RunProgress>();
                ResourceDropTable table = ScriptableObject.CreateInstance<ResourceDropTable>();
                ResourceDropEntry heart = assembler.ChestContentTable.Consumables.Single(entry => entry.DropId == "heart");
                ResourceDropEntry key = assembler.ChestContentTable.Consumables.Single(entry => entry.DropId == "key");
                ResourceDropEntry bomb = assembler.ChestContentTable.Consumables.Single(entry => entry.DropId == "bomb");
                // Never drops by itself, so only the guarantee can put a key or bomb down.
                table.Configure(0f, new[] { heart, key, bomb });
                RoomClearRewardSpawner spawner = root.AddComponent<RoomClearRewardSpawner>();
                RoomRunState state = new("drop-0-clear");
                spawner.Configure(table, root.transform, root.transform, state, 1234, progress);

                progress.Pity.SetDroughtForDevelopment(RunResourceType.Key, ResourcePity.KeyThreshold);
                progress.Pity.SetDroughtForDevelopment(RunResourceType.Bomb, ResourcePity.BombThreshold - 1);
                Assert(!spawner.TrySpawn() && spawner.LastPityDrops.Count == 1, "A dry clear at the key threshold must drop one key.");
                GameObject pity = spawner.LastPityDrops[0];
                Assert(pity.GetComponent<RunResourcePickup>().ResourceType == RunResourceType.Key &&
                       pity.name.StartsWith("Pity Drop key", StringComparison.Ordinal) &&
                       progress.Pity.KeyDroughtClears == 0 &&
                       progress.Pity.BombDroughtClears == ResourcePity.BombThreshold,
                    "The guarantee must drop the key and restart its count while the bomb count grows.");
                Assert(pity.GetComponent<RunResourcePickup>().Collect(progress) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "The guaranteed key must be an ordinary key pickup.");
                Assert(!spawner.TrySpawn() && progress.Pity.BombDroughtClears == ResourcePity.BombThreshold &&
                       progress.Pity.KeyDroughtClears == 0,
                    "A cleared room must not count or drop again.");
                Object.DestroyImmediate(table);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Chest mode in a generated room: the guarantee lands inside the room, clear of the chest and the player.
        private static void VerifyGeneratedRoomPity(RoomGraphAssembler assembler)
        {
            int dropped = 0, withChest = 0;
            for (int seed = 1; seed <= 6 && dropped < 4; seed++)
            {
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                Assert(assembler.TryLoadFloor(1, assembler.Graph.Player, out error), error);
                RunProgress progress = assembler.Progress;
                foreach (RoomPrefab room in assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true))
                {
                    RoomClearRewardSpawner spawner = room.Controller != null
                        ? room.Controller.GetComponent<RoomClearRewardSpawner>()
                        : null;
                    if (spawner == null || spawner.ChestTable == null || spawner.HasRolled) continue;
                    progress.Pity.SetDroughtForDevelopment(RunResourceType.Key, ResourcePity.KeyThreshold);
                    progress.Pity.SetDroughtForDevelopment(RunResourceType.Bomb, ResourcePity.BombThreshold);
                    spawner.TrySpawn();
                    Assert(spawner.LastPityDrops.Count == 2, $"Room {room.Node.RoomId} must drop a key and a bomb.");
                    GeneratedRoomNode node = assembler.GeneratedGraph.Nodes.Single(candidate =>
                        candidate.RoomId == room.Node.RoomId);
                    Rect movement = node.Template.Profile.MovementBounds;
                    foreach (GameObject pickup in spawner.LastPityDrops)
                    {
                        Vector2 position = pickup.transform.position;
                        Vector2 local = room.transform.InverseTransformPoint(position);
                        Assert(movement.Contains(local),
                            $"Room {room.Node.RoomId} put its guaranteed pickup at {local}, outside {movement}.");
                        if (spawner.LastSpawnedChest != null)
                            Assert(Vector2.Distance(position, spawner.LastSpawnedChest.transform.position) >=
                                   ChestPlacement.ChestHalfSize,
                                $"Room {room.Node.RoomId} put its guaranteed pickup on the chest.");
                    }

                    Assert(Vector2.Distance(spawner.LastPityDrops[0].transform.position,
                               spawner.LastPityDrops[1].transform.position) > 0.3f,
                        $"Room {room.Node.RoomId} stacked its guaranteed key and bomb.");
                    if (spawner.LastSpawnedChest != null) withChest++;
                    dropped++;
                }
            }

            Assert(dropped >= 4 && withChest >= 1,
                $"Generated rooms must place guaranteed pickups, with and without a chest ({dropped}, {withChest}).");
        }

        private static void VerifyChampionRule()
        {
            Assert(ChampionKillReward.DropPercent(1) == 0 && ChampionKillReward.DropPercent(2) == 20 &&
                   ChampionKillReward.DropPercent(3) == 25 && ChampionKillReward.DropPercent(5) == 30 &&
                   ChampionKillReward.DropPercent(8) == 35,
                "Champion drops must rise with depth: 0/20/25/30/35%.");
            int hits = 0;
            const int samples = 20000;
            for (int sample = 0; sample < samples; sample++)
            {
                int roomSeed = ChampionKillReward.DeriveRoomSeed(sample * 31 + 7);
                bool rolled = ChampionKillReward.Rolls(3, roomSeed, sample % 6);
                Assert(rolled == ChampionKillReward.Rolls(3, roomSeed, sample % 6), "The champion roll must replay.");
                if (rolled) hits++;
            }

            float rate = hits / (float)samples;
            Assert(Mathf.Abs(rate - 0.25f) < 0.015f, $"Floor 3 champion drops must land near 25% ({rate:P1}).");
        }

        // A champion in a running room rolls once for its slot; an escaped fight brings it back without another roll.
        private static void VerifyChampionRoom(RoomGraphAssembler assembler)
        {
            GameObject root = new("Drop-0 Champion Room", typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            root.transform.position = Origin + Vector2.right * 240f;
            try
            {
                Assert(assembler.EnemyRoster.TryResolve(EncounterEnemyRole.Chaser, out GameObject prefab,
                    out string error), error);
                RoomController room = root.AddComponent<RoomController>();
                Transform[] points = Enumerable.Range(0, 2).Select(index =>
                {
                    GameObject point = new($"Drop-0 Spawn {index + 1}");
                    point.transform.SetParent(root.transform);
                    point.transform.position = (Vector2)root.transform.position + Vector2.right * (index + 1) * 4f;
                    return point.transform;
                }).ToArray();
                room.Configure(3, 1, null, null, points, null);
                room.ConfigureEncounterWaves(new[]
                {
                    new EncounterRuntimeWave(Enumerable.Repeat(prefab, 2).ToArray(), points,
                        new[] { EnemyVariantKind.None, EnemyVariantKind.ChampionPuddle }),
                });
                int slot = EnemyVariantRules.SlotOf(0, 1);
                int roomSeed = Enumerable.Range(1, 400).Select(ChampionKillReward.DeriveRoomSeed)
                    .First(seed => ChampionKillReward.Rolls(3, seed, slot));
                RoomRunState state = new("drop-0-champion-room");
                room.BindRunState(state, false);
                room.ConfigureChampionReward(new ChampionKillReward(assembler.ChampionKillDropTable, root.transform,
                    state, 3, roomSeed));

                Health player = CreatePlayer(root.transform, (Vector2)root.transform.position + Vector2.down * 20f);
                room.BeginCombat(player);
                Assert(room.TryAbandonCombat() && state.RewardedChampionSlots.Count == 0,
                    "Escaping the fight must not roll the champion drop.");
                room.BeginCombat(player);
                Health champion = room.AliveEnemies.Single(enemy => EnemyVariant.IsChampionEnemy(enemy.gameObject));
                Health plain = room.AliveEnemies.Single(enemy => !EnemyVariant.IsChampionEnemy(enemy.gameObject));
                Vector3 fallen = champion.transform.position;
                plain.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, plain.MaxHealth + 1f));
                Assert(state.RewardedChampionSlots.Count == 0 && room.ChampionKillReward.LastSpawnedReward == null,
                    "A plain enemy's death must not roll the champion drop.");
                champion.TakeDamage(new DamageContext(null, DamageSourceType.PlayerAttack, champion.MaxHealth + 1f));
                GameObject reward = room.ChampionKillReward.LastSpawnedReward;
                Assert(champion.IsDead && state.RewardedChampionSlots.Contains(slot) && reward != null &&
                       ((Vector2)(reward.transform.position - fallen)).sqrMagnitude < 0.0001f,
                    "Killing a champion whose slot rolls must drop one pickup where it fell.");
                Assert(!room.ChampionKillReward.TrySpawn(slot, fallen), "A champion slot must drop only once per Run.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (EnemyChampionPuddle puddle in EnemyChampionPuddle.Live.ToArray())
                    if (puddle != null) Object.DestroyImmediate(puddle.gameObject);
                Physics2D.SyncTransforms();
            }
        }

        private static Health CreatePlayer(Transform parent, Vector2 position)
        {
            GameObject player = new("Drop-0 Player", typeof(PlayerMovement), typeof(CircleCollider2D));
            player.transform.SetParent(parent);
            player.transform.position = position;
            player.layer = LayerMask.NameToLayer("Player");
            PlayerFeet.Ensure(player, out _);
            player.GetComponent<Rigidbody2D>().gravityScale = 0f;
            Health health = player.GetComponent<Health>();
            health.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
            health.EnableHealthUnits();
            return health;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
