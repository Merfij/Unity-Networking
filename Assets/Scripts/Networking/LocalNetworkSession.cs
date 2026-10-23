using System.Collections.Generic;
using NetworkTankArena.Presentation;
using NetworkTankArena.Tanks;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace NetworkTankArena.Networking
{
    /// <summary>Scene-owned localhost buttons, two spawn slots, and one local input/camera binding.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalNetworkSession : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LocalTankController localInput;
        [SerializeField] private TankFollowCamera followCamera;
        [SerializeField] private Transform hostSpawn;
        [SerializeField] private Transform clientSpawn;
        [SerializeField] private NetworkDamageTestTarget damageTargetPrefab;
        [SerializeField] private Transform damageTargetSpawn;
        [SerializeField] private ushort port = 7777;

        private readonly Dictionary<ulong, int> spawnSlots = new Dictionary<ulong, int>();
        private NetworkTankController localPlayer;
        private Vector3 idleCameraPosition;
        private Quaternion idleCameraRotation;
        private string status = "Choose Host or Client (localhost).";

        private void Awake()
        {
            if (localInput != null)
                localInput.BindNetworkTank(null, null);
            if (followCamera != null)
            {
                idleCameraPosition = followCamera.transform.position;
                idleCameraRotation = followCamera.transform.rotation;
                followCamera.SetTarget(null);
            }
        }

        private void OnEnable()
        {
            if (networkManager == null)
                return;

            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnClientStopped += OnClientStopped;
            networkManager.OnServerStopped += OnServerStopped;
        }

        private void OnDisable()
        {
            ClearLocalPlayer();
            spawnSlots.Clear();
            if (networkManager == null)
                return;

            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientStopped -= OnClientStopped;
            networkManager.OnServerStopped -= OnServerStopped;
            if (networkManager.ConnectionApprovalCallback == ApproveConnection)
                networkManager.ConnectionApprovalCallback = null;
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }

        public void StartHost() => StartSession(true);
        public void StartClient() => StartSession(false);

        private void StartSession(bool host)
        {
            if (networkManager == null || networkManager.IsListening || networkManager.ShutdownInProgress)
                return;

            if (localInput == null || followCamera == null || hostSpawn == null || clientSpawn == null ||
                port == 0 || networkManager.DistributedAuthorityMode ||
                !(networkManager.NetworkConfig.NetworkTransport is UnityTransport transport) ||
                networkManager.NetworkConfig.PlayerPrefab == null ||
                networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkTankController>() == null ||
                damageTargetPrefab == null || !damageTargetPrefab.enabled || !damageTargetPrefab.gameObject.activeSelf ||
                !damageTargetPrefab.TryGetComponent(out NetworkObject _) || damageTargetSpawn == null ||
                !TankMath.IsFinite(damageTargetSpawn.position) || !TankMath.IsFinite(damageTargetSpawn.eulerAngles.y) ||
                !networkManager.NetworkConfig.Prefabs.Contains(damageTargetPrefab.gameObject) ||
                !networkManager.NetworkConfig.PlayerPrefab.TryGetComponent(out NetworkTransform rootTransform) ||
                rootTransform.AuthorityMode != NetworkTransform.AuthorityModes.Server)
            {
                status = "Session setup is incomplete; see the Console.";
                Debug.LogError("Assign the scene input/camera, two clear spawn points, a UnityTransport, " +
                    "the network tank Player Prefab, and a registered damage target prefab/marker " +
                    "on a client-server NetworkManager. See docs/NETWORK_COMBAT.md.", this);
                return;
            }

            ClearLocalPlayer();
            spawnSlots.Clear();
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            bool started = host ? networkManager.StartHost() : networkManager.StartClient();
            if (started && host)
            {
                // Exactly one server-owned target per new Host session. Clients receive this spawn
                // and its current NetworkVariables, including on a later connection.
                NetworkDamageTestTarget target = Instantiate(damageTargetPrefab, damageTargetSpawn.position,
                    Quaternion.Euler(0f, damageTargetSpawn.eulerAngles.y, 0f));
                target.NetworkObject.Spawn();
            }
            status = started ? (host ? $"Hosting on localhost:{port}." : "Connecting to localhost...") : "Session could not start.";
        }

        public void StopSession()
        {
            ClearLocalPlayer();
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();
            status = "Stopping session...";
        }

        internal void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Reserve approval slots too, so simultaneous connection attempts cannot overlap spawns.
            int slot = !spawnSlots.ContainsValue(0) ? 0 : !spawnSlots.ContainsValue(1) ? 1 : -1;
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (slot < 0 || spawnSlots.ContainsKey(request.ClientNetworkId))
            {
                response.Reason = "This lab session has room for two players.";
                return;
            }

            Transform spawn = slot == 0 ? hostSpawn : clientSpawn;
            // References can disappear after StartSession validation. Reject without reserving a
            // slot or letting NGO fall back to the prefab pose (the host cannot be declined by NGO).
            if (spawn == null || !TankMath.IsFinite(spawn.position) || !TankMath.IsFinite(spawn.eulerAngles.y))
            {
                response.Reason = "The reserved player spawn marker is missing or has an invalid pose.";
                return;
            }

            response.Position = spawn.position;
            response.Rotation = Quaternion.Euler(0f, spawn.eulerAngles.y, 0f);
            spawnSlots.Add(request.ClientNetworkId, slot);
            response.Approved = true;
            response.CreatePlayerObject = true;
            // NGO creates exactly one owned PlayerObject using the registered Player Prefab.
        }

        internal void TryRespawn(NetworkTankController deadTank, ulong clientId)
        {
            //Implement respawn here.
            Debug.Log("Respawn requested, but not implemented yet.");

            if (networkManager == null || !networkManager.IsServer || deadTank == null || !deadTank.IsSpawned || !deadTank.IsDead || deadTank.OwnerClientId != clientId)
                return;

            if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject != deadTank.NetworkObject)
                return;

            if(!spawnSlots.TryGetValue(clientId, out int slot))
                return;

            Transform spawn = slot == 0 ? hostSpawn : clientSpawn;

            if (spawn == null || !TankMath.IsFinite(spawn.position) || !TankMath.IsFinite(spawn.eulerAngles.y))
            {
                Debug.Log("Can't respawn: spawn point is invalid."); return;
            }

            GameObject newTankObject = networkManager.NetworkConfig.PlayerPrefab;

            if (newTankObject == null || !newTankObject.TryGetComponent(out NetworkObject networkObject) || !newTankObject.TryGetComponent(out NetworkTankController networkTankController) || !newTankObject.TryGetComponent(out NetworkTankHealth networkTankHealth))
            {
                Debug.LogError(
                    "Cannot respawn: the Player Prefab needs NetworkObject, " +
                    "NetworkTankController, and NetworkTankHealth components.",
                    this);

                return;
            }

            deadTank.NetworkObject.Despawn(true);

            //Instantiates a new tank on the server but not on the cliet.
            GameObject respawnedTank = Instantiate(newTankObject, spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
            NetworkObject respawnedNetworkObject = respawnedTank.GetComponent<NetworkObject>();

            try
            {
                //Assigns the ownership of the new tank to the client and spawns it on the network.
                respawnedNetworkObject.SpawnAsPlayerObject(clientId);
            } catch (System.Exception ex)
            {
                if (respawnedNetworkObject != null && respawnedNetworkObject.IsSpawned)
                {
                    respawnedNetworkObject.Despawn(true);
                }
                Debug.LogError($"Failed to spawn respawned tank for client {clientId}: {ex.Message}");
                Destroy(respawnedTank);
                return;

            }

        }

        internal void BindLocalPlayer(NetworkTankController player)
        {
            if (player == null || !player.CanReadLocalInput)
                return;

            ClearLocalPlayer();
            localPlayer = player;
            followCamera.SetTarget(player.transform);
            localInput.BindNetworkTank(player, followCamera.GetComponent<Camera>());
        }

        internal void ReleaseLocalPlayer(NetworkTankController player)
        {
            if (localPlayer == player)
                ClearLocalPlayer();
        }

        private void ClearLocalPlayer()
        {
            if (localInput != null)
                localInput.BindNetworkTank(null, null);
            if (followCamera != null)
            {
                followCamera.SetTarget(null);
                followCamera.transform.SetPositionAndRotation(idleCameraPosition, idleCameraRotation);
            }
            localPlayer = null;
        }

        internal void OnClientDisconnected(ulong clientId)
        {
            spawnSlots.Remove(clientId);
            if (networkManager != null && clientId == networkManager.LocalClientId)
            {
                ClearLocalPlayer();
                status = string.IsNullOrEmpty(networkManager.DisconnectReason) ?
                    "Disconnected." : networkManager.DisconnectReason;
            }
        }

        private void OnClientStopped(bool wasHost)
        {
            ClearLocalPlayer();
            spawnSlots.Clear();
        }

        internal void OnServerStopped(bool wasHost) => spawnSlots.Clear();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 300f, 150f), GUI.skin.box);
            GUILayout.Label("Network Tank Arena — two-player lab");
            GUILayout.Label(networkManager != null && networkManager.IsConnectedClient ?
                (networkManager.IsHost ? "Host connected" : "Client connected") : status);
            GUI.enabled = networkManager != null && !networkManager.ShutdownInProgress;
            if (networkManager != null && networkManager.IsListening)
            {
                if (GUILayout.Button("Stop session"))
                    StopSession();
            }
            else
            {
                if (GUILayout.Button("Start Host"))
                    StartHost();
                if (GUILayout.Button("Start Client (localhost)"))
                    StartClient();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
