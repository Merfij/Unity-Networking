using System.Collections;
using NetworkTankArena.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NetworkTankArena.Tests
{
    public sealed class ExternalProjectileLifecycleTests
    {
        [UnityTest]
        public IEnumerator ExternalDriverOwnsDestructionAfterTerminalSimulation()
        {
            bool expectDomainReload = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            yield return new EnterPlayMode(expectDomainReload);

            var owner = new GameObject("External projectile lifecycle shooter");
            var root = new GameObject("External projectile lifecycle shot");
            try
            {
                var shot = root.AddComponent<Projectile>();
                shot.UseExternalDriver();
                Assert.That(FiringPose.TryCalculate(new Vector3(5000f, 100f, 5000f), 0f, 0f,
                    out var pose), Is.True);
                Assert.That(shot.Initialize(pose, owner, 20f, 0.1f, 0.1f, 25, 1), Is.True);
                yield return null; // Its LateUpdate must not step gameplay.
                Assert.That(shot.Age, Is.Zero);
                shot.Simulate(0.1f);
                Assert.That(shot.IsTerminal, Is.True);
                yield return null; // A plain local Destroy would have removed it by this point.
                Assert.That(root != null, Is.True);
                Assert.That(shot.Age, Is.EqualTo(0.1f));
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                Object.DestroyImmediate(owner);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
