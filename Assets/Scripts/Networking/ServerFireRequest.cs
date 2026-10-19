using NetworkTankArena.Tanks;
using UnityEngine;

namespace NetworkTankArena.Networking
{
    /// <summary>Discrete fire ordering and aim validation. No retained command or movement sequence.</summary>
    internal sealed class ServerFireRequest
    {
        internal const float MaximumAimDistance = 1000f;
        private ulong lastSequence;

        internal bool TryReceive(ulong sender, ulong owner, ulong sequence, Vector3 aimPoint,
            Vector3 turretOrigin, out float shotYaw)
        {
            shotYaw = 0f;
            if (sender != owner || sequence == 0 || sequence <= lastSequence)
                return false;

            // An authenticated newer request stays observed even if its payload or cooldown fails.
            lastSequence = sequence;
            if (!TankMath.IsFinite(aimPoint) || !TankMath.IsFinite(turretOrigin))
                return false;

            // Double arithmetic prevents extreme finite coordinates overflowing during validation.
            double x = (double)aimPoint.x - turretOrigin.x;
            double z = (double)aimPoint.z - turretOrigin.z;
            if (x * x + z * z > (double)MaximumAimDistance * MaximumAimDistance)
                return false;

            aimPoint.y = turretOrigin.y; // Firing is horizontal; client height cannot pitch the shot.
            return TankMath.TryGetAimYaw(turretOrigin, aimPoint, out shotYaw);
        }

        internal void Reset() => lastSequence = 0;
    }
}
