using UnityEngine;

namespace NetworkTankArena.Combat
{
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private bool initialized;
        private bool externallyDriven;
        private int creationFrame;
        private GameObject source;
        private Transform owner;
        private float speed;
        private float radius;
        private float lifetime;
        private int damage;
        private LayerMask hitMask;

        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Age { get; private set; }
        public bool IsTerminal { get; private set; }
        internal bool IsInitialized => initialized;

        // The network adapter owns stepping and destruction; the local prefab keeps its old adapter.
        internal void UseExternalDriver() => externallyDriven = true;

        internal bool HasPhysicsShape => GetComponentInParent<Rigidbody>() != null ||
            GetComponentInParent<Collider>() != null || GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
            GetComponentsInChildren<Collider>(true).Length != 0;

        /// <summary>Called once, after weapon launch clearance. Tuning and pose are copied for this shot.</summary>
        public bool Initialize(FiringPose pose, GameObject firingTank, float projectileSpeed, float projectileRadius,
            float projectileLifetime, int projectileDamage, LayerMask mask)
        {
            if (initialized || IsTerminal || !isActiveAndEnabled || firingTank == null || !pose.IsValid ||
                HasPhysicsShape || !CombatValidation.IsValidShot(projectileSpeed, projectileRadius,
                    projectileLifetime, projectileDamage, mask))
                return false;

            source = firingTank;
            owner = firingTank.transform;
            speed = projectileSpeed;
            radius = projectileRadius;
            lifetime = projectileLifetime;
            damage = projectileDamage;
            hitMask = mask;
            Position = pose.Origin;
            Direction = pose.Forward;
            Age = 0f;
            creationFrame = Time.frameCount;
            initialized = true;
            transform.SetPositionAndRotation(Position, Quaternion.LookRotation(Direction, Vector3.up));
            return true;
        }

        private void LateUpdate()
        {
            SimulateLocalFrame(Time.frameCount, Time.deltaTime);
        }

        internal void SimulateLocalFrame(int currentFrame, float deltaTime)
        {
            // Enforce next-frame travel regardless of when Unity first calls this component's adapter.
            if (!externallyDriven && currentFrame != creationFrame)
                Simulate(deltaTime);
        }

        /// <summary>One explicit gameplay step. The local adapter skips the creation frame.</summary>
        public void Simulate(float deltaTime)
        {
            if (!initialized || IsTerminal || !isActiveAndEnabled ||
                !CombatValidation.IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            float remainingTime = lifetime - Age;
            float stepTime = Mathf.Min(deltaTime, remainingTime);
            float distance = speed * stepTime;
            if (ProjectileCollision.TryFindHit(Position, Direction, distance, radius, hitMask, owner, out var hit))
            {
                Position += Direction * hit.Distance;
                Age += stepTime * (distance > 0f ? hit.Distance / distance : 0f);
                transform.position = Position;
                IsTerminal = true;
                try
                {
                    ProjectileCollision.DeliverDamage(hit.Collider, damage, source);
                }
                finally
                {
                    Remove();
                }
                return;
            }

            Position += Direction * distance;
            Age = stepTime >= remainingTime ? lifetime : Age + stepTime;
            transform.position = Position;
            if (Age >= lifetime)
            {
                IsTerminal = true;
                Remove();
            }
        }

        private void Remove()
        {
            // EditMode tests retain the terminal instance for assertions and destroy it in teardown.
            if (!externallyDriven && Application.isPlaying)
                Destroy(gameObject);
        }
    }
}
