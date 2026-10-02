using System;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Enemy;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week16Reward3Verification
    {
        private const int VerificationSeed = 1604;

        [MenuItem("Trickal Fan Game/Week 16/Verify Reward-3 Room Integration")]
        public static void Verify()
        {
            Scene scene = EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            RoomGraphAssembler assembler = FindSingle<RoomGraphAssembler>(scene, "RoomGraphAssembler");
            ValidateSceneContract(assembler);
            ValidateIntegratedRun(assembler);
            Debug.Log("Week 16 Reward-3 verification passed: treasure rooms require a center interaction, " +
                      "cancel and reopen the same three candidates, preserve completion across reconstruction, " +
                      "floor 1/2 bosses drop one mixed-pool Item, and the final boss grants no growth reward.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Reward2Verification.SetupAndVerifyBatch();
            string sceneGuid = AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath);
            Week16Reward3Setup.Setup();
            Week16Reward3Setup.Setup();
            Assert(sceneGuid == AssetDatabase.AssetPathToGUID(Week13FrontendSetup.GameScenePath),
                "Reward-3 setup changed the Game Scene GUID.");
            Verify();
        }

        private static void ValidateSceneContract(RoomGraphAssembler assembler)
        {
            Assert(assembler.RewardSelectionSession != null &&
                   assembler.SelectionRewardPool.Count >= ArtifactRewardSelector.MaximumCandidateCount,
                "Reward-3 requires the scene selection session and unified Item pool.");
            Assert(assembler.SelectionRewardPool.All(definition =>
                       definition != null && definition.IsActive && definition.IsValid) &&
                   assembler.SelectionRewardPool.Select(definition => definition.ItemId)
                       .Distinct(StringComparer.Ordinal).Count() == assembler.SelectionRewardPool.Count,
                "Reward-3 selection pool must contain only unique active valid Items.");
            Assert(assembler.SelectionRewardPool.Any(definition => definition.Kind == ItemKind.Artifact) &&
                   assembler.SelectionRewardPool.All(definition => definition.Kind != ItemKind.Spell),
                "Reward-3 must hold Artifacts and no retired legacy Spell in one pool (Contract-0 §3).");
            string[] roomPrefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert(roomPrefabPaths.Length > 0 && roomPrefabPaths.All(path =>
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RewardRoom reward = prefab != null ? prefab.GetComponentInChildren<RewardRoom>(true) : null;
                BoxCollider2D zone = reward != null ? reward.GetComponent<BoxCollider2D>() : null;
                return reward != null && reward.InteractionMarker != null && reward.InteractionPrompt != null &&
                       zone != null && zone.isTrigger && zone.size == new Vector2(2.6f, 2.6f);
            }), "Every Room Prefab must contain the configured center treasure interaction marker, prompt, and zone.");
        }

        private static void ValidateIntegratedRun(RoomGraphAssembler assembler)
        {
            RunProgress progress = assembler.Progress;
            ItemRewardSelectionSession session = assembler.RewardSelectionSession;
            PlayerMovement player = assembler.Graph.Player;
            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            Assert(progress != null && session != null && player != null && inventory != null,
                "Reward-3 integration requires Run progress, selection session, and configured player inventory.");

            Health playerHealth = player.GetComponent<Health>();
            PlayerActionState actionState = player.GetComponent<PlayerActionState>();
            InvokeLifecycle(playerHealth, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
            InvokeLifecycle(inventory, "Awake");
            session.Configure(progress, inventory, playerHealth, actionState);

            progress.ResetProgress();
            Assert(progress.TryInitializeRunSeed(VerificationSeed, out string error), error);
            Assert(assembler.TryApplyGeneratedGraphForVerification(VerificationSeed, out error), error);
            ValidateTreasureReward(assembler, session, player, inventory);

            int finalClearEvents = 0;
            Action onFinalClear = () => finalClearEvents++;
            progress.FinalBossCleared += onFinalClear;
            try
            {
                for (int floor = 1; floor <= 3; floor++)
                {
                    Assert(assembler.TryLoadFloor(floor, player, out error), error);
                    RoomPrefab bossRoom = FindRoom(assembler, RoomType.Boss);
                    BossController boss = null;
                    bossRoom.Controller.EnemySpawned += enemy => boss = enemy?.GetComponent<BossController>();
                    bossRoom.Controller.BeginCombat(player.GetComponent<Health>());
                    Assert(boss != null, $"Floor {floor} boss did not spawn.");
                    ItemDropSource source = boss.GetComponent<ItemDropSource>();
                    BossItemDrop bossReward = boss.GetComponent<BossItemDrop>();
                    int pickupCount = CountRuntimePickups(assembler);
                    int selectionCount = progress.RewardSelections.Count;
                    boss.GetComponent<Health>().TakeDamage(99999f);
                    Assert(bossRoom.Controller.State == RoomState.Cleared,
                        $"Floor {floor} boss room did not clear.");

                    if (floor < 3)
                    {
                        string expectedId = $"{bossRoom.Node.RoomId}:boss";
                        FloorAdvancePortal portal = bossRoom.GetComponentInChildren<FloorAdvancePortal>(true);
                        Assert(bossReward != null && !bossReward.UsesSelectionReward &&
                               bossReward.RewardId == expectedId && !session.IsOpen &&
                               source != null && source.HasDropped &&
                               CountRuntimePickups(assembler) == pickupCount + 1 &&
                               source.LastDroppedDefinition != null &&
                               assembler.SelectionRewardPool.Contains(source.LastDroppedDefinition),
                            $"Floor {floor} boss did not drop exactly one Item from the mixed reward pool.");
                        Assert(portal != null && string.IsNullOrWhiteSpace(portal.RequiredRewardId) &&
                               portal.IsUnlocked && !progress.IsRewardSelectionPending &&
                               progress.RewardSelections.Count == selectionCount,
                            $"Floor {floor} boss drop must not create or wait for a selection session.");
                        int acquiredCount = inventory.AcquiredItems.Count;
                        Assert(!bossReward.TryHandleBossDefeated() && inventory.AcquiredItems.Count == acquiredCount,
                            $"Floor {floor} boss reward was repeatable.");
                        Assert(portal.TryEnter(player) && progress.CurrentFloor == floor + 1,
                            $"Floor {floor} portal did not advance after reward confirmation.");
                    }
                    else
                    {
                        Assert(bossReward != null && bossReward.IsFinalBoss &&
                               progress.HasClearedFinalBoss && finalClearEvents == 1 &&
                               progress.RewardSelections.Count == selectionCount && !session.IsOpen &&
                               (source == null || !source.HasDropped) && CountRuntimePickups(assembler) == pickupCount,
                            "The final boss must complete the Run without another growth reward.");
                    }
                }
            }
            finally
            {
                progress.FinalBossCleared -= onFinalClear;
            }
        }

        private static void ValidateTreasureReward(RoomGraphAssembler assembler,
            ItemRewardSelectionSession session, PlayerMovement player, PlayerInventory inventory)
        {
            RoomPrefab treasure = FindRoom(assembler, RoomType.Reward);
            RewardRoom reward = treasure.RewardRoom;
            string expectedId = $"{treasure.Node.RoomId}:treasure";
            int acquiredCount = inventory.AcquiredItems.Count;
            int pickupCount = CountRuntimePickups(assembler);
            reward.SetPlayerPresenceForVerification(true, inventory);
            Assert(reward.InteractionMarker != null && reward.InteractionMarker.activeSelf &&
                   reward.InteractionPrompt != null && reward.InteractionPrompt.activeSelf &&
                   reward.CanInteract && !session.IsOpen,
                "Entering a treasure room interaction zone must show a prompt without opening the selection.");
            Assert(reward.TryInteract() && session.IsOpen &&
                   session.State.RewardId == expectedId && session.Candidates.Count == 3,
                "Treasure interaction did not open its stable three-choice reward.");
            string signature = string.Join("|", session.Candidates.Select(candidate => candidate.StableId));
            Assert(session.TryCancel() && !session.IsOpen && !assembler.Progress.IsRewardSelectionPending &&
                   reward.CanInteract && reward.InteractionPrompt.activeSelf,
                "Cancelling a treasure reward must close the selection and restore interaction/progression.");
            Assert(assembler.TryLoadFloor(2, player, out string leaveFloorError), leaveFloorError);
            Assert(assembler.TryLoadFloor(1, player, out string reopenFloorError), reopenFloorError);
            RoomPrefab reopenedTreasureRoom = FindRoom(assembler, RoomType.Reward);
            reward = reopenedTreasureRoom.RewardRoom;
            reward.SetPlayerPresenceForVerification(true, inventory);
            Assert(reward.TryInteract() &&
                   string.Join("|", session.Candidates.Select(candidate => candidate.StableId)) == signature,
                "Cancelling and revisiting a treasure reward must reopen the same candidates.");
            Assert(!reward.HasRewarded &&
                   !assembler.Progress.GetRoomState(reopenedTreasureRoom.Node.RoomId).HasClaimedArtifact &&
                   CountRuntimePickups(assembler) == pickupCount &&
                   !assembler.TryLoadFloor(2, player, out _),
                "Treasure reward must remain unclaimed and block floor loading until selection.");
            Assert(session.TrySelect(0) && reward.HasRewarded &&
                   assembler.Progress.GetRoomState(reopenedTreasureRoom.Node.RoomId).HasClaimedArtifact &&
                   inventory.AcquiredItems.Count == acquiredCount + 1,
                "Treasure reward selection did not atomically grant and complete the reward.");
            Assert(assembler.TryLoadFloor(1, player, out string error), error);
            RoomPrefab rebuiltTreasure = FindRoom(assembler, RoomType.Reward);
            rebuiltTreasure.RewardRoom.SetPlayerPresenceForVerification(true, inventory);
            Assert(rebuiltTreasure.RewardRoom.HasRewarded &&
                   !rebuiltTreasure.RewardRoom.TryOpenRewardSelection(inventory) &&
                   inventory.AcquiredItems.Count == acquiredCount + 1 &&
                   assembler.Progress.GetRewardSelection(expectedId)?.IsCompleted == true,
                "Reconstructed treasure rooms must preserve completion without reopening or regranting.");
        }

        private static int CountRuntimePickups(RoomGraphAssembler assembler) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<ItemPickup>(true).Length;

        // Special-3 secret rooms are also Reward rooms; this verifier checks the floor's treasure room.
        private static RoomPrefab FindRoom(RoomGraphAssembler assembler, RoomType type) =>
            assembler.CurrentFloorRoot.GetComponentsInChildren<RoomPrefab>(true)
                .Single(room => room.Node.Definition.RoomType == type &&
                                assembler.GeneratedGraph.Nodes.Single(node => node.RoomId == room.Node.RoomId).Role !=
                                GeneratedRoomRole.Secret);

        private static T FindSingle<T>(Scene scene, string label) where T : Component
        {
            T[] matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Game Scene requires exactly one {label}; found {matches.Length}.");
            return matches[0];
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target?.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
