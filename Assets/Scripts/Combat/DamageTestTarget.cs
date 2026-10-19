using UnityEngine;

namespace NetworkTankArena.Combat
{
    /// <summary>A persistent local test receiver, without health or a destruction lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageTestTarget : MonoBehaviour, IDamageable
    {
        [SerializeField] private int hitCount;
        [SerializeField] private int accumulatedDamage;
        [SerializeField] private GameObject lastSource;

        public int HitCount => hitCount;
        public int AccumulatedDamage => accumulatedDamage;
        public GameObject LastSource => lastSource;

        private void OnEnable()
        {
            hitCount = accumulatedDamage = 0;
            lastSource = null;
        }

        public void ApplyDamage(int amount, GameObject source)
        {
            if (!isActiveAndEnabled || amount <= 0)
                return;

            hitCount++;
            accumulatedDamage += amount;
            lastSource = source;
        }
    }
}
