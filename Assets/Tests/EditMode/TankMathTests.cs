using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class TankMathTests
    {
        private const float Tolerance = 0.0001f;

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NonPositiveTimeDoesNotMoveOrTurn(float deltaTime)
        {
            Assert.That(TankMath.TurnYaw(45f, 1f, 120f, deltaTime), Is.EqualTo(45f));
            Assert.That(TankMath.ForwardDisplacement(45f, 1f, 6f, deltaTime), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void NeutralInputDoesNotMoveOrTurn()
        {
            Assert.That(TankMath.TurnYaw(45f, 0f, 120f, 1f), Is.EqualTo(45f));
            Assert.That(TankMath.ForwardDisplacement(45f, 0f, 6f, 1f), Is.EqualTo(Vector3.zero));
        }

        [TestCase(0f, 0f, 3f)]
        [TestCase(90f, 3f, 0f)]
        [TestCase(180f, 0f, -3f)]
        [TestCase(270f, -3f, 0f)]
        public void ForwardMovementUsesChassisYawAndElapsedTime(float yaw, float x, float z)
        {
            Vector3 displacement = TankMath.ForwardDisplacement(yaw, 1f, 6f, 0.5f);
            Assert.That(displacement.x, Is.EqualTo(x).Within(Tolerance));
            Assert.That(displacement.y, Is.Zero);
            Assert.That(displacement.z, Is.EqualTo(z).Within(Tolerance));
        }

        [TestCase(-2f, -3f)]
        [TestCase(-0.5f, -1.5f)]
        [TestCase(2f, 3f)]
        public void ThrottleSupportsReverseAnalogueInputAndClamping(float throttle, float expectedZ)
        {
            Assert.That(TankMath.ForwardDisplacement(0f, throttle, 6f, 0.5f).z,
                Is.EqualTo(expectedZ).Within(Tolerance));
        }

        [TestCase(350f, 2f, 20f)]
        [TestCase(10f, -2f, 340f)]
        [TestCase(10f, 0.5f, 25f)]
        public void TurningClampsInputScalesTimeAndWrapsYaw(float yaw, float turn, float expectedYaw)
        {
            Assert.That(TankMath.TurnYaw(yaw, turn, 120f, 0.25f), Is.EqualTo(expectedYaw).Within(Tolerance));
        }

        [Test]
        public void TurningDoesNotReduceFullThrottleSpeed()
        {
            float yaw = TankMath.TurnYaw(0f, 1f, 120f, 0.25f);
            Vector3 displacement = TankMath.ForwardDisplacement(yaw, 1f, 6f, 0.25f);
            Assert.That(displacement.magnitude, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 90f)]
        [TestCase(0f, -1f, 180f)]
        [TestCase(-1f, 0f, 270f)]
        public void AimYawUsesWorldDirectionAndIgnoresHeight(float x, float z, float expectedYaw)
        {
            Vector3 origin = new Vector3(10f, 1.35f, 20f);
            Assert.That(TankMath.TryGetAimYaw(origin, origin + new Vector3(x, 50f, z), out float yaw), Is.True);
            Assert.That(yaw, Is.EqualTo(expectedYaw).Within(Tolerance));
        }

        [TestCase(0f)]
        [TestCase(0.005f)]
        public void NearCoincidentAimHasNoDirection(float separation)
        {
            Assert.That(TankMath.TryGetAimYaw(new Vector3(10f, 1.35f, 20f),
                new Vector3(10f + separation, 100f, 20f), out _), Is.False);
        }
    }
}
