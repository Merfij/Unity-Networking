using UnityEngine;

namespace NetworkTankArena.Tanks
{
    /// <summary>Planar gameplay maths. Yaw is measured in degrees, with +Z forward.</summary>
    public static class TankMath
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        public static float TurnYaw(float yaw, float turn, float turnSpeed, float deltaTime)
        {
            if (deltaTime <= 0f)
                return yaw;

            return Mathf.Repeat(yaw + Mathf.Clamp(turn, -1f, 1f) * turnSpeed * deltaTime, 360f);
        }

        public static Vector3 ForwardDisplacement(float yaw, float throttle, float moveSpeed, float deltaTime)
        {
            if (deltaTime <= 0f)
                return Vector3.zero;

            float radians = yaw * Mathf.Deg2Rad;
            float distance = Mathf.Clamp(throttle, -1f, 1f) * moveSpeed * deltaTime;
            return new Vector3(Mathf.Sin(radians) * distance, 0f, Mathf.Cos(radians) * distance);
        }

        public static bool TryGetAimYaw(Vector3 origin, Vector3 aimPoint, out float yaw)
        {
            yaw = 0f;
            if (!IsFinite(origin) || !IsFinite(aimPoint))
                return false;

            float x = aimPoint.x - origin.x;
            float z = aimPoint.z - origin.z;
            float distanceSquared = x * x + z * z;

            // A target within one centimetre has no useful horizontal direction.
            if (!IsFinite(distanceSquared) || distanceSquared <= 0.0001f)
            {
                yaw = 0f;
                return false;
            }

            yaw = Mathf.Repeat(Mathf.Atan2(x, z) * Mathf.Rad2Deg, 360f);
            return true;
        }
    }
}
