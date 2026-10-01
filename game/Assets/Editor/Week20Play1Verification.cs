using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrickalFanGame.Room;
using TrickalFanGame.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TrickalFanGame.Editor
{
    public static class Week20Play1Verification
    {
        [MenuItem("Trickal Fan Game/Week 20/Verify Play-1 Preparation and Integration")]
        public static void VerifyBatch()
        {
            ValidateTimeRecording();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ValidateSessionRecording();
            VerifyInSavedScene(Week14Room8Verification.Verify);
            VerifyInSavedScene(Week17Resource1Verification.Verify);
            VerifyInSavedScene(Week17Resource3Verification.Verify);
            VerifyInSavedScene(Week20Obstacle4Verification.Verify);
            VerifyInSavedScene(Week20Special1Verification.Verify);
            VerifyInSavedScene(Week20Special2Verification.Verify);
            VerifyInSavedScene(Week20Special3Verification.Verify);
            VerifyInSavedScene(Week20Special4Verification.Verify);
            VerifyInSavedScene(Week16Reward3Verification.Verify);
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            PrintManualSeeds();
            Debug.Log("Play-1 preparation verification passed: local floor-time recording and session end freeze, " +
                      "current Room-8 integration, resources, obstacles, locks, bombs, secrets, shop and rewards. " +
                      "Manual full Runs and actual floor times are still required; no automated full-Run player was used.");
        }

        private static void VerifyInSavedScene(Action verification)
        {
            EditorSceneManager.OpenScene(Week13FrontendSetup.GameScenePath, OpenSceneMode.Single);
            verification();
        }

        private static void ValidateTimeRecording()
        {
            DevelopmentPlaytestRecord record = new(-42, 1, 10, "erpin", 8, 12);
            record.EnterFloor(1, 15); // A room transition on the same floor must not lose time.
            record.EnterFloor(2, 30);
            record.EnterFloor(1, 40); // A floor rebuild/backtrack accumulates instead of overwriting.
            record.EnterFloor(3, 45);
            Assert(record.GetFloorSeconds(1, 50) == 25 && record.GetFloorSeconds(2, 50) == 10 &&
                   record.GetFloorSeconds(3, 50) == 5, "Floor time must accumulate across same-floor rooms and returns.");
            record.MarkAssisted("Full HP");
            record.MarkAssisted("Full HP");
            Assert(record.Finish("CLEARED", 60) && !record.Finish("DIED", 100),
                "A finished measurement must reject a second ending.");
            record.EnterFloor(1, 200);
            Assert(record.GetFloorSeconds(1, 200) == 25 && record.GetFloorSeconds(3, 200) == 15 &&
                   record.Outcome == "CLEARED" && record.IsAssisted &&
                   record.Format(200).Contains("content=8/12") && record.Format(200).Contains("assistance=Full HP"),
                "Frozen measurements and metadata must survive later scene/result time.");
            DevelopmentPlaytestRecord normal = new(42, 0, 0);
            normal.EnterFloor(2, 10);
            normal.EnterFloor(2, 5); // Defensive monotonic clamp.
            Assert(normal.GetFloorSeconds(1, 20) == 10 && normal.GetFloorSeconds(2, 20) == 10 &&
                   !normal.IsAssisted, "Backward timestamps must not add or subtract recorded time.");
            Debug.Log("Play-1 time recorder verification passed: accumulation, room revisits, floor returns, " +
                      "terminal freeze, duplicate ending, seed/content metadata, assistance and monotonic times.");
        }

        private static void ValidateSessionRecording()
        {
            GameObject holder = new("Play-1 session record verification");
            try
            {
                RunProgress progress = holder.AddComponent<RunProgress>();
                RunSession session = holder.AddComponent<RunSession>();
                session.Configure(null, progress, null);
                session.SetResultSavingEnabled(false);
                Assert(progress.TryInitializeRunSeed(314159, out string error), error);
                progress.RecordRoomEntry(1, 1);
                Assert(session.BeginRun("erpin") && session.DevelopmentPlaytest?.Seed == 314159,
                    "A real RunSession must start a measurement with its initialized seed.");
                progress.RecordRoomEntry(2, 1);
                typeof(RunSession).GetMethod("EndRun", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { false, "UNKNOWN" });
                Assert(session.DevelopmentPlaytest.IsFinished && session.DevelopmentPlaytest.Outcome == "DIED:UNKNOWN" &&
                       session.PendingRequest.reachedFloor == 2 &&
                       RunSession.LastDevelopmentPlaytestReport.Contains("seed=314159"),
                    "Run end must freeze and retain the local record without changing the result contract.");
            }
            finally { Object.DestroyImmediate(holder); }
        }

        private static void PrintManualSeeds()
        {
            FloorGenerator generator = Object.FindFirstObjectByType<FloorGenerator>();
            Assert(generator != null && generator.FloorSettings.Count == 3, "Play-1 needs the configured Floor-1 scene.");
            Dictionary<string, int> chosen = new() { ["baseline"] = 1 };
            for (int seed = 2; seed <= 256 && chosen.Count < 3; seed++)
            {
                Assert(generator.TryGenerateForSeed(seed, out GeneratedFloorGraph graph, out string error), error);
                if (!chosen.ContainsKey("specials") && graph.Floors.Any(floor =>
                        floor.Nodes.Any(node => node.Role == GeneratedRoomRole.Secret) &&
                        floor.Nodes.Any(node => node.Role == GeneratedRoomRole.Shop) &&
                        floor.Nodes.Any(node => node.Role == GeneratedRoomRole.Treasure && node.RequiresKey)))
                    chosen.Add("specials", seed);
                else if (!chosen.ContainsKey("large-floor") && graph.FindFloor(3).Nodes.Count == 18)
                    chosen.Add("large-floor", seed);
            }
            Assert(chosen.Count == 3, "Could not find distinct Play-1 manual seeds for special rooms and a large floor.");
            foreach (var entry in chosen)
            {
                Assert(generator.TryGenerateForSeed(entry.Value, out GeneratedFloorGraph graph, out string error), error);
                Debug.Log($"Play-1 manual seed {entry.Key}={entry.Value}: " + string.Join("; ", graph.Floors.Select(floor =>
                    $"F{floor.FloorNumber} rooms={floor.Nodes.Count} secret={floor.Nodes.Any(n => n.Role == GeneratedRoomRole.Secret)} " +
                    $"shop={floor.Nodes.Any(n => n.Role == GeneratedRoomRole.Shop)} " +
                    $"locked-treasure={floor.Nodes.Any(n => n.Role == GeneratedRoomRole.Treasure && n.RequiresKey)}")));
            }
        }

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
