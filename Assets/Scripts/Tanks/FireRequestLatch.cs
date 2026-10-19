namespace NetworkTankArena.Tanks
{
    /// <summary>Requires an observed release after interrupted local control. Never retains a press.</summary>
    internal sealed class FireRequestLatch
    {
        private bool waitingForRelease = true;

        internal void RequireRelease() => waitingForRelease = true;

        internal bool TryRequest(bool isHeld, bool pressedThisFrame, bool canRequest)
        {
            if (waitingForRelease)
            {
                if (!isHeld)
                    waitingForRelease = false;
                return false;
            }

            return canRequest && pressedThisFrame;
        }
    }
}
