using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// 일정 시간 동안 한 지점에 지속되며, 일정 간격으로 반경 내 적 전원에게 틱 데미지를
    /// 입히는 "장판" 오브젝트.
    ///
    /// LavaZoneAbility 가 클릭 지점에 인스턴스화 후 Init 으로 스탯을 주입한다.
    /// 인스펙터 시각요소(자식 SpriteRenderer/ParticleSystem 등) 를 붙이면 그대로 보여지고,
    /// 비어있으면 LineRenderer 로 자동 원형 외곽선을 그려준다 (자산 없이도 동작 보장).
    /// </summary>
    public class LavaZone : MonoBehaviour
    {
        [Header("스탯 (런타임에 능력이 세팅하므로 인스펙터 값은 무시될 수 있음)")]
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float damagePerTick = 1.5f;
        [SerializeField] private float tickInterval = 0.4f;
        [SerializeField] private float duration = 4f;
        [SerializeField] private AttackType attackType = AttackType.Magic;

        [Header("기본 시각 (자식 비주얼이 없을 때 fallback)")]
        [Tooltip("LineRenderer 외곽선 색 (자동 생성될 때만 사용).")]
        [SerializeField] private Color fallbackOutlineColor = new Color(1f, 0.3f, 0.05f, 0.9f);
        [SerializeField] private float fallbackOutlineWidth = 0.1f;
        [SerializeField] private int fallbackSegments = 48;

        private float spawnedAt;
        private float nextTickTime;
        private LineRenderer fallbackOutline;

        /// <summary>
        /// 능력에서 호출. 스탯 주입 + 시각요소 갱신. transform.position 은 이미 클릭 지점.
        /// </summary>
        public void Init(float radius, float damagePerTick, float tickInterval, float duration, AttackType attackType)
        {
            this.radius = radius;
            this.damagePerTick = damagePerTick;
            this.tickInterval = Mathf.Max(0.05f, tickInterval);
            this.duration = duration;
            this.attackType = attackType;

            spawnedAt = Time.time;
            // 첫 틱은 곧바로 한 번 발생시켜 "들어서자마자 데미지" 체감을 준다.
            nextTickTime = Time.time;

            EnsureFallbackOutline();
        }

        private void Awake()
        {
            // Init 이 호출되지 않은 채 인스펙터 직접 배치 케이스도 지원.
            spawnedAt = Time.time;
            nextTickTime = Time.time;
        }

        private void Start()
        {
            EnsureFallbackOutline();
        }

        private void Update()
        {
            // 1. 만료 확인
            if (Time.time - spawnedAt >= duration)
            {
                Destroy(gameObject);
                return;
            }

            // 2. 틱 데미지
            if (Time.time >= nextTickTime)
            {
                ApplyTick();
                nextTickTime += tickInterval;
                // 시간이 크게 밀린 경우 다음 틱이 과거가 되지 않도록 보정.
                if (nextTickTime < Time.time) nextTickTime = Time.time + tickInterval;
            }
        }

        private void ApplyTick()
        {
            // 반경 안 모든 Collider2D 중 Enemy 만 골라 데미지.
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                var enemy = col.GetComponent<Enemy>();
                if (enemy == null || enemy.IsDead) continue;
                enemy.TakeDamage(damagePerTick, attackType);
            }
        }

        /// <summary>
        /// 자식 시각요소(SpriteRenderer/ParticleSystem 등) 가 하나도 없을 때만
        /// LineRenderer 외곽선을 자동 생성해 사용자가 어디에 시전됐는지 인지 가능하게 한다.
        /// </summary>
        private void EnsureFallbackOutline()
        {
            bool hasOwnVisual = false;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) { if (sr != null) { hasOwnVisual = true; break; } }
            if (!hasOwnVisual)
            {
                foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) { if (ps != null) { hasOwnVisual = true; break; } }
            }

            if (hasOwnVisual)
            {
                // 외곽선이 이전에 만들어졌다면 정리.
                if (fallbackOutline != null) { Destroy(fallbackOutline.gameObject); fallbackOutline = null; }
                return;
            }

            if (fallbackOutline == null)
            {
                var go = new GameObject("FallbackOutline");
                go.transform.SetParent(transform, false);
                fallbackOutline = go.AddComponent<LineRenderer>();
                fallbackOutline.material = new Material(Shader.Find("Sprites/Default"));
                fallbackOutline.useWorldSpace = false;
                fallbackOutline.loop = true;
                fallbackOutline.sortingOrder = 50;
            }

            fallbackOutline.startWidth = fallbackOutlineWidth;
            fallbackOutline.endWidth = fallbackOutlineWidth;
            fallbackOutline.startColor = fallbackOutlineColor;
            fallbackOutline.endColor = fallbackOutlineColor;

            int seg = Mathf.Max(8, fallbackSegments);
            fallbackOutline.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i * 2f * Mathf.PI / seg;
                fallbackOutline.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.05f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
