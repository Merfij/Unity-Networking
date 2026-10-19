using UnityEngine;

namespace NetworkTankArena.Tanks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class TankSimulation : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float turnSpeed = 120f;
        [SerializeField, Min(0f)] private float turretHeight = 1.35f;

        private CharacterController characterController;

        public float TurretYaw { get; private set; }
        public Vector3 TurretOrigin => transform.position + Vector3.up * turretHeight;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            TurretYaw = transform.eulerAngles.y;
        }

        /// <summary>
        /// Called only by the active controller. This component has no independent update loop.
        /// Position and chassis yaw belong to the root; turret yaw is canonical gameplay state.
        /// </summary>
        public void Simulate(TankControlIntent intent, float deltaTime)
        {
            if (!isActiveAndEnabled || !characterController.enabled ||
                !TankMath.IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            intent = intent.Sanitize();
            float yaw = TankMath.TurnYaw(transform.eulerAngles.y, intent.Turn, turnSpeed, deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            characterController.Move(TankMath.ForwardDisplacement(yaw, intent.Throttle, moveSpeed, deltaTime));

            if (intent.HasAimPoint && TankMath.TryGetAimYaw(TurretOrigin, intent.AimWorldPoint, out float aimYaw))
                TurretYaw = aimYaw;
        }

        /// <summary>Clients consume server-written canonical yaw without running movement simulation.</summary>
        public void ApplyReplicatedTurretYaw(float yaw)
        {
            if (TankMath.IsFinite(yaw))
                TurretYaw = Mathf.Repeat(yaw, 360f);
        }
    }
}
