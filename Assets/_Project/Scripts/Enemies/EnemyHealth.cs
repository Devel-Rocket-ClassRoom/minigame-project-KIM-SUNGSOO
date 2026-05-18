using UnityEngine;
using KRTD.Core;
using KRTD.Data;
using KRTD.Economy;
using KRTD.Pooling;

namespace KRTD.Enemies
{
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private float maxHP;
        [SerializeField] private float currentHP;
        [SerializeField] private float physicalResist;
        [SerializeField] private float magicResist;
        [SerializeField] private int goldReward;

        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;

        private EnemyController owner;

        private void Awake() { owner = GetComponent<EnemyController>(); }

        public void Initialize(EnemyData data)
        {
            maxHP           = data.maxHP;
            currentHP       = data.maxHP;
            physicalResist  = data.physicalResist;
            magicResist     = data.magicResist;
            goldReward      = data.goldOnKill;
        }

        public void TakeDamage(float amount, DamageType type)
        {
            if (currentHP <= 0f) return;

            float final = amount;
            switch (type)
            {
                case DamageType.Physical:  final *= (1f - physicalResist); break;
                case DamageType.Magical:   final *= (1f - magicResist);    break;
                case DamageType.Explosive: final *= (1f - physicalResist * 0.5f); break;
                case DamageType.True:      break; // 저항 무시
            }

            currentHP -= final;
            if (currentHP <= 0f) Die();
        }

        private void Die()
        {
            currentHP = 0f;
            GoldManager.Instance.Add(goldReward);
            EventBus.Raise(new EnemyDiedEvent(owner));
            ObjectPool.Instance.Despawn(gameObject);
        }
    }

    public readonly struct EnemyDiedEvent
    {
        public readonly EnemyController Enemy;
        public EnemyDiedEvent(EnemyController e) { Enemy = e; }
    }
}
