using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RunProgress : MonoBehaviour
    {
        private readonly Dictionary<string, RoomRunState> roomStates = new(StringComparer.Ordinal);

        public int RunSeed { get; private set; }
        public bool HasRunSeed { get; private set; }
        public int CurrentFloor { get; private set; }
        public int CurrentRoom { get; private set; }
        public int KillCount { get; private set; }
        public bool IsProgressionStopped { get; private set; }
        public bool HasClearedFinalBoss { get; private set; }
        public GeneratedFloorGraph GeneratedGraph { get; private set; }
        public IReadOnlyDictionary<string, RoomRunState> RoomStates => roomStates;
        public event Action FinalBossCleared;

        public event Action<int, int> RoomChanged;

        public bool TrySetGeneratedGraph(GeneratedFloorGraph graph, out string error)
        {
            if (graph == null)
            {
                error = "RunProgress requires a generated graph.";
                return false;
            }

            if (!graph.TryValidate(out error))
            {
                error = $"RunProgress rejected an invalid generated graph. {error}";
                return false;
            }

            if (GeneratedGraph != null)
            {
                if (ReferenceEquals(GeneratedGraph, graph)) { error = null; return true; }
                error = "RunProgress already owns a generated graph for this Run.";
                return false;
            }

            GeneratedGraph = graph;
            foreach (GeneratedRoomNode node in graph.Nodes)
                roomStates.Add(node.RoomId, new RoomRunState(node.RoomId));
            error = null;
            return true;
        }

        public RoomRunState GetRoomState(string roomId)
        {
            return !string.IsNullOrWhiteSpace(roomId) && roomStates.TryGetValue(roomId, out RoomRunState state)
                ? state : null;
        }

        public void RecordFinalBossCleared()
        {
            if (IsProgressionStopped || HasClearedFinalBoss) return;
            HasClearedFinalBoss = true;
            FinalBossCleared?.Invoke();
        }

        public bool TryInitializeRunSeed(int configuredRunSeed, out string error)
        {
            if (HasRunSeed)
            {
                if (RunSeed == configuredRunSeed)
                {
                    error = null;
                    return true;
                }

                error = $"Run seed is already initialized to {RunSeed} and cannot change during the Run.";
                return false;
            }

            RunSeed = configuredRunSeed;
            HasRunSeed = true;
            error = null;
            return true;
        }

        public void RecordRoomEntry(int floorNumber, int roomNumber)
        {
            if (IsProgressionStopped)
            {
                return;
            }

            CurrentFloor = Mathf.Max(1, floorNumber);
            CurrentRoom = Mathf.Max(1, roomNumber);
            GetRoomState(FloorGenerator.BuildRoomId(CurrentFloor, CurrentRoom))?.MarkVisited();
            RoomChanged?.Invoke(CurrentFloor, CurrentRoom);
            Debug.Log($"Run progress: Floor {CurrentFloor}, Room {CurrentRoom}.", this);
        }

        public void StopProgression()
        {
            IsProgressionStopped = true;
            Debug.Log($"Run progress stopped at Floor {CurrentFloor}, Room {CurrentRoom}.", this);
        }

        public void RecordKill()
        {
            if (!IsProgressionStopped)
            {
                KillCount++;
            }
        }

        public void ResetProgress()
        {
            CurrentFloor = 0;
            CurrentRoom = 0;
            KillCount = 0;
            IsProgressionStopped = false;
            HasClearedFinalBoss = false;
            GeneratedGraph = null;
            roomStates.Clear();
        }
    }
}
