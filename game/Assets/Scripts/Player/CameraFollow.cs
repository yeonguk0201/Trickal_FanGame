using UnityEngine;

namespace TrickalFanGame.Player
{
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float followSpeed = 8f;

        private Vector3 offset;

        private void Awake()
        {
            if (target != null)
            {
                offset = transform.position - target.position;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            offset = transform.position - target.position;
        }
    }
}
