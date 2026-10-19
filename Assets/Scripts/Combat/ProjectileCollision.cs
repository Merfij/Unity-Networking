using UnityEngine;

namespace NetworkTankArena.Combat
{
    /// <summary>The same solid-geometry queries and owner filtering serve launch clearance and travel.</summary>
    internal static class ProjectileCollision
    {
        internal readonly struct Hit
        {
            internal Collider Collider { get; }
            internal float Distance { get; }

            internal Hit(Collider collider, float distance)
            {
                Collider = collider;
                Distance = distance;
            }
        }

        internal static bool TryFindHit(Vector3 origin, Vector3 forward, float distance, float radius,
            LayerMask mask, Transform owner, out Hit hit)
        {
            // Sweeps alone are insufficient for shots beginning inside a solid collider.
            foreach (Collider collider in Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Ignore))
            {
                if (IsObstruction(collider, owner))
                {
                    hit = new Hit(collider, 0f);
                    return true;
                }
            }

            hit = default;
            bool found = false;
            if (distance > 0f)
            {
                // Filter every candidate before choosing the nearest; an ignored owner must not hide cover.
                foreach (RaycastHit candidate in Physics.SphereCastAll(origin, radius, forward, distance,
                    mask, QueryTriggerInteraction.Ignore))
                {
                    if (IsObstruction(candidate.collider, owner) && (!found || candidate.distance < hit.Distance))
                    {
                        hit = new Hit(candidate.collider, candidate.distance);
                        found = true;
                    }
                }

                if (!found)
                {
                    foreach (Collider collider in Physics.OverlapSphere(origin + forward * distance, radius,
                        mask, QueryTriggerInteraction.Ignore))
                    {
                        if (IsObstruction(collider, owner))
                        {
                            hit = new Hit(collider, distance);
                            return true;
                        }
                    }
                }
            }

            return found;
        }

        internal static void DeliverDamage(Collider collider, int amount, GameObject source)
        {
            // The nearest enabled receiver owns the response, including any future health rules.
            for (Transform current = collider.transform; current != null; current = current.parent)
            {
                foreach (MonoBehaviour component in current.GetComponents<MonoBehaviour>())
                {
                    if (component != null && component.isActiveAndEnabled && component is IDamageable receiver)
                    {
                        receiver.ApplyDamage(amount, source);
                        return;
                    }
                }
            }
        }

        private static bool IsObstruction(Collider collider, Transform owner) =>
            collider != null && (owner == null ||
                (collider.transform != owner && !collider.transform.IsChildOf(owner)));
    }
}
