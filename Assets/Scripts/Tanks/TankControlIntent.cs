using UnityEngine;

namespace NetworkTankArena.Tanks
{
    /// <summary>Device-independent requests for one gameplay simulation step.</summary>
    public readonly struct TankControlIntent
    {
        public float Throttle { get; }
        public float Turn { get; }
        public Vector3 AimWorldPoint { get; }
        public bool HasAimPoint { get; }
        public bool FireRequested { get; }

        public TankControlIntent(float throttle, float turn, Vector3 aimWorldPoint, bool hasAimPoint,
            bool fireRequested = false)
        {
            Throttle = throttle;
            Turn = turn;
            AimWorldPoint = aimWorldPoint;
            HasAimPoint = hasAimPoint;
            FireRequested = fireRequested;
        }

        /// <summary>Clamp axes and discard non-finite aim at the gameplay boundary.</summary>
        public TankControlIntent Sanitize() => new TankControlIntent(
            TankMath.IsFinite(Throttle) ? Mathf.Clamp(Throttle, -1f, 1f) : 0f,
            TankMath.IsFinite(Turn) ? Mathf.Clamp(Turn, -1f, 1f) : 0f,
            TankMath.IsFinite(AimWorldPoint) ? AimWorldPoint : Vector3.zero,
            HasAimPoint && TankMath.IsFinite(AimWorldPoint), FireRequested);
    }
}
