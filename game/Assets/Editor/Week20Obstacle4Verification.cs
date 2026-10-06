using System;
using System.Collections.Generic;
using System.Linq;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Obstacle4Verification
    {
        private const int SeedCount = 1024;

        [MenuItem("Trickal Fan Game/Week 20/Setup and Verify Obstacle-4 Fairy Kingdom Obstacles and Layouts")]
        public static void SetupAndVerifyBatch()
        {
            Week20Obstacle4Setup.Setup();
            Dictionary<string, string> guids = Week20Obstacle4Setup.CreatedAssetPaths().ToDictionary(
                path => path, AssetDatabase.AssetPathToGUID, StringComparer.Ordinal);
            Week20Obstacle4Setup.Setup();
            foreach (KeyValuePair<string, string> entry in guids)
                Assert(!string.IsNullOrWhiteSpace(entry.Value) && entry.Value == AssetDatabase.AssetPathToGUID(entry.Key),
                    $"Obstacle-4 setup changed or lost the GUID for {entry.Key}.");
            Verify();
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Obstacle-4 Fairy Kingdom Obstacles and Layouts")]
        public static void Verify()
        {
            Week18Obstacle2Setup.OpenGameScene();
            FloorGenerator generator = GameObject.Find(Week8RandomRoomSetup.GeneratorObjectName)
                ?.GetComponent<FloorGenerator>();
            Assert(generator != null && generator.RoomContentVersion >= Week20Obstacle4Setup.RoomContentVersion,
                "Run Obstacle-4 setup before verification.");

            ObstacleVariantTable table = AssetDatabase.LoadAssetAtPath<ObstacleVariantTable>(
                Week20Obstacle4Setup.FairyKingdomVariantTablePath);
            ObstacleVariantDefinition marie = AssetDatabase.LoadAssetAtPath<ObstacleVariantDefinition>(
                Week20Obstacle4Setup.MarieVariantPath);
            ResourceDropTable marieDrops = AssetDatabase.LoadAssetAtPath<ResourceDropTable>(
                Week20Obstacle4Setup.MarieDropTablePath);
            Assert(table != null, "The Fairy Kingdom obstacle variant table is missing.");
            Assert(table.TryValidate(out string error) &&
                   Mathf.Approximately(table.SpecialRoomChance, Week20Obstacle4Setup.SpecialRoomChance), error);
            Assert(marie != null, "The Marie bomb box obstacle variant is missing.");
            Assert(marie.TryValidate(out error) && marie.VariantId == "marie-bomb-box" &&
                   marie.DropTable == marieDrops, error);
            ValidateMarieDrops(marieDrops);

            string[] allLayoutIds = Week18Obstacle2Setup.Layouts.Select(spec => spec.TemplateId)
                .Concat(Week20Obstacle4Setup.Layouts.Select(spec => spec.TemplateId)).ToArray();
            foreach (string id in allLayoutIds)
            {
                RoomTemplateDefinition template = generator.RoomTemplates.SingleOrDefault(candidate =>
                    candidate != null && candidate.TemplateId == id);
                Assert(template != null && template.TryValidate(out error) && template.TryValidateLayout(out error),
                    $"Obstacle Layout '{id}' is missing or invalid. {error}");
                Assert(template.LayoutDifficultyModifier == 1,
                    $"Obstacle Layout '{id}' must keep the +1 Layout difficulty modifier.");
                DestructibleObstacle[] obstacles =
                    template.RoomPrefabAsset.GetComponentsInChildren<DestructibleObstacle>(true);
                RoomObstacleVariantSlot[] slots =
                    template.RoomPrefabAsset.GetComponentsInChildren<RoomObstacleVariantSlot>(true);
                Assert(obstacles.Length > 0 && slots.Length == obstacles.Length &&
                       slots.All(slot => slot.VariantTable == table && slot.TryValidate(out _)),
                    $"Obstacle Layout '{id}' must mark every authored obstacle as a special candidate slot.");
            }

            foreach (Week20Obstacle4Setup.LayoutSpec spec in Week20Obstacle4Setup.Layouts)
            {
                RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                    candidate != null && candidate.TemplateId == spec.TemplateId);
                Assert(template.Profile.ProfileId == spec.ProfileId &&
                       template.RoomPrefabAsset.GetComponentsInChildren<DestructibleObstacle>(true).Length ==
                       spec.Cells.Length,
                    $"Layout '{spec.TemplateId}' must use its authored profile and obstacle cells.");
            }

            ValidateVariantSelection(generator, allLayoutIds[0]);
            ValidateCatalogSelection(generator, allLayoutIds);
            Debug.Log("Obstacle-4 verification passed: setup is idempotent, four new profile-sized obstacle " +
                      "Layouts pass the room contract, all seven obstacle Layouts expose safe candidate slots, " +
                      "the room-level seeded roll produces at most one deterministic Marie bomb box near 40%, " +
                      "and its 20% drop table favors bombs at weight 60.");
        }

        [MenuItem("Trickal Fan Game/Week 20/Verify Obstacle-4 With Regressions")]
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week18Obstacle1Verification.Verify();
            Week18Obstacle2Verification.Verify();
            Week19Difficulty1Verification.Verify();
            Week19Encounter4Verification.Verify();
            Week20Special2Verification.Verify();
            Debug.Log("Obstacle-4 regression verification passed.");
        }

        private static void ValidateVariantSelection(FloorGenerator generator, string templateId)
        {
            RoomTemplateDefinition template = generator.RoomTemplates.Single(candidate =>
                candidate != null && candidate.TemplateId == templateId);
            int specialRooms = 0;
            Dictionary<string, int> chosenSlots = new(StringComparer.Ordinal);
            for (int seed = 0; seed < SeedCount; seed++)
            {
                string first = Resolve(template, seed, out int firstCount);
                string second = Resolve(template, seed, out int secondCount);
                Assert(first == second && firstCount == secondCount,
                    $"Obstacle variants changed for repeated seed {seed}.");
                Assert(firstCount is 0 or 1, $"Seed {seed} resolved more than one special obstacle.");
                if (firstCount == 0) continue;
                specialRooms++;
                chosenSlots[first] = chosenSlots.TryGetValue(first, out int count) ? count + 1 : 1;
            }

            float rate = specialRooms / (float)SeedCount;
            Assert(rate >= 0.37f && rate <= 0.43f,
                $"Special obstacle room rate {rate:P1} is outside the expected 40% band.");
            Assert(chosenSlots.Count > 1, "The room-level roll must distribute the special obstacle across slots.");
        }

        private static string Resolve(RoomTemplateDefinition template, int seed, out int specialCount)
        {
            GameObject instance = Object.Instantiate(template.RoomPrefabAsset);
            try
            {
                RoomPrefab room = instance.GetComponent<RoomPrefab>();
                Assert(RoomObstacleVariantSlot.TryResolveForRoom(room, seed, out string error), error);
                DestructibleObstacle[] special = instance.GetComponentsInChildren<DestructibleObstacle>(true)
                    .Where(obstacle => obstacle.VariantId == "marie-bomb-box").ToArray();
                specialCount = special.Length;
                return special.Length == 1 ? special[0].ObstacleId : string.Empty;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateMarieDrops(ResourceDropTable table)
        {
            Assert(table != null, "The Marie bomb box drop table is missing.");
            Assert(table.TryValidate(out string error) &&
                   Mathf.Approximately(table.DropChance, Week20Obstacle4Setup.MarieDropChance), error);
            Assert(table.Entries.Single(entry => entry.DropId == "bomb").Weight == 60,
                "Marie bomb box must keep bomb weight 60.");
            int drops = 0;
            int bombs = 0;
            for (int seed = 0; seed < SeedCount * 4; seed++)
            {
                if (!table.TryRoll(seed, out ResourceDropEntry entry)) continue;
                drops++;
                if (entry.DropId == "bomb") bombs++;
            }

            float dropRate = drops / (float)(SeedCount * 4);
            float bombShare = bombs / (float)drops;
            Assert(dropRate >= 0.18f && dropRate <= 0.22f && bombShare >= 0.56f && bombShare <= 0.64f,
                $"Marie drops were {dropRate:P1} overall with {bombShare:P1} bombs.");
        }

        private static void ValidateCatalogSelection(FloorGenerator generator, IReadOnlyCollection<string> layoutIds)
        {
            HashSet<string> selected = new(StringComparer.Ordinal);
            for (int seed = 1; seed <= 512; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                foreach (GeneratedRoomNode node in graph.Nodes)
                    if (node.Template != null && layoutIds.Contains(node.Template.TemplateId))
                        selected.Add(node.Template.TemplateId);
            }

            foreach (string id in layoutIds)
                Assert(selected.Contains(id), $"Layout '{id}' was never selected across 512 generated seeds.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
