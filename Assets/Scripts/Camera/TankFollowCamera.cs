using UnityEngine;

namespace NetworkTankArena.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TankFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 12f, -8f);
        [SerializeField] private float lookAtHeight = 1.35f;

        private void Awake()
        {
            if ((target != null && (transform == target || transform.IsChildOf(target))) ||
                (followOffset - Vector3.up * lookAtHeight).sqrMagnitude < 0.0001f ||
                new Vector2(followOffset.x, followOffset.z).sqrMagnitude < 0.0001f)
            {
                Debug.LogError("TankFollowCamera needs an independent gameplay-root target when assigned, and an offset " +
                    "with horizontal separation from that target.", this);
                enabled = false;
            }
        }

        /// <summary>The scene camera is bound only to the locally owned spawned gameplay root.</summary>
        public void SetTarget(Transform gameplayRoot)
        {
            if (gameplayRoot != null && (transform == gameplayRoot || transform.IsChildOf(gameplayRoot)))
            {
                Debug.LogError("The gameplay camera must be independent of the tank hierarchy.", this);
                return;
            }

            target = gameplayRoot;
            FollowTarget();
        }

        private void Start()
        {
            // All Start calls precede the first Update, so mouse aiming starts with this pose.
            FollowTarget();
        }

        private void LateUpdate()
        {
            FollowTarget();
        }

        private void FollowTarget()
        {
            if (target == null)
                return;

            transform.position = target.position + followOffset;
            transform.rotation = Quaternion.LookRotation(Vector3.up * lookAtHeight - followOffset, Vector3.up);
        }
    }
}
