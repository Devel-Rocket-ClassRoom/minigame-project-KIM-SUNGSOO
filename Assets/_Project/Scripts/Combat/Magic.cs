using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 타겟을 향해 직선으로 날아가는 마법 투사체.
    /// 화살(Arrow)과 동일한 호밍/명중 로직을 따르되,
    /// 비행 중 자기 자신을 회전시키며 마법 느낌을 더한다.
    /// </summary>
    public class Magic : MonoBehaviour
    {
        [Header("스탯")]
        [SerializeField] private float speed = 10f;
        [Tooltip("타겟에 이만큼 가까워지면 명중 처리")]
        [SerializeField] private float hitRadius = 0.2f;
        [Tooltip("타겟이 사라져도 이 시간 동안은 날아간 뒤 소멸")]
        [SerializeField] private float lifeTime = 3f;

        [Header("시각")]
        [Tooltip("비행 중 자체 회전 속도(도/초). 0이면 회전하지 않는다.")]
        [SerializeField] private float spinSpeed = 360f;

        private IDamageable target;
        private float damage;
        private AttackType attackType = AttackType.Magic;
        private float spawnedAt;

        public void Init(IDamageable target, float damage, AttackType attackType)
        {
            this.target = target;
            this.damage = damage;
            this.attackType = attackType;
            spawnedAt = Time.time;
        }

        /// <summary>구버전 호환 — 공격 유형 미지정 시 Magic.</summary>
        public void Init(IDamageable target, float damage) => Init(target, damage, AttackType.Magic);

        private void Update()
        {
            if (Time.time - spawnedAt > lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            // 인터페이스 참조는 Unity 의 == null 오버로드를 통과 못하므로 UnityEngine.Object 캐스트로 명시 체크
            if (target == null || (target as Object) == null || target.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 toTarget = target.Position - transform.position;
            float distSq = toTarget.sqrMagnitude;

            if (distSq <= hitRadius * hitRadius)
            {
                target.TakeDamage(damage, attackType);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = toTarget.normalized;
            transform.position += dir * speed * Time.deltaTime;

            if (spinSpeed != 0f)
            {
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
        }
    }
}
