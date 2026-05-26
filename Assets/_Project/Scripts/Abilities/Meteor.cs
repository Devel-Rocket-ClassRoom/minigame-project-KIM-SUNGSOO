using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// 단일 유성. 화면 위쪽에서 시작해 fallDuration 동안 타겟 좌표로 떨어진 뒤,
    /// 도착 지점 주변 반경 내 적들에게 AoE 데미지를 가하고 사라진다.
    ///
    /// MeteorShowerAbility 가 시차를 두고 여러 개를 인스턴스화한다.
    /// </summary>
    public class Meteor : MonoBehaviour
    {
        [Header("낙하")]
        [Tooltip("위에서 떨어지는 출발 오프셋 (월드 단위). +Y 쪽이 위.")]
        [SerializeField] private Vector3 fallOffset = new Vector3(0f, 6f, 0f);

        [Tooltip("출발점 → 임팩트 지점까지 걸리는 시간(초).")]
        [SerializeField] private float fallDuration = 0.7f;

        [Header("임팩트 (런타임에 능력이 세팅)")]
        [Tooltip("임팩트 반경 안에 있는 적이 데미지를 받는다.")]
        [SerializeField] private float aoeRadius = 1.2f;

        [Tooltip("AoE 데미지.")]
        [SerializeField] private float damage = 6f;

        [Tooltip("공격 유형. Enemy 의 방어력 계산에 사용.")]
        [SerializeField] private AttackType attackType = AttackType.Magic;

        [Tooltip("(선택) 임팩트 위치에 잠시 띄울 폭발 이펙트 프리팹. null 이면 생략.")]
        [SerializeField] private GameObject impactVfxPrefab;

        [Tooltip("폭발 이펙트 자동 정리 시간(초).")]
        [SerializeField] private float impactVfxLifetime = 0.6f;

        private Vector3 startPos;
        private Vector3 impactPos;
        private float spawnedAt;
        private bool impacted;

        /// <summary>
        /// 능력에서 호출. 임팩트 좌표와 능력의 스탯을 주입.
        /// 호출 시 transform.position 은 시작 좌표로 세팅된다.
        /// </summary>
        public void Init(Vector3 worldImpactPos, float damage, float aoeRadius, AttackType attackType)
        {
            this.impactPos = worldImpactPos;
            this.damage = damage;
            this.aoeRadius = aoeRadius;
            this.attackType = attackType;

            startPos = worldImpactPos + fallOffset;
            transform.position = startPos;
            spawnedAt = Time.time;
        }

        private void Update()
        {
            if (impacted) return;

            float t = fallDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - spawnedAt) / fallDuration);
            transform.position = Vector3.Lerp(startPos, impactPos, t);

            if (t >= 1f) Impact();
        }

        private void Impact()
        {
            impacted = true;

            // 반경 안의 모든 Collider2D 중 Enemy 만 데미지.
            var hits = Physics2D.OverlapCircleAll(impactPos, aoeRadius);
            foreach (var col in hits)
            {
                if (col == null) continue;
                var enemy = col.GetComponent<Enemy>();
                if (enemy == null || enemy.IsDead) continue;
                enemy.TakeDamage(damage, attackType);
            }

            if (impactVfxPrefab != null)
            {
                var vfx = Instantiate(impactVfxPrefab, impactPos, Quaternion.identity);
                Destroy(vfx, impactVfxLifetime);
            }

            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
#endif
    }
}
