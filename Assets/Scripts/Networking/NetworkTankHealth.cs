using NetworkTankArena.Combat;
using Unity.Netcode;
using UnityEngine;

namespace NetworkTankArena.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTankHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private int maxHealth = 100;

        private NetworkVariable<int> currentHealth = new NetworkVariable<int>(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
            );

        private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int CurrentHealth => currentHealth.Value;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead.Value;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                currentHealth.Value = maxHealth;
                isDead.Value = false;
            }
        }

        public void ApplyDamage(int amount, GameObject source)
        {
            if (!IsServer || !IsSpawned || amount <= 0)
                return;

            if (currentHealth.Value <= 0)
                return;

            currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);

            Debug.Log(
                $"{gameObject.name} took {amount} damage. " +
                $"Health: {currentHealth.Value}/{maxHealth}",
                this);

            if (currentHealth.Value <= 0)
            {
                isDead.Value = true;
                Debug.Log($"{gameObject.name} has died.", this);
            }
        }
    }
}
