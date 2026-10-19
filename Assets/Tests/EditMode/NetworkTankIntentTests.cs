using NetworkTankArena.Networking;
using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class NetworkTankIntentTests
    {
        [Test]
        public void FiniteAxesClampAndAnalogueInputIsPreserved()
        {
            TankControlIntent safe = new TankControlIntent(2f, -3f, Vector3.one, true).Sanitize();
            Assert.That(safe.Throttle, Is.EqualTo(1f));
            Assert.That(safe.Turn, Is.EqualTo(-1f));
            Assert.That(safe.AimWorldPoint, Is.EqualTo(Vector3.one));
            Assert.That(safe.HasAimPoint, Is.True);
            Assert.That(new TankControlIntent(0.4f, -0.7f, default, false).Sanitize().Turn, Is.EqualTo(-0.7f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteAxesBecomeNeutralAndAnyNonFiniteAimCoordinateIsDiscarded(float value)
        {
            TankControlIntent safe = new TankControlIntent(value, value, new Vector3(value, 0f, 1f), true).Sanitize();
            Assert.That(safe.Throttle, Is.Zero);
            Assert.That(safe.Turn, Is.Zero);
            Assert.That(safe.HasAimPoint, Is.False);
            Assert.That(safe.AimWorldPoint, Is.EqualTo(Vector3.zero));
            Assert.That(new TankControlIntent(0f, 0f, new Vector3(0f, value, 0f), true).Sanitize().HasAimPoint, Is.False);
            Assert.That(new TankControlIntent(0f, 0f, new Vector3(0f, 0f, value), true).Sanitize().HasAimPoint, Is.False);
        }

        [Test]
        public void UnclaimedAimPointDoesNotBecomeActive()
        {
            Assert.That(new TankControlIntent(0f, 0f, Vector3.one, false).Sanitize().HasAimPoint, Is.False);
        }

        [Test]
        public void ServerAcceptsOnlyOwnerAndIncreasingNonzeroSequence()
        {
            var input = new ServerTankIntent();
            var forward = new TankControlIntent(1f, 0f, Vector3.right, true);
            Assert.That(input.TryReceive(8, 7, 1, forward, 10d), Is.False);
            Assert.That(input.TryReceive(7, 7, 0, forward, 10d), Is.False);
            Assert.That(input.TryReceive(7, 7, 2, forward, 10d), Is.True);
            Assert.That(input.TryReceive(7, 7, 1, default, 10.1d), Is.False);
            Assert.That(input.TryReceive(7, 7, 2, default, 10.1d), Is.False);
            Assert.That(input.Read(10.1d, 0.5d).Throttle, Is.EqualTo(1f));
            Assert.That(input.TryReceive(7, 7, 3, default, 10.2d), Is.True);
            Assert.That(input.Read(10.2d, 0.5d).Throttle, Is.Zero);
        }

        [Test]
        public void ServerSanitizesAtReceiveAndNeverRetainsFireRequests()
        {
            var input = new ServerTankIntent();
            Assert.That(input.TryReceive(1, 1, 1,
                new TankControlIntent(99f, float.NaN, new Vector3(float.NaN, 0f, 0f), true, true), 10d), Is.True);
            TankControlIntent safe = input.Read(10d, 0.5d);
            Assert.That(safe.Throttle, Is.EqualTo(1f));
            Assert.That(safe.Turn, Is.Zero);
            Assert.That(safe.HasAimPoint, Is.False);
            Assert.That(safe.FireRequested, Is.False);
        }

        [Test]
        public void StaleInputExpiresAtBoundaryAndRejectedPacketsDoNotExtendIt()
        {
            var input = new ServerTankIntent();
            input.TryReceive(1, 1, 1, new TankControlIntent(1f, 1f, Vector3.right, true), 10d);
            Assert.That(input.Read(10.499d, 0.5d).Throttle, Is.EqualTo(1f));
            Assert.That(input.TryReceive(2, 1, 2, default, 10.4d), Is.False);
            Assert.That(input.TryReceive(1, 1, 1, default, 10.4d), Is.False);
            TankControlIntent expired = input.Read(10.5d, 0.5d);
            Assert.That(expired.Throttle, Is.Zero);
            Assert.That(expired.Turn, Is.Zero);
            Assert.That(expired.HasAimPoint, Is.False);
        }

        [Test]
        public void ResetDropsInputAndAcceptsAFreshOwnerSequence()
        {
            var input = new ServerTankIntent();
            input.TryReceive(1, 1, 99, new TankControlIntent(1f, 1f, Vector3.one, true), 10d);
            input.Reset();
            Assert.That(input.Read(10d, 0.5d).HasAimPoint, Is.False);
            Assert.That(input.Read(10d, 0.5d).Throttle, Is.Zero);
            Assert.That(input.TryReceive(2, 2, 1, new TankControlIntent(-1f, 0f, default, false), 10d), Is.True);
            Assert.That(input.Read(10d, 0.5d).Throttle, Is.EqualTo(-1f));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonFiniteServerClockCannotAcceptOrReviveInput(double now)
        {
            var input = new ServerTankIntent();
            Assert.That(input.TryReceive(1, 1, 1, default, now), Is.False);
            Assert.That(input.TryReceive(1, 1, 1, new TankControlIntent(1f, 0f, default, false), 10d), Is.True);
            Assert.That(input.Read(now, 0.5d).Throttle, Is.Zero);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        public void InvalidLeaseOrClockBeforeReceiptReturnsNeutral(double timeout)
        {
            var input = new ServerTankIntent();
            input.TryReceive(1, 1, 1, new TankControlIntent(1f, 0f, default, false), 10d);
            Assert.That(input.Read(10d, timeout).Throttle, Is.Zero);
            Assert.That(input.Read(9d, 0.5d).Throttle, Is.Zero);
        }
    }
}
