using UnityEngine;

namespace NetworkTankArena.Combat
{
    internal static class CombatValidation
    {
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        internal static bool IsValidShot(float speed, float radius, float lifetime, int damage, LayerMask mask) =>
            IsFinite(speed) && speed > 0f && IsFinite(radius) && radius > 0f &&
            IsFinite(lifetime) && lifetime > 0f && IsFinite(speed * lifetime) && damage > 0 && mask.value != 0;
    }
}
