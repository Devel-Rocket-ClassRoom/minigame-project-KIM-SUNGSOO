using UnityEngine;
using KRTD.Data;
using KRTD.Enemies;
using KRTD.Pooling;

namespace KRTD.Projectiles
{
    /// <summary>
    /// 투사체 베이스. 화살/탄/마법탄 등이 이 클래스를 상속하거나 그대로 사용.
    /// Launch() 호출 시 타겟을 추적하다 충돌(또는 위치 도달)하면 데미지 + 스플래시 처리.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] protected float speed = 12f;
        [SerializeField] protected bool homing = true;
        [SerializeField] protected LayerMask enemyMask;

        protected EnemyController target;
        protected Vector3 cachedTargetPos;
        protected float damage;
        protected DamageType damageType;
        protected float splashRadius;

        public virtual void Launch(EnemyController target, float dmg, DamageType type, float splash)
        {
            this.target       = target;
            this.cachedTargetPos = target != null ? target.transform.position : transform.position;
            this.damage       = dmg;
            this.damageType   = type;
            this.splashRadius = splash;
        }

        protected virtual void Update()
        {
            Vector3 destination = (homing && target != null && target.IsAlive)
                ? target.transform.position
                : cachedTargetPos;

            var dir = destination - transform.position;
            float step = speed * Time.deltaTime;

            if (dir.sqrMagnitude <= step * step)
            {
                transform.position = destination;
                OnHit(target);
            }
            else
            {
                dir.Normalize();
                transform.position += dir * step;
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        protected virtual void OnHit(EnemyController primary)
        {
            if (splashRadius > 0f)
            {
                var hits = Physics.OverlapSphere(transform.position, splashRadius, enemyMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    var e = hits[i].GetComponentInParent<EnemyController>();
                    if (e != null && e.IsAlive) e.Health.TakeDamage(damage, damageType);
                }
            }
            else if (primary != null && primary.IsAlive)
            {
                primary.Health.TakeDamage(damage, damageType);
            }

            ObjectPool.Instance.Despawn(gameObject);
        }
    }
}
