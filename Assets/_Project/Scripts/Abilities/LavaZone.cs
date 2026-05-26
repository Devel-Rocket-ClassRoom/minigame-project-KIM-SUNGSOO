using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// 일정 시간 동안 한 지점에 지속되며, 일정 간격으로 반경 내 적 전원에게 틱 데미지를
    /// 입히는 "장판" 오브젝트.
    ///
    /// LavaZoneAbility 가 클릭 지점에 인스턴스화 후 Init 으로 스탯을 주입한다.
    /// 시각요소(자식 SpriteRenderer/ParticleSystem 등) 는 보통 클릭 지점보다 위로 솟구치는
    /// 불꽃 같은 장식이라 실제 데미지 판정 영역을 정확히 보여주지 않는다. 그래서 LineRenderer
    /// 데미지 외곽선을 항상 클릭 지점 반경에 그려 플레이어가 판정 영역을 명확히 인지할 수 있게 한다.
    /// </summary>
    public class LavaZone : MonoBehaviour
    {
        [Header("스탯 (런타임에 능력이 세팅하므로 인스펙터 값은 무시될 수 있음)")]
        [SerializeField] private float radius = 1.5f;
        [SerializeField] private float damagePerTick = 1.5f;
        [SerializeField] private float tickInterval = 0.4f;
        [SerializeField] private float duration = 4f;
        [SerializeField] private AttackType attackType = AttackType.Magic;

        [Header("시각요소 자동 스케일")]
        [Tooltip("능력의 radius 가 바뀔 때 함께 균등 스케일될 자식 Transform. " +
            "프리팹 안의 모든 시각요소(Puddle/Flame 등) 의 공통 부모로 두면 한 번에 맞춰진다. " +
            "비워두면 스케일 동기화 없이 자식들이 원본 크기 그대로 보인다.")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("visualRoot 가 localScale=1 일 때의 기준 반경. " +
            "능력의 radius / referenceRadius 가 visualRoot 의 균등 스케일이 된다. " +
            "예: 프리팹을 radius=1.8 기준으로 만들었으면 1.8 입력.")]
        [Min(0.01f)]
        [SerializeField] private float referenceRadius = 1.8f;

        [Header("데미지 외곽선 (실제 판정 영역)")]
        [Tooltip("켜져 있으면 시각요소(Puddle/Flame 등) 와 무관하게 항상 클릭 지점 반경에 " +
            "LineRenderer 외곽선을 그려 정확한 데미지 영역을 보여준다. " +
            "끄면 자식 비주얼이 하나도 없을 때만 외곽선이 자동 생성된다 (구버전 동작).")]
        [SerializeField] private bool alwaysShowDamageOutline = true;

        [Tooltip("외곽선 색.")]
        [SerializeField] private Color outlineColor = new Color(1f, 0.55f, 0.1f, 1f);

        [Tooltip("외곽선 두께(월드 단위).")]
        [SerializeField] private float outlineWidth = 0.12f;

        [Tooltip("외곽선 분할 수. 클수록 원이 매끄럽지만 비용 증가.")]
        [SerializeField] private int outlineSegments = 48;

        private float spawnedAt;
        private float nextTickTime;
        private LineRenderer damageOutline;

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

            ApplyVisualScale();
            EnsureDamageOutline();
        }

        private void Awake()
        {
            // Init 이 호출되지 않은 채 인스펙터 직접 배치 케이스도 지원.
            spawnedAt = Time.time;
            nextTickTime = Time.time;
        }

        private void Start()
        {
            // Init 없이 인스펙터 배치된 경우에도 안전하게 시각 동기화.
            ApplyVisualScale();
            EnsureDamageOutline();
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
            // 기존 타워들(ArcherTower/MageTower) 과 동일하게 FindObjectsByType + 거리 비교 방식.
            // Physics2D.OverlapCircleAll 은 이 프로젝트의 적 셋업(콜라이더 미사용) 과 맞지 않는다.
            Vector3 center = transform.position;
            float rangeSq = radius * radius;

            // NOTE: 매 틱 FindObjectsByType 은 비효율적. 적 수가 많아지면 EnemyManager 등록 방식으로.
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                if ((e.Position - center).sqrMagnitude > rangeSq) continue;
                e.TakeDamage(damagePerTick, attackType);
            }
        }

        /// <summary>
        /// visualRoot 가 지정돼 있으면 radius / referenceRadius 배율로 균등 스케일.
        /// 디자이너가 프리팹을 referenceRadius 기준으로 한 번만 맞춰 두면, 능력 인스펙터의
        /// radius 가 바뀌어도 시각/판정이 자동으로 맞춰진다.
        /// </summary>
        private void ApplyVisualScale()
        {
            if (visualRoot == null) return;
            if (referenceRadius <= 0.001f) return;
            float scale = radius / referenceRadius;
            visualRoot.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 클릭 지점 반경에 데미지 영역 외곽선을 그린다.
        /// alwaysShowDamageOutline 이 켜져 있으면 시각요소 유무와 무관하게 항상 표시 — 시각이
        /// 솟구치는 불꽃처럼 클릭 지점 위쪽으로 오프셋돼 있어도 플레이어가 정확한 판정 영역을 본다.
        /// 꺼져 있으면 자식 비주얼이 하나도 없을 때만 자동 생성한다 (구버전 fallback 동작).
        /// </summary>
        private void EnsureDamageOutline()
        {
            if (!alwaysShowDamageOutline)
            {
                bool hasOwnVisual = false;
                foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) { if (sr != null) { hasOwnVisual = true; break; } }
                if (!hasOwnVisual)
                {
                    foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) { if (ps != null) { hasOwnVisual = true; break; } }
                }

                if (hasOwnVisual)
                {
                    if (damageOutline != null) { Destroy(damageOutline.gameObject); damageOutline = null; }
                    return;
                }
            }

            if (damageOutline == null)
            {
                var go = new GameObject("DamageOutline");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;   // 클릭 지점(=transform.position) 정중앙
                damageOutline = go.AddComponent<LineRenderer>();
                damageOutline.material = new Material(Shader.Find("Sprites/Default"));
                damageOutline.useWorldSpace = false;
                damageOutline.loop = true;
                damageOutline.sortingOrder = 100;            // 시각요소(Puddle=1, Flame=2) 위로 명확히
            }

            damageOutline.startWidth = outlineWidth;
            damageOutline.endWidth = outlineWidth;
            damageOutline.startColor = outlineColor;
            damageOutline.endColor = outlineColor;

            int seg = Mathf.Max(8, outlineSegments);
            damageOutline.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i * 2f * Mathf.PI / seg;
                damageOutline.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
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
