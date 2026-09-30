using TrickalFanGame.Player;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // The floor's secret room, shared by every obstacle on the floor. Target is filled in once all rooms exist.
    public sealed class SecretRoomLink
    {
        public SecretRoomLink(RoomGraphController graph) => Graph = graph;
        public RoomGraphController Graph { get; }
        public RoomNode Target { get; set; }
    }

    // Special-3: left where a broken obstacle rolled the secret-passage drop. After the room is cleared, stepping in
    // drops the player into the floor's secret room however far away it is. It stays as a shortcut after discovery.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class SecretPit : MonoBehaviour
    {
        // The player's center must be over the pit, so brushing its rim with the body does not drop them in.
        public const float EnterRadius = 0.45f;

        private SecretRoomLink link;
        private RoomNode sourceNode;
        private RoomController sourceRoom;

        public RoomNode SourceNode => sourceNode;
        public RoomNode Target => link?.Target;

        public void Bind(SecretRoomLink configuredLink, RoomNode configuredSourceNode,
            RoomController configuredSourceRoom)
        {
            link = configuredLink;
            sourceNode = configuredSourceNode;
            sourceRoom = configuredSourceRoom;
        }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEnter(other.GetComponentInParent<PlayerMovement>());
        }

        // Stay retries once the room clears or the transition cooldown ends while the player stands on the pit.
        private void OnTriggerStay2D(Collider2D other)
        {
            TryEnter(other.GetComponentInParent<PlayerMovement>());
        }

        public bool TryEnter(PlayerMovement player)
        {
            RoomNode target = link?.Target;
            if (player == null || target == null || link.Graph == null || sourceNode == null ||
                (sourceRoom != null && sourceRoom.State != RoomState.Cleared) ||
                ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude >
                EnterRadius * EnterRadius)
            {
                return false;
            }

            return link.Graph.TryTeleport(sourceNode, target, target.InitialSpawnPosition, player);
        }
    }
}
