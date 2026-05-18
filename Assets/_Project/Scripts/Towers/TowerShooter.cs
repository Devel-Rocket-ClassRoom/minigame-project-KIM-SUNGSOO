using UnityEngine;
using KRTD.Data;

namespace KRTD.Towers
{
    /// <summary>
    /// 모든 타워의 "공격 행동" 추상 베이스.
    /// 4종 타워(궁수/마법사/포병/막사)는 이 클래스를 상속하는 구체 컴포넌트로 분기됨.
    /// </summary>
    public abstract class TowerShooter : MonoBehaviour
    {
        [Header("Tier State (런타임)")]
        [SerializeField] protected TowerUpgradeData currentTier;
        protected TowerController owner;

        protected float damage;
        protected float fireRate;
        protected float fireCooldown;
        protected DamageType damageType;
        protected float splashRadius;

        public virtual void Initialize(TowerController controller)
        {
            owner = controller;
        }

        public virtual void ApplyTier(TowerUpgradeData tier)
        {
            currentTier  = tier;
            damage       = tier.damage;
            fireRate     = tier.fireRate;
            damageType   = tier.damageType;
            splashRadius = tier.splashRadius;
        }

        protected virtual void Update()
        {
            if (currentTier == null || owner == null) return;
            fireCooldown -= Time.deltaTime;
            if (fireCooldown > 0f) return;

            var target = owner.TargetFinder.FindTarget();
            if (target == null) return;

            Fire(target);
            fireCooldown = 1f / Mathf.Max(fireRate, 0.0001f);
        }

        /// <summary>구체 타워가 실제 발사 로직을 구현.</summary>
        protected abstract void Fire(KRTD.Enemies.EnemyController target);
    }
}
