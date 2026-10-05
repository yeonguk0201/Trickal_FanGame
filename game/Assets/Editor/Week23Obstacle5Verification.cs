using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Item;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week23Obstacle5Verification
    {
        // Far from every authored scene so the verification objects never meet scene content.
        private static readonly Vector2 Origin = new(-5200f, 5200f);
        private const int SeedCount = 8192;
        private const string ObstacleId = Week18Obstacle1Setup.DefaultObstacleId;

        [MenuItem("Trickal Fan Game/Week 23/Setup and Verify Obstacle-5 Resource Obstacles")]
        public static void SetupAndVerifyBatch()
        {
            Week23Obstacle5Setup.Setup();
            string[] paths = Week23Obstacle5Setup.CreatedAssetPaths()
                .Concat(new[] { Week20Obstacle4Setup.MarieVariantPath, Week20Obstacle4Setup.FairyKingdomVariantTablePath })
                .ToArray();
            Dictionary<string, string> guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            Week23Obstacle5Setup.Setup();
            // Obstacle-4 setup owns the table asset; re-running it must keep the Obstacle-5 kinds.
            Week20Obstacle4Setup.EnsureVariantAssets();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Obstacle-5 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-5 Resource Obstacles")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week23Obstacle5Setup.RoomContentVersion,
                "Run Obstacle-5 setup before verification.");

            ObstacleVariantTable table = AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(
                Week20Obstacle4Setup.FairyKingdomVariantTablePath);
            Assert(table != null, "The Fairy Kingdom obstacle variant table is missing.");
            Assert(table.TryValidate(out string error), error);
            Assert(Mathf.Approximately(table.SpecialRoomChance, Week20Obstacle4Setup.SpecialRoomChance),
                "Obstacle-5 must keep the 40% special obstacle room chance.");
            Assert(table.Entries.Select(entry => (entry.Variant.VariantId, entry.Weight))
                    .SequenceEqual(Week23Obstacle5Setup.ExpectedTableWeights()),
                "The special obstacle table must hold the Marie bomb box and every Obstacle-5 kind at its weight.");

            Dictionary<string, ObstacleVariantDefinition> variants = new(StringComparer.Ordinal);
            foreach (Week23Obstacle5Setup.VariantSpec spec in Week23Obstacle5Setup.Variants)
                variants[spec.VariantId] = ValidateVariant(spec);
            ValidateFoodBoxOrder(variants);
            ValidateVariantSelection(generator, table);
            ValidateSlotsKeepTheirFootprint(generator, table);
            ValidateMultipleDrops(variants);
            ValidateVault(variants[Week23Obstacle5Setup.SistVault.VariantId],
                variants[Week23Obstacle5Setup.GoldRock.VariantId]);
            Debug.Log("Obstacle-5 verification passed: setup is idempotent, the 40% room-level roll still resolves at " +
                      "most one seeded special obstacle among the Marie bomb box and the Obstacle-5 kinds near their " +
                      "table weights, each kind keeps its drop chance, main resource and drop count, the food boxes " +
                      "grow rarer and richer from Erpin to Eshur to Ricotta, multiple pickups replay per seed and " +
                      "never drop again after a rebuild, a candidate slot keeps its footprint under every kind, and " +
                      "the vault ignores hits, opens once by a bomb or one key, and rolls its rare spell or artifact.");
        }

        [MenuItem("Trickal Fan Game/Week 23/Verify Obstacle-5 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week20Obstacle4Verification.VerifyWithRegressionsBatch();
            Week20Special3Verification.Verify();
            Week22Terrain0Verification.Verify();
            Debug.Log("Obstacle-5 regression verification passed.");
        }

        private static ObstacleVariantDefinition ValidateVariant(Week23Obstacle5Setup.VariantSpec spec)
        {
            ObstacleVariantDefinition variant = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(spec.VariantPath);
            ResourceDropTable drops = spec.HasDropTable
                ? AssetDatabase.LoadAssetAtPath<ResourceDropTable>(spec.DropTablePath)
                : null;
            Assert(variant != null && (drops != null || !spec.HasDropTable),
                $"Obstacle kind '{spec.VariantId}' or its drop table is missing.");
            Assert(variant.TryValidate(out string error), error);
            Assert(variant.VariantId == spec.VariantId && variant.DropTable == drops &&
                   variant.RequiredHits == spec.RequiredHits &&
                   variant.BreakRule == spec.BreakRule && variant.DropCount == spec.DropCount,
                $"Obstacle kind '{spec.VariantId}' must keep its ID, drop table, hits, break rule and drop count.");
            Assert(Mathf.Approximately(variant.ExplosionChance, spec.ExplosionChance) &&
                   (spec.ExplosionChance <= 0f ||
                    (variant.ExplosionPrefab != null &&
                     Mathf.Approximately(variant.ExplosionFuse, Week23Obstacle5Setup.ExplosionFuse))),
                $"Obstacle kind '{spec.VariantId}' must keep its explosion chance, fuse and bomb Prefab.");
            Assert(Mathf.Approximately(variant.EnemyChance, spec.EnemyChance) &&
                   (spec.EnemyChance <= 0f ||
                    (variant.EnemyCount == spec.EnemyCount && variant.EnemyPrefab ==
                        AssetDatabase.LoadAssetAtPath<GameObject>(Week23Enemy6Setup.PrefabPath))),
                $"Obstacle kind '{spec.VariantId}' must keep its enemy chance, count and Prefab.");
            Assert(Mathf.Approximately(variant.RareItemChance, spec.RareItemChance) &&
                   Mathf.Approximately(variant.RareArtifactShare, spec.RareArtifactShare),
                $"Obstacle kind '{spec.VariantId}' must keep its rare item chance.");
            if (drops == null) return variant;
            Assert(Mathf.Approximately(drops.DropChance, spec.DropChance) &&
                   drops.Entries.Select(entry => (entry.DropId, entry.Weight)).SequenceEqual(spec.DropWeights) &&
                   drops.Entries.All(entry => entry.Prefab != null),
                $"Obstacle kind '{spec.VariantId}' must keep its drop chance and candidates.");

            int rolled = 0;
            int main = 0;
            for (int seed = 0; seed < SeedCount * 2; seed++)
            {
                if (!drops.TryRoll(DestructibleObstacle.DeriveDropSeed(seed, ObstacleId), out ResourceDropEntry entry))
                    continue;
                rolled++;
                if (entry.DropId == spec.MainDropId) main++;
            }

            float rate = rolled / (float)(SeedCount * 2);
            float share = rolled > 0 ? main / (float)rolled : 0f;
            float expectedShare = spec.DropWeights.Max(entry => entry.weight) /
                                  (float)spec.DropWeights.Sum(entry => entry.weight);
            Assert(Mathf.Abs(rate - spec.DropChance) <= 0.02f && Mathf.Abs(share - expectedShare) <= 0.04f,
                $"Obstacle kind '{spec.VariantId}' dropped {rate:P1} of the time with {share:P1} " +
                $"{spec.MainDropId}; expected {spec.DropChance:P0} and {expectedShare:P0}.");
            return variant;
        }

        private static void ValidateFoodBoxOrder(IReadOnlyDictionary<string, ObstacleVariantDefinition> variants)
        {
            Week23Obstacle5Setup.VariantSpec[] order =
            {
                Week23Obstacle5Setup.ErpinSnackBox, Week23Obstacle5Setup.EshurBreadBox,
                Week23Obstacle5Setup.RicottaFoodBox,
            };
            for (int index = 1; index < order.Length; index++)
            {
                ObstacleVariantDefinition common = variants[order[index - 1].VariantId];
                ObstacleVariantDefinition rare = variants[order[index].VariantId];
                Assert(order[index].TableWeight < order[index - 1].TableWeight &&
                       rare.DropCount > common.DropCount && rare.DropTable.DropChance > common.DropTable.DropChance,
                    $"'{rare.VariantId}' must be rarer than '{common.VariantId}' and drop more.");
            }

            foreach (Week23Obstacle5Setup.VariantSpec spec in order)
            {
                ResourceDropTable drops = variants[spec.VariantId].DropTable;
                int total = drops.Entries.Sum(entry => entry.Weight);
                int food = drops.Entries.Where(entry => entry.DropId is "heart" or "sp").Sum(entry => entry.Weight);
                Assert(drops.Entries.Any(entry => entry.DropId == "heart") &&
                       drops.Entries.Any(entry => entry.DropId == "sp") && food * 10 >= total * 8,
                    $"'{spec.VariantId}' must mostly drop hearts and SP.");
            }
        }

        private static void ValidateVariantSelection(FloorGenerator generator, ObstacleVariantTable table)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                candidate != null && candidate.TemplateId == Week18Obstacle2Setup.Layouts[0].TemplateId);
            Dictionary<string, int> counts = table.Entries.ToDictionary(entry => entry.Variant.VariantId, _ => 0,
                StringComparer.Ordinal);
            int specialRooms = 0;
            for (int seed = 0; seed < SeedCount; seed++)
            {
                string first = Resolve(template, seed);
                Assert(first == Resolve(template, seed), $"Obstacle kinds changed for repeated seed {seed}.");
                if (first.Length == 0) continue;
                specialRooms++;
                string variantId = first.Substring(first.IndexOf(':') + 1);
                Assert(counts.ContainsKey(variantId), $"Seed {seed} resolved the unknown obstacle kind '{variantId}'.");
                counts[variantId]++;
            }

            float rate = specialRooms / (float)SeedCount;
            Assert(rate >= 0.37f && rate <= 0.43f,
                $"Special obstacle room rate {rate:P1} is outside the expected 40% band.");
            int totalWeight = table.Entries.Sum(entry => entry.Weight);
            foreach (ObstacleVariantEntry entry in table.Entries)
            {
                float share = counts[entry.Variant.VariantId] / (float)specialRooms;
                float expected = entry.Weight / (float)totalWeight;
                Assert(counts[entry.Variant.VariantId] > 0 && Mathf.Abs(share - expected) <= 0.025f,
                    $"Obstacle kind '{entry.Variant.VariantId}' share {share:P1} must stay near {expected:P0}.");
            }

            Assert(counts[Week23Obstacle5Setup.RicottaFoodBox.VariantId] <
                   counts[Week23Obstacle5Setup.EshurBreadBox.VariantId] &&
                   counts[Week23Obstacle5Setup.EshurBreadBox.VariantId] <
                   counts[Week23Obstacle5Setup.ErpinSnackBox.VariantId],
                "Ricotta's food box must appear less often than Eshur's bread box, and that less than Erpin's snack box.");
        }

        // "slot:variant" of the room's one special obstacle, or empty. More than one special obstacle fails.
        private static string Resolve(RoomTemplateDefinition template, int seed)
        {
            GameObject instance = Object.Instantiate(template.RoomPrefabAsset);
            try
            {
                Assert(RoomObstacleVariantSlot.TryResolveForRoom(instance.GetComponent<RoomPrefab>(), seed,
                    out string error), error);
                DestructibleObstacle[] special = instance.GetComponentsInChildren<DestructibleObstacle>(true)
                    .Where(obstacle => obstacle.VariantId != "rock").ToArray();
                Assert(special.Length <= 1, $"Seed {seed} resolved more than one special obstacle.");
                return special.Length == 1 ? $"{special[0].ObstacleId}:{special[0].VariantId}" : string.Empty;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // A kind only changes what an authored slot is, never where it stands or how large it is, so the Layout
        // contract (required paths, door approaches, SpawnPoints) that passed for the slots holds for every kind.
        private static void ValidateSlotsKeepTheirFootprint(FloorGenerator generator, ObstacleVariantTable table)
        {
            int layouts = 0;
            foreach (RoomTemplateDefinition template in generator.RoomTemplates)
            {
                if (template == null || template.RoomPrefabAsset == null ||
                    template.RoomPrefabAsset.GetComponentsInChildren<RoomObstacleVariantSlot>(true).Length == 0)
                {
                    continue;
                }

                layouts++;
                Assert(template.TryValidate(out string error) && template.TryValidateLayout(out error),
                    $"Obstacle Layout '{template.TemplateId}' must still pass the room contract. {error}");
                GameObject instance = Object.Instantiate(template.RoomPrefabAsset);
                try
                {
                    foreach (RoomObstacleVariantSlot slot in instance.GetComponentsInChildren<RoomObstacleVariantSlot>(true))
                    {
                        Assert(slot.VariantTable == table,
                            $"Layout '{template.TemplateId}' must use the Fairy Kingdom special obstacle table.");
                        DestructibleObstacle obstacle = slot.GetComponent<DestructibleObstacle>();
                        BoxCollider2D box = slot.GetComponent<BoxCollider2D>();
                        Vector3 position = slot.transform.localPosition;
                        Vector2 size = box.size;
                        foreach (ObstacleVariantEntry entry in table.Entries)
                        {
                            obstacle.ApplyVariant(entry.Variant);
                            Assert(obstacle.TryValidate(out error) && slot.transform.localPosition == position &&
                                   box.size == size && !box.isTrigger &&
                                   RoomMovementClass.IsLowObstacle(box),
                                $"'{entry.Variant.VariantId}' must leave slot '{obstacle.ObstacleId}' of " +
                                $"'{template.TemplateId}' a solid low obstacle on its authored cell. {error}");
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Assert(layouts >= Week18Obstacle2Setup.Layouts.Length + Week20Obstacle4Setup.Layouts.Length,
                "Every authored obstacle Layout must expose special obstacle candidate slots.");
        }

        private static void ValidateMultipleDrops(IReadOnlyDictionary<string, ObstacleVariantDefinition> variants)
        {
            ObstacleVariantDefinition ricotta = variants[Week23Obstacle5Setup.RicottaFoodBox.VariantId];
            ObstacleVariantDefinition eshur = variants[Week23Obstacle5Setup.EshurBreadBox.VariantId];
            GameObject root = new("Obstacle-5 Drop Verification");
            GameObject progressHolder = new("Obstacle-5 Drop Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                DestructibleObstacle probe = CreateObstacle(root.transform, ricotta);
                int fullSeed = 0;
                int emptySeed = 0;
                for (int seed = 1; seed < 100000 && (fullSeed == 0 || emptySeed == 0); seed++)
                {
                    probe.Bind(new RoomRunState("floor-01-room-02"), seed, root.transform, progress);
                    int count = probe.RollDrops().Count;
                    Assert(count == 0 || count == ricotta.DropCount,
                        "A food box without a secret room must drop nothing or its full drop count.");
                    if (count == ricotta.DropCount && fullSeed == 0) fullSeed = seed;
                    if (count == 0 && emptySeed == 0) emptySeed = seed;
                }

                Assert(fullSeed != 0 && emptySeed != 0, "No room seed matched the food box drop verification cases.");

                DestructibleObstacle eshurProbe = CreateObstacle(root.transform, eshur);
                eshurProbe.Bind(new RoomRunState("floor-01-room-02"), FindDropSeed(eshurProbe, root.transform, progress),
                    root.transform, progress);
                Assert(eshurProbe.RollDrops().Count == eshur.DropCount && eshur.DropCount == 2,
                    "Eshur's bread box must leave two pickups when it drops.");

                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle box = CreateObstacle(root.transform, ricotta);
                box.Bind(state, fullSeed, root.transform, progress);
                string[] expected = box.RollDrops().Select(entry => entry.DropId).ToArray();
                BreakWithHits(box);
                Assert(state.IsObstacleDestroyed(ObstacleId) && box.LastDrops.Count == expected.Length &&
                       box.LastDrop == box.LastDrops[0] &&
                       box.LastDrops.Select((drop, index) => drop.name.Contains($"Drop {expected[index]} ")).All(ok => ok),
                    "Breaking Ricotta's food box must record it and leave exactly its rolled pickups.");
                Vector2[] positions = box.LastDrops.Select(drop => (Vector2)drop.transform.position).ToArray();
                Assert(positions.Distinct().Count() == positions.Length && positions.All(position =>
                        Vector2.Distance(position, box.transform.position) <=
                        DestructibleObstacle.DropRingRadius + 0.001f),
                    "Multiple pickups must spread to distinct points inside the broken obstacle's cell.");
                foreach (GameObject drop in box.LastDrops)
                    Assert(drop.GetComponent<RunResourcePickup>() != null || drop.GetComponent<HealthPickup>() != null ||
                           drop.GetComponent<TrickalFanGame.Player.SPPickup>() != null,
                        "Food box pickups must be ordinary floor pickups.");

                DestructibleObstacle rebuilt = CreateObstacle(root.transform, ricotta);
                rebuilt.Bind(state, fullSeed, root.transform, progress);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && rebuilt.LastDrops.Count == 0 &&
                       !rebuilt.RegisterPlayerHit(),
                    "A rebuilt room must keep the food box broken and never drop again.");

                DestructibleObstacle replay = CreateObstacle(root.transform, ricotta);
                replay.Bind(new RoomRunState("floor-01-room-02"), fullSeed, root.transform, progress);
                BreakWithHits(replay);
                Assert(replay.LastDrops.Select(drop => drop.name).SequenceEqual(box.LastDrops.Select(drop => drop.name)),
                    "The same room seed and obstacle ID must replay the same pickups in another Run.");

                DestructibleObstacle empty = CreateObstacle(root.transform, ricotta);
                empty.Bind(new RoomRunState("floor-01-room-03"), emptySeed, root.transform, progress);
                BreakWithHits(empty);
                Assert(empty.IsBroken && empty.LastDrops.Count == 0 && empty.LastDrop == null,
                    "A food box whose drop roll misses must break without a pickup.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateVault(ObstacleVariantDefinition vaultVariant, ObstacleVariantDefinition goldRock)
        {
            RoomGraphAssembler assembler = Object.FindFirstObjectByType<RoomGraphAssembler>();
            Assert(assembler != null && assembler.SelectionRewardPool.Any(item =>
                    item != null && item.Kind == ItemKind.Artifact),
                "The Game Scene assembler must hold the artifact pool the vault's rare item uses.");
            Assert(vaultVariant.RareSpells.Count > 0 && vaultVariant.RareSpells.OrderBy(spell => spell.ItemId)
                    .SequenceEqual(Week22Chest1Setup.FindSingleUseItems(ItemKind.SingleUseSpell)
                        .OrderBy(spell => spell.ItemId)),
                "The vault's rare spells must be every implemented single-use spell. Re-run Obstacle-5 setup.");

            GameObject root = new("Obstacle-5 Vault Verification");
            GameObject progressHolder = new("Obstacle-5 Vault Progress");
            try
            {
                RunProgress progress = progressHolder.AddComponent<RunProgress>();
                RoomRunState state = new("floor-01-room-02");
                DestructibleObstacle vault = CreateObstacle(root.transform, vaultVariant);
                vault.Bind(state, 7, root.transform, progress);
                for (int hit = 0; hit < DestructibleObstacle.DefaultRequiredHits * 3; hit++)
                    Assert(!vault.RegisterPlayerHit(), "The vault must ignore attack and skill hits.");
                Assert(!vault.IsBroken && vault.HitsTaken == 0 && vault.gameObject.activeSelf &&
                       RoomMovementClass.IsLowObstacle(vault.GetComponent<Collider2D>()),
                    "The vault must stay a solid obstacle however often it is hit.");
                Assert(!vault.TryOpenWithKey() && !vault.IsBroken, "The vault must not open without a key.");

                DestructibleObstacle rock = CreateObstacle(root.transform, goldRock);
                rock.Bind(new RoomRunState("floor-01-room-03"), 7, root.transform, progress);
                progress.TryAddResource(RunResourceType.Key, 2);
                Assert(!rock.TryOpenWithKey() && !rock.IsBroken && progress.GetResourceCount(RunResourceType.Key) == 2,
                    "An obstacle that breaks by hits must never spend a key.");

                Assert(vault.TryOpenWithKey() && vault.IsBroken && !vault.gameObject.activeSelf &&
                       state.IsObstacleDestroyed(ObstacleId) && progress.GetResourceCount(RunResourceType.Key) == 1,
                    "Touching the vault with a key must spend exactly one key and open it.");
                int resources = vault.LastDrops.Count(drop => drop.GetComponent<RunResourcePickup>() != null);
                Assert(resources == vaultVariant.DropCount && vaultVariant.DropCount == 3 &&
                       vault.LastDrops.Where(drop => drop.GetComponent<RunResourcePickup>() != null).All(drop =>
                           drop.GetComponent<RunResourcePickup>().ResourceType is RunResourceType.Gold
                               or RunResourceType.Key),
                    "An opened vault must always leave three gold or key pickups.");
                Assert(!vault.TryOpenWithKey() && !vault.TryDestroyByBomb() &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "An opened vault must not open or spend a key again.");

                DestructibleObstacle rebuilt = CreateObstacle(root.transform, vaultVariant);
                rebuilt.Bind(state, 7, root.transform, progress);
                Assert(rebuilt.IsBroken && !rebuilt.gameObject.activeSelf && rebuilt.LastDrops.Count == 0 &&
                       !rebuilt.TryOpenWithKey() && progress.GetResourceCount(RunResourceType.Key) == 1,
                    "A rebuilt room must keep the vault opened without another payout or key.");

                DestructibleObstacle bombed = CreateObstacle(root.transform, vaultVariant);
                bombed.Bind(new RoomRunState("floor-01-room-04"), 7, root.transform, progress);
                Assert(bombed.TryDestroyByBomb() && bombed.IsBroken &&
                       bombed.LastDrops.Select(drop => drop.name).SequenceEqual(vault.LastDrops.Select(drop => drop.name)) &&
                       progress.GetResourceCount(RunResourceType.Key) == 1,
                    "A bomb must open the vault without a key and replay the same seeded payout.");

                ValidateVaultRareItems(vaultVariant, assembler, root.transform, progress);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(progressHolder);
                Physics2D.SyncTransforms();
            }
        }

        private static void ValidateVaultRareItems(ObstacleVariantDefinition vaultVariant, RoomGraphAssembler assembler,
            Transform parent, RunProgress progress)
        {
            DestructibleObstacle probe = CreateObstacle(parent, vaultVariant);
            int rare = 0;
            int artifacts = 0;
            int spellSeed = 0;
            int artifactSeed = 0;
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                probe.Bind(new RoomRunState("floor-01-room-05"), seed, parent, progress);
                probe.BindRareItems(null, null);
                bool withoutPool = probe.TryRollRareItem(out ItemDefinition spellOnly);
                Assert(!withoutPool || (spellOnly != null && spellOnly.Kind == ItemKind.SingleUseSpell &&
                                        vaultVariant.RareSpells.Contains(spellOnly)),
                    "Without an artifact pool the vault's rare item must be one of its spells.");

                probe.BindRareItems(assembler.SelectionRewardPool, null);
                bool rolled = probe.TryRollRareItem(out ItemDefinition item);
                Assert(rolled == withoutPool && (!rolled || item != null),
                    "The artifact pool must not change whether the vault's rare item roll hits.");
                Assert(rolled == probe.TryRollRareItem(out ItemDefinition again) && again == item,
                    $"The vault's rare item changed for repeated seed {seed}.");
                if (!rolled) continue;
                rare++;
                Assert(item.Kind is ItemKind.Artifact or ItemKind.SingleUseSpell && item.IsActive,
                    "The vault's rare item must be an active artifact or single-use spell.");
                if (item.Kind == ItemKind.Artifact)
                {
                    Assert(assembler.SelectionRewardPool.Contains(item),
                        "The vault's rare artifact must come from the selection reward pool.");
                    artifacts++;
                    if (artifactSeed == 0) artifactSeed = seed;
                }
                else if (spellSeed == 0)
                {
                    spellSeed = seed;
                }
            }

            float rate = rare / (float)SeedCount;
            float artifactShare = rare > 0 ? artifacts / (float)rare : 0f;
            Assert(Mathf.Abs(rate - Week23Obstacle5Setup.VaultRareItemChance) <= 0.015f &&
                   Mathf.Abs(artifactShare - Week23Obstacle5Setup.VaultRareArtifactShare) <= 0.1f &&
                   spellSeed != 0 && artifactSeed != 0,
                $"The vault rolled a rare item {rate:P1} of the time with {artifactShare:P1} artifacts; expected " +
                $"{Week23Obstacle5Setup.VaultRareItemChance:P0} and {Week23Obstacle5Setup.VaultRareArtifactShare:P0}.");

            DestructibleObstacle spellVault = CreateObstacle(parent, vaultVariant);
            spellVault.Bind(new RoomRunState("floor-01-room-06"), spellSeed, parent, progress);
            spellVault.BindRareItems(assembler.SelectionRewardPool, null);
            spellVault.TryRollRareItem(out ItemDefinition expectedSpell);
            Assert(spellVault.TryDestroyByBomb() && spellVault.LastDrops.Count == vaultVariant.DropCount + 1,
                "A vault whose rare roll hits must leave its three pickups and one item.");
            SingleUseItemPickup spell = spellVault.LastDrops[^1].GetComponent<SingleUseItemPickup>();
            Assert(spell != null && spell.Definition == expectedSpell && spell.WaitsForPlayerExit &&
                   !string.IsNullOrWhiteSpace(spell.InstanceId),
                "The vault's rare spell must be an ordinary single-use item pickup with a stable instance ID.");

            DestructibleObstacle artifactVault = CreateObstacle(parent, vaultVariant);
            artifactVault.Bind(new RoomRunState("floor-01-room-07"), artifactSeed, parent, progress);
            artifactVault.BindRareItems(assembler.SelectionRewardPool, null);
            artifactVault.TryRollRareItem(out ItemDefinition expectedArtifact);
            Assert(artifactVault.TryDestroyByBomb() && artifactVault.LastDrops.Count == vaultVariant.DropCount + 1,
                "A vault whose rare artifact roll hits must leave its three pickups and one artifact.");
            ItemPickup artifact = artifactVault.LastDrops[^1].GetComponent<ItemPickup>();
            Assert(artifact != null && artifact.Definition == expectedArtifact &&
                   expectedArtifact.Kind == ItemKind.Artifact,
                "The vault's rare artifact must be an ordinary item pickup.");
        }

        private static int FindDropSeed(DestructibleObstacle probe, Transform parent, RunProgress progress)
        {
            for (int seed = 1; seed < 100000; seed++)
            {
                probe.Bind(new RoomRunState("floor-01-room-02"), seed, parent, progress);
                if (probe.RollDrops().Count > 0) return seed;
            }

            throw new InvalidOperationException("No room seed made the obstacle drop.");
        }

        private static void BreakWithHits(DestructibleObstacle obstacle)
        {
            for (int hit = 0; hit < obstacle.RequiredHits; hit++) obstacle.RegisterPlayerHit();
            Assert(obstacle.IsBroken, "The obstacle must break after its required hits.");
        }

        private static DestructibleObstacle CreateObstacle(Transform parent, ObstacleVariantDefinition variant)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week18Obstacle1Setup.PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = Origin;
            DestructibleObstacle obstacle = instance.GetComponent<DestructibleObstacle>();
            obstacle.ApplyVariant(variant);
            return obstacle;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
