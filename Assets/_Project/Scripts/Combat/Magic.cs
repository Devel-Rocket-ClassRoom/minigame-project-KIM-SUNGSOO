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

        [Header("광역 (Pyromancer 분기)")]
        [Tooltip("0 보다 크면 명중 지점 반경 내 모든 적에게 데미지(단일 타겟 모드 비활성). " +
            "0 이면 단일 타겟 — 기존 동작.")]
        [SerializeField] private float splashRadius = 0f;

        [Header("둔화 (Frost Mage 분기)")]
        [Tooltip("0 보다 크면 명중한 적에게 이동 속도 감소를 적용. " +
            "1.0 = 정지, 0.5 = 절반 감속, 0 = 둔화 없음. " +
            "광역(splashRadius > 0) 모드에서는 반경 내 모든 적에게 적용.")]
        [Range(0f, 1f)] [SerializeField] private float slowAmount = 0f;
        [Tooltip("둔화 지속 시간(초). slowAmount > 0 일 때만 의미 있음.")]
        [SerializeField] private float slowDuration = 2f;

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

            // 명중 판정 — Arrow.cs 와 동일한 이유로 step-aware 처리.
            // 저프레임(모바일 30fps)에서 한 프레임 이동량이 hitRadius 보다 클 때
            // 적을 건너뛰어 진동하는 "잔존" 현상 방지.
            float step = speed * Time.deltaTime;
            if (distSq <= hitRadius * hitRadius || toTarget.magnitude <= step)
            {
                ApplyHit(target.Position);
                Destroy(gameObject);
                return;
            }

            Vector3 dir = toTarget.normalized;
            transform.position += dir * step;

            if (spinSpeed != 0f)
            {
                transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 명중 처리 — 단일/광역 분기 + 둔화 적용.
        /// splashRadius > 0 이면 hitCenter 반경 내 모든 적에게 데미지(+둔화),
        /// 아니면 원래 타겟 1마리에게만.
        /// </summary>
        private void ApplyHit(Vector3 hitCenter)
        {
            if (splashRadius > 0f)
            {
                float radSq = splashRadius * splashRadius;
                // NOTE: 매 명중마다 FindObjectsByType — 적 수 많아지면 EnemyManager 등록 방식으로 교체.
                Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
                foreach (var e in enemies)
                {
                    if (e == null || e.IsDead) continue;
                    if ((e.Position - hitCenter).sqrMagnitude > radSq) continue;
                    e.TakeDamage(damage, attackType);
                    TryApplySlow(e);
                }
                return;
            }

            // 단일 타겟 — 기존 동작.
            target.TakeDamage(damage, attackType);
            if (target is Enemy single) TryApplySlow(single);
        }

        private void TryApplySlow(Enemy enemy)
        {
            if (slowAmount <= 0f || slowDuration <= 0f) return;
            // slowAmount 1.0 = 정지 → multiplier 0. slowAmount 0.5 = 절반 감속 → multiplier 0.5.
            float multiplier = Mathf.Clamp01(1f - slowAmount);
            enemy.ApplySlow(multiplier, slowDuration);
        }
    }
}
