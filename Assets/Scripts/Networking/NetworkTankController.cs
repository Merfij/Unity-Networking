using NetworkTankArena.Combat;
using NetworkTankArena.Tanks;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetworkTankArena.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(TankSimulation))]
    public sealed class NetworkTankController : NetworkBehaviour
    {
        [SerializeField, Range(1f, 60f)] private float inputSendRate = 20f;
        [SerializeField, Min(0.1f)] private float inputTimeoutSeconds = 0.5f;

        private readonly NetworkVariable<float> turretYaw = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly ServerTankIntent serverIntent = new ServerTankIntent();
        private readonly ServerFireRequest serverFire = new ServerFireRequest();
        private readonly HashSet<NetworkProjectile> projectiles = new HashSet<NetworkProjectile>();
        private TankSimulation tank;
        private TankWeapon weapon;
        private LocalNetworkSession localSession;
        private TankControlIntent localIntent;
        private ulong sequence;
        private ulong fireSequence;
        private double nextSendTime;

        private NetworkTankHealth health;
        public bool IsDead => health != null && health.IsDead;

        public bool CanReadLocalInput => IsSpawned && IsClient && IsOwner && isActiveAndEnabled;

        private void Awake()
        {
            tank = GetComponent<TankSimulation>();
            weapon = GetComponent<TankWeapon>();
            // No weapon gameplay before spawn, or on a client copy.
            if (weapon != null)
                weapon.enabled = false;
            health = GetComponent<NetworkTankHealth>();
        }

        public override void OnNetworkSpawn()
        {
            ResetIntent();
            ResetFireStream();
            if (IsServer)
            {
                // NGO instantiates the active prefab before applying the approval pose. Flush that
                // pose to the CharacterController before the first Move can restore its cached origin.
                Physics.SyncTransforms();
                turretYaw.Value = transform.eulerAngles.y;
                if (weapon != null)
                {
                    weapon.ResetCooldown();
                    weapon.enabled = isActiveAndEnabled;
                    if (!NetworkProjectile.IsValidPrefab(weapon.ProjectilePrefab, NetworkManager))
                    {
                        weapon.enabled = false;
                        Debug.LogError("Assign a registered server-authoritative NetworkProjectile prefab to the network tank weapon. See docs/NETWORK_COMBAT.md.", this);
                    }
                }
            }
            tank.ApplyReplicatedTurretYaw(turretYaw.Value);
            turretYaw.OnValueChanged += OnTurretYawChanged;
            BindLocalOwner();
        }

        public override void OnGainedOwnership() => BindLocalOwner();

        public override void OnLostOwnership()
        {
            ReleaseLocalOwner();
            ResetIntent();
            ResetFireStream();
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            // Includes the server's remote-player copy, which never gains local ownership.
            ResetIntent();
            ResetFireStream();
        }

        public override void OnNetworkDespawn()
        {
            // Projectiles are server-owned, so NGO owner-disconnect cleanup cannot do this for us.
            foreach (NetworkProjectile projectile in new List<NetworkProjectile>(projectiles))
                if (projectile != null)
                    projectile.DespawnFromServer();
            projectiles.Clear();
            if (weapon != null)
                weapon.enabled = false;
            turretYaw.OnValueChanged -= OnTurretYawChanged;
            ReleaseLocalOwner();
            ResetIntent();
            ResetFireStream();
        }

        private void OnDisable()
        {
            ReleaseLocalOwner();
            localIntent = default;
            serverIntent.Reset();
            if (weapon != null)
                weapon.enabled = false;
            // Keep the sender sequence across component re-enables on the same spawned object.
            // Fire receiver ordering and cooldown also survive disable/re-enable.
        }

        private void OnEnable()
        {
            if (IsSpawned)
            {
                if (weapon != null)
                    weapon.enabled = IsServer && NetworkProjectile.IsValidPrefab(weapon.ProjectilePrefab, NetworkManager);
                BindLocalOwner();
            }
        }

        public void SubmitLocalIntent(TankControlIntent intent)
        {
            if (CanReadLocalInput)
                localIntent = intent.Sanitize();
        }

        public void SubmitLocalFire(Vector3 desiredAimWorldPoint)
        {
            if (CanReadLocalInput && fireSequence < ulong.MaxValue)
                RequestFireRpc(++fireSequence, desiredAimWorldPoint);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestFireRpc(ulong requestSequence, Vector3 desiredAimWorldPoint,
            RpcParams rpcParams = default)
        {
            if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId || !NetworkManager.ConnectedClients.TryGetValue(sender, out var client) ||
                client.PlayerObject != NetworkObject)
                return;

            if (health == null || health.IsDead || !serverFire.TryReceive(sender, OwnerClientId, requestSequence, desiredAimWorldPoint,
                tank.TurretOrigin, out float shotYaw) || !isActiveAndEnabled ||
                weapon == null || !weapon.isActiveAndEnabled ||
                !NetworkProjectile.IsValidPrefab(weapon.ProjectilePrefab, NetworkManager))
                return;

            // Execute immediately against current server state. Shot yaw never changes turretYaw or
            // TankSimulation.TurretYaw: continuous aim belongs exclusively to the movement stream.
            if (weapon.TryFire(Time.timeAsDouble, shotYaw, out Projectile projectile) && projectile != null)
            {
                if (!projectile.GetComponent<NetworkProjectile>().SpawnOnServer(this))
                    Destroy(projectile.gameObject); // Failed before network spawn; never leave local gameplay.
            }
        }

        internal void RegisterProjectile(NetworkProjectile projectile) => projectiles.Add(projectile);
        internal void UnregisterProjectile(NetworkProjectile projectile) => projectiles.Remove(projectile);

        private void Update()
        {
            if (!CanReadLocalInput || Time.unscaledTimeAsDouble < nextSendTime)
                return;

            float rate = TankMath.IsFinite(inputSendRate) ? Mathf.Clamp(inputSendRate, 1f, 60f) : 20f;
            nextSendTime = Time.unscaledTimeAsDouble + 1d / rate;
            // Host follows the same RPC/validation path as a remote owner. No catch-up bursts.
            SubmitIntentRpc(localIntent.Throttle, localIntent.Turn, localIntent.AimWorldPoint,
                localIntent.HasAimPoint, ++sequence);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitIntentRpc(float throttle, float turn, Vector3 aimPoint, bool hasAimPoint,
            ulong inputSequence, RpcParams rpcParams = default)
        {
            if (!IsSpawned || !IsServer || !isActiveAndEnabled)
                return;

            serverIntent.TryReceive(rpcParams.Receive.SenderClientId, OwnerClientId, inputSequence,
                new TankControlIntent(throttle, turn, aimPoint, hasAimPoint), Time.unscaledTimeAsDouble);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer)
                return;

            if (health != null && health.IsDead)
                return;

            float timeout = TankMath.IsFinite(inputTimeoutSeconds) ? Mathf.Max(0.1f, inputTimeoutSeconds) : 0.5f;
            tank.Simulate(serverIntent.Read(Time.unscaledTimeAsDouble, timeout), Time.fixedDeltaTime);
            turretYaw.Value = tank.TurretYaw;
        }

        private void OnTurretYawChanged(float previous, float current)
        {
            if (!IsServer)
                tank.ApplyReplicatedTurretYaw(current);
        }

        private void BindLocalOwner()
        {
            if (!CanReadLocalInput || localSession != null)
                return;

            localSession = FindFirstObjectByType<LocalNetworkSession>();
            if (localSession == null)
                Debug.LogError("The local player needs a LocalNetworkSession in the gameplay scene.", this);
            else
                localSession.BindLocalPlayer(this);
        }

        private void ReleaseLocalOwner()
        {
            if (localSession != null)
                localSession.ReleaseLocalPlayer(this);
            localSession = null;
        }

        private void ResetIntent()
        {
            localIntent = default;
            serverIntent.Reset();
            sequence = 0;
            nextSendTime = 0d;
        }

        private void ResetFireStream()
        {
            fireSequence = 0;
            serverFire.Reset();
        }

        public void RequestRespawn()
        {
            if (!CanReadLocalInput || !IsDead)
                return;

            RequestRespawnRpc();
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void RequestRespawnRpc(RpcParams rpcParams = default)
        {
            if (!IsSpawned || !IsServer || NetworkManager.ShutdownInProgress)
                return;

            ulong sender = rpcParams.Receive.SenderClientId;

            if (sender != OwnerClientId ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out var client) ||
                client.PlayerObject != NetworkObject ||
                !IsDead)
                return;

            LocalNetworkSession session =
                FindFirstObjectByType<LocalNetworkSession>();

            if (session != null)
                session.TryRespawn(this, sender);
        }
    }

}
