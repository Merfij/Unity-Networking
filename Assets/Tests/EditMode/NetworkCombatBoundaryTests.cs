using NetworkTankArena.Combat;
using NetworkTankArena.Networking;
using NUnit.Framework;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    public sealed class NetworkCombatBoundaryTests
    {
        [Test]
        public void UnspawnedNetworkTargetCannotAcceptDamage()
        {
            var root = new GameObject("Unspawned network damage target");
            try
            {
                var target = root.AddComponent<NetworkDamageTestTarget>();
                target.ApplyDamage(25, null);
                Assert.That(target.HitCount, Is.Zero);
                Assert.That(target.AccumulatedDamage, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LocalProjectileIsNotANetworkPrefab()
        {
            var root = new GameObject("Local projectile boundary");
            try
            {
                var projectile = root.AddComponent<Projectile>();
                Assert.That(NetworkProjectile.IsValidPrefab(projectile, null), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
