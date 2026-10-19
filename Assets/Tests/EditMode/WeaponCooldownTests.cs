using NetworkTankArena.Combat;
using NUnit.Framework;

namespace NetworkTankArena.Tests
{
    public sealed class WeaponCooldownTests
    {
        [Test]
        public void FirstShotIsImmediatelyAvailable()
        {
            Assert.That(new WeaponCooldown().TryConsume(0d, 0.30d), Is.True);
        }

        [Test]
        public void JustBeforeCooldownBoundaryIsRejected()
        {
            var cooldown = new WeaponCooldown();
            Assert.That(cooldown.TryConsume(0d, 0.30d), Is.True);
            Assert.That(cooldown.TryConsume(0.299999d, 0.30d), Is.False);
        }

        [Test]
        public void ExactCooldownBoundaryIsAccepted()
        {
            var cooldown = new WeaponCooldown();
            // Use the serialized float's actual duration when testing the weapon's tuning representation.
            double duration = 0.30f;
            Assert.That(cooldown.TryConsume(10d, duration), Is.True);
            Assert.That(cooldown.TryConsume(10d + duration, duration), Is.True);
        }

        [Test]
        public void RejectedRequestsDoNotExtendCooldown()
        {
            var cooldown = new WeaponCooldown();
            Assert.That(cooldown.TryConsume(0d, 0.30d), Is.True);
            Assert.That(cooldown.TryConsume(0.10d, 0.30d), Is.False);
            Assert.That(cooldown.TryConsume(0.29d, 0.30d), Is.False);
            Assert.That(cooldown.TryConsume(0.30d, 0.30d), Is.True);
        }

        [Test]
        public void RepeatedRequestsAtOneTimestampDoNotProduceAnotherShot()
        {
            var cooldown = new WeaponCooldown();
            Assert.That(cooldown.TryConsume(10d, 0.30d), Is.True);
            Assert.That(cooldown.TryConsume(10d, 0.30d), Is.False);
            Assert.That(cooldown.TryConsume(10d, 0.30d), Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidClockDoesNotConsumeFirstShot(double now)
        {
            var cooldown = new WeaponCooldown();
            Assert.That(cooldown.TryConsume(now, 0.30d), Is.False);
            Assert.That(cooldown.TryConsume(0d, 0.30d), Is.True);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidDurationDoesNotConsumeFirstShot(double duration)
        {
            var cooldown = new WeaponCooldown();
            Assert.That(cooldown.TryConsume(0d, duration), Is.False);
            Assert.That(cooldown.TryConsume(0d, 0.30d), Is.True);
        }
    }
}
