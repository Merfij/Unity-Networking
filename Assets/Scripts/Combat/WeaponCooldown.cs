namespace NetworkTankArena.Combat
{
    /// <summary>Consumes discrete requests against an explicitly supplied simulation clock.</summary>
    public sealed class WeaponCooldown
    {
        private double nextAllowedTime = double.NegativeInfinity;

        public bool TryConsume(double now, double cooldownSeconds)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(cooldownSeconds) ||
                double.IsInfinity(cooldownSeconds) || cooldownSeconds <= 0d || now < nextAllowedTime)
                return false;

            double next = now + cooldownSeconds;
            if (double.IsInfinity(next) || next <= now)
                return false;

            nextAllowedTime = next;
            return true;
        }
    }
}
