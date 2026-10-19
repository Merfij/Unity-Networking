using UnityEngine;

namespace NetworkTankArena.Combat
{
    public interface IDamageable
    {
        void ApplyDamage(int amount, GameObject source);
    }
}
