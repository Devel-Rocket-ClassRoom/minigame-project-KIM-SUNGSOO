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

        [Header("이동 유형")]
        [Tooltip("공중 유닛 여부 (fallback). EnemyData 가 있으면 그쪽 값을 우선. " +
            "true 면 보병에게 막히지 않고 통과하며, 보병도 이 적을 타겟에 넣지 않는다.")]
        [SerializeField] private bool isFlying = false;

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

        [Header("보스 페이즈 전환 시각 피드백 (isBoss + phases 가 있을 때만 동작)")]
        [Tooltip("페이즈 진입 순간 자식 SpriteRenderer 들의 color 를 잠깐 이 색으로 덮어 깜빡인다.")]
        [SerializeField] private Color phaseFlashColor = Color.white;
        [Tooltip("페이즈 전환 플래시 지속 시간(초). 0 이하이면 플래시 생략.")]
        [SerializeField] private float phaseFlashDuration = 0.15f;

        private float currentHp;
        private EnemyPath path;
        private int nextWaypointIndex;
        private bool reachedEnd;

        // 교전 모드 상태 — 보병(Soldier) / 영웅(Hero) 공통 (IEnemyEngageable).
        private IEnemyEngageable currentEngageTarget;
        private float nextAttackTime;

        // 1:1 페어 락. 이 적을 currentTarget 으로 잡고 있는 보병이 있다면 그 인스턴스.
        // null 이면 자유 상태 — 다른 보병이 후보로 잡을 수 있다.
        // Soldier 측에서 SetTargetedBy 로 설정/해제한다.
        private Soldier targetedBy;

        // 직전 프레임에 타겟이 공격 사거리 안이었는지 — 사거리 밖→안 진입 시 windup 적용용.
        private bool wasTargetInAttackRange;

        // 힐러 모드 상태
        private float nextHealTime;
        private bool isHealing;     // 힐 모션 시전 중(이동 멈춤)
        private float healEndTime;  // 이 시각이 지나면 시전 종료 후 다시 이동

        // --- 보스 페이즈 상태 ---
        // 현재 활성 페이즈 인덱스. -1 = 아직 진입한 페이즈 없음(베이스 스탯 사용).
        // HP 가 다시 차올라도 인덱스는 줄어들지 않는다 — 한 번 들어간 페이즈에서 후퇴 X.
        private int activePhaseIndex = -1;

        // 페이즈 오버라이드. null = 베이스/데이터값 사용, 값 있음 = 그 값으로 덮어씀.
        // ApplyPhaseOverrides 가 활성 페이즈의 override* 체크박스를 보고 세팅한다.
        private float? overrideMoveSpeed;
        private float? overrideAttackDamage;
        private float? overrideAttackInterval;
        private float? overrideAttackRange;
        private float? overridePhysicalDefense;
        private float? overrideMagicDefense;

        public bool IsDead => currentHp <= 0f;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 공중 유닛 여부 — data 우선, 없으면 fallback. 보병이 이 적을 타겟에서 제외하고,
        /// 이 적도 보병을 무시하고 경로를 통과한다. ArcherTower/MageTower 는 영향 없음.
        /// </summary>
        public bool IsFlying => data != null ? data.isFlying : isFlying;

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

            // 보스 페이즈 초기화 — 풀링이 들어왔을 때를 대비해 명시적으로 리셋.
            // 스폰 직후에도 임계값(예: 1.0) 이 정의돼 있다면 EvaluatePhases 가 잡아준다.
            activePhaseIndex = -1;
            ClearPhaseOverrides();
            EvaluatePhases();

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

            // 1. 탐지범위 안 보병이 있으면 멈춤. 공격범위 안에 들어왔을 때만 실제 데미지 + 공격 자세.
            //    공격력 0 인 적은 둘 다 패스 (그냥 지나가는 적).
            //    공중 유닛(IsFlying) 도 패스 — 보병에게 막히지 않고 경로 통과.
            // engaging : 이번 프레임 이동 정지 여부
            // isAttackingState : Animator 의 공격 자세(Attack 모션) 활성 여부 — 사거리 안일 때만 true
            //   (detection 진입만으로 Attack 모션이 무한 재생되던 어색함 방지.
            //    Animator 컨트롤러에서 isAttacking=false 인 동안엔 별도 Idle/대기 상태를 권장.)
            bool engaging = false;
            bool isAttackingState = false;
            if (ResolveAttackDamage() > 0f && !IsFlying)
            {
                if (currentEngageTarget == null || currentEngageTarget.IsDead || !IsTargetInDetection(currentEngageTarget))
                {
                    SetCurrentEngageTarget(FindNearestEngageableInDetection());
                    // 타겟이 끊기면 사거리 상태도 리셋 — 새 타겟의 첫 진입 때 다시 windup.
                    if (currentEngageTarget == null) wasTargetInAttackRange = false;
                }

                if (currentEngageTarget != null)
                {
                    engaging = true; // 탐지됨 → 무조건 멈춤 (자세는 아직 결정 X)
                    UpdateFacing(currentEngageTarget.Position.x - transform.position.x);

                    bool inAttackRange = IsTargetInAttackRange(currentEngageTarget);
                    if (inAttackRange && !wasTargetInAttackRange)
                    {
                        // 사거리 진입 첫 프레임: 누적 쿨다운으로 즉발타가 나가지 않도록 windup 만큼 강제 대기.
                        // Animator Run→Attack 블렌드 시간을 벌어 \"멈춤 → 모션 → 데미지\" 순서 보장.
                        nextAttackTime = Mathf.Max(nextAttackTime, Time.time + ResolveAttackWindup());
                    }
                    wasTargetInAttackRange = inAttackRange;

                    if (inAttackRange)
                    {
                        // 사거리 안 — 공격 자세 활성.
                        isAttackingState = true;
                        if (Time.time >= nextAttackTime)
                        {
                            AttackTarget(currentEngageTarget);
                            nextAttackTime = Time.time + ResolveAttackInterval();
                        }
                    }
                    else if (!currentEngageTarget.ApproachesEnemies)
                    {
                        // 사거리 밖 + 타겟이 다가오지 않는 유닛(영웅 등) → 적이 직접 다가간다.
                        // 보병처럼 ApproachesEnemies=true 인 타겟은 sideEngage 로 옆에 붙어주니 대기.
                        Vector3 toEngage = currentEngageTarget.Position - transform.position;
                        float engageDist = toEngage.magnitude;
                        if (engageDist > 0.01f)
                        {
                            float engageStep = ResolveMoveSpeed() * Time.deltaTime;
                            transform.position += toEngage / engageDist * Mathf.Min(engageStep, engageDist);
                        }
                        // engaging 유지 — 경로 진행은 멈춤. 도달하면 다음 프레임에 attack range 안.
                    }
                    // else: 탐지됐지만 사거리 밖 + 타겟이 알아서 다가옴 — 대기 (자세 X)
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

            // Run / Attack 전환을 Animator 에 알린다.
            // engaging(detection 진입 멈춤) 과 isAttackingState(사거리 안 공격 자세) 를 분리해서,
            // 보병이 사거리 밖에서 다가오는 동안 Attack 모션이 무한 재생되는 어색함을 막는다.
            if (animator != null && !string.IsNullOrEmpty(isAttackingBool))
                animator.SetBool(isAttackingBool, isAttackingState);

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

        // --- 교전 (보병/영웅 공통) ------------------------------------------

        /// <summary>
        /// currentEngageTarget 갱신. 이전 타겟의 TargetedBy 를 풀고, 새 타겟의 TargetedBy 를 this 로 설정해
        /// 1:1 페어 lock 을 유지한다. 모든 currentEngageTarget 변경은 이 메서드로만 한다.
        /// internal: Soldier/Hero.Die 의 cascading 정리에서 호출.
        /// </summary>
        internal void SetCurrentEngageTarget(IEnemyEngageable newTarget)
        {
            if (currentEngageTarget == newTarget) return;
            if (currentEngageTarget != null && currentEngageTarget.TargetedBy == this)
                currentEngageTarget.SetTargetedBy(null);
            currentEngageTarget = newTarget;
            if (currentEngageTarget != null)
                currentEngageTarget.SetTargetedBy(this);
        }

        private bool IsTargetInAttackRange(IEnemyEngageable t)
        {
            float r = ResolveAttackRange();
            return (t.Position - transform.position).sqrMagnitude <= r * r;
        }

        private bool IsTargetInDetection(IEnemyEngageable t)
        {
            float r = ResolveDetectionRange();
            return (t.Position - transform.position).sqrMagnitude <= r * r;
        }

        /// <summary>
        /// 탐지 범위 안의 가장 가까운 IEnemyEngageable (Soldier 또는 Hero) 후보를 찾는다.
        /// 죽었거나 배치 중이거나 다른 적이 페어 락한 대상은 제외.
        /// </summary>
        private IEnemyEngageable FindNearestEngageableInDetection()
        {
            Vector3 origin = transform.position;
            float r = ResolveDetectionRange();
            float rangeSq = r * r;
            IEnemyEngageable nearest = null;
            float bestDistSq = float.MaxValue;

            // NOTE: 매 프레임 FindObjectsByType 는 비효율적. 유닛 수 늘면 매니저 등록 방식으로 교체.
            Soldier[] soldiers = Object.FindObjectsByType<Soldier>(FindObjectsSortMode.None);
            foreach (var s in soldiers)
                ConsiderCandidate(s, origin, rangeSq, ref nearest, ref bestDistSq);

            // 영웅은 씬에 최대 1마리 — 정적 슬롯으로 바로 접근.
            if (Hero.Instance != null)
                ConsiderCandidate(Hero.Instance, origin, rangeSq, ref nearest, ref bestDistSq);

            return nearest;
        }

        private void ConsiderCandidate(IEnemyEngageable cand, Vector3 origin, float rangeSq,
            ref IEnemyEngageable nearest, ref float bestDistSq)
        {
            if (cand == null || cand.IsDead) return;
            // Unity null 체크 — 파괴된 MonoBehaviour 도 걸러야 한다.
            if ((cand as Object) == null) return;
            // 배치 중(Soldier 만 의미 있음) — Hero 는 항상 false 반환.
            if (cand.IsDeploying) return;
            // 1:1 페어 정책: 이미 다른 적이 잡고 있는 후보는 제외.
            // 단, AcceptsMultipleAttackers=true 인 대상(영웅 등) 은 멀티 어태커 허용 — 페어 lock 우회.
            if (!cand.AcceptsMultipleAttackers && cand.TargetedBy != null && cand.TargetedBy != this) return;
            float d = (cand.Position - origin).sqrMagnitude;
            if (d > rangeSq) return;
            if (d < bestDistSq) { bestDistSq = d; nearest = cand; }
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

        private void AttackTarget(IEnemyEngageable t)
        {
            Arrow prefab = ResolveArrowPrefab();
            if (prefab != null)
            {
                // 원거리: 투사체 발사. 화살이 도달하면 그 시점에 데미지가 들어간다.
                // Arrow 는 IDamageable 을 받으므로 Soldier/Hero 둘 다 그대로 작동.
                var arrow = Instantiate(prefab, transform.position, Quaternion.identity);
                arrow.Init(t, ResolveAttackDamage(), ResolveAttackType());
            }
            else
            {
                // 근접: 즉시 데미지.
                t.TakeDamage(ResolveAttackDamage(), ResolveAttackType());
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
                return;
            }
            // 살아남은 경우에만 페이즈 평가 — 죽는 타격에서 페이즈 전환 플래시가 일어나지 않게.
            EvaluatePhases();
        }

        /// <summary>공격 유형이 명시되지 않은 외부 호출 호환용. Physical 로 간주.</summary>
        public void TakeDamage(float damage) => TakeDamage(damage, AttackType.Physical);

        private void Die()
        {
            // 1:1 페어 lock 해제 — 내가 노리던 타겟의 TargetedBy 풀기.
            SetCurrentEngageTarget(null);
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
            SetCurrentEngageTarget(null);
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
            isFlying = data.isFlying;
        }

        private float ResolveMaxHp() => (data != null ? data.maxHp : maxHp) * hpMultiplier;
        private float ResolveMoveSpeed() => overrideMoveSpeed ?? (data != null ? data.moveSpeed : moveSpeed);
        private int ResolveGoldReward() => data != null ? data.goldReward : goldReward;
        private int ResolveLifeDamage() => data != null ? data.lifeDamage : lifeDamage;
        private float ResolveMinDamage() => data != null ? data.minDamage : minDamage;
        private float ResolveAttackDamage() => overrideAttackDamage ?? (data != null ? data.attackDamage : attackDamage);
        private float ResolveAttackRange() => overrideAttackRange ?? (data != null ? data.attackRange : attackRange);
        private float ResolveDetectionRange()
        {
            float detect = data != null ? data.detectionRange : detectionRange;
            float atk = ResolveAttackRange();
            // detectionRange 가 0 이거나 attackRange 보다 작으면 attackRange 와 동일하게 (즉시 공격 모드)
            return detect < atk ? atk : detect;
        }
        private float ResolveAttackInterval() => overrideAttackInterval ?? (data != null ? data.attackInterval : attackInterval);
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
                    return overridePhysicalDefense ?? (data != null ? data.physicalDefense : physicalDefense);
                case AttackType.Magic:
                    return overrideMagicDefense ?? (data != null ? data.magicDefense : magicDefense);
                default:
                    return 0f;
            }
        }

        // --- 보스 페이즈 --------------------------------------------------------

        /// <summary>
        /// 현재 HP 비율이 phases 중 통과된 임계값 안에서 가장 낮은 임계값의 페이즈를 활성화한다.
        /// 한 번 들어간 페이즈에서 후퇴는 없다(HP 가 다시 차도 인덱스 감소 X).
        /// isBoss 가 아니거나 phases 가 비어있으면 no-op.
        /// </summary>
        private void EvaluatePhases()
        {
            if (data == null || !data.isBoss) return;
            var phases = data.phases;
            if (phases == null || phases.Count == 0) return;

            float ratio = HpRatio;
            int targetIndex = -1;
            float lowestThreshold = float.MaxValue;
            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                if (p == null) continue;
                // "이 임계값 이하로 떨어졌다" + "지금까지 본 것 중 가장 낮은 임계값"
                if (ratio <= p.hpThreshold && p.hpThreshold <= lowestThreshold)
                {
                    lowestThreshold = p.hpThreshold;
                    targetIndex = i;
                }
            }

            if (targetIndex == -1 || targetIndex == activePhaseIndex) return;

            activePhaseIndex = targetIndex;
            ApplyPhaseOverrides(phases[targetIndex]);
            TriggerPhaseFlash();
        }

        /// <summary>활성 페이즈의 override* 체크박스를 보고 nullable override 필드들을 세팅.</summary>
        private void ApplyPhaseOverrides(EnemyData.BossPhase phase)
        {
            if (phase == null) return;
            overrideMoveSpeed = phase.overrideMoveSpeed ? (float?)phase.moveSpeed : null;
            overrideAttackDamage = phase.overrideAttackDamage ? (float?)phase.attackDamage : null;
            overrideAttackInterval = phase.overrideAttackInterval ? (float?)phase.attackInterval : null;
            overrideAttackRange = phase.overrideAttackRange ? (float?)phase.attackRange : null;
            overridePhysicalDefense = phase.overridePhysicalDefense ? (float?)phase.physicalDefense : null;
            overrideMagicDefense = phase.overrideMagicDefense ? (float?)phase.magicDefense : null;
        }

        private void ClearPhaseOverrides()
        {
            overrideMoveSpeed = null;
            overrideAttackDamage = null;
            overrideAttackInterval = null;
            overrideAttackRange = null;
            overridePhysicalDefense = null;
            overrideMagicDefense = null;
        }

        private void TriggerPhaseFlash()
        {
            if (phaseFlashDuration <= 0f) return;
            StartCoroutine(FlashSpritesCoroutine(phaseFlashColor, phaseFlashDuration));
        }

        // 자식 SpriteRenderer 들의 color 를 잠깐 tint 로 덮고 원복. 페이즈 전환 알림용.
        private System.Collections.IEnumerator FlashSpritesCoroutine(Color tint, float duration)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) yield break;
            var originals = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].color;
                renderers[i].color = tint;
            }
            yield return new WaitForSeconds(duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].color = originals[i];
            }
        }
    }
}
