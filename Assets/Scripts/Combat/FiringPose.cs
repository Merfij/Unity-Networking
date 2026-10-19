using UnityEngine;

namespace NetworkTankArena.Combat
{
    /// <summary>A world-space gameplay snapshot. No presentation transform participates.</summary>
    public readonly struct FiringPose
    {
        public Vector3 TurretOrigin { get; }
        public Vector3 Origin { get; }
        public Vector3 Forward { get; }

        private FiringPose(Vector3 turretOrigin, Vector3 origin, Vector3 forward)
        {
            TurretOrigin = turretOrigin;
            Origin = origin;
            Forward = forward;
        }

        internal bool IsValid => CombatValidation.IsFinite(TurretOrigin) && CombatValidation.IsFinite(Origin) &&
            CombatValidation.IsFinite(Forward) && Mathf.Abs(Forward.sqrMagnitude - 1f) < 0.0001f &&
            Mathf.Abs(Forward.y) < 0.0001f;

        public static bool TryCalculate(Vector3 turretOrigin, float turretYaw, float muzzleForwardOffset,
            out FiringPose pose)
        {
            pose = default;
            if (!CombatValidation.IsFinite(turretOrigin) || !CombatValidation.IsFinite(turretYaw) ||
                !CombatValidation.IsFinite(muzzleForwardOffset) || muzzleForwardOffset < 0f)
                return false;

            Vector3 forward = (Quaternion.Euler(0f, Mathf.Repeat(turretYaw, 360f), 0f) * Vector3.forward).normalized;
            Vector3 origin = turretOrigin + forward * muzzleForwardOffset;
            if (!CombatValidation.IsFinite(origin))
                return false;

            pose = new FiringPose(turretOrigin, origin, forward);
            return pose.IsValid;
        }
    }
}
