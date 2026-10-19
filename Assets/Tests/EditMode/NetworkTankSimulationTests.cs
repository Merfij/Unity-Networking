using System.Reflection;
using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class NetworkTankSimulationTests
    {
        private GameObject root;
        private TankSimulation tank;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Canonical network tank test");
            root.transform.position = new Vector3(3000f, 100f, 3000f);
            tank = root.AddComponent<TankSimulation>();
            // EditMode does not invoke this MonoBehaviour's Play Mode initialization callback.
            typeof(TankSimulation).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(tank, null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidInputCannotCorruptCanonicalState(float value)
        {
            Vector3 position = root.transform.position;
            tank.Simulate(new TankControlIntent(value, value, new Vector3(value, 0f, 0f), true), 0.1f);
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.eulerAngles.y, Is.Zero);
            Assert.That(tank.TurretYaw, Is.Zero);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        [TestCase(0f)]
        public void InvalidStepTimeCannotMoveOrAim(float deltaTime)
        {
            Vector3 position = root.transform.position;
            tank.Simulate(new TankControlIntent(1f, 1f, tank.TurretOrigin + Vector3.right, true), deltaTime);
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.eulerAngles.y, Is.Zero);
            Assert.That(tank.TurretYaw, Is.Zero);
        }

        [Test]
        public void ValidServerStepUsesExistingMovementAndWorldAim()
        {
            Vector3 position = root.transform.position;
            tank.Simulate(new TankControlIntent(2f, 0f, tank.TurretOrigin + Vector3.right * 100f, true), 0.1f);
            Assert.That(root.transform.position.z, Is.EqualTo(position.z + 0.6f).Within(0.001f));
            Assert.That(tank.TurretYaw, Is.EqualTo(90f).Within(0.5f));
        }

        [Test]
        public void DisabledSimulationOrCollisionControllerCannotMoveOrAim()
        {
            Vector3 position = root.transform.position;
            var intent = new TankControlIntent(1f, 1f, tank.TurretOrigin + Vector3.right, true);
            tank.enabled = false;
            tank.Simulate(intent, 0.1f);
            tank.enabled = true;
            root.GetComponent<CharacterController>().enabled = false;
            tank.Simulate(intent, 0.1f);
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.eulerAngles.y, Is.Zero);
            Assert.That(tank.TurretYaw, Is.Zero);
        }

        [Test]
        public void ReplicatedWorldYawWrapsAndDoesNotChangeRootPose()
        {
            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            tank.ApplyReplicatedTurretYaw(-90f);
            Assert.That(tank.TurretYaw, Is.EqualTo(270f));
            tank.ApplyReplicatedTurretYaw(float.NaN);
            tank.ApplyReplicatedTurretYaw(float.PositiveInfinity);
            Assert.That(tank.TurretYaw, Is.EqualTo(270f));
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void ExtremeFiniteAimCannotOverflowIntoCanonicalYaw()
        {
            Assert.That(TankMath.TryGetAimYaw(Vector3.zero, new Vector3(float.MaxValue, 0f, float.MaxValue), out _), Is.False);
            tank.ApplyReplicatedTurretYaw(45f);
            tank.Simulate(new TankControlIntent(0f, 0f, new Vector3(float.MaxValue, 0f, float.MaxValue), true), 0.1f);
            Assert.That(tank.TurretYaw, Is.EqualTo(45f));
        }
    }
}
