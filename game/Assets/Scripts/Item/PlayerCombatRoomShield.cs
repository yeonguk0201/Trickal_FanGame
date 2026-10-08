using System;
using TrickalFanGame.Combat;
using TrickalFanGame.Room;
using UnityEngine;

namespace TrickalFanGame.Item
{
    // 긴급 보호 벨트: entering a room that starts a new combat Encounter adds shield. The shield stacks room after
    // room until current health + shield reaches 15 hearts. Start, treasure, shop and cleared rooms give nothing.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerCombatRoomShield : MonoBehaviour
    {
        private Health health;
        private RunProgress runProgress;
        private float shieldAmount;

        public float ShieldAmount => shieldAmount;
        public int TriggerCount { get; private set; }

        private void OnDestroy()
        {
            if (runProgress != null) runProgress.RoomChanged -= HandleRoomChanged;
        }

        public void AddShieldPerRoom(float amount)
        {
            shieldAmount += Mathf.Max(0f, amount);
        }

        public void BindRunProgress(RunProgress configuredRunProgress)
        {
            if (runProgress == configuredRunProgress)
            {
                return;
            }

            if (runProgress != null) runProgress.RoomChanged -= HandleRoomChanged;
            runProgress = configuredRunProgress;
            if (runProgress != null) runProgress.RoomChanged += HandleRoomChanged;
        }

        // Returns the shield gained for entering a room that starts a combat Encounter.
        public float GrantForCombatRoomEntry()
        {
            if (shieldAmount <= 0f)
            {
                return 0f;
            }

            if (health == null) health = GetComponent<Health>();
            TriggerCount++;
            return health.GainShield(shieldAmount);
        }

        private void HandleRoomChanged(int floor, int room)
        {
            if (IsUnclearedCombatRoom(floor, room))
            {
                GrantForCombatRoomEntry();
            }
        }

        private bool IsUnclearedCombatRoom(int floor, int room)
        {
            if (runProgress == null || runProgress.GeneratedGraph == null || floor <= 0 || room <= 0)
            {
                return false;
            }

            string roomId = FloorGenerator.BuildRoomId(floor, room);
            RoomRunState state = runProgress.GetRoomState(roomId);
            if (state == null || state.IsCleared)
            {
                return false;
            }

            foreach (GeneratedRoomNode node in runProgress.GeneratedGraph.Nodes)
            {
                if (node != null && string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                {
                    return node.Role is GeneratedRoomRole.Intermediate or GeneratedRoomRole.Boss;
                }
            }

            return false;
        }
    }
}
