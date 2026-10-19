using NetworkTankArena.Tanks;
using UnityEngine;

namespace NetworkTankArena.Combat
{
    [DisallowMultipleComponent]
    public sealed class TankWeapon : MonoBehaviour
    {
        [SerializeField] private TankSimulation tank;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField, Min(0.01f)] private float cooldownSeconds = 0.30f;
        [SerializeField, Min(0f)] private float muzzleForwardOffset = 1.30f;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 20f;
        [SerializeField, Min(0.001f)] private float projectileRadius = 0.10f;
        [SerializeField, Min(0.01f)] private float projectileLifetime = 3f;
        [SerializeField, Min(1)] private int damage = 25;
        [SerializeField] private LayerMask hitMask = 1; // Default: current tank and arena geometry.

        private WeaponCooldown cooldown = new WeaponCooldown();

        public TankSimulation Tank => tank;
        internal Projectile ProjectilePrefab => projectilePrefab;

        private void Awake()
        {
            // Initialize each Play session, including starts with domain reload disabled.
            // Ordinary component/root enable transitions must preserve the cooldown deadline.
            ResetCooldown();
        }

        /// <summary>Explicit initialization/reset. Enabling the weapon never resets its cooldown.</summary>
        public void ResetCooldown()
        {
            cooldown = new WeaponCooldown();
        }

        private void Start()
        {
            if (!HasValidConfiguration())
            {
                Debug.LogError("TankWeapon needs its root TankSimulation, an enabled projectile prefab without " +
                    "physics shapes, a nonempty hit mask, and finite valid shot tuning.", this);
                enabled = false;
            }
        }

        /// <summary>Consumes one request using the local scaled clock. Rejected requests are discarded.</summary>
        public bool TryFire() => TryFire(Time.timeAsDouble);

        // The local adapter and boundary tests share the same shot path and explicit simulation clock.
        internal bool TryFire(double simulationTime) =>
            TryFire(simulationTime, tank != null ? tank.TurretYaw : 0f, out _);

        /// <summary>The caller derives shot yaw. Firing never writes the tank's continuous turret aim.
        /// An accepted blocked launch returns true with no projectile.</summary>
        internal bool TryFire(double simulationTime, float shotYaw, out Projectile spawnedProjectile)
        {
            spawnedProjectile = null;
            if (!isActiveAndEnabled || Time.timeScale <= 0f || !HasValidConfiguration() ||
                !tank.isActiveAndEnabled)
                return false;

            CharacterController controller = tank.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled ||
                !FiringPose.TryCalculate(tank.TurretOrigin, shotYaw, muzzleForwardOffset, out FiringPose pose) ||
                cooldown == null || !cooldown.TryConsume(simulationTime, cooldownSeconds))
                return false;

            // Consume cooldown before impact callbacks. Do not instantiate beyond blocked launch geometry.
            if (ProjectileCollision.TryFindHit(pose.TurretOrigin, pose.Forward,
                muzzleForwardOffset, projectileRadius, hitMask, tank.transform, out var hit))
            {
                ProjectileCollision.DeliverDamage(hit.Collider, damage, tank.gameObject);
                return true;
            }

            Projectile projectile = Instantiate(projectilePrefab, pose.Origin, Quaternion.LookRotation(pose.Forward));
            if (!projectile.Initialize(pose, tank.gameObject, projectileSpeed, projectileRadius,
                projectileLifetime, damage, hitMask))
            {
                Debug.LogError("The accepted shot could not initialize its projectile. Check the projectile prefab.", this);
                if (Application.isPlaying)
                    Destroy(projectile.gameObject);
                else
                    DestroyImmediate(projectile.gameObject);
            }
            else
            {
                spawnedProjectile = projectile;
            }
            return true;
        }

        private bool HasValidConfiguration() => tank != null && tank.gameObject == gameObject &&
            projectilePrefab != null && projectilePrefab.enabled && projectilePrefab.gameObject.activeSelf &&
            !projectilePrefab.HasPhysicsShape && CombatValidation.IsFinite(cooldownSeconds) && cooldownSeconds > 0f &&
            CombatValidation.IsFinite(muzzleForwardOffset) && muzzleForwardOffset >= 0f &&
            CombatValidation.IsFinite(tank.TurretOrigin) && CombatValidation.IsFinite(tank.TurretYaw) &&
            CombatValidation.IsValidShot(projectileSpeed, projectileRadius, projectileLifetime, damage, hitMask);
    }
}
