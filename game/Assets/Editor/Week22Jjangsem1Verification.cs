using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using TrickalFanGame.Combat;
using TrickalFanGame.Frontend;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Resource;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    // Jjangsem-1: 멜룬카드 copies each unopened chest (normal, golden, diamond) and each floor consumable of the current
    // room once (D4). A copied chest has its own stable ID, its own content roll and its kind's opening cost, and the
    // original list is fixed before copying, so a use never copies its own copies while the next use does. Copies stay
    // on revisits and floor rebuilds and leave with the floor like any chest. Chests now push freely like a heart.
    public static class Week22Jjangsem1Verification
    {
        private const int MaximumSeedSearch = 4096;
        private static readonly Vector2 PushOrigin = new(-500f, -500f);

        private static readonly string[] ConsumablePrefabPaths =
        {
            "Assets/Prefabs/HealthPickup.prefab",
            "Assets/Prefabs/SPPickup.prefab",
            "Assets/Prefabs/ElifPickup.prefab",
            "Assets/Prefabs/KeyPickup.prefab",
            "Assets/Prefabs/BombPickup.prefab",
        };

        public static void SetupAndVerifyBatch()
        {
            Week22Jjangsem1Setup.Setup();
            string itemGuid = AssetDatabase.AssetPathToGUID(Week22Jjangsem1Setup.MeluneCardPath);
            string tableGuid = AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath);
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week22Jjangsem1Setup.Setup();
            Assert(!string.IsNullOrWhiteSpace(itemGuid) &&
                   itemGuid == AssetDatabase.AssetPathToGUID(Week22Jjangsem1Setup.MeluneCardPath) &&
                   tableGuid == AssetDatabase.AssetPathToGUID(Week22Chest1Setup.ChestContentTablePath) &&
                   sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Jjangsem-1 setup changed the item, chest content table or Game Scene GUID.");
            Verify();
        }

        // Also re-runs the chest opening, push and content rules the copies reuse, the shared slot and the other
        // jjangsem spell, and the development panel.
        public static void VerifyWithRegressionsBatch()
        {
            Verify();
            Week22Chest0Verification.Verify();
            Week22Chest1Verification.Verify();
            Week22Chest2Verification.Verify();
            Week22Jjangsem0Verification.Verify();
            Week21Slot0Verification.Verify();
            Week20DevPanelVerification.Verify();
            Debug.Log("Jjangsem-1 regression verification passed.");
        }

        [MenuItem("Trickal Fan Game/Week 22/Verify Jjangsem-1 Melune Card")]
        public static void Verify()
        {
            ValidateContract();
            ValidateAcquisitionPath();
            ValidateDuplication();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateFreePush();
            Debug.Log("Jjangsem-1 verification passed: 멜룬카드 (Rare jjangsem spell, effect 41) copies every unopened " +
                      "chest and floor consumable of the current room once, with new chest IDs, separate content " +
                      "rolls and the same opening cost, never copies its own copies in one use but does on the next, " +
                      "keeps copies on revisits and rebuilds, refuses an empty room without being consumed, drops only " +
                      "from diamond chests, and chests push freely like a heart.");
        }

        private static void ValidateContract()
        {
            Assert((int)ItemEffectType.DuplicateRoomChestsAndPickups == 41 &&
                   (int)ItemEffectType.CurrentRoomCriticalBonus == 40 &&
                   (int)ItemEffectType.ReduceAndRecoverDamageTaken == 36,
                "Jjangsem-1 must add effect type 41 after the existing effects without renumbering them.");
            Assert(new ItemEffectEntry(ItemEffectType.DuplicateRoomChestsAndPickups).TryValidate(out string error),
                "The duplication effect must not need value fields. " + error);

            ItemDefinition card = LoadCard();
            Assert(card.ItemId == "jjangsem-melune-card" && card.DisplayName == "멜룬카드" &&
                   card.Kind == ItemKind.JjangsemSpell && card.Rarity == ItemRarity.Rare && card.IsActive &&
                   card.MaxStacks == 1 && card.IsValid && card.IsSingleUse,
                "jjangsem-melune-card must be a valid active Rare jjangsem spell named 멜룬카드.");
            Assert(ItemDefinition.IsItemIdValidForKind(card.ItemId, ItemKind.JjangsemSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(card.ItemId, ItemKind.SingleUseSpell) &&
                   !ItemDefinition.IsItemIdValidForKind(card.ItemId, ItemKind.Artifact),
                "The card ID must be a jjangsem spell ID only (Contract-0 §2.3).");
            Assert(card.Effects.Count == 1 &&
                   card.Effects[0].EffectType == ItemEffectType.DuplicateRoomChestsAndPickups,
                "The card must carry exactly the duplication effect.");

            string description = ArtifactEffectDescription.Build(card);
            Assert(description == "현재 방의 열지 않은 상자와 바닥 소모품을 하나씩 복제 (복제 상자는 내용물을 따로 추첨)",
                $"Unexpected card description: {description}");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Week13FrontendSetup.FontPath);
            Assert(font != null && font.HasCharacters(card.DisplayName + description),
                "The Frontend font is missing a card glyph.");
        }

        // Contract-0 §3: a jjangsem spell comes only from diamond chests.
        private static void ValidateAcquisitionPath()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            ItemDefinition card = LoadCard();
            ChestContentTable table = LoadTable();
            Assert(table.JjangsemSpells.Contains(card) && !table.Spells.Contains(card) &&
                   !table.GoldenExclusiveArtifacts.Contains(card),
                "The card must be in the chest table's jjangsem spell list only.");

            bool diamondHoldsCard = false;
            for (int index = 0; index < 8192 && !diamondHoldsCard; index++)
                diamondHoldsCard = table.RollContents(RoomClearRewardSpawner.DeriveChestSeed(index * 104729 + 7),
                    ChestKind.Diamond).Spell == card;
            Assert(diamondHoldsCard, "A diamond chest must be able to hold the card.");

            RoomGraphAssembler assembler = Assembler(scene);
            Assert(assembler.ChestContentTable == table && !assembler.SelectionRewardPool.Contains(card),
                "The card must not join the selection reward pool or the shop stock that shares it.");
            PlayerSingleUseEffects effects = assembler.Graph.Player.GetComponent<PlayerSingleUseEffects>();
            Assert(effects != null && effects.ChestContentTable == table,
                "The Game Scene player must copy chests with the chest content table.");
        }

        private static void ValidateDuplication()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = Assembler(scene);
            ChestContentTable table = LoadTable();
            ItemDefinition card = LoadCard();
            int seed = FindSeed(assembler.Generator);
            using TestRun run = new(assembler, seed);
            GeneratedRoomNode node = CombatRooms(run.Floor).First();
            RoomPrefab room = run.Room(node.RoomId);
            run.MoveTo(room);
            RoomRunState state = run.Progress.GetRoomState(node.RoomId);
            Transform content = room.Node.ContentRoot.transform;
            PlayerSingleUseEffects effects = run.Effects;
            Assert(Chests(content).Length == 0 && RoomDuplication.CollectTargets(content, true).Count == 0,
                $"{node.RoomId} must start without chests or floor consumables.");

            // A room with nothing to copy refuses the card and keeps it.
            run.Give(card, "jjangsem1-empty");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.ConditionNotMet && run.Slot.HeldDefinition == card,
                "Using the card in a room with nothing to copy must fail without consuming it.");

            // Targets: three closed chests, one opened chest and the five floor consumables. Item pickups, an opened
            // chest and the wallet are never copied.
            RoomChestSite site = new(room, node.Template, state, run.Progress, run.Graph.Player.transform);
            TreasureChest normal = DevelopmentChest(table, site, node, ChestKind.Normal);
            TreasureChest golden = DevelopmentChest(table, site, node, ChestKind.Golden);
            TreasureChest diamond = DevelopmentChest(table, site, node, ChestKind.Diamond);
            TreasureChest opened = DevelopmentChest(table, site, node, ChestKind.Normal);
            Assert(opened.TryOpenByTouch(run.Graph.Player), "Setup: open one chest.");
            List<GameObject> spawnedConsumables = SpawnConsumables(content, room, run.Progress);
            // The opened chest dropped its own consumables; they are floor consumables too.
            List<GameObject> originals = RoomDuplication.CollectTargets(content, true).Pickups.ToList();
            Assert(spawnedConsumables.All(originals.Contains) && originals.Count > spawnedConsumables.Count,
                "Setup: the spawned consumables and the opened chest's drops must all be targets.");
            Object.Instantiate(run.Slot.PickupPrefab, room.transform.position, Quaternion.identity, content)
                .Configure(LoadCard(), "jjangsem1-floor-card", false);
            ItemPickup artifact = Object.Instantiate(table.ArtifactPickupPrefab, room.transform.position,
                Quaternion.identity, content);
            artifact.Configure(table.GoldenExclusiveArtifacts[0]);
            int itemPickups = ItemPickups(content);
            int gold = run.Progress.GetResourceCount(RunResourceType.Gold);
            int chestsBefore = state.Chests.Count;
            Physics2D.SyncTransforms();

            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used && !run.Slot.HasItem,
                "Using the card in a room with targets must consume it.");
            RoomDuplicationResult first = effects.LastDuplication;
            Assert(first != null && first.Errors.Count == 0 &&
                   first.CopiedChests.Select(chest => chest.Kind)
                       .SequenceEqual(new[] { ChestKind.Normal, ChestKind.Golden, ChestKind.Diamond }) &&
                   first.CopiedChests.Select(chest => chest.ChestId).SequenceEqual(new[]
                       { RoomDuplication.CopyChestId(1), RoomDuplication.CopyChestId(2), RoomDuplication.CopyChestId(3) }) &&
                   first.CopiedChests.All(chest => chest.IsClosed && state.GetChest(chest.ChestId) == chest.Record),
                "Each unopened chest must get one closed copy of its kind with a new room chest ID.");
            Assert(state.Chests.Count == chestsBefore + 3 && opened.IsOpened && Chests(content).Length == 7,
                "The opened chest must not be copied.");
            Assert(first.CopiedPickups.Count == originals.Count &&
                   first.CopiedPickups.Zip(originals, SameConsumable).All(same => same) &&
                   first.CopiedPickups.All(copy => copy.transform.parent == content && RoomDuplication.IsFloorConsumable(copy)),
                $"Each floor consumable must get one copy of the same kind (copied " +
                $"{string.Join(", ", first.CopiedPickups.Select(copy => copy.name))}).");
            Assert(ItemPickups(content) == itemPickups && run.Progress.GetResourceCount(RunResourceType.Gold) == gold,
                $"Item pickups ({itemPickups} → {ItemPickups(content)}) and the wallet must stay as they are.");
            AssertPlacement(content, room, node);

            // Each copy rolls its own contents from its own seed.
            for (int index = 0; index < first.CopiedChests.Count; index++)
            {
                ChestRunState record = first.CopiedChests[index].Record;
                Assert(record.HasContentSeed && record.HasPosition &&
                       record.ContentSeed == FloorGenerator.DeriveSeed(node.ContentSeed, index + 1,
                           RoomDuplication.CopySeedSalt) &&
                       record.ContentSeed != state.GetChest(new[] { normal, golden, diamond }[index].ChestId).ContentSeed,
                    "A copied chest must record its own content seed, separate from its original.");
            }

            // A copy opens like its kind and alone: a key for golden, a bomb for diamond.
            room.Node.SetVisible(true);
            TreasureChest goldenCopy = first.CopiedChests[1];
            TreasureChest diamondCopy = first.CopiedChests[2];
            while (run.Progress.GetResourceCount(RunResourceType.Key) > 0)
                run.Progress.TrySpendResource(RunResourceType.Key);
            Assert(!goldenCopy.TryOpenByTouch(run.Graph.Player) && goldenCopy.IsClosed,
                "A golden copy must not open without a key.");
            run.Progress.TryAddResource(RunResourceType.Key, 1);
            Assert(goldenCopy.TryOpenByTouch(run.Graph.Player) && goldenCopy.IsOpened && golden.IsClosed &&
                   run.Progress.GetResourceCount(RunResourceType.Key) == 0,
                "A golden copy must spend one key and open without opening its original.");
            Assert(!diamondCopy.TryOpenByTouch(run.Graph.Player) && diamondCopy.TryOpenByBomb() && diamond.IsClosed,
                "A diamond copy must open only from a bomb, without opening its original.");
            ChestContents expected = table.RollContents(diamondCopy.Record.ContentSeed, ChestKind.Diamond);
            Assert(DropsOf(content, diamondCopy.ChestId) == expected.Consumables.Count &&
                   SpellsOf(content, diamondCopy.ChestId) == (expected.Spell != null ? 1 : 0),
                "An opened copy must drop the contents rolled from its own seed.");

            // A copied pickup is collected on its own.
            GameObject goldCopy = first.CopiedPickups.First(copy =>
                copy.TryGetComponent(out RunResourcePickup resource) && resource.ResourceType == RunResourceType.Gold);
            RunResourcePickup goldOriginal = originals.Select(pickup => pickup.GetComponent<RunResourcePickup>())
                .First(resource => resource != null && resource.ResourceType == RunResourceType.Gold);
            Assert(goldCopy.GetComponent<RunResourcePickup>().Collect(run.Progress) &&
                   run.Progress.GetResourceCount(RunResourceType.Gold) == gold + 1 && !goldOriginal.IsCollected,
                "Collecting a copied gold must add one gold and leave the original on the floor.");

            // The next use copies the copies of the last one; the targets are fixed before it copies anything.
            RoomDuplicationTargets before = RoomDuplication.CollectTargets(content, true);
            run.Give(card, "jjangsem1-second");
            Assert(run.Slot.TryUse() == SpellSlotUseResult.Used, "The second card must be used.");
            RoomDuplicationResult second = effects.LastDuplication;
            Assert(second.Errors.Count == 0 && second.CopiedChests.Count == before.Chests.Count &&
                   before.Chests.Contains(first.CopiedChests[0]) &&
                   second.CopiedChests.Select(chest => chest.ChestId).SequenceEqual(
                       Enumerable.Range(4, before.Chests.Count).Select(RoomDuplication.CopyChestId)) &&
                   second.CopiedPickups.Count == before.Pickups.Count &&
                   before.Pickups.Contains(first.CopiedPickups.Last()),
                "A second use must copy every unopened chest and pickup, earlier copies included, exactly once.");
            AssertPlacement(content, room, node);

            // Revisits keep the copies as they are.
            int closed = Chests(content).Count(chest => chest.IsClosed);
            run.MoveTo(run.Room(run.Graph.StartingNode.RoomId));
            run.MoveTo(room);
            Assert(Chests(content).Count(chest => chest.IsClosed) == closed && goldenCopy.IsOpened,
                "Leaving and entering the room must keep every copy and its open state.");

            // A floor rebuild restores every chest of the room where it was, closed or opened, and drops nothing again.
            Dictionary<string, Vector2> positions = Chests(content)
                .ToDictionary(chest => chest.ChestId, chest => (Vector2)chest.transform.position);
            HashSet<string> openedIds = new(Chests(content).Where(chest => chest.IsOpened).Select(chest => chest.ChestId));
            Assert(assembler.TryLoadFloor(1, run.Graph.Player, out string error), error);
            Transform rebuilt = run.Room(node.RoomId).Node.ContentRoot.transform;
            TreasureChest[] restored = Chests(rebuilt);
            Assert(restored.Length == positions.Count &&
                   restored.All(chest => positions.TryGetValue(chest.ChestId, out Vector2 position) &&
                                         Vector2.Distance(position, chest.transform.position) < 0.001f &&
                                         chest.IsOpened == openedIds.Contains(chest.ChestId)) &&
                   rebuilt.Cast<Transform>().All(child => !child.name.StartsWith("Chest Drop ", StringComparison.Ordinal)),
                "A floor rebuild must restore every copy at its spot with its open state and drop nothing again.");

            // Leaving the floor takes the unopened copies with it.
            Assert(assembler.TryLoadFloor(2, run.Graph.Player, out error), error);
            Assert(state.Chests.Where(chest => chest.ChestId.StartsWith(RoomDuplication.CopyChestPrefix,
                       StringComparison.Ordinal)).All(chest => chest.IsOpened || chest.IsDiscarded),
                "Leaving the floor must discard every unopened copy.");
            Assert(assembler.TryLoadFloor(1, run.Graph.Player, out error), error);
            Assert(Chests(run.Room(node.RoomId).Node.ContentRoot.transform).All(chest => chest.IsOpened),
                "Discarded copies must not return when the floor is rebuilt.");
        }

        // No two chest bodies overlap, and every chest stands where the player can reach it.
        private static void AssertPlacement(Transform content, RoomPrefab room, GeneratedRoomNode node)
        {
            TreasureChest[] chests = Chests(content);
            for (int first = 0; first < chests.Length; first++)
            for (int second = first + 1; second < chests.Length; second++)
                Assert(Vector2.Distance(chests[first].transform.position, chests[second].transform.position) >=
                       ChestPlacement.ChestWorldSize - 0.001f,
                    $"Chests {chests[first].ChestId} and {chests[second].ChestId} overlap.");

            Assert(RoomObstacleLayout.TryCollectFootprints(room.gameObject, out List<RoomObstacleFootprint> footprints,
                out string error), error);
            Func<Vector2, bool> reachable = RoomObstacleLayout.CreateReachability(node.Template.Profile.MovementBounds,
                node.Template.DoorSlots[0].SafeEntryPosition, footprints);
            Assert(chests.All(chest => reachable(room.transform.InverseTransformPoint(chest.transform.position))),
                "Every chest copy must stand where the player can reach it.");
        }

        // D4: a chest moves away from the player in any direction like a heart, and slides along a wall it is pushed
        // into at an angle. A wall it only touches never holds it, and a heart in the way is shoved ahead unless a wall
        // holds the heart. The axis push and the wall stop stay covered by Chest-0.
        private static void ValidateFreePush()
        {
            TreasureChest prefab = LoadTable().ChestPrefab;
            GameObject heartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Week17Resource0Setup.PrefabPath);
            Assert(heartPrefab != null, $"{Week17Resource0Setup.PrefabPath} is missing.");
            Vector2 diagonal = new Vector2(1f, 1f).normalized;
            Vector2 moved = Push(prefab, PushOrigin, diagonal, diagonal, null, out _);
            Assert(moved.x > 0.3f && moved.y > 0.3f && Mathf.Abs(moved.x - moved.y) < 0.05f &&
                   Mathf.Abs(moved.magnitude - TreasureChest.PushSpeed) < 0.1f,
                $"A diagonal push must move the chest diagonally at PushSpeed (moved {moved}).");
            Vector2 slid = Push(prefab, PushOrigin + new Vector2(0f, 20f), diagonal, diagonal,
                created => created.Add(Wall(PushOrigin + new Vector2(0f, 20f), ChestPlacement.ChestHalfSize + 0.05f)),
                out _);
            Assert(slid.y > 0.3f && slid.x < 0.5f,
                $"A diagonal push into a wall must slide the chest along it (moved {slid}).");
            Vector2 sideways = Push(prefab, PushOrigin + new Vector2(0f, 40f), Vector2.right, Vector2.up, null, out _);
            Assert(sideways.sqrMagnitude < 0.0001f, "Walking past a chest must not push it.");

            // Pushing along a wall the chest already touches, and away from it.
            Vector2 alongStart = PushOrigin + new Vector2(0f, 60f);
            Vector2 along = Push(prefab, alongStart, Vector2.up, Vector2.up,
                created => created.Add(Wall(alongStart, ChestPlacement.ChestHalfSize)), out _);
            Assert(Mathf.Abs(along.y - TreasureChest.PushSpeed) < 0.1f && Mathf.Abs(along.x) < 0.01f,
                $"A chest touching a wall must slide along it at PushSpeed (moved {along}).");
            Vector2 awayStart = PushOrigin + new Vector2(0f, 80f);
            Vector2 away = Push(prefab, awayStart, Vector2.left, Vector2.left,
                created => created.Add(Wall(awayStart, ChestPlacement.ChestHalfSize)), out _);
            Assert(Mathf.Abs(away.x + TreasureChest.PushSpeed) < 0.1f,
                $"A chest touching a wall must move away from it at PushSpeed (moved {away}).");

            // A heart touching the chest is shoved ahead; a heart held by a wall stops the chest.
            Vector2 heartStart = PushOrigin + new Vector2(0f, 100f);
            Vector2 shoved = Push(prefab, heartStart, Vector2.right, Vector2.right,
                created => created.Add(Heart(heartPrefab, heartStart, out _)), out List<Vector2> shovedEnds);
            Vector2 heartEnd = shovedEnds[0];
            Assert(Mathf.Abs(shoved.x - TreasureChest.PushSpeed) < 0.15f &&
                   heartEnd.x - heartStart.x > ChestPlacement.ChestHalfSize + TreasureChest.PushSpeed * 0.8f,
                $"A chest touching a heart must push the heart ahead (chest moved {shoved}, heart at {heartEnd}).");
            Vector2 heldStart = PushOrigin + new Vector2(0f, 120f);
            Vector2 held = Push(prefab, heldStart, Vector2.right, Vector2.right, created =>
            {
                created.Add(Heart(heartPrefab, heldStart, out float heartEdge));
                created.Add(Wall(heldStart, heartEdge));
            }, out _);
            Assert(held.x < 0.05f, $"A heart held by a wall must stop the chest (moved {held}).");
        }

        // A tall wall whose left face is offset right of center.
        private static GameObject Wall(Vector2 center, float offset)
        {
            GameObject wall = new("Jjangsem-1 wall", typeof(BoxCollider2D));
            wall.layer = LayerMask.NameToLayer("Environment");
            wall.transform.position = center + Vector2.right * (offset + 0.5f);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(1f, 20f);
            return wall;
        }

        // A heart touching the chest's right face; rightEdge is the heart's right edge offset from the chest center.
        private static GameObject Heart(GameObject prefab, Vector2 chestCenter, out float rightEdge)
        {
            GameObject heart = Object.Instantiate(prefab);
            Physics2D.SyncTransforms();
            float radius = heart.GetComponent<Collider2D>().bounds.extents.x;
            float offset = ChestPlacement.ChestHalfSize + radius + 0.005f;
            heart.transform.position = chestCenter + Vector2.right * offset;
            rightEdge = offset + radius;
            return heart;
        }

        // fromDirection: where the pusher stands, seen from the chest (opposite). setup adds the surroundings.
        private static Vector2 Push(TreasureChest prefab, Vector2 start, Vector2 fromDirection, Vector2 intent,
            Action<List<GameObject>> setup, out List<Vector2> surroundingEnds)
        {
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject chestObject = null;
            List<GameObject> created = new();
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                chestObject = Object.Instantiate(prefab.gameObject, start, Quaternion.identity);
                TreasureChest chest = chestObject.GetComponent<TreasureChest>();
                setup?.Invoke(created);
                Physics2D.SyncTransforms();
                for (int step = 0; step < 50; step++)
                {
                    Vector2 pusher = (Vector2)chest.transform.position -
                                     fromDirection * (ChestPlacement.ChestHalfSize + 0.3f);
                    chest.RegisterPush(pusher, intent);
                    chest.StepPush(0.02f);
                    Physics2D.Simulate(0.02f);
                }

                surroundingEnds = created.Select(item => (Vector2)item.transform.position).ToList();
                return chestObject.GetComponent<Rigidbody2D>().position - start;
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                if (chestObject != null) Object.DestroyImmediate(chestObject);
                foreach (GameObject item in created)
                    if (item != null) Object.DestroyImmediate(item);
            }
        }

        private static TreasureChest DevelopmentChest(ChestContentTable table, RoomChestSite site,
            GeneratedRoomNode node, ChestKind kind)
        {
            TreasureChest chest = RoomChestSpawner.SpawnDevelopmentChest(table, site, node.ContentSeed, kind,
                out string error);
            Assert(chest != null && chest.IsClosed, error);
            return chest;
        }

        // One of each floor consumable on a free row of the room, bound to the Run like a chest drop.
        private static List<GameObject> SpawnConsumables(Transform content, RoomPrefab room, RunProgress progress)
        {
            List<GameObject> spawned = new();
            for (int index = 0; index < ConsumablePrefabPaths.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConsumablePrefabPaths[index]);
                Assert(prefab != null, $"{ConsumablePrefabPaths[index]} is missing.");
                Vector3 position = room.transform.TransformPoint(new Vector3(-4f + index * 2f, -3f, 0f));
                GameObject pickup = Object.Instantiate(prefab, position, Quaternion.identity, content);
                pickup.name = $"Jjangsem-1 {prefab.name}";
                if (pickup.TryGetComponent(out RunResourcePickup resource)) resource.BindRunProgress(progress);
                spawned.Add(pickup);
            }

            Assert(spawned.All(RoomDuplication.IsFloorConsumable), "Setup: every consumable must be a target.");
            return spawned;
        }

        private static bool SameConsumable(GameObject copy, GameObject original)
        {
            if (copy == original) return false;
            if (original.TryGetComponent(out HealthPickup heart))
                return copy.TryGetComponent(out HealthPickup copied) && copied.HealUnits == heart.HealUnits;
            if (original.TryGetComponent(out RunResourcePickup resource))
                return copy.TryGetComponent(out RunResourcePickup copied) &&
                       copied.ResourceType == resource.ResourceType && copied.Amount == resource.Amount;
            return original.GetComponent<SPPickup>() != null && copy.GetComponent<SPPickup>() != null;
        }

        private static int ItemPickups(Transform content) =>
            content.GetComponentsInChildren<ItemPickup>(true).Length +
            content.GetComponentsInChildren<SingleUseItemPickup>(true).Length;

        private static TreasureChest[] Chests(Transform content) =>
            content.GetComponentsInChildren<TreasureChest>(true).Where(chest => chest.gameObject.activeSelf).ToArray();

        private static int DropsOf(Transform content, string chestId) =>
            content.Cast<Transform>().Count(child => child.name.StartsWith("Chest Drop ", StringComparison.Ordinal) &&
                                                     child.name.EndsWith(" " + chestId, StringComparison.Ordinal));

        private static int SpellsOf(Transform content, string chestId) =>
            content.Cast<Transform>().Count(child => child.name.StartsWith("Chest Spell ", StringComparison.Ordinal) &&
                                                     child.name.EndsWith(" " + chestId, StringComparison.Ordinal));

        private static IEnumerable<GeneratedRoomNode> CombatRooms(GeneratedFloor floor) =>
            floor.Nodes.Where(node => node.Role == GeneratedRoomRole.Intermediate && node.Encounter != null &&
                                      node.Template != null);

        private static int FindSeed(FloorGenerator generator)
        {
            for (int seed = 1; seed <= MaximumSeedSearch; seed++)
            {
                if (generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out _) &&
                    graph.FindFloor(2) != null && CombatRooms(graph.FindFloor(1)).Any())
                    return seed;
            }

            throw new InvalidOperationException("No seed produced the Jjangsem-1 verification floor.");
        }

        private static RoomGraphAssembler Assembler(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RoomGraphAssembler>(true)).Single();

        private static ChestContentTable LoadTable()
        {
            ChestContentTable table =
                AssetDatabase.LoadAssetAtPath<ChestContentTable>(Week22Chest1Setup.ChestContentTablePath);
            Assert(table != null && table.TryValidate(out _), "The chest content table is missing or invalid.");
            return table;
        }

        private static ItemDefinition LoadCard()
        {
            ItemDefinition card = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Week22Jjangsem1Setup.MeluneCardPath);
            Assert(card != null, $"Run Jjangsem-1 setup first: {Week22Jjangsem1Setup.MeluneCardPath} is missing.");
            return card;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // The Game Scene Run with the real player, slot and executor, reset when disposed.
        private sealed class TestRun : IDisposable
        {
            private readonly float previousTimeScale;
            private int givenCount;

            public RunProgress Progress { get; }
            public RoomGraphController Graph { get; }
            public GeneratedFloor Floor { get; }
            public PlayerSpellSlot Slot { get; }
            public PlayerSingleUseEffects Effects { get; }

            public TestRun(RoomGraphAssembler assembler, int seed)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                Progress = assembler.Progress;
                Graph = assembler.Graph;
                Progress.ResetProgress();
                Assert(assembler.TryApplyGeneratedGraphForVerification(seed, out string error), error);
                Floor = assembler.GeneratedGraph.FindFloor(1);

                GameObject player = Graph.Player.gameObject;
                Slot = player.GetComponent<PlayerSpellSlot>();
                Effects = player.GetComponent<PlayerSingleUseEffects>();
                Assert(Slot != null && Slot.PickupPrefab != null && Effects != null,
                    "Run Slot-0 setup first: the Game Scene player needs the spell slot.");
                InvokeLifecycle(player.GetComponent<Health>(), "Awake");
                InvokeLifecycle(player.GetComponent<PlayerInventory>(), "Awake");
                InvokeLifecycle(Slot, "Awake");
                InvokeLifecycle(Effects, "Awake");
            }

            public void Give(ItemDefinition item, string instanceId)
            {
                SingleUseItemPickup pickup = Object.Instantiate(Slot.PickupPrefab, Graph.CurrentNode.ContentRoot.transform);
                pickup.Configure(item, $"{instanceId}-{++givenCount}", false);
                Assert(Slot.TryCollect(pickup) && Slot.HeldDefinition == item, $"The slot must take {item.ItemId}.");
            }

            public RoomPrefab Room(string roomId)
            {
                foreach (RoomNode node in Graph.Nodes)
                    if (node.RoomId == roomId) return node.GetComponent<RoomPrefab>();
                throw new InvalidOperationException($"Runtime room {roomId} is missing.");
            }

            public void MoveTo(RoomPrefab room)
            {
                if (Graph.CurrentNode == room.Node) return;
                typeof(RoomGraphController).GetField("nextTransitionTime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(Graph, 0f);
                Assert(Graph.TryTeleport(Graph.CurrentNode, room.Node, room.Node.DefaultEntryPoint.position,
                        Graph.Player) && Graph.CurrentNode == room.Node,
                    $"The verification player must reach {room.Node.RoomId}.");
                Physics2D.SyncTransforms();
            }

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                Progress.ResetProgress();
            }

            private static void InvokeLifecycle(object target, string methodName)
            {
                if (target == null) return;
                MethodInfo method = target.GetType().GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                method?.Invoke(target, null);
            }
        }
    }
}
