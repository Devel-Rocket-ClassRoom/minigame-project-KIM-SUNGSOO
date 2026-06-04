using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 플레이어가 직접 위치를 지정하는 영웅 유닛.
    /// 경로(EnemyPath) 위에 랠리를 잡고, 사거리 안 적을 자동 공격한다.
    /// 사망 시 게임오버 X — respawnDelay 후 시작 위치(또는 마지막 랠리)에 자동 부활.
    ///
    /// 책임:
    ///   - HeroData 의 스탯으로 동작
    ///   - 외부(HeroPathRallyController) 가 SetRally 로 새 위치 지정 → 영웅이 걸어서 이동
    ///   - 도착 후엔 가장 가까운 적을 currentTarget 으로 잡고 attackInterval 마다 공격
    ///   - HP 0 → Die → respawnDelay 후 Respawn (인스턴스 재사용)
    /// </summary>
    public class Hero : MonoBehaviour, IDamageable
    {
        [Header("데이터")]
        [SerializeField] private HeroData data;

        [Header("Fallback 스탯 (data 가 없을 때만 사용)")]
        [SerializeField] private float maxHp = 30f;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackInterval = 0.8f;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private AttackType attackType = AttackType.Physical;
        [SerializeField] private float physicalDefense = 1f;
        [SerializeField] private float magicDefense = 1f;
        [SerializeField] private float minDamage = 1f;
        [SerializeField] private float respawnDelay = 5f;
        [SerializeField] private bool respawnAtLastRally = true;

        [Header("랠리 / 이동")]
        [Tooltip("랠리 위치에 이만큼 가까워지면 도착으로 판정.")]
        [SerializeField] private float rallyArriveRadius = 0.05f;

        [Header("좌우 방향 반전")]
        [Tooltip("진행/타겟 방향에 따라 좌우 반전할 시각 Transform. 비워두면 자기 자신.")]
        [SerializeField] private Transform visualRoot;

        [Header("애니메이션 (선택)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string runBool = "isRunning";
        [SerializeField] private string attackBool = "isAttacking";
        [SerializeField] private string attackTrigger = "";
        [SerializeField] private string deathTrigger = "Death";

        [Header("부활 시각 처리")]
        [Tooltip("사망 동안 자식 visualRoot 를 비활성화해서 시각적으로 사라지게 한다.")]
        [SerializeField] private bool hideVisualWhileDead = true;

        // --- 런타임 상태 ---
        private float currentHp;
        private float nextAttackTime;
        private Enemy currentTarget;
        private bool isDead;
        private float respawnAt;
        private Vector3 rallyPoint;
        private Vector3 spawnPoint;
        private bool hasArrivedAtRally;

        // 씬에 단일 인스턴스 가정 — UI/컨트롤러가 정적 슬롯으로 접근.
        public static Hero Instance { get; private set; }

        public bool IsDead => isDead;
        public Vector3 Position => transform.position;
        public Vector3 RallyPoint => rallyPoint;

        public float HpRatio
        {
            get
            {
                float max = ResolveMaxHp();
                return max > 0f ? Mathf.Clamp01(currentHp / max) : 0f;
            }
        }

        /// <summary>사망 후 남은 부활 대기 초. 살아있으면 0.</summary>
        public float RespawnRemaining => isDead ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState() { Instance = null; }

        private void Awake()
        {
            // 단일 인스턴스 — 두 번째가 생기면 자기 자신을 정리한다.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Hero] 이미 활성 Hero 가 있습니다. 새 인스턴스 파괴.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            currentHp = ResolveMaxHp();
            spawnPoint = transform.position;
            rallyPoint = spawnPoint;
            hasArrivedAtRally = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>HeroPathRallyController 가 호출. 새 랠리 위치를 지정 — 영웅은 걸어서 이동한다.</summary>
        public void SetRally(Vector3 worldPos)
        {
            if (isDead) return;
            rallyPoint = worldPos;
            // 현재 위치에서 멀면 도착 상태 해제 (걸어가야 함). 가까우면 그대로 도착으로.
            hasArrivedAtRally =
                (rallyPoint - transform.position).sqrMagnitude
                <= rallyArriveRadius * rallyArriveRadius;

            // 이동 모드 진입 시 교전 컨텍스트는 끊는다 — 새 위치에 도착 후 다시 잡는다.
            currentTarget = null;
        }

        private void Update()
        {
            // 사망 → 부활 카운트다운 처리만.
            if (isDead)
            {
                if (Time.time >= respawnAt) Respawn();
                return;
            }

            bool nextIsRunning = false;
            bool nextIsAttacking = false;

            // 1. 랠리 이동 중 — 적과의 상호작용 일시 중지.
            if (!hasArrivedAtRally)
            {
                Vector3 toRally = rallyPoint - transform.position;
                if (toRally.sqrMagnitude <= rallyArriveRadius * rallyArriveRadius)
                {
                    hasArrivedAtRally = true;
                }
                else
                {
                    nextIsRunning = true;
                    UpdateFacing(toRally.x);
                    MoveToward(rallyPoint);
                    SyncAnimator(nextIsRunning, nextIsAttacking);
                    return;
                }
            }

            // 2. 랠리 도착 — 사거리 안 적 자동 공격.
            if (currentTarget == null || currentTarget.IsDead || !IsInAttackRange(currentTarget))
                currentTarget = FindNearestEnemyInAttackRange();

            if (currentTarget != null)
            {
                UpdateFacing(currentTarget.Position.x - transform.position.x);
                nextIsAttacking = true;
                if (Time.time >= nextAttackTime)
                {
                    Attack(currentTarget);
                    nextAttackTime = Time.time + ResolveAttackInterval();
                }
            }

            SyncAnimator(nextIsRunning, nextIsAttacking);
        }

        // --- 적 탐지/공격 -------------------------------------------------------

        private bool IsInAttackRange(Enemy e)
        {
            float r = ResolveAttackRange();
            return (e.Position - transform.position).sqrMagnitude <= r * r;
        }

        private Enemy FindNearestEnemyInAttackRange()
        {
            Vector3 origin = transform.position;
            float r = ResolveAttackRange();
            float rangeSq = r * r;
            Enemy nearest = null;
            float bestDistSq = float.MaxValue;

            // NOTE: 매 프레임 FindObjectsByType 는 비효율 — 적 수 늘면 매니저 등록 방식으로 교체.
            //       Soldier.FindNearestEnemyInDetection 과 동일 NOTE.
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead || e.HasReachedEnd) continue;
                float d = (e.Position - origin).sqrMagnitude;
                if (d > rangeSq) continue;
                if (d < bestDistSq) { bestDistSq = d; nearest = e; }
            }
            return nearest;
        }

        private void Attack(Enemy target)
        {
            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);
            target.TakeDamage(ResolveDamage(), ResolveAttackType());
        }

        // --- 피격/사망/부활 -----------------------------------------------------

        public void TakeDamage(float amount, AttackType type)
        {
            if (isDead) return;
            float defense = type == AttackType.Magic ? ResolveMagicDefense() : ResolvePhysicalDefense();
            float effective = Mathf.Max(ResolveMinDamage(), amount - defense);
            currentHp -= effective;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        public void TakeDamage(float amount) => TakeDamage(amount, AttackType.Physical);

        private void Die()
        {
            isDead = true;
            currentTarget = null;
            respawnAt = Time.time + ResolveRespawnDelay();

            if (animator != null && !string.IsNullOrEmpty(deathTrigger))
                animator.SetTrigger(deathTrigger);

            if (hideVisualWhileDead)
            {
                Transform t = visualRoot != null ? visualRoot : transform;
                t.gameObject.SetActive(false);
            }
        }

        private void Respawn()
        {
            isDead = false;
            currentHp = ResolveMaxHp();
            nextAttackTime = 0f;
            currentTarget = null;

            // 부활 위치 결정 — data 기본은 마지막 랠리, false 면 시작 위치.
            Vector3 spawnAt = ResolveRespawnAtLastRally() ? rallyPoint : spawnPoint;
            transform.position = spawnAt;
            rallyPoint = spawnAt;
            hasArrivedAtRally = true;

            if (hideVisualWhileDead)
            {
                Transform t = visualRoot != null ? visualRoot : transform;
                t.gameObject.SetActive(true);
            }
        }

        // --- 이동/시각 ----------------------------------------------------------

        private void MoveToward(Vector3 worldPos)
        {
            Vector3 toTarget = worldPos - transform.position;
            float dist = toTarget.magnitude;
            if (dist < 1e-4f) return;
            float step = ResolveMoveSpeed() * Time.deltaTime;
            transform.position += toTarget / dist * Mathf.Min(step, dist);
        }

        private void UpdateFacing(float dirX)
        {
            if (Mathf.Abs(dirX) < 1e-4f) return;
            Transform t = visualRoot != null ? visualRoot : transform;
            Vector3 scale = t.localScale;
            float abs = Mathf.Abs(scale.x);
            scale.x = dirX > 0 ? abs : -abs;
            t.localScale = scale;
        }

        private void SyncAnimator(bool running, bool attacking)
        {
            if (animator == null) return;
            if (!string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, running);
            if (!string.IsNullOrEmpty(attackBool)) animator.SetBool(attackBool, attacking);
        }

        // --- 데이터/Fallback 해석 헬퍼 ------------------------------------------

        private float ResolveMaxHp() => data != null ? data.maxHp : maxHp;
        private float ResolveDamage() => data != null ? data.damage : damage;
        private float ResolveAttackRange() => data != null ? data.attackRange : attackRange;
        private float ResolveAttackInterval() => data != null ? data.attackInterval : attackInterval;
        private float ResolveMoveSpeed() => data != null ? data.moveSpeed : moveSpeed;
        private AttackType ResolveAttackType() => data != null ? data.attackType : attackType;
        private float ResolvePhysicalDefense() => data != null ? data.physicalDefense : physicalDefense;
        private float ResolveMagicDefense() => data != null ? data.magicDefense : magicDefense;
        private float ResolveMinDamage() => data != null ? data.minDamage : minDamage;
        private float ResolveRespawnDelay() => data != null ? data.respawnDelay : respawnDelay;
        private bool ResolveRespawnAtLastRally() => data != null ? data.respawnAtLastRally : respawnAtLastRally;
    }
}
