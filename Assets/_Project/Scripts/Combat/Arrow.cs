using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 타겟을 향해 직선으로 날아가는 화살 투사체.
    /// 타겟에 접근하면 데미지를 주고 사라진다.
    /// </summary>
    public class Arrow : MonoBehaviour
    {
        [Header("스탯")]
        [SerializeField] private float speed = 14f;
        [Tooltip("타겟에 이만큼 가까워지면 명중 처리")]
        [SerializeField] private float hitRadius = 0.15f;
        [Tooltip("타겟이 사라져도 이 시간 동안은 날아간 뒤 소멸")]
        [SerializeField] private float lifeTime = 3f;

        private Enemy target;
        private float damage;
        private float spawnedAt;

        public void Init(Enemy target, float damage)
        {
            this.target = target;
            this.damage = damage;
            spawnedAt = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnedAt > lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            if (target == null || target.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 toTarget = target.Position - transform.position;
            float distSq = toTarget.sqrMagnitude;

            if (distSq <= hitRadius * hitRadius)
            {
                target.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = toTarget.normalized;
            transform.position += dir * speed * Time.deltaTime;

            // 스프라이트가 오른쪽(+X)을 향한 상태 기준으로 회전 적용
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
