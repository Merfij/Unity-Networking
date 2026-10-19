using System.Collections;
using NetworkTankArena.Combat;
using NetworkTankArena.Networking;
using NetworkTankArena.Tanks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace NetworkTankArena.Tests
{
    public sealed class LocalTankInputLifecycleTests
    {
        [Test]
        public void UnspawnedNetworkTankCannotAcquireLocalInput()
        {
            var playerRoot = new GameObject("Unspawned player test");
            var inputRoot = new GameObject("Unbound input test");
            inputRoot.SetActive(false);
            try
            {
                var player = playerRoot.AddComponent<NetworkTankController>();
                var input = inputRoot.AddComponent<LocalTankController>();
                Assert.That(player.CanReadLocalInput, Is.False);
                input.BindNetworkTank(player, null);
                Assert.That(input.enabled, Is.False);
                input.BindNetworkTank(null, null);
                Assert.That(input.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(inputRoot);
                Object.DestroyImmediate(playerRoot);
            }
        }

        [UnityTest]
        public IEnumerator EnableDisableAndNetworkReleasePreserveSharedActionLifetimes()
        {
            bool expectDomainReload = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            yield return new EnterPlayMode(expectDomainReload);

            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            InputAction sharedThrottle = asset.FindAction("Tank/Throttle", true);
            InputAction sharedTurn = asset.FindAction("Tank/Turn", true);
            bool throttleWasEnabled = sharedThrottle.enabled;
            bool turnWasEnabled = sharedTurn.enabled;
            var references = new InputActionReference[4];
            var root = new GameObject("Local input lifecycle test");
            var cameraRoot = new GameObject("Local input lifecycle camera");
            root.SetActive(false);
            try
            {
                sharedThrottle.Enable();
                sharedTurn.Disable();
                TankSimulation tank = root.AddComponent<TankSimulation>();
                TankWeapon weapon = root.AddComponent<TankWeapon>();
                weapon.enabled = false; // No projectile is needed to exercise input lifetimes.
                var weaponData = new SerializedObject(weapon);
                weaponData.FindProperty("tank").objectReferenceValue = tank;
                weaponData.ApplyModifiedPropertiesWithoutUndo();
                Camera camera = cameraRoot.AddComponent<Camera>();
                camera.enabled = false;
                LocalTankController input = root.AddComponent<LocalTankController>();
                var inputData = new SerializedObject(input);
                inputData.FindProperty("tank").objectReferenceValue = tank;
                inputData.FindProperty("weapon").objectReferenceValue = weapon;
                inputData.FindProperty("aimCamera").objectReferenceValue = camera;
                string[] properties = { "throttle", "turn", "aimPosition", "fire" };
                string[] actions = { "Tank/Throttle", "Tank/Turn", "Tank/AimPosition", "Tank/Fire" };
                for (int i = 0; i < properties.Length; i++)
                {
                    references[i] = InputActionReference.Create(asset.FindAction(actions[i], true));
                    inputData.FindProperty(properties[i]).objectReferenceValue = references[i];
                }
                inputData.ApplyModifiedPropertiesWithoutUndo();

                root.SetActive(true); // Real OnEnable, with private clones.
                Assert.That(input.enabled, Is.True);
                AssertSharedActions(sharedThrottle, sharedTurn);
                input.enabled = false;
                AssertSharedActions(sharedThrottle, sharedTurn);
                input.enabled = true;
                AssertSharedActions(sharedThrottle, sharedTurn);
                input.BindNetworkTank(null, null); // Session shutdown clears input and disposes clones.
                Assert.That(input.enabled, Is.False);
                AssertSharedActions(sharedThrottle, sharedTurn);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraRoot);
                foreach (InputActionReference reference in references)
                    if (reference != null)
                        Object.DestroyImmediate(reference);
                if (throttleWasEnabled) sharedThrottle.Enable(); else sharedThrottle.Disable();
                if (turnWasEnabled) sharedTurn.Enable(); else sharedTurn.Disable();
            }
        }

        private static void AssertSharedActions(InputAction throttle, InputAction turn)
        {
            Assert.That(throttle.enabled, Is.True, "Local input must not disable an enabled shared action.");
            Assert.That(turn.enabled, Is.False, "Local input must not enable a disabled shared action.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }
    }
}
