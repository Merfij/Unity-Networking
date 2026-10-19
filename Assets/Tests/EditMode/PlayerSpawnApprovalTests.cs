using NetworkTankArena.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace NetworkTankArena.Tests
{
    /// <summary>Actual approval responses and slot lifetimes; no running or mocked NGO session.</summary>
    public sealed class PlayerSpawnApprovalTests
    {
        private GameObject sessionRoot;
        private GameObject hostMarker;
        private GameObject clientMarker;
        private LocalNetworkSession session;

        [SetUp]
        public void SetUp()
        {
            sessionRoot = new GameObject("Spawn approval test session");
            sessionRoot.SetActive(false);
            session = sessionRoot.AddComponent<LocalNetworkSession>();
            hostMarker = new GameObject("Spawn approval host marker");
            clientMarker = new GameObject("Spawn approval client marker");
            hostMarker.transform.SetPositionAndRotation(new Vector3(-4f, 0f, -4f), Quaternion.Euler(0f, 30f, 0f));
            clientMarker.transform.SetPositionAndRotation(new Vector3(4f, 0f, -4f), Quaternion.Euler(0f, 210f, 0f));
            AssignMarker("hostSpawn", hostMarker.transform);
            AssignMarker("clientSpawn", clientMarker.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sessionRoot);
            Object.DestroyImmediate(hostMarker);
            Object.DestroyImmediate(clientMarker);
        }

        [Test]
        public void HostAndFirstClientReceiveDistinctServerChosenInitialPoses()
        {
            AssertPose(Approve(NetworkManager.ServerClientId), hostMarker.transform);
            AssertPose(Approve(1), clientMarker.transform);
        }

        [Test]
        public void ThirdConnectionCannotCreateAnotherPlayer()
        {
            Approve(NetworkManager.ServerClientId);
            Approve(1);
            AssertRejected(Approve(2));
        }

        [Test]
        public void DuplicateApprovalCannotConsumeTheOtherSlot()
        {
            Approve(NetworkManager.ServerClientId);
            AssertRejected(Approve(NetworkManager.ServerClientId));
            AssertPose(Approve(1), clientMarker.transform);
        }

        [Test]
        public void ClientDisconnectReleasesOnlyItsSlotForANewConnection()
        {
            Approve(NetworkManager.ServerClientId);
            Approve(1);
            session.OnClientDisconnected(1);
            AssertPose(Approve(2), clientMarker.transform);
            AssertRejected(Approve(3));
        }

        [Test]
        public void SessionStopClearsReservationsForTheNextHostAndClient()
        {
            Approve(NetworkManager.ServerClientId);
            Approve(1);
            session.OnServerStopped(true);
            AssertPose(Approve(NetworkManager.ServerClientId), hostMarker.transform);
            AssertPose(Approve(2), clientMarker.transform);
        }

        [Test]
        public void MissingHostMarkerDoesNotAllowPrefabFallbackOrReserveASlot()
        {
            AssignMarker("hostSpawn", null);
            AssertRejected(Approve(NetworkManager.ServerClientId));
            AssignMarker("hostSpawn", hostMarker.transform);
            AssertPose(Approve(NetworkManager.ServerClientId), hostMarker.transform);
            AssertPose(Approve(1), clientMarker.transform);
        }

        [Test]
        public void MissingClientMarkerAfterHostApprovalDoesNotReserveItsSlot()
        {
            Approve(NetworkManager.ServerClientId);
            AssignMarker("clientSpawn", null);
            AssertRejected(Approve(1));
            AssignMarker("clientSpawn", clientMarker.transform);
            AssertPose(Approve(2), clientMarker.transform);
        }

        [Test]
        public void ApprovalUsesCurrentWorldPositionAndYawRatherThanPrefabOrLocalPose()
        {
            clientMarker.transform.SetParent(sessionRoot.transform, false);
            sessionRoot.transform.SetPositionAndRotation(new Vector3(10f, 0f, 5f), Quaternion.Euler(0f, 70f, 0f));
            clientMarker.transform.localRotation = Quaternion.Euler(15f, 40f, 10f);
            Approve(NetworkManager.ServerClientId);
            AssertPose(Approve(1), clientMarker.transform);
        }

        private NetworkManager.ConnectionApprovalResponse Approve(ulong clientId)
        {
            var response = new NetworkManager.ConnectionApprovalResponse();
            session.ApproveConnection(new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = clientId }, response);
            return response;
        }

        private static void AssertPose(NetworkManager.ConnectionApprovalResponse response, Transform marker)
        {
            Assert.That(response.Approved, Is.True);
            Assert.That(response.CreatePlayerObject, Is.True);
            Assert.That(response.Pending, Is.False);
            Assert.That(response.Position.HasValue, Is.True);
            Assert.That(response.Position.Value, Is.EqualTo(marker.position));
            Assert.That(response.Rotation.HasValue, Is.True);
            Assert.That(Quaternion.Angle(response.Rotation.Value, Quaternion.Euler(0f, marker.eulerAngles.y, 0f)),
                Is.LessThan(0.001f));
        }

        private static void AssertRejected(NetworkManager.ConnectionApprovalResponse response)
        {
            Assert.That(response.Approved, Is.False);
            Assert.That(response.CreatePlayerObject, Is.False);
            Assert.That(response.Pending, Is.False);
            Assert.That(response.Position.HasValue, Is.False);
            Assert.That(response.Rotation.HasValue, Is.False);
        }

        private void AssignMarker(string property, Transform marker)
        {
            var data = new SerializedObject(session);
            data.FindProperty(property).objectReferenceValue = marker;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
