using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TrickalFanGame.Item;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrickalFanGame.Editor
{
    public static class Week8RunSeedVerification
    {
        private const int FirstVerificationSeed = Week8RandomRoomSetup.FixedVerificationSeed;
        private const int SecondVerificationSeed = 20260829;

        [MenuItem("Trickal Fan Game/Verify Phase F-4 Run Seed Lifecycle")]
        public static void Verify()
        {
            Week8RandomRoomBindingVerification.Verify();
            Scene scene = SceneManager.GetActiveScene();
            FloorGenerator generator = FindSceneObject<FloorGenerator>(
                scene,
                Week8RandomRoomSetup.GeneratorObjectName);
            RoomGraphAssembler assembler = FindSceneObject<RoomGraphAssembler>(
                scene,
                Week8RandomRoomSetup.GeneratorObjectName);
            RunSession session = FindSceneObject<RunSession>(scene, "Run Session");
            Assert(generator != null && assembler != null && session != null,
                "Phase F-4 requires the configured FloorGenerator, RoomGraphAssembler, and RunSession.");
            Assert(!generator.HasRunSeed,
                "The scene FloorGenerator must not serialize or pre-initialize a fixed runtime seed.");
            Assert(session.Progress != null && assembler.Progress == session.Progress &&
                   assembler.Graph != null && assembler.Graph.Progress == session.Progress,
                "RunSession, RunProgress, FloorGenerator binding, and RoomGraphController initialization order drifted.");

            ValidateRunSeedOwnership();
            ValidateGeneratorSeeds(generator);
            ValidateRevisitDoesNotReroll(assembler);

            Debug.Log(
                "Phase F-4 Run seed lifecycle verification passed: new Runs receive distinct non-Unity-Random " +
                "seeds, a Run seed is immutable across floor movement and revisit, equal seeds reproduce the " +
                "same room/enemy configuration, different seeds change it, stable room IDs are unchanged, " +
                "and repeated spawn points intentionally repeat a definition's prefab sequence.");
        }

        private static void ValidateRunSeedOwnership()
        {
            GameObject firstHolder = new("Phase F-4 First Run");
            GameObject secondHolder = new("Phase F-4 Second Run");
            UnityEngine.Random.State originalRandomState = UnityEngine.Random.state;
            try
            {
                RunProgress firstProgress = firstHolder.AddComponent<RunProgress>();
                RunSession firstSession = firstHolder.AddComponent<RunSession>();
                firstSession.Configure(null, firstProgress, null);
                firstSession.SetResultSavingEnabled(false);

                UnityEngine.Random.InitState(80421);
                float expectedNextRandomValue = UnityEngine.Random.value;
                UnityEngine.Random.InitState(80421);
                Assert(firstSession.BeginRun("verification-first"),
                    "The first verification Run must initialize successfully.");
                float actualNextRandomValue = UnityEngine.Random.value;
                Assert(expectedNextRandomValue == actualNextRandomValue,
                    "Run seed creation must not read or mutate UnityEngine.Random state.");

                int firstSeed = firstSession.RunSeed;
                Assert(firstProgress.HasRunSeed,
                    "Beginning a Run must store its seed in RunProgress before room generation.");
                Assert(firstProgress.TryInitializeRunSeed(firstSeed, out string error), error);
                Assert(!firstProgress.TryInitializeRunSeed(unchecked(firstSeed + 1), out error) &&
                       error.Contains("cannot change", StringComparison.Ordinal),
                    "The same Run must reject a seed change during floor movement or revisit.");
                firstProgress.RecordRoomEntry(2, 1);
                firstProgress.RecordRoomEntry(1, 2);
                firstProgress.ResetProgress();
                Assert(firstProgress.HasRunSeed && firstProgress.RunSeed == firstSeed,
                    "Progress reset and room movement must not discard or reroll the Run seed.");

                RunProgress secondProgress = secondHolder.AddComponent<RunProgress>();
                RunSession secondSession = secondHolder.AddComponent<RunSession>();
                secondSession.Configure(null, secondProgress, null);
                secondSession.SetResultSavingEnabled(false);
                Assert(secondSession.BeginRun("verification-second"),
                    "The second verification Run must initialize successfully.");
                Assert(secondSession.RunSeed != firstSeed,
                    "Consecutive new Runs must not reuse the immediately previous Run seed.");
            }
            finally
            {
                UnityEngine.Random.state = originalRandomState;
                UnityEngine.Object.DestroyImmediate(firstHolder);
                UnityEngine.Object.DestroyImmediate(secondHolder);
            }
        }

        private static void ValidateGeneratorSeeds(FloorGenerator generator)
        {
            Assert(generator.TryGenerateForSeed(
                FirstVerificationSeed,
                out GeneratedFloorGraph first,
                out string error), error);
            Assert(generator.TryGenerateForSeed(
                FirstVerificationSeed,
                out GeneratedFloorGraph repeated,
                out error), error);
            Assert(generator.TryGenerateForSeed(
                SecondVerificationSeed,
                out GeneratedFloorGraph different,
                out error), error);

            string firstSignature = BuildGeneratedSignature(first);
            Assert(firstSignature == BuildGeneratedSignature(repeated),
                "The same Run seed must reproduce the same room and enemy definitions.");
            Assert(firstSignature != BuildGeneratedSignature(different),
                "The two Phase F-4 verification seeds must produce different room/enemy definitions.");
            Assert(first.Nodes.Count == different.Nodes.Count,
                "Changing the seed must not change the stable room address set.");
            for (int index = 0; index < first.Nodes.Count; index++)
            {
                Assert(first.Nodes[index].RoomId == different.Nodes[index].RoomId &&
                       first.Nodes[index].RoomId == FloorGenerator.BuildRoomId(
                           first.Nodes[index].FloorNumber,
                           first.Nodes[index].RoomNumber),
                    "Stable floor-XX-room-YY IDs must not depend on the Run seed.");
            }
        }

        private static void ValidateRevisitDoesNotReroll(RoomGraphAssembler assembler)
        {
            Assert(assembler.TryApplyGeneratedGraphForVerification(
                FirstVerificationSeed,
                out string error), error);
            RoomGraphController graph = assembler.Graph;
            string initialSignature = BuildAppliedSignature(graph);
            RoomNode revisitedNode = null;
            foreach (RoomNode candidate in graph.Nodes)
            {
                if (candidate.ContentRoot.GetComponentInChildren<RewardRoom>(true) != null &&
                    candidate.ContentRoot.GetComponentInChildren<RoomController>(true) != null)
                {
                    revisitedNode = candidate;
                    break;
                }
            }

            Assert(revisitedNode != null,
                "The revisit verification needs a room with its verified RoomController and RewardRoom.");
            revisitedNode.MarkVisited();
            RoomController controller = revisitedNode.ContentRoot.GetComponentInChildren<RoomController>(true);
            RewardRoom reward = revisitedNode.ContentRoot.GetComponentInChildren<RewardRoom>(true);
            Assert(controller != null && reward != null,
                "The revisit verification room lost its verified state holders.");

            SetAutoProperty(controller, "State", RoomState.Cleared);
            SetAutoProperty(controller, "HasStarted", true);
            SetAutoProperty(reward, "HasRewarded", true);
            assembler.Progress.RecordRoomEntry(2, 1);
            assembler.Progress.RecordRoomEntry(revisitedNode.FloorNumber, revisitedNode.RoomNumber);
            Assert(initialSignature == BuildAppliedSignature(graph),
                "Floor movement and room revisit must not invoke a configuration reroll.");

            Assert(assembler.TryApplyGeneratedGraphForVerification(FirstVerificationSeed, out error), error);
            Assert(initialSignature == BuildAppliedSignature(graph) && revisitedNode.HasBeenVisited &&
                   controller.State == RoomState.Cleared && controller.HasStarted && reward.HasRewarded,
                "Even an idempotent same-seed reapply must preserve visited, cleared, and rewarded state.");

            foreach (RoomNode node in graph.Nodes)
            {
                RoomController encounter = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                if (node.Definition.RoomType == RoomType.Boss || encounter.EnemyPrefabs.Count == 0)
                {
                    continue;
                }

                for (int index = 0; index < encounter.EnemyPrefabs.Count; index++)
                {
                    GameObject expected = node.Definition.EncounterPrefabs[
                        index % node.Definition.EncounterPrefabs.Count];
                    Assert(encounter.EnemyPrefabs[index] == expected,
                        $"{node.RoomId} spawn {index + 1} must intentionally repeat the definition prefab sequence.");
                }
            }

            Assert(assembler.Progress.TryInitializeRunSeed(FirstVerificationSeed, out error), error);
            Assert(assembler.TryApplyGeneratedGraph(out error), error);
            Assert(assembler.HasAppliedRuntimeGraph &&
                   assembler.AppliedRunSeed == FirstVerificationSeed &&
                   assembler.Generator.HasRunSeed &&
                   assembler.Generator.RunSeed == FirstVerificationSeed,
                "Runtime assembly must consume the immutable RunProgress seed before RoomGraphController starts.");
        }

        private static string BuildGeneratedSignature(GeneratedFloorGraph graph)
        {
            StringBuilder signature = new();
            foreach (GeneratedRoomNode node in graph.Nodes)
            {
                signature.Append(node.RoomId).Append(':')
                    .Append(node.Definition.RoomDefinitionId).Append('|');
            }

            return signature.ToString();
        }

        private static string BuildAppliedSignature(RoomGraphController graph)
        {
            StringBuilder signature = new();
            foreach (RoomNode node in graph.Nodes)
            {
                signature.Append(node.RoomId).Append(':')
                    .Append(node.Definition.RoomDefinitionId).Append(':');
                RoomController controller = node.ContentRoot.GetComponentInChildren<RoomController>(true);
                foreach (GameObject prefab in controller.EnemyPrefabs)
                {
                    signature.Append(prefab != null ? prefab.name : "missing").Append(',');
                }

                signature.Append('|');
            }

            return signature.ToString();
        }

        private static void SetAutoProperty<T>(object target, string propertyName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(field != null, $"Missing verification state backing field for {propertyName}.");
            field.SetValue(target, value);
        }

        private static T FindSceneObject<T>(Scene scene, string requiredName) where T : Component
        {
            T match = null;
            foreach (T candidate in Resources.FindObjectsOfTypeAll<T>())
            {
                if (candidate == null || candidate.gameObject.scene != scene ||
                    candidate.gameObject.name != requiredName)
                {
                    continue;
                }

                Assert(match == null, $"Found multiple scene objects named {requiredName} with {typeof(T).Name}.");
                match = candidate;
            }

            return match;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
