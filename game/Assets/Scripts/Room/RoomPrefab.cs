using System;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Room
{
    public static class RoomLayout
    {
        public const float Width = 16f;
        public const float Height = 9f;
        public const float WallThickness = 0.5f;
        public const float DoorOpeningLength = 2.4f;
        public const float DoorThickness = 0.45f;
        public const float DoorLength = 2.2f;
        public const float SealThickness = 0.55f;
        public const float SealLength = 2.3f;
        public const float TransitionThickness = 0.3f;
        public const float TransitionInset = 0.2f;
        public const float EntryInsetFromTransition = 1.7f;
        public const float RoomSpacingX = 20f;
        public const float RoomSpacingY = 13f;
        public const float CameraOrthographicSize = 5.25f;
        public const float SpawnHorizontalSpacing = 3f;
        public const float SpawnVerticalOffset = 2f;
        public const float FloorExitVerticalOffset = 3.2f;

        public static Vector2 RoomSize => new(Width, Height);
        public static Vector2 EncounterSize => new(14.5f, 7.5f);
        public static Vector2 RewardTriggerSize => new(10f, 6f);
        public static float HorizontalWallCenter => Width * 0.5f - WallThickness * 0.5f;
        public static float VerticalWallCenter => Height * 0.5f - WallThickness * 0.5f;
        public static float HorizontalTransitionCenter => HorizontalWallCenter - TransitionInset;
        public static float VerticalTransitionCenter => VerticalWallCenter - TransitionInset;
        public static float TransitionInnerEdgeInset => TransitionInset + TransitionThickness * 0.5f;
        public static float SafeEntryInsetFromWall => TransitionInset + EntryInsetFromTransition;
        public static float HorizontalWallSegmentLength => (Width - DoorOpeningLength) * 0.5f;
        public static float VerticalWallSegmentLength => (Height - DoorOpeningLength) * 0.5f;
        public static float HorizontalWallSegmentCenter =>
            DoorOpeningLength * 0.5f + HorizontalWallSegmentLength * 0.5f;
        public static float VerticalWallSegmentCenter =>
            DoorOpeningLength * 0.5f + VerticalWallSegmentLength * 0.5f;

        public static bool IsSideDoor(RoomDoorDirection direction) =>
            direction == RoomDoorDirection.Left || direction == RoomDoorDirection.Right;

        public static Vector2 Direction(RoomDoorDirection direction) => direction switch
        {
            RoomDoorDirection.Left => Vector2.left,
            RoomDoorDirection.Right => Vector2.right,
            RoomDoorDirection.Up => Vector2.up,
            _ => Vector2.down,
        };

        public static float TransitionCenter(RoomDoorDirection direction) =>
            IsSideDoor(direction) ? HorizontalTransitionCenter : VerticalTransitionCenter;

        public static Vector2 SpawnPosition(int index, int count)
        {
            float x = (index - (count - 1) * 0.5f) * SpawnHorizontalSpacing;
            float y = index % 2 == 0 ? SpawnVerticalOffset : -SpawnVerticalOffset;
            return new Vector2(x, y);
        }
    }

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
