using UnityEngine;
using KRTD.Pooling;
using KRTD.Projectiles;

namespace KRTD.Towers.Types
{
    /// <summary>
    /// 투사체 발사 타워(궁수/마법사/포병 공용).
    /// 차이점은 currentTier.projectilePrefab 과 damageType 으로 표현.
    /// </summary>
    public class ProjectileShooter : TowerShooter
    {
        [SerializeField] private Transform muzzle;

        protected override void Fire(KRTD.Enemies.EnemyController target)
        {
            if (currentTier == null || currentTier.projectilePrefab == null) return;

            var go = ObjectPool.Instance.Spawn(
                currentTier.projectilePrefab,
                muzzle != null ? muzzle.position : transform.position,
                Quaternion.identity);

            var proj = go.GetComponent<Projectile>();
            proj.Launch(target, damage, damageType, splashRadius);
        }
    }
}
