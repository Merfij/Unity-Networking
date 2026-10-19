using System.Collections;
using NetworkTankArena.Combat;
using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace NetworkTankArena.Tests
{
    /// <summary>One focused test enters Play Mode for real weapon/root lifecycle callbacks.
    /// It does not exercise controller input, focus restoration, or projectile destruction.</summary>
    public sealed class TankWeaponLifecycleTests
    {
        [UnityTest]
        public IEnumerator CooldownSurvivesEnableTransitionsAndOnlyExplicitResetClearsIt()
        {
            bool expectDomainReload = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            yield return new EnterPlayMode(expectDomainReload);

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            var root = new GameObject("Weapon lifecycle test tank");
            var template = new GameObject("Weapon lifecycle test projectile template");
            var obstruction = new GameObject("Weapon lifecycle test launch obstruction");
            try
            {
                root.transform.position = new Vector3(2000f, 100f, 2000f);
                root.AddComponent<CharacterController>();
                TankSimulation tank = root.AddComponent<TankSimulation>();
                TankWeapon weapon = root.AddComponent<TankWeapon>();
                Projectile projectile = template.AddComponent<Projectile>();
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("tank").objectReferenceValue = tank;
                serialized.FindProperty("projectilePrefab").objectReferenceValue = projectile;
                double duration = serialized.FindProperty("cooldownSeconds").floatValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                obstruction.transform.position = tank.TurretOrigin + Vector3.forward * 0.65f;
                obstruction.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.2f);
                DamageTestTarget target = obstruction.AddComponent<DamageTestTarget>();
                // A blocked accepted shot exercises cooldown without leaving projectile clones behind.
                const double firstShotTime = 10d;
                double boundary = firstShotTime + duration;
                double beforeBoundary = boundary - 0.000001d;
                Assert.That(weapon.TryFire(firstShotTime), Is.True);
                Assert.That(target.HitCount, Is.EqualTo(1));
                Assert.That(weapon.TryFire(firstShotTime), Is.False);

                weapon.enabled = false;
                Assert.That(weapon.TryFire(beforeBoundary), Is.False);
                weapon.enabled = true;
                Assert.That(weapon.TryFire(beforeBoundary), Is.False);

                root.SetActive(false);
                Assert.That(weapon.TryFire(beforeBoundary), Is.False);
                root.SetActive(true);
                Assert.That(weapon.TryFire(beforeBoundary), Is.False);
                Assert.That(target.HitCount, Is.EqualTo(1));

                Assert.That(weapon.TryFire(boundary), Is.True);
                Assert.That(target.HitCount, Is.EqualTo(2));
                Assert.That(weapon.TryFire(boundary), Is.False);
                weapon.ResetCooldown();
                Assert.That(weapon.TryFire(boundary), Is.True);
                Assert.That(target.HitCount, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(obstruction);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(root);
                Time.timeScale = previousTimeScale;
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
