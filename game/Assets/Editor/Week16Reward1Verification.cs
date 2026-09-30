using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using TrickalFanGame.Player;
using TrickalFanGame.Room;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week16Reward1Verification
    {
        [MenuItem("Trickal Fan Game/Week 16/Verify Reward-1 Selection Session")]
        public static void Verify()
        {
            ValidateSelectionPersistenceAndSingleGrant();
            ValidateHealingAndRunReset();
            Debug.Log("Week 16 Reward-1 verification passed: candidates survive cancellation and reconstruction, " +
                      "combat and progression are blocked only while the selection is open, repeated input and " +
                      "revisits cannot grant twice, only the selected Item is recorded, healing applies once, " +
                      "and Run reset clears both selection state and its input block.");
        }

        public static void SetupAndVerifyBatch()
        {
            Week16Content0Setup.Setup();
            Week16Reward0Verification.Verify();
            Verify();
        }

        private static void ValidateSelectionPersistenceAndSingleGrant()
        {
            GameObject player = CreatePlayer(out Health health, out PlayerInventory inventory,
                out PlayerActionState actionState);
            GameObject progressObject = new("Reward-1 Selection Progress", typeof(RunProgress));
            RunProgress progress = progressObject.GetComponent<RunProgress>();
            List<ItemDefinition> definitions = CreateDefinitions("persistence", 4);
            try
            {
                Assert(progress.TryInitializeRunSeed(1601, out string seedError), seedError);
                progress.RecordRoomEntry(1, 3);
                ItemRewardSelectionSession first = player.AddComponent<ItemRewardSelectionSession>();
                first.Configure(progress, inventory, health, actionState);
                const string rewardId = "floor-01-room-03:treasure";
                Assert(first.TryOpen(definitions, rewardId, out string openError), openError);
                string initialSignature = Signature(first.Candidates);
                Assert(first.Candidates.Count == 3 && progress.IsRewardSelectionPending &&
                       actionState.IsRewardSelectionBlocked &&
                       !actionState.CanMove && !actionState.CanBasicAttack &&
                       !actionState.CanUseLowerGradeSkill && !actionState.CanStartUltimate &&
                       !actionState.CanTransition,
                    "An open selection must expose three candidates and block all player combat/progression input.");

                Assert(first.TryCancel() && !first.IsOpen && !progress.IsRewardSelectionPending &&
                       !actionState.IsRewardSelectionBlocked && actionState.CanMove && actionState.CanTransition,
                    "Cancelling an open reward must close only the UI block and restore player progression.");
                Assert(first.TryOpen(definitions, rewardId, out string reopenError), reopenError);
                Assert(Signature(first.Candidates) == initialSignature,
                    "Reopening a cancelled reward must restore its original candidates without rerolling.");

                Object.DestroyImmediate(first);
                Assert(actionState.IsRewardSelectionBlocked,
                    "Destroying the selection view/controller must not release a pending selection block.");
                ItemRewardSelectionSession reconstructed = player.AddComponent<ItemRewardSelectionSession>();
                reconstructed.Configure(progress, inventory, health, actionState);
                definitions.Reverse();
                Assert(reconstructed.TryOpen(definitions, rewardId, out string restoreError), restoreError);
                Assert(Signature(reconstructed.Candidates) == initialSignature,
                    "Reconstruction and reordered source data must restore the already-open candidates without rerolling.");

                int selectedIndex = FindFirstItemIndex(reconstructed.Candidates);
                ItemRewardCandidate selected = reconstructed.Candidates[selectedIndex];
                Assert(reconstructed.TrySelect(selectedIndex), "The selected Item candidate was not applied.");
                Assert(!progress.IsRewardSelectionPending && !actionState.IsRewardSelectionBlocked &&
                       actionState.CanMove && actionState.CanBasicAttack && actionState.CanUseLowerGradeSkill &&
                       actionState.CanStartUltimate && actionState.CanTransition,
                    "Completing the selection must release combat and progression input.");
                Assert(inventory.AcquiredItems.Count == 1 &&
                       inventory.AcquiredItems[0].ItemId == selected.StableId &&
                       inventory.AcquiredItems[0].Order == 1 &&
                       definitions.Where(definition => definition.ItemId != selected.StableId)
                           .All(definition => inventory.GetStackCount(definition.ItemId) == 0),
                    "Only the selected Item may be applied and recorded in acquisition order.");
                Assert(!reconstructed.TrySelect(selectedIndex) &&
                       !reconstructed.TrySelect(selected.StableId) &&
                       inventory.AcquiredItems.Count == 1,
                    "Repeated index or stable-ID input must not grant the completed reward twice.");

                Object.DestroyImmediate(reconstructed);
                ItemRewardSelectionSession revisit = player.AddComponent<ItemRewardSelectionSession>();
                revisit.Configure(progress, inventory, health, actionState);
                Assert(!revisit.TryOpen(definitions, rewardId, out string revisitError) &&
                       revisitError.Contains("already completed", StringComparison.Ordinal) &&
                       revisit.State.IsCompleted && revisit.State.SelectedCandidateId == selected.StableId &&
                       Signature(revisit.Candidates) == initialSignature && inventory.AcquiredItems.Count == 1,
                    "A revisit must restore the completed choice without reopening, rerolling, or granting it again.");
            }
            finally
            {
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(player);
            }
        }

        private static void ValidateHealingAndRunReset()
        {
            GameObject player = CreatePlayer(out Health health, out PlayerInventory inventory,
                out PlayerActionState actionState);
            GameObject progressObject = new("Reward-1 Healing Progress", typeof(RunProgress));
            RunProgress progress = progressObject.GetComponent<RunProgress>();
            List<ItemDefinition> definitions = CreateDefinitions("healing", 2);
            ItemDefinition inactive = CreateDefinition("spell-reward1-inactive", false);
            try
            {
                Assert(progress.TryInitializeRunSeed(1602, out string seedError), seedError);
                foreach (ItemDefinition definition in definitions)
                    Assert(inventory.TryAcquire(definition), "Healing boundary setup could not cap its Item pool.");

                health.TakeDamage(health.MaxHealth * 0.5f);
                float healthBefore = health.CurrentHealth;
                int acquiredBefore = inventory.AcquiredItems.Count;
                ItemRewardSelectionSession session = player.AddComponent<ItemRewardSelectionSession>();
                session.Configure(progress, inventory, health, actionState);
                const string healingRewardId = "floor-02-room-07:treasure";
                Assert(session.TryOpen(definitions, healingRewardId, out string openError), openError);
                Assert(session.Candidates.Count == 1 && session.Candidates[0].IsHealing,
                    "A fully capped valid pool must open one healing candidate.");
                Assert(session.TrySelect(0) &&
                       Mathf.Approximately(health.CurrentHealth,
                           healthBefore + health.MaxHealth * ArtifactRewardSelector.FallbackHealMaxHealthRatio) &&
                       inventory.AcquiredItems.Count == acquiredBefore,
                    "Healing must apply immediately without creating an Item acquisition record.");
                float healthAfter = health.CurrentHealth;
                Assert(!session.TrySelect(0) && Mathf.Approximately(health.CurrentHealth, healthAfter),
                    "Repeated healing input must not heal twice.");

                Assert(!session.TryOpen(new[] { inactive }, "reward-1:invalid", out _) &&
                       !progress.IsRewardSelectionPending && !actionState.IsRewardSelectionBlocked,
                    "An invalid pool must fail without leaving a phantom selection or input block.");
                Assert(session.TryOpen(definitions, "reward-1:reset", out string resetError), resetError);
                Assert(progress.IsRewardSelectionPending && actionState.IsRewardSelectionBlocked,
                    "Reset boundary setup did not create a pending selection.");
                progress.ResetProgress();
                Assert(!progress.IsRewardSelectionPending && progress.RewardSelections.Count == 0 &&
                       !actionState.IsRewardSelectionBlocked && !session.IsOpen && session.State == null,
                    "Run reset must discard pending choices and release their action block.");
            }
            finally
            {
                Object.DestroyImmediate(inactive);
                foreach (ItemDefinition definition in definitions) Object.DestroyImmediate(definition);
                Object.DestroyImmediate(progressObject);
                Object.DestroyImmediate(player);
            }
        }

        private static GameObject CreatePlayer(out Health health, out PlayerInventory inventory,
            out PlayerActionState actionState)
        {
            GameObject player = new("Reward-1 Verification Player", typeof(Health), typeof(PlayerSP),
                typeof(PlayerStats), typeof(PlayerInventory), typeof(PlayerActionState));
            health = player.GetComponent<Health>();
            inventory = player.GetComponent<PlayerInventory>();
            actionState = player.GetComponent<PlayerActionState>();
            InvokeLifecycle(health, "Awake");
            InvokeLifecycle(player.GetComponent<PlayerSP>(), "Awake");
            InvokeLifecycle(player.GetComponent<PlayerStats>(), "Awake");
            InvokeLifecycle(inventory, "Awake");
            return player;
        }

        private static List<ItemDefinition> CreateDefinitions(string suffix, int count)
        {
            List<ItemDefinition> definitions = new();
            for (int index = 0; index < count; index++)
            {
                string prefix = index % 2 == 0 ? "artifact" : "spell";
                definitions.Add(CreateDefinition($"{prefix}-reward1-{suffix}-{index}"));
            }
            return definitions;
        }

        private static ItemDefinition CreateDefinition(string itemId, bool isActive = true)
        {
            ItemDefinition definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.name = itemId;
            ItemKind kind = itemId.StartsWith("spell-", StringComparison.Ordinal)
                ? ItemKind.Spell
                : ItemKind.Artifact;
            definition.ConfigureContract(itemId, itemId, kind, ItemRarity.Common, isActive, 1,
                new ItemEffectEntry(ItemEffectType.AttackDamagePercent, 0.01f));
            return definition;
        }

        private static int FindFirstItemIndex(IReadOnlyList<ItemRewardCandidate> candidates)
        {
            for (int index = 0; index < candidates.Count; index++)
                if (candidates[index].IsItem) return index;
            throw new InvalidOperationException("Expected at least one Item reward candidate.");
        }

        private static string Signature(IEnumerable<ItemRewardCandidate> candidates) =>
            string.Join("|", candidates.Select(candidate => candidate.StableId));

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
