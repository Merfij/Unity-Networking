using NetworkTankArena.Combat;
using NetworkTankArena.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NetworkTankArena.Tanks
{
    [DisallowMultipleComponent]
    public sealed class LocalTankController : MonoBehaviour
    {
        [SerializeField] private TankSimulation tank;
        [SerializeField] private TankWeapon weapon;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private InputActionReference throttle;
        [SerializeField] private InputActionReference turn;
        [SerializeField] private InputActionReference aimPosition;
        [SerializeField] private InputActionReference fire;

        private InputAction throttleAction;
        private InputAction turnAction;
        private InputAction aimPositionAction;
        private InputAction fireAction;
        private NetworkTankController networkTank;
        private bool waitingForNeutralMovement;
        private readonly FireRequestLatch fireLatch = new FireRequestLatch();

        private void OnEnable()
        {
            fireLatch.RequireRelease();
            waitingForNeutralMovement = true;
            // Private action instances have an explicit lifetime and never disable project-wide actions.
            throttleAction = throttle != null ? throttle.action?.Clone() : null;
            turnAction = turn != null ? turn.action?.Clone() : null;
            aimPositionAction = aimPosition != null ? aimPosition.action?.Clone() : null;
            fireAction = fire != null ? fire.action?.Clone() : null;

            if (tank == null || (networkTank == null && (weapon == null || weapon.Tank != tank)) || aimCamera == null ||
                throttleAction == null || turnAction == null || aimPositionAction == null || fireAction == null ||
                throttleAction.expectedControlType != "Axis" || turnAction.expectedControlType != "Axis" ||
                aimPositionAction.expectedControlType != "Vector2" || fireAction.type != InputActionType.Button ||
                fireAction.expectedControlType != "Button")
            {
                Debug.LogError("LocalTankController needs a tank, its weapon, an aim camera, and " +
                    "Tank/Throttle (Axis), Tank/Turn (Axis), Tank/AimPosition (Vector2), and " +
                    "Tank/Fire (Button) action references.", this);
                enabled = false;
                return;
            }

            throttleAction.Enable();
            turnAction.Enable();
            aimPositionAction.Enable();
            fireAction.Enable();
        }

        private void OnDisable()
        {
            fireLatch.RequireRelease();
            waitingForNeutralMovement = true;
            if (networkTank != null)
                networkTank.SubmitLocalIntent(default);
            throttleAction?.Dispose();
            turnAction?.Dispose();
            aimPositionAction?.Dispose();
            fireAction?.Dispose();
            throttleAction = turnAction = aimPositionAction = fireAction = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                fireLatch.RequireRelease();
                waitingForNeutralMovement = true;
            }
        }

        private void Update()
        {
            if (tank == null || (networkTank != null && !networkTank.CanReadLocalInput))
                return;

            TankControlIntent intent = default;
            if (Application.isFocused)
            {
                bool hasAimPoint = TryGetAimPoint(out Vector3 aimPoint);
                bool canRequestFire = hasAimPoint && Time.timeScale > 0f && Time.deltaTime > 0f &&
                    throttleAction.enabled && turnAction.enabled;
                bool fireRequested = TryReadFireHeld(out bool fireHeld) &&
                    fireLatch.TryRequest(fireHeld, fireAction.WasPressedThisFrame(), canRequestFire);
                float throttleValue = throttleAction.ReadValue<float>();
                float turnValue = turnAction.ReadValue<float>();
                if (waitingForNeutralMovement)
                {
                    waitingForNeutralMovement = Mathf.Abs(throttleValue) > 0.01f || Mathf.Abs(turnValue) > 0.01f;
                    throttleValue = turnValue = 0f;
                }
                intent = new TankControlIntent(throttleValue, turnValue,
                    aimPoint, hasAimPoint, fireRequested);
            }
            else
            {
                fireLatch.RequireRelease();
            }

            if (networkTank != null)
            {
                networkTank.SubmitLocalIntent(intent);
                // A press is discrete: never retain it until the rate-limited movement sender runs.
                if (intent.FireRequested)
                    networkTank.SubmitLocalFire(intent.AimWorldPoint);
                return;
            }

            tank.Simulate(intent, Time.deltaTime);
            // Canonical aim is applied before consuming this request. Rejected clicks are never retained.
            if (intent.FireRequested && weapon != null && weapon.Tank == tank)
                weapon.TryFire();
        }

        private bool TryReadFireHeld(out bool held)
        {
            held = false;
            if (!fireAction.enabled)
                return false;

            // With initial-state checking off, a newly enabled action can report unpressed
            // while its physical button is still held. Observe the bound controls directly.
            bool hasEnabledButton = false;
            foreach (InputControl control in fireAction.controls)
            {
                if (control is ButtonControl button && button.device.enabled)
                {
                    hasEnabledButton = true;
                    held |= button.isPressed;
                }
            }
            return hasEnabledButton;
        }

        private bool TryGetAimPoint(out Vector3 aimPoint)
        {
            aimPoint = default;
            if (aimCamera == null || !aimCamera.isActiveAndEnabled || !aimPositionAction.enabled ||
                aimPositionAction.controls.Count == 0)
                return false;

            Vector2 screenPoint = aimPositionAction.ReadValue<Vector2>();
            if (!aimCamera.pixelRect.Contains(screenPoint))
                return false;

            Ray ray = aimCamera.ScreenPointToRay(screenPoint);
            Plane plane = new Plane(Vector3.up, tank.TurretOrigin);
            if (!plane.Raycast(ray, out float distance))
                return false;

            aimPoint = ray.GetPoint(distance);
            return true;
        }

        /// <summary>Called by the scene session when a local owner spawns or releases control.</summary>
        public void BindNetworkTank(NetworkTankController player, Camera camera)
        {
            enabled = false;
            networkTank = player;
            tank = player != null ? player.GetComponent<TankSimulation>() : null;
            weapon = null; // Network owners submit requests; only the server invokes their weapon.
            aimCamera = camera;
            if (player != null && player.CanReadLocalInput)
                enabled = true;
        }
    }
}
