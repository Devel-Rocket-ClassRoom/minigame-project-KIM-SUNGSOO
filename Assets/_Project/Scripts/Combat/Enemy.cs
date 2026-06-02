using UnityEngine;
using KRTD.Game;
using KRTD.Map;

namespace KRTD.Combat
{
    /// <summary>
    /// 적 유닛. EnemyPath 의 웨이포인트를 순서대로 따라 이동하며,
    /// 골인 시 GameState 의 생명을 깎고, 처치되면 골드를 보상한다.
    ///
    /// 초기화 흐름:
    ///   EnemySpawner.Spawn(...) → Instantiate → Enemy.Init(data, path)
    ///   Init 이 호출되지 않은 경우(에디터 직접 배치) 인스펙터 fallback 값으로 동작.
    /// </summary>
    public class Enemy : MonoBehaviour, IDamageable
    {
        [Header("데이터 (없으면 아래 fallback 사용)")]
        [SerializeField] private EnemyData data;

        [Header("Fallback 스탯 (data 가 없을 때만 사용)")]
        [SerializeField] private float maxHp = 10f;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private int goldReward = 5;
        [SerializeField] private int lifeDamage = 1;
        [SerializeField] private float physicalDefense = 0f;
        [SerializeField] private float magicDefense = 0f;
        [SerializeField] private float minDamage = 1f;
        [SerializeField] private float attackDamage = 0f;
        [SerializeField] private float attackRange = 0.8f;
        [SerializeField] private float detectionRange = 2.5f;
        [SerializeField] private float attackInterval = 1f;
        [Tooltip("사거리에 막 진입한 직후 첫 데미지가 나가기까지의 최소 대기 시간(초). " +
            "Animator Run→Attack 블렌드 시간 이상으로 잡아 공격 모션이 반드시 보이도록 한다.")]
        [SerializeField] private float attackWindupSeconds = 0.25f;
        [SerializeField] private AttackType attackType = AttackType.Physical;
        [Tooltip("원거리 공격용 투사체 프리팹. 비어있으면 근접(즉시) 데미지.")]
        [SerializeField] private Arrow arrowPrefab;
        [Tooltip("0 이면 힐러 아님. 0 보다 크면 healInterval 마다 사거리 안 가장 많이 다친 아군 적을 회복.")]
        [SerializeField] private float healAmount = 0f;
        [SerializeField] private float healRange = 2.5f;
        [SerializeField] private float healInterval = 2f;
        [Tooltip("힐 시전 동안 멈춰 있는 시간(초). Heal 애니메이션 길이와 맞춘다.")]
        [SerializeField] private float healCastDuration = 1.1f;

        [Header("이동")]
        [Tooltip("웨이포인트에 이만큼 가까워지면 다음 점으로 진행")]
        [SerializeField] private float waypointReachRadius = 0.05f;

        [Header("애니메이션 (선택)")]
        [Tooltip("Run / Attack 두 상태를 가진 Animator. 비워두면 무시.")]
        [SerializeField] private Animator animator;
        [Tooltip("Run ⇄ Attack 전환용 Bool 파라미터 이름. 전투 중이면 true, 아니면 false.")]
        [SerializeField] private string isAttackingBool = "isAttacking";
        [Tooltip("사망 시 호출할 Trigger 파라미터 이름. 비워두면 무시.")]
        [SerializeField] private string deathTrigger = "";
        [Tooltip("힐 시전 시 호출할 Trigger 파라미터 이름. 컨트롤러에 파라미터가 없으면 비워둔다(Monk 기본).")]
        [SerializeField] private string healTrigger = "";

        [Header("좌우 방향 반전")]
        [Tooltip("진행 방향에 따라 좌우 반전할 시각 Transform (보통 자식 Body). " +
            "비워두면 자기 자신 transform 사용. 기본 스프라이트가 오른쪽(+X) 향한다고 가정.")]
        [SerializeField] private Transform visualRoot;

        private float currentHp;
        private EnemyPath path;
        private int nextWaypointIndex;
        private bool reachedEnd;

        // 보병 공격 모드 상태
        private Soldier currentSoldierTarget;
        private float nextAttackTime;

        // 1:1 페어 락. 이 적을 currentTarget 으로 잡고 있는 보병이 있다면 그 인스턴스.
        // null 이면 자유 상태 — 다른 보병이 후보로 잡을 수 있다.
        // Soldier 측에서 SetTargetedBy 로 설정/해제한다.
        private Soldier targetedBy;

        // 직전 프레임에 보병이 공격 사거리 안이었는지 — 사거리 밖→안 진입 시 windup 적용용.
        private bool wasSoldierInAttackRange;

        // 힐러 모드 상태
        private float nextHealTime;
        private bool isHealing;     // 힐 모션 시전 중(이동 멈춤)
        private float healEndTime;  // 이 시각이 지나면 시전 종료 후 다시 이동

        public bool IsDead => currentHp <= 0f;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 이 적을 노리고 있는 보병(있다면). 1:1 페어 정책: 다른 보병은 이 적을 후보에 넣지 않는다.
        /// null 이면 자유.
        /// </summary>
        public Soldier TargetedBy => targetedBy;

        /// <summary>
        /// Soldier 측에서 \"이 적을 내 currentTarget 으로 잡았다 / 풀었다\" 알릴 때 호출.
        /// null 을 넣으면 페어 해제. Soldier.SetCurrentTarget 이 자동으로 갱신한다 — 외부 직접 호출 비권장.
        /// </summary>
        public void SetTargetedBy(Soldier s) { targetedBy = s; }

        // 필드에 살아있는 적 수 — GameOutcomeWatcher 가 승리 조건 판정에 사용.
        // OnEnable/OnDisable 로 증감 (Destroy/Pool 어느 쪽이든 동기화).
        public static int AliveCount { get; private set; }

        // Domain Reload 가 꺼진 경우(Enter Play Mode Options) 정적 값이 남아 다음 플레이에 누적되는 걸 방지.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticState() { AliveCount = 0; }

        private void OnEnable() { AliveCount++; }
        private void OnDisable() { AliveCount = Mathf.Max(0, AliveCount - 1); }

        /// <summary>골인 처리됐는지. 골인한 적은 힐 대상에서 제외된다.</summary>
        public bool HasReachedEnd => reachedEnd;

        /// <summary>현재 체력이 최대치보다 적으면(=다쳤으면) true.</summary>
        public bool IsWounded => currentHp < ResolveMaxHp();

        /// <summary>0~1 체력 비율. 힐러가 "가장 많이 다친" 대상을 고를 때 사용.</summary>
        public float HpRatio
        {
            get
            {
                float max = ResolveMaxHp();
                return max > 0f ? currentHp / max : 1f;
            }
        }

        /// <summary>
        /// 체력 회복. 최대 체력을 넘지 않으며, 죽었거나 골인한 적에게는 무효.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || reachedEnd || amount <= 0f) return;
            currentHp = Mathf.Min(ResolveMaxHp(), currentHp + amount);
        }

        /// <summary>
        /// 현재 향하고 있는 다음 웨이포인트의 인덱스. 클수록 경로상 더 앞서 있다.
        /// path 가 없거나 골인한 적은 0/마지막 값으로 고정된다.
        /// </summary>
        public int WaypointIndex => nextWaypointIndex;

        /// <summary>
        /// 현재 위치에서 다음 웨이포인트까지 남은 직선 거리. WaypointIndex 가 같을 때
        /// 더 작은 쪽이 경로상 더 앞서 있다고 본다.
        /// path 미설정 시 0 으로 간주.
        /// </summary>
        public float DistanceToNextWaypoint
        {
            get
            {
                if (path == null || path.Count == 0) return 0f;
                return (path.GetPoint(nextWaypointIndex) - transform.position).magnitude;
            }
        }

        /// <summary>
        /// 이 적이 other 보다 경로상 더 앞서 있으면 true.
        /// 비교 키: WaypointIndex 내림차순 → DistanceToNextWaypoint 오름차순.
        /// other 가 null 이면 항상 true (첫 후보).
        /// </summary>
        public bool IsAheadOf(Enemy other)
        {
            if (other == null) return true;
            if (WaypointIndex != other.WaypointIndex) return WaypointIndex > other.WaypointIndex;
            return DistanceToNextWaypoint < other.DistanceToNextWaypoint;
        }

        private void Awake()
        {
            // 데이터가 있으면 스탯/시각 동기화. 없으면 fallback 값 그대로.
            ApplyDataIfPresent();
            currentHp = ResolveMaxHp();
        }

        // WaveDirector 의 hpMultiplier 곡선에서 전달된 배율. ResolveMaxHp 에 곱해진다.
        private float hpMultiplier = 1f;

        /// <summary>
        /// 스포너에서 호출. 데이터와 경로를 주입하고 스폰 위치로 이동.
        /// hpMultiplier 는 WaveDirector 의 난이도 곡선에서 전달 (기본 1).
        /// </summary>
        public void Init(EnemyData data, EnemyPath path, float hpMultiplier = 1f)
        {
            this.data = data;
            this.path = path;
            this.hpMultiplier = Mathf.Max(0.01f, hpMultiplier);

            ApplyDataIfPresent();
            currentHp = ResolveMaxHp();
            nextWaypointIndex = 0;
            reachedEnd = false;

            // 스폰 위치 = 경로 시작점
            if (path != null && path.Count > 0)
            {
                transform.position = path.SpawnPoint;
                // 첫 프레임에 곧바로 1번 웨이포인트를 향해 출발하도록 인덱스를 1로 둔다.
                nextWaypointIndex = Mathf.Min(1, path.Count - 1);
            }
        }

        private void Update()
        {
            if (IsDead || reachedEnd) return;

            // 1. 탐지범위 안 보병이 있으면 멈춤. 공격범위 안에 들어왔을 때만 실제 데미지.
            //    공격력 0 인 적은 둘 다 패스 (그냥 지나가는 적).
            bool engaging = false;
            if (ResolveAttackDamage() > 0f)
            {
                if (currentSoldierTarget == null || currentSoldierTarget.IsDead || !IsSoldierInDetection(currentSoldierTarget))
                {
                    SetCurrentSoldierTarget(FindNearestSoldierInDetection());
                    // 타겟이 끊기면 사거리 상태도 리셋 — 새 타겟의 첫 진입 때 다시 windup.
                    if (currentSoldierTarget == null) wasSoldierInAttackRange = false;
                }

                if (currentSoldierTarget != null)
                {
                    engaging = true; // 탐지됨 → 무조건 멈춤
                    UpdateFacing(currentSoldierTarget.Position.x - transform.position.x);

                    bool inAttackRange = IsSoldierInAttackRange(currentSoldierTarget);
                    if (inAttackRange && !wasSoldierInAttackRange)
                    {
                        // 사거리 진입 첫 프레임: 누적 쿨다운으로 즉발타가 나가지 않도록 windup 만큼 강제 대기.
                        // Animator Run→Attack 블렌드 시간을 벌어 \"멈춤 → 모션 → 데미지\" 순서 보장.
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + ResolveAttackWindup());
                    }
                    wasSoldierInAttackRange = inAttackRange;

                    if (inAttackRange && Time.time >= nextAttackTime)
                    {
                        AttackSoldier(currentSoldierTarget);
                        nextAttackTime = Time.time + ResolveAttackInterval();
                    }
                    // 탐지는 됐지만 아직 공격범위 밖이면 대기 (보병이 다가올 때까지)
                }
            }

            // 1-b. 힐러: 다친 아군 적을 회복. 시전하는 동안(healCastDuration) 멈춰서 힐 모션을 재생하고,
            //      모션이 끝나면 다시 이동한다.
            if (ResolveHealAmount() > 0f)
            {
                if (isHealing)
                {
                    // 시전 중 — 모션이 끝날 때까지 멈춰 있는다.
                    if (Time.time >= healEndTime) isHealing = false;
                    else engaging = true; // 이번 프레임 이동 정지
                }
                else if (Time.time >= nextHealTime)
                {
                    Enemy ally = FindMostWoundedAllyInHealRange();
                    if (ally != null)
                    {
                        ally.Heal(ResolveHealAmount());
                        nextHealTime = Time.time + ResolveHealInterval();

                        // 시전 시작: 멈추고, 대상을 바라보며, 힐 애니메이션 트리거.
                        isHealing = true;
                        healEndTime = Time.time + ResolveHealCastDuration();
                        engaging = true;
                        UpdateFacing(ally.Position.x - transform.position.x);
                        if (animator != null && !string.IsNullOrEmpty(healTrigger))
                            animator.SetTrigger(healTrigger);
                    }
                }
            }

            // Run / Attack 전환을 Animator 에 알린다 (Attack 시 멈춰 있으므로 Run 루프가 어색하지 않게)
            if (animator != null && !string.IsNullOrEmpty(isAttackingBool))
                animator.SetBool(isAttackingBool, engaging);

            if (engaging) return; // 전투 중엔 이동 안 함

            // 2. 보병 없음 → 경로 따라 이동
            if (path == null) return;

            Vector3 target = path.GetPoint(nextWaypointIndex);
            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;
            float step = ResolveMoveSpeed() * Time.deltaTime;

            UpdateFacing(toTarget.x);

            if (dist <= step + waypointReachRadius)
            {
                // 이 웨이포인트 도착: 다음으로 진행하거나, 마지막이면 골인 처리.
                transform.position = target;
                if (nextWaypointIndex >= path.Count - 1)
                {
                    ReachEnd();
                    return;
                }
                nextWaypointIndex++;
                return;
            }

            transform.position += toTarget / dist * step;
        }

        /// <summary>
        /// 진행 방향(또는 타겟 방향) 의 X 부호에 따라 visualRoot 의 localScale.x 를 ±|x| 로 설정.
        /// 기본 스프라이트가 오른쪽 향한다고 가정 — 음수 X 면 좌우반전.
        /// </summary>
        private void UpdateFacing(float dirX)
        {
            if (Mathf.Abs(dirX) < 1e-4f) return; // 거의 0 방향은 무시 (현재 facing 유지)
            Transform t = visualRoot != null ? visualRoot : transform;
            Vector3 scale = t.localScale;
            float abs = Mathf.Abs(scale.x);
            scale.x = dirX > 0 ? abs : -abs;
            t.localScale = scale;
        }

        // --- 보병 공격 -------------------------------------------------------

        /// <summary>
        /// currentSoldierTarget 갱신. 이전 보병의 TargetedBy 를 풀고, 새 보병의 TargetedBy 를 this 로 설정해
        /// 1:1 페어 lock 을 유지한다. 모든 currentSoldierTarget 변경은 이 메서드로만 한다.
        /// internal: Soldier.Die 의 cascading 정리에서 호출.
        /// </summary>
        internal void SetCurrentSoldierTarget(Soldier newTarget)
        {
            if (currentSoldierTarget == newTarget) return;
            if (currentSoldierTarget != null && currentSoldierTarget.TargetedBy == this)
                currentSoldierTarget.SetTargetedBy(null);
            currentSoldierTarget = newTarget;
            if (currentSoldierTarget != null)
                currentSoldierTarget.SetTargetedBy(this);
        }

        private bool IsSoldierInAttackRange(Soldier s)
        {
            float r = ResolveAttackRange();
            return (s.Position - transform.position).sqrMagnitude <= r * r;
        }

        private bool IsSoldierInDetection(Soldier s)
        {
            float r = ResolveDetectionRange();
            return (s.Position - transform.position).sqrMagnitude <= r * r;
        }

        private Soldier FindNearestSoldierInDetection()
        {
            Vector3 origin = transform.position;
            float r = ResolveDetectionRange();
            float rangeSq = r * r;
            Soldier nearest = null;
            float bestDistSq = float.MaxValue;

            // NOTE: 매 프레임 FindObjectsByType 는 비효율적. 보병 수 늘면 매니저 등록 방식으로 교체.
            Soldier[] soldiers = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None);
            foreach (var s in soldiers)
            {
                if (s == null || s.IsDead) continue;
                // 배치 중인 보병(아직 랠리에 도달 못함)은 적에게도 보이지 않는다 — 무시.
                if (s.IsDeploying) continue;
                // 1:1 페어 정책: 이미 다른 적이 잡고 있는 보병은 후보에서 제외.
                if (s.TargetedBy != null && s.TargetedBy != this) continue;
                float d = (s.Position - origin).sqrMagnitude;
                if (d > rangeSq) continue;
                if (d < bestDistSq) { bestDistSq = d; nearest = s; }
            }
            return nearest;
        }

        // --- 힐러 -----------------------------------------------------------

        /// <summary>
        /// healRange 안에서 가장 많이 다친(HpRatio 가 가장 낮은) 아군 적을 찾는다.
        /// 자기 자신, 죽었거나 골인한 적, 멀쩡한(다치지 않은) 적은 제외.
        /// </summary>
        private Enemy FindMostWoundedAllyInHealRange()
        {
            Vector3 origin = transform.position;
            float r = ResolveHealRange();
            float rangeSq = r * r;
            Enemy best = null;
            float bestRatio = float.MaxValue;

            // NOTE: 매 시전마다 FindObjectsByType 는 비효율적. 적 수 늘면 매니저 등록 방식으로 교체.
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e == this) continue;
                if (e.IsDead || e.HasReachedEnd || !e.IsWounded) continue;
                if ((e.Position - origin).sqrMagnitude > rangeSq) continue;
                if (e.HpRatio < bestRatio) { bestRatio = e.HpRatio; best = e; }
            }
            return best;
        }

        private void AttackSoldier(Soldier s)
        {
            Arrow prefab = ResolveArrowPrefab();
            if (prefab != null)
            {
                // 원거리: 투사체 발사. 화살이 보병에게 도달하면 그 시점에 데미지가 들어간다.
                var arrow = Instantiate(prefab, transform.position, Quaternion.identity);
                arrow.Init(s, ResolveAttackDamage(), ResolveAttackType());
            }
            else
            {
                // 근접: 즉시 데미지.
                s.TakeDamage(ResolveAttackDamage(), ResolveAttackType());
            }
        }

        /// <summary>
        /// 데미지 적용. 공격 유형에 따라 방어력(flat 차감) 적용 후 최소 데미지로 클램프.
        /// 식: effective = max(minDamage, damage - defense(attackType))
        /// </summary>
        public void TakeDamage(float damage, AttackType attackType)
        {
            if (IsDead || reachedEnd) return;

            float defense = ResolveDefense(attackType);
            float effective = Mathf.Max(ResolveMinDamage(), damage - defense);

            currentHp -= effective;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        /// <summary>공격 유형이 명시되지 않은 외부 호출 호환용. Physical 로 간주.</summary>
        public void TakeDamage(float damage) => TakeDamage(damage, AttackType.Physical);

        private void Die()
        {
            // 1:1 페어 lock 해제 — 내가 노리던 보병의 TargetedBy 풀기.
            SetCurrentSoldierTarget(null);
            // 나를 노리던 보병도 자기 currentTarget 을 즉시 풀어 다른 적을 잡을 수 있게.
            if (targetedBy != null) targetedBy.SetCurrentTarget(null);

            // 처치 보상.
            var state = GameState.Instance;
            if (state != null) state.AddGold(ResolveGoldReward());

            if (animator != null && !string.IsNullOrEmpty(deathTrigger))
                animator.SetTrigger(deathTrigger);

            Destroy(gameObject);
        }

        private void ReachEnd()
        {
            reachedEnd = true;
            // 골인 시점에도 페어 lock 정리 — 보병이 헛 swing 하지 않도록.
            SetCurrentSoldierTarget(null);
            if (targetedBy != null) targetedBy.SetCurrentTarget(null);

            var state = GameState.Instance;
            if (state != null) state.LoseLife(ResolveLifeDamage());

            // 골인한 적은 보상 없이 사라진다.
            Destroy(gameObject);
        }

        // --- 데이터/Fallback 해석 헬퍼 -----------------------------------------

        private void ApplyDataIfPresent()
        {
            if (data == null) return;
            // 데이터가 있는 경우 fallback 필드를 데이터 값으로 갱신해 두면
            // 인스펙터에서도 현재 값이 보여 디버깅이 쉽다.
            maxHp = data.maxHp;
            moveSpeed = data.moveSpeed;
            goldReward = data.goldReward;
            lifeDamage = data.lifeDamage;
            physicalDefense = data.physicalDefense;
            magicDefense = data.magicDefense;
            minDamage = data.minDamage;
            attackDamage = data.attackDamage;
            attackRange = data.attackRange;
            detectionRange = data.detectionRange;
            attackInterval = data.attackInterval;
            attackWindupSeconds = data.attackWindupSeconds;
            attackType = data.attackType;
            arrowPrefab = data.arrowPrefab;
            healAmount = data.healAmount;
            healRange = data.healRange;
            healInterval = data.healInterval;
            healCastDuration = data.healCastDuration;
        }

        private float ResolveMaxHp() => (data != null ? data.maxHp : maxHp) * hpMultiplier;
        private float ResolveMoveSpeed() => data != null ? data.moveSpeed : moveSpeed;
        private int ResolveGoldReward() => data != null ? data.goldReward : goldReward;
        private int ResolveLifeDamage() => data != null ? data.lifeDamage : lifeDamage;
        private float ResolveMinDamage() => data != null ? data.minDamage : minDamage;
        private float ResolveAttackDamage() => data != null ? data.attackDamage : attackDamage;
        private float ResolveAttackRange() => data != null ? data.attackRange : attackRange;
        private float ResolveDetectionRange()
        {
            float detect = data != null ? data.detectionRange : detectionRange;
            float atk = ResolveAttackRange();
            // detectionRange 가 0 이거나 attackRange 보다 작으면 attackRange 와 동일하게 (즉시 공격 모드)
            return detect < atk ? atk : detect;
        }
        private float ResolveAttackInterval() => data != null ? data.attackInterval : attackInterval;
        private float ResolveAttackWindup() => data != null ? data.attackWindupSeconds : attackWindupSeconds;
        private AttackType ResolveAttackType() => data != null ? data.attackType : attackType;
        private Arrow ResolveArrowPrefab() => data != null && data.arrowPrefab != null ? data.arrowPrefab : arrowPrefab;
        private float ResolveHealAmount() => data != null ? data.healAmount : healAmount;
        private float ResolveHealRange() => data != null ? data.healRange : healRange;
        private float ResolveHealInterval() => data != null ? data.healInterval : healInterval;
        private float ResolveHealCastDuration() => data != null ? data.healCastDuration : healCastDuration;

        private float ResolveDefense(AttackType type)
        {
            switch (type)
            {
                case AttackType.Physical:
                    return data != null ? data.physicalDefense : physicalDefense;
                case AttackType.Magic:
                    return data != null ? data.magicDefense : magicDefense;
                default:
                    return 0f;
            }
        }
    }
}
