using NetworkTankArena.Combat;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class FiringPoseTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(0f, 0f, 1f)]
        [TestCase(90f, 1f, 0f)]
        [TestCase(180f, 0f, -1f)]
        [TestCase(270f, -1f, 0f)]
        [TestCase(45f, 0.70710678f, 0.70710678f)]
        [TestCase(30f, 0.5f, 0.8660254f)]
        public void CanonicalWorldYawProducesNormalizedHorizontalDirection(float yaw, float x, float z)
        {
            Assert.That(FiringPose.TryCalculate(new Vector3(10f, 1.35f, 20f), yaw, 1.3f, out var pose), Is.True);
            Assert.That(pose.Forward.x, Is.EqualTo(x).Within(Tolerance));
            Assert.That(pose.Forward.y, Is.Zero.Within(Tolerance));
            Assert.That(pose.Forward.z, Is.EqualTo(z).Within(Tolerance));
            Assert.That(pose.Forward.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void RootTranslationAndFixedTurretHeightTranslateBothOrigins()
        {
            Vector3 root = new Vector3(10f, 2f, 20f);
            Vector3 translation = new Vector3(-3f, 4f, 7f);
            Assert.That(FiringPose.TryCalculate(root + Vector3.up * 1.35f, 90f, 1.3f, out var first), Is.True);
            Assert.That(FiringPose.TryCalculate(root + translation + Vector3.up * 1.35f, 90f, 1.3f,
                out var translated), Is.True);
            Assert.That(first.TurretOrigin, Is.EqualTo(new Vector3(10f, 3.35f, 20f)));
            Assert.That(first.Origin.x, Is.EqualTo(11.3f).Within(Tolerance));
            Assert.That(first.Origin.y, Is.EqualTo(3.35f).Within(Tolerance));
            Assert.That(Vector3.Distance(translated.TurretOrigin, first.TurretOrigin + translation),
                Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(translated.Origin, first.Origin + translation), Is.LessThan(Tolerance));
            Assert.That(translated.Forward, Is.EqualTo(first.Forward));
        }

        [TestCase(0f)]
        [TestCase(1.3f)]
        [TestCase(3f)]
        public void MuzzleOffsetIsAWorldDistanceAlongCanonicalForward(float offset)
        {
            Assert.That(FiringPose.TryCalculate(new Vector3(3f, 1.35f, 4f), 123f, offset, out var pose), Is.True);
            Assert.That(Vector3.Distance(pose.Origin, pose.TurretOrigin), Is.EqualTo(offset).Within(Tolerance));
            Assert.That(Vector3.Distance(pose.Origin - pose.TurretOrigin, pose.Forward * offset),
                Is.LessThan(Tolerance));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteCanonicalStateIsRejected(float value)
        {
            Assert.That(FiringPose.TryCalculate(Vector3.zero, value, 1.3f, out _), Is.False);
            Assert.That(FiringPose.TryCalculate(new Vector3(value, 0f, 0f), 0f, 1.3f, out _), Is.False);
            Assert.That(FiringPose.TryCalculate(Vector3.zero, 0f, value, out _), Is.False);
        }
    }
}
