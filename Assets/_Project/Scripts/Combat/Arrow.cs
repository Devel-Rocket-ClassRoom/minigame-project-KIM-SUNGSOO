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

        [Header("궤적")]
        [Tooltip("0 = 직선 호밍 (계속 타겟 추적). " +
            "> 0 = 포물선. 발사 시점의 타겟 위치로 고정해서 이 높이만큼 위로 솟았다가 떨어진다. " +
            "활시위 → 위로 솟는 화살 애니메이션과 어울림.")]
        [SerializeField] private float arcHeight = 0f;

        private IDamageable target;
        private float damage;
        private AttackType attackType = AttackType.Physical;
        private float spawnedAt;

        // 포물선 모드용 (arcHeight > 0 일 때만 사용)
        private Vector3 arcStartPos;
        private Vector3 arcEndPos;
        private float arcFlightTime;
        private float arcElapsed;

        public void Init(IDamageable target, float damage, AttackType attackType)
        {
            this.target = target;
            this.damage = damage;
            this.attackType = attackType;
            spawnedAt = Time.time;

            if (arcHeight > 0f && target != null)
            {
                arcStartPos = transform.position;
                arcEndPos = target.Position;
                float distance = Vector3.Distance(arcStartPos, arcEndPos);
                arcFlightTime = distance / Mathf.Max(0.01f, speed);
                arcElapsed = 0f;
            }
        }

        /// <summary>구버전 호환 — 공격 유형 미지정 시 Physical.</summary>
        public void Init(IDamageable target, float damage) => Init(target, damage, AttackType.Physical);

        private void Update()
        {
            if (Time.time - spawnedAt > lifeTime)
            {
                Destroy(gameObject);
                return;
            }

            if (arcHeight > 0f)
            {
                UpdateArc();
                return;
            }

            // --- 직선 호밍 (기존 동작) ---------------------------------------
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

            // 스프라이트가 오른쪽(+X)을 향한 상태 기준으로 회전 적용
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// 포물선 궤적 진행. 발사 시점에 고정된 시작·끝 위치 사이를
        /// 시간 비례로 보간하며, 중간에 arcHeight 만큼 위로 솟았다 떨어진다.
        /// </summary>
        private void UpdateArc()
        {
            arcElapsed += Time.deltaTime;
            float t = arcFlightTime > 0f ? arcElapsed / arcFlightTime : 1f;

            // 착탄: t >= 1 시점에 도착 위치에 데미지 적용 (타겟이 그때까지 살아있고 가까우면)
            if (t >= 1f)
            {
                transform.position = arcEndPos;
                if (target != null && (target as Object) != null && !target.IsDead)
                {
                    float distSq = (target.Position - arcEndPos).sqrMagnitude;
                    // hitRadius 의 3배까지는 관대하게 (보병이 살짝 움직였을 경우 보정)
                    float hr = hitRadius * 3f;
                    if (distSq <= hr * hr)
                        target.TakeDamage(damage, attackType);
                }
                Destroy(gameObject);
                return;
            }

            // 보간
            Vector3 prevPos = transform.position;
            Vector3 linearPos = Vector3.Lerp(arcStartPos, arcEndPos, t);
            // 포물선: y_offset = arcHeight * 4 * t * (1 - t). t=0 → 0, t=0.5 → max, t=1 → 0.
            float heightOffset = arcHeight * 4f * t * (1f - t);
            Vector3 newPos = new Vector3(linearPos.x, linearPos.y + heightOffset, linearPos.z);
            transform.position = newPos;

            // 화살 회전: 진행 방향(접선) 기준
            Vector3 dir = newPos - prevPos;
            if (dir.sqrMagnitude > 1e-6f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
