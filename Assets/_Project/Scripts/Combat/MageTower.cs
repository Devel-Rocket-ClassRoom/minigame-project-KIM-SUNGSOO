using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 사거리 내의 적을 자동으로 탐지해 마법 투사체를 발사하는 마법사 타워.
    /// ArcherTower와 동일한 자동 공격 패턴을 따르되, 화살 대신 마법 투사체를 던진다.
    /// 타겟팅 정책: 사거리 내에서 경로상 가장 앞선 적을 우선.
    ///
    /// 구조 권장:
    ///   MageTower (이 컴포넌트)
    ///   ├─ TowerBody  (SpriteRenderer - Blue Tower)
    ///   ├─ MageUnit   (SpriteRenderer + Animator - Monk Idle)
    ///   └─ FirePoint  (빈 Transform - 마법이 생성되는 위치, 보통 Monk 손 근처)
    /// </summary>
    public class MageTower : MonoBehaviour, ISelectableTower
    {
        [Header("스탯")]
        [SerializeField] private float range = 4.5f;
        [SerializeField] private float attackInterval = 1.6f;
        [SerializeField] private float damage = 5f;
        [Tooltip("이 타워의 공격 유형. 기본 Magic.")]
        [SerializeField] private AttackType attackType = AttackType.Magic;

        [Header("발사")]
        [Tooltip("발사할 마법 투사체 프리팹")]
        [SerializeField] private Magic magicPrefab;
        [Tooltip("마법이 생성될 위치. 비워두면 타워 transform 사용")]
        [SerializeField] private Transform firePoint;

        [Header("애니메이션 (선택)")]
        [Tooltip("발사 시 Trigger 를 호출할 Animator. 비워두면 무시.")]
        [SerializeField] private Animator unitAnimator;
        [Tooltip("발사 시 호출할 Trigger 파라미터 이름. Monk 컨트롤러에는 기본 트리거가 없으니 비워두면 호출하지 않는다.")]
        [SerializeField] private string castTrigger = "";

        [Header("사거리 표시 (런타임)")]
        [Tooltip("플레이어가 타워를 선택했을 때 보이는 사거리 원의 두께/색.")]
        [SerializeField] private float rangeLineWidth = 0.08f;
        [SerializeField] private Color rangeColor = new Color(0.6f, 0.4f, 1f, 0.85f);
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

        public void SetRangeVisible(bool visible)
        {
            rangeVisible = visible;
            if (rangeRenderer != null) rangeRenderer.enabled = visible;
        }

        public void ToggleRangeVisible()
        {
            SetRangeVisible(!rangeVisible);
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
            if (magicPrefab == null)
            {
                Debug.LogWarning($"[MageTower] magicPrefab이 비어있다: {name}");
                return;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Magic projectile = Instantiate(magicPrefab, spawnPos, Quaternion.identity);
            projectile.Init(target, damage, attackType);

            if (unitAnimator != null && !string.IsNullOrEmpty(castTrigger))
            {
                unitAnimator.SetTrigger(castTrigger);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.15f);
            Gizmos.DrawSphere(transform.position, range);
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, range);

            if (firePoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            }
        }
#endif
    }
}
