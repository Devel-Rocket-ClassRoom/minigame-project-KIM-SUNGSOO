using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 데미지를 받을 수 있는 대상. 투사체가 Enemy / Soldier 어느 쪽이든 추적/타격할 수 있도록 추상화.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        Vector3 Position { get; }
        void TakeDamage(float damage, AttackType attackType);
    }
}
