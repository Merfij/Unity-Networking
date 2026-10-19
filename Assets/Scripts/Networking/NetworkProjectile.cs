using NetworkTankArena.Combat;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace NetworkTankArena.Networking
{
    /// <summary>Server-owned lifecycle around the existing swept projectile simulation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Projectile))]
    public sealed class NetworkProjectile : NetworkBehaviour
    {
        private Projectile projectile;
        private NetworkTankController shooter;

        private void Awake()
        {
            projectile = GetComponent<Projectile>();
            projectile.UseExternalDriver();
        }

        internal static bool IsValidPrefab(Projectile prefab, NetworkManager manager) =>
            prefab != null && prefab.enabled && prefab.gameObject.activeSelf && !prefab.HasPhysicsShape &&
            prefab.TryGetComponent(out NetworkProjectile adapter) && adapter.enabled &&
            prefab.TryGetComponent(out NetworkObject _) &&
            prefab.TryGetComponent(out NetworkTransform replication) && replication.enabled &&
            replication.AuthorityMode == NetworkTransform.AuthorityModes.Server &&
            manager != null && manager.NetworkConfig.Prefabs.Contains(prefab.gameObject);

        // Called after weapon initialization and before Spawn; clients never receive gameplay tuning.
        internal bool SpawnOnServer(NetworkTankController firingTank)
        {
            if (firingTank == null || !firingTank.IsServer || !firingTank.IsSpawned ||
                !projectile.IsInitialized || IsSpawned)
                return false;

            shooter = firingTank;
            try
            {
                NetworkObject.Spawn(); // Server owns the projectile; shooter is attribution, not ownership.
            }
            catch
            {
                if (IsSpawned)
                    DespawnFromServer();
                else
                    Destroy(gameObject);
                throw;
            }
            return IsSpawned;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (shooter == null || !shooter.IsSpawned || !projectile.IsInitialized)
                {
                    Debug.LogError("NetworkProjectile must be initialized by its server weapon before Spawn.", this);
                    DespawnFromServer();
                    return;
                }
                shooter.RegisterProjectile(this);
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
                return;

            if (shooter == null || !shooter.IsSpawned)
            {
                DespawnFromServer();
                return;
            }

            try
            {
                projectile.Simulate(Time.fixedDeltaTime);
            }
            finally
            {
                // Damage callbacks cannot leave a terminal network projectile alive.
                if (projectile.IsTerminal)
                    DespawnFromServer();
            }
        }

        internal void DespawnFromServer()
        {
            if (IsServer && IsSpawned)
                NetworkObject.Despawn(true);
        }

        public override void OnNetworkDespawn()
        {
            if (shooter != null)
                shooter.UnregisterProjectile(this);
            shooter = null;
        }
    }
}
