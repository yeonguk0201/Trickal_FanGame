using TrickalFanGame.Combat;
using TrickalFanGame.Item;
using UnityEngine;

namespace TrickalFanGame.Room
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class RoomStaticObstacle : MonoBehaviour
    {
        private void OnEnable() => TrickalFanGame.Frontend.GroundShadow.AttachDuringPlay(gameObject);

        [SerializeField] private string obstacleId;
        [Tooltip("나무 같은 높은 장애물입니다. 켜면 비행 중인 플레이어도 지나가지 못합니다.")]
        [SerializeField] private bool blocksFlight;

        public string ObstacleId => obstacleId;
        public bool BlocksFlight => blocksFlight;

        public void ConfigureHeight(bool configuredBlocksFlight) => blocksFlight = configuredBlocksFlight;

        public void Configure(string configuredObstacleId)
        {
            obstacleId = configuredObstacleId;
        }

        public bool TryValidate(out string error)
        {
            if (!StableRoomId.TryValidate(obstacleId, "Obstacle", out error))
            {
                return false;
            }

            Collider2D obstacleCollider = GetComponent<Collider2D>();
            if (obstacleCollider == null || !obstacleCollider.enabled || obstacleCollider.isTrigger)
            {
                error = $"Room obstacle '{obstacleId}' requires one enabled solid Collider2D.";
                return false;
            }

            if (gameObject.layer != LayerMask.NameToLayer("Environment"))
            {
                error = $"Room obstacle '{obstacleId}' must use the Environment layer.";
                return false;
            }

            if (GetComponentInChildren<Health>(true) != null ||
                GetComponentInChildren<ItemDropSource>(true) != null)
            {
                error = $"Room obstacle '{obstacleId}' must not have health or item-drop behavior.";
                return false;
            }

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null && body.bodyType != RigidbodyType2D.Static)
            {
                error = $"Room obstacle '{obstacleId}' cannot use a moving Rigidbody2D.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
