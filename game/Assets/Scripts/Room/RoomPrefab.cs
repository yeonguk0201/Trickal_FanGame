using System;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public sealed class RoomPrefab : MonoBehaviour
    {
        [SerializeField] private RoomNode node;
        [SerializeField] private RoomController controller;
        [SerializeField] private RewardRoom rewardRoom;
        [SerializeField] private RoomDoorSlot[] doorSlots = Array.Empty<RoomDoorSlot>();

        public RoomNode Node => node;
        public RoomController Controller => controller;
        public RewardRoom RewardRoom => rewardRoom;
        public RoomDoorSlot[] DoorSlots => doorSlots;
        public void Configure(RoomNode configuredNode, RoomController configuredController,
            RewardRoom configuredRewardRoom, RoomDoorSlot[] configuredSlots)
        { node = configuredNode; controller = configuredController; rewardRoom = configuredRewardRoom;
          doorSlots = configuredSlots ?? Array.Empty<RoomDoorSlot>(); }

        public RoomDoorSlot FindSlot(RoomDoorDirection direction)
        { foreach (RoomDoorSlot slot in doorSlots) if (slot != null && slot.Direction == direction) return slot; return null; }

        public bool TryValidate(out string error)
        {
            if (node == null || controller == null || doorSlots == null || doorSlots.Length != 4)
            { error = "RoomPrefab requires a RoomNode, RoomController, and exactly four door slots."; return false; }
            bool[] found = new bool[4];
            foreach (RoomDoorSlot slot in doorSlots)
            {
                if (slot == null || slot.Doorway == null || slot.EntryPoint == null || slot.Blocker == null || slot.Seal == null)
                { error = "Every RoomPrefab door slot needs a doorway, opposite entry point, blocker, and seal."; return false; }
                int index = (int)slot.Direction;
                if (found[index]) { error = $"RoomPrefab duplicates its {slot.Direction} slot."; return false; }
                found[index] = true;
            }
            error = null; return true;
        }
    }
}
