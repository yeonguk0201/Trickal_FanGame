using System;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RunProgress : MonoBehaviour
    {
        public int CurrentFloor { get; private set; }
        public int CurrentRoom { get; private set; }
        public int KillCount { get; private set; }
        public bool IsProgressionStopped { get; private set; }

        public event Action<int, int> RoomChanged;

        public void RecordRoomEntry(int floorNumber, int roomNumber)
        {
            if (IsProgressionStopped)
            {
                return;
            }

            CurrentFloor = Mathf.Max(1, floorNumber);
            CurrentRoom = Mathf.Max(1, roomNumber);
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
        }
    }
}
