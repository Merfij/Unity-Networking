using NetworkTankArena.Tanks;

namespace NetworkTankArena.Networking
{
    /// <summary>Latest owner input, ordered and leased against the server's clock. No queued commands.</summary>
    internal sealed class ServerTankIntent
    {
        private TankControlIntent latest;
        private ulong lastSequence;
        private double receivedAt;
        private bool hasInput;

        internal bool TryReceive(ulong sender, ulong owner, ulong sequence, TankControlIntent intent, double now)
        {
            if (sender != owner || sequence == 0 || (hasInput && sequence <= lastSequence) || !IsFinite(now))
                return false;

            TankControlIntent safe = intent.Sanitize();
            // Discrete fire requests must never be retained/repeated as movement input.
            latest = new TankControlIntent(safe.Throttle, safe.Turn, safe.AimWorldPoint, safe.HasAimPoint);
            lastSequence = sequence;
            receivedAt = now;
            hasInput = true;
            return true;
        }

        internal TankControlIntent Read(double now, double timeoutSeconds) =>
            hasInput && IsFinite(now) && IsFinite(timeoutSeconds) && timeoutSeconds > 0d &&
            now >= receivedAt && now - receivedAt < timeoutSeconds ? latest : default;

        internal void Reset()
        {
            latest = default;
            lastSequence = 0;
            receivedAt = 0d;
            hasInput = false;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
