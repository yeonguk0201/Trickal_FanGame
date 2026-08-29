using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class FloorAdvancePortal : MonoBehaviour
    {
        private RoomGraphAssembler assembler;
        private RoomController bossRoom;
        private int destinationFloor;

        public void Configure(RoomGraphAssembler configuredAssembler, RoomController configuredBossRoom, int configuredDestinationFloor)
        { assembler = configuredAssembler; bossRoom = configuredBossRoom; destinationFloor = configuredDestinationFloor; }
        private void Awake() => GetComponent<Collider2D>().isTrigger = true;
        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            if (player != null) TryEnter(player);
        }
        public bool TryEnter(PlayerMovement player)
        {
            if (player == null || assembler == null || bossRoom == null || bossRoom.State != RoomState.Cleared ||
                player.GetComponent<PlayerActionState>()?.CanTransition == false) return false;
            return assembler.TryLoadFloor(destinationFloor, player, out _);
        }
    }
}
