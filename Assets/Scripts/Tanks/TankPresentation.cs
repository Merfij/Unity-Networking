using UnityEngine;

namespace NetworkTankArena.Tanks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TankSimulation))]
    public sealed class TankPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform chassisTilt;
        [SerializeField] private Transform turretPivot;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float bobFrequency = 1.5f;
        [SerializeField, Min(0f)] private float bankAngle = 6f;
        [SerializeField, Min(0f)] private float pitchAngle = 4f;
        [SerializeField, Min(0f)] private float wobbleAngle = 1f;
        [SerializeField, Min(0f)] private float wobbleFrequency = 3f;
        [SerializeField, Min(0.01f)] private float fullTiltSpeed = 6f;
        [SerializeField, Min(0.01f)] private float fullBankTurnSpeed = 120f;
        [SerializeField, Min(0f)] private float tiltResponse = 8f;

        private TankSimulation tank;
        private Vector3 baseVisualPosition;
        private Quaternion baseChassisRotation;
        private Quaternion baseTurretRotation;
        private Vector3 previousPosition;
        private float previousYaw;
        private float cosmeticTime;
        private float pitch;
        private float roll;
        private bool initialized;

        private void Awake()
        {
            tank = GetComponent<TankSimulation>();
            if (visualRoot == null || chassisTilt == null || turretPivot == null ||
                visualRoot.parent != transform || chassisTilt.parent != visualRoot ||
                turretPivot.parent != visualRoot || chassisTilt == turretPivot)
            {
                Debug.LogError("TankPresentation needs VisualRoot directly beneath the tank root, with " +
                    "distinct ChassisTilt and TurretPivot children directly beneath VisualRoot.", this);
                enabled = false;
                return;
            }

            baseVisualPosition = visualRoot.localPosition;
            baseChassisRotation = chassisTilt.localRotation;
            baseTurretRotation = turretPivot.localRotation;
            initialized = true;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            previousYaw = transform.eulerAngles.y;
            cosmeticTime = pitch = roll = 0f;
        }

        private void LateUpdate()
        {
            if (!initialized || Time.deltaTime <= 0f)
                return;

            float deltaTime = Time.deltaTime;
            cosmeticTime += deltaTime;
            Vector3 velocity = (transform.position - previousPosition) / deltaTime;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float yaw = transform.eulerAngles.y;
            float turnRate = Mathf.DeltaAngle(previousYaw, yaw) / deltaTime;
            previousPosition = transform.position;
            previousYaw = yaw;

            float motion = Mathf.Clamp01(velocity.magnitude / fullTiltSpeed);
            float response = 1f - Mathf.Exp(-tiltResponse * deltaTime);
            pitch = Mathf.Lerp(pitch, -Mathf.Clamp(forwardSpeed / fullTiltSpeed, -1f, 1f) * pitchAngle, response);
            roll = Mathf.Lerp(roll, -Mathf.Clamp(turnRate / fullBankTurnSpeed, -1f, 1f) * bankAngle, response);

            visualRoot.localPosition = baseVisualPosition + Vector3.up *
                (Mathf.Sin(cosmeticTime * bobFrequency * 2f * Mathf.PI) * bobAmplitude);
            float wobble = Mathf.Sin(cosmeticTime * wobbleFrequency * 2f * Mathf.PI) * wobbleAngle * motion;
            chassisTilt.localRotation = baseChassisRotation * Quaternion.Euler(pitch, 0f, roll + wobble);

            // Compensate for chassis yaw. Cosmetic bob and chassis tilt never determine aim.
            turretPivot.rotation = Quaternion.Euler(0f, tank.TurretYaw, 0f);
        }

        private void OnDisable()
        {
            if (!initialized)
                return;

            visualRoot.localPosition = baseVisualPosition;
            chassisTilt.localRotation = baseChassisRotation;
            turretPivot.localRotation = baseTurretRotation;
        }
    }
}
