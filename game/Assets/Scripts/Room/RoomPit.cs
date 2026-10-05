using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Room
{
    // Terrain-0: an authored floor pit. Its solid collider sits on the Pit layer, which collides only with walking
    // bodies (player, enemies, pickups), so it blocks movement while projectiles and lines of fire pass over it.
    // It is unrelated to SecretPit, the trigger a broken obstacle can leave that drops the player into the secret room.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class RoomPit : MonoBehaviour
    {
        public const string LayerName = "Pit";

        [SerializeField] private string pitId = "pit-01";

        public string PitId => pitId;

        public static int Layer => LayerMask.NameToLayer(LayerName);

        // Everything a walking body cannot cross: walls, doors, obstacles, chests and pits.
        public static int MovementBlockMask => LayerMask.GetMask("Environment", LayerName);

        public void Configure(string configuredPitId)
        {
            pitId = configuredPitId;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(pitId, "Pit", out error)) return false;
            BoxCollider2D pitCollider = GetComponent<BoxCollider2D>();
            if (pitCollider == null || !pitCollider.enabled || pitCollider.isTrigger)
            {
                error = $"Pit '{pitId}' requires one enabled solid BoxCollider2D.";
                return false;
            }

            if (Layer < 0 || gameObject.layer != Layer)
            {
                error = $"Pit '{pitId}' must use the {LayerName} layer.";
                return false;
            }

            if (GetComponentInChildren<Health>(true) != null ||
                GetComponentInChildren<ItemDropSource>(true) != null ||
                GetComponent<DestructibleObstacle>() != null ||
                GetComponent<RoomStaticObstacle>() != null ||
                GetComponent<SecretPit>() != null)
            {
                error = $"Pit '{pitId}' must not carry health, drops, obstacle or secret-pit behavior.";
                return false;
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null && body.bodyType != RigidbodyType2D.Static)
            {
                error = $"Pit '{pitId}' cannot use a moving Rigidbody2D.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
