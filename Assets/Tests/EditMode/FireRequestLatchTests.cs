using NetworkTankArena.Tanks;
using NUnit.Framework;

namespace NetworkTankArena.Tests
{
    /// <summary>Pure latch transitions; does not simulate focus events or Input System lifecycle.</summary>
    public sealed class FireRequestLatchTests
    {
        [Test]
        public void InitiallyHeldButtonNeedsReleaseBeforeItsNextPress()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(true, true, true), Is.False);
            Assert.That(latch.TryRequest(true, false, true), Is.False);
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
        }

        [Test]
        public void InterruptionRequiresAnotherReleaseEvenIfTheActionReportsANewPress()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);

            latch.RequireRelease();
            Assert.That(latch.TryRequest(true, true, true), Is.False);
            Assert.That(latch.TryRequest(true, false, true), Is.False);
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
        }

        [Test]
        public void HoldingWithoutAnotherPressDoesNotRepeat()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
            Assert.That(latch.TryRequest(true, false, true), Is.False);
            Assert.That(latch.TryRequest(true, false, true), Is.False);
        }

        [Test]
        public void SuppressedPressIsDiscardedRatherThanQueued()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, false), Is.False);
            Assert.That(latch.TryRequest(true, false, true), Is.False);
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
        }

        [Test]
        public void ReleaseCanArmTheLatchWhileRequestsAreSuppressed()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(true, true, false), Is.False);
            Assert.That(latch.TryRequest(false, false, false), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
        }

        [Test]
        public void ReleaseSampleCannotAlsoProduceALatchedRequest()
        {
            var latch = new FireRequestLatch();
            Assert.That(latch.TryRequest(false, true, true), Is.False);
            Assert.That(latch.TryRequest(false, false, true), Is.False);
            Assert.That(latch.TryRequest(true, true, true), Is.True);
        }
    }
}
