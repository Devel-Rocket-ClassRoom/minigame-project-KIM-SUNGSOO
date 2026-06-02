using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 사거리 내의 적을 자동으로 탐지해 화살을 발사하는 타워.
    /// 사거리 내에서 경로상 가장 앞선 적(곧 골인할 적)을 우선 대상으로 한다.
    ///
    /// 구조 권장:
    ///   ArcherTower (이 컴포넌트)
    ///   ├─ TowerBody  (SpriteRenderer - Blue Tower)
    ///   ├─ ArcherUnit (SpriteRenderer + Animator - Idle/Shoot)
    ///   └─ FirePoint  (빈 Transform - 화살이 생성되는 위치, 보통 Archer 손 근처)
    /// </summary>
    public class ArcherTower : MonoBehaviour, ISelectableTower, ICooldownPreservable
    {
        [Header("스탯")]
        [SerializeField] private float range = 4f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private float damage = 3f;
        [Tooltip("이 타워의 공격 유형. 기본 Physical.")]
        [SerializeField] private AttackType attackType = AttackType.Physical;

        [Header("발사")]
        [Tooltip("발사할 화살 프리팹")]
        [SerializeField] private Arrow arrowPrefab;
        [Tooltip("화살이 생성될 위치. 비워두면 타워 transform 사용")]
        [SerializeField] private Transform firePoint;

        [Header("애니메이션 (선택)")]
        [Tooltip("발사 시 Trigger 를 호출할 Animator. 비워두면 무시.")]
        [SerializeField] private Animator unitAnimator;
        [Tooltip("발사 시 호출할 Trigger 파라미터 이름")]
        [SerializeField] private string shootTrigger = "Shoot";

        [Header("사거리 표시 (런타임)")]
        [Tooltip("플레이어가 타워를 선택했을 때 보이는 사거리 원의 두께/색.")]
        [SerializeField] private float rangeLineWidth = 0.08f;
        [SerializeField] private Color rangeColor = new Color(0.3f, 1f, 0.6f, 0.85f);
        [SerializeField] private int rangeSegments = 48;

        private float nextFireTime;
        private LineRenderer rangeRenderer;
        private bool rangeVisible;

        private void Awake()
        {
            CreateRangeRenderer();
            SetRangeVisible(false);
        }

        private void Update()
        {
            if (Time.time < nextFireTime) return;

            Enemy target = FindLeadingEnemyInRange();
            if (target == null) return;

            Fire(target);
            nextFireTime = Time.time + attackInterval;
        }

        /// <summary>
        /// 사거리 원 표시 on/off. BuildSpot 의 클릭 핸들러에서 호출한다.
        /// </summary>
        public void SetRangeVisible(bool visible)
        {
            rangeVisible = visible;
            if (rangeRenderer != null) rangeRenderer.enabled = visible;
        }

        public void ToggleRangeVisible()
        {
            SetRangeVisible(!rangeVisible);
        }

        // ICooldownPreservable: 업그레이드 시 BuildSpot.ReplaceBuilding 이 옛 인스턴스에서 캡쳐 → 새 인스턴스에 복원.
        public float RemainingCooldown => Mathf.Max(0f, nextFireTime - Time.time);
        public void SetRemainingCooldown(float remaining)
        {
            nextFireTime = Time.time + Mathf.Max(0f, remaining);
        }

        private void CreateRangeRenderer()
        {
            var go = new GameObject("RangeIndicator");
            go.transform.SetParent(transform, false);

            rangeRenderer = go.AddComponent<LineRenderer>();
            rangeRenderer.useWorldSpace = false;
            rangeRenderer.loop = true;
            rangeRenderer.startWidth = rangeLineWidth;
            rangeRenderer.endWidth = rangeLineWidth;
            rangeRenderer.material = new Material(Shader.Find("Sprites/Default"));
            rangeRenderer.startColor = rangeColor;
            rangeRenderer.endColor = rangeColor;
            rangeRenderer.sortingOrder = 100;

            UpdateRangeCircle();
        }

        private void UpdateRangeCircle()
        {
            if (rangeRenderer == null) return;
            int segments = Mathf.Max(8, rangeSegments);
            rangeRenderer.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                rangeRenderer.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * range,
                    Mathf.Sin(angle) * range,
                    0f));
            }
        }

        private Enemy FindLeadingEnemyInRange()
        {
            Vector3 origin = transform.position;
            float rangeSq = range * range;
            Enemy best = null;

            // NOTE: 매 프레임 FindObjectsByType은 비효율적.
            //       적 수가 많아지면 EnemyManager에 등록/해제 방식으로 바꿀 것.
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                if ((e.Position - origin).sqrMagnitude > rangeSq) continue;

                if (e.IsAheadOf(best)) best = e;
            }
            return best;
        }

        private void Fire(Enemy target)
        {
            if (arrowPrefab == null)
            {
                Debug.LogWarning($"[ArcherTower] arrowPrefab이 비어있다: {name}");
                return;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Arrow arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
            arrow.Init(target, damage, attackType);

            // 발사 애니메이션 트리거 (있으면)
            if (unitAnimator != null && !string.IsNullOrEmpty(shootTrigger))
            {
                unitAnimator.SetTrigger(shootTrigger);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 사거리 원
            Gizmos.color = new Color(0f, 1f, 0.6f, 0.15f);
            Gizmos.DrawSphere(transform.position, range);
            Gizmos.color = new Color(0f, 1f, 0.6f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, range);

            // 발사 지점 표시
            if (firePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            }
        }
#endif
    }
}
