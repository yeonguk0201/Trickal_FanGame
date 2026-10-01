using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // A locked doorway has an inner physical edge. Unlock here without transitioning;
    // the existing passage trigger still owns the later room transition.
    public sealed class ClosedDoorBoundary : MonoBehaviour
    {
        [SerializeField] private RoomDoorway doorway;

        public void Configure(RoomDoorway configuredDoorway) => doorway = configuredDoorway;

        private void OnCollisionEnter2D(Collision2D collision) => TryUnlockFromContact(
            collision.collider.GetComponentInParent<PlayerMovement>());
        private void OnCollisionStay2D(Collision2D collision) => TryUnlockFromContact(
            collision.collider.GetComponentInParent<PlayerMovement>());

        public bool TryUnlockFromContact(PlayerMovement player)
        {
            return player != null && doorway != null && doorway.RequiresKey && !doorway.IsKeyLockOpen &&
                (doorway.RequiredClearedRoom == null || doorway.RequiredClearedRoom.State == RoomState.Cleared) &&
                doorway.ContainsPassageCenter(player.transform.position) &&
                doorway.IsMovingIntoPassage(player.MovementIntent) && doorway.TryUnlockWithKey();
        }
    }
}
