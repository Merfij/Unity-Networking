using NetworkTankArena.Combat;
using NetworkTankArena.Networking;
using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class ServerFireRequestTests
    {
        private static readonly Vector3 Origin = new Vector3(10f, 1.35f, 20f);
        private static readonly Vector3 Aim = Origin + Vector3.right * 10f;

        [Test]
        public void OnlyOwnerAndIncreasingNonzeroRequestsAreAccepted()
        {
            var fire = new ServerFireRequest();
            Assert.That(fire.TryReceive(8, 7, 100, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(7, 7, 0, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(7, 7, 2, Aim, Origin, out float yaw), Is.True);
            Assert.That(yaw, Is.EqualTo(90f).Within(0.001f));
            Assert.That(fire.TryReceive(7, 7, 2, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(7, 7, 1, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(7, 7, 3, Aim, Origin, out _), Is.True);
        }

        [Test]
        public void FireAndMovementSequencesAreIndependent()
        {
            var movement = new ServerTankIntent();
            var fire = new ServerFireRequest();
            Assert.That(movement.TryReceive(1, 1, 900, default, 0d), Is.True);
            Assert.That(fire.TryReceive(1, 1, 1, Aim, Origin, out _), Is.True);
            Assert.That(movement.TryReceive(1, 1, 901, default, 0.1d), Is.True);
            Assert.That(fire.TryReceive(1, 1, 2, Aim, Origin, out _), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void EveryNonfiniteAimCoordinateIsRejectedAndCannotBeReplayed(float value)
        {
            var fire = new ServerFireRequest();
            Assert.That(fire.TryReceive(1, 1, 1, new Vector3(value, 0f, 30f), Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 2, new Vector3(20f, value, 30f), Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 3, new Vector3(20f, 0f, value), Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 3, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 4, Aim, Origin, out _), Is.True);
        }

        [Test]
        public void AimBoundIsHorizontalRadialAndInclusive()
        {
            var fire = new ServerFireRequest();
            Vector3 boundary = Origin + Vector3.right * 1000f;
            boundary.y = float.MaxValue; // Finite height is discarded, never used for pitch/range.
            Assert.That(fire.TryReceive(1, 1, 1, boundary, Origin, out float yaw), Is.True);
            Assert.That(yaw, Is.EqualTo(90f).Within(0.001f));
            Assert.That(fire.TryReceive(1, 1, 2, Origin + Vector3.right * 1000.01f, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 3, Origin + new Vector3(800f, 0f, 800f), Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 4, new Vector3(float.MaxValue, 0f, float.MaxValue), Origin, out _), Is.False);
        }

        [Test]
        public void DegenerateDirectionAndInvalidServerOriginAreRejected()
        {
            var fire = new ServerFireRequest();
            Assert.That(fire.TryReceive(1, 1, 1, Origin, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 2, Origin + Vector3.right * 0.005f, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 3, Aim, new Vector3(float.NaN, 0f, 0f), out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 4, Origin + Vector3.right * 0.02f, Origin, out _), Is.True);
        }

        [Test]
        public void CooldownRejectionCannotBecomeALaterShotWithTheSameSequence()
        {
            var fire = new ServerFireRequest();
            var cooldown = new WeaponCooldown();
            Assert.That(fire.TryReceive(1, 1, 1, Aim, Origin, out _), Is.True);
            Assert.That(cooldown.TryConsume(0d, 0.30d), Is.True);
            Assert.That(fire.TryReceive(1, 1, 2, Aim, Origin, out _), Is.True);
            Assert.That(cooldown.TryConsume(0.1d, 0.30d), Is.False);
            Assert.That(fire.TryReceive(1, 1, 2, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 3, Aim, Origin, out _), Is.True);
            Assert.That(cooldown.TryConsume(0.30d, 0.30d), Is.True);
        }

        [Test]
        public void SequenceDoesNotWrapAndExplicitLifecycleResetAllowsANewStream()
        {
            var fire = new ServerFireRequest();
            Assert.That(fire.TryReceive(1, 1, ulong.MaxValue, Aim, Origin, out _), Is.True);
            Assert.That(fire.TryReceive(1, 1, ulong.MaxValue, Aim, Origin, out _), Is.False);
            Assert.That(fire.TryReceive(1, 1, 1, Aim, Origin, out _), Is.False);
            fire.Reset();
            Assert.That(fire.TryReceive(2, 2, 1, Aim, Origin, out _), Is.True);
        }
    }
}
