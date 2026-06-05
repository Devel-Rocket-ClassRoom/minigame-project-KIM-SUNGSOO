using UnityEngine;
using UnityEngine.EventSystems;
using KRTD.UI;

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
    public class Hero : MonoBehaviour, IEnemyEngageable
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
        [Tooltip("프리팹의 기본 스프라이트가 오른쪽(+X) 을 향한다고 가정하면 true. " +
            "왼쪽을 향한 에셋(예: sword_man) 이면 false 로 두면 좌우 반전이 자연스럽게 맞춰진다.")]
        [SerializeField] private bool spriteFacesRight = true;

        [Header("애니메이션 (선택)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string runBool = "isRunning";
        [SerializeField] private string attackBool = "isAttacking";
        [SerializeField] private string attackTrigger = "";
        [Tooltip("사망 상태 Bool 파라미터 이름. true=죽음, false=부활. " +
            "Animator 에 isDead Bool 추가 + Idle/Run/Attack → Die 전환(true), Die → Idle 전환(false) 필요.")]
        [SerializeField] private string deathBool = "isDead";

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

        // 1:1 페어 락 — 적이 이 영웅을 currentEngageTarget 으로 잡고 있을 때 그 적 인스턴스.
        // null 이면 자유 상태(다른 적이 후보로 잡을 수 있음). Enemy.SetCurrentEngageTarget 가 자동 갱신.
        private Enemy targetedBy;

        // 씬에 단일 인스턴스 가정 — UI/컨트롤러가 정적 슬롯으로 접근.
        public static Hero Instance { get; private set; }

        public bool IsDead => isDead;
        public Vector3 Position => transform.position;
        public Vector3 RallyPoint => rallyPoint;

        // --- IEnemyEngageable 구현 ---
        /// <summary>영웅은 \"배치\" 개념이 없으므로 항상 false. 적 검색에서 항상 후보로 고려된다.</summary>
        public bool IsDeploying => false;

        /// <summary>이 영웅을 노리고 있는 적(있다면). 1:1 페어 정책 — 다른 적은 후보로 잡지 않는다.</summary>
        public Enemy TargetedBy => targetedBy;

        /// <summary>Enemy.SetCurrentEngageTarget 가 자동으로 갱신 — 외부 직접 호출 비권장.</summary>
        public void SetTargetedBy(Enemy e) { targetedBy = e; }

        /// <summary>영웅은 탱커 컨셉 — 여러 적이 동시에 달려들 수 있다(페어 lock 우회).</summary>
        public bool AcceptsMultipleAttackers => true;

        /// <summary>영웅은 랠리에 고정 — 적이 다가오지 않는다. 적이 직접 접근해야 함.</summary>
        public bool ApproachesEnemies => false;

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

        /// <summary>
        /// 월드에서 영웅(Collider2D) 을 클릭하면 HeroPathRallyController 의 조준 모드를 시작한다.
        /// 호출 조건:
        ///   - 영웅이 살아있어야 함
        ///   - 마우스가 UI 위가 아닐 것 (Canvas Button 등의 클릭이 흘러들어오는 것 방지)
        ///   - Collider2D 가 프리팹에 부착돼 있어야 OnMouseDown 이 발화함
        ///
        /// 토글 정책 (Portrait 와 비대칭 — 의도적):
        ///   - 조준 모드가 꺼져 있을 때 영웅 클릭 → 조준 시작
        ///   - 조준 모드가 켜져 있을 때 영웅 클릭 → 아무 일도 안 함. 그대로 컨트롤러의 Update 가
        ///     같은 프레임 클릭을 받아 영웅 위치 근처(경로 위) 에 랠리를 설정한다.
        ///   - 취소는 ESC / 우클릭 / Portrait 클릭으로.
        /// 이렇게 안 하면 영웅이 경로 위에 서 있을 때, 그 경로를 클릭하려는 시도가 영웅 콜라이더에
        /// 먼저 닿아 OnMouseDown 이 조준을 꺼버리고 랠리 설정이 되지 않는다.
        /// </summary>
        private void OnMouseDown()
        {
            if (isDead) return;
            // UI 위 클릭(Portrait Button 등) 은 무시 — UI 가 자체 핸들러로 이미 토글했을 것.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var controller = HeroPathRallyController.Instance;
            if (controller == null) return;

            // 조준 중이면 클릭을 컨트롤러 Update 가 받아 처리하도록 흘려보낸다.
            if (controller.IsTargeting) return;

            controller.BeginTargeting(this);
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

            // 나를 노리던 적도 자기 currentEngageTarget 을 즉시 풀어 다른 후보를 잡을 수 있게.
            if (targetedBy != null) targetedBy.SetCurrentEngageTarget(null);

            // Animator 사망 상태 진입 — Bool true.
            if (animator != null && !string.IsNullOrEmpty(deathBool))
                animator.SetBool(deathBool, true);

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

            // Animator 사망 상태 해제 — Bool false → Die→Idle 전환 발화.
            if (animator != null && !string.IsNullOrEmpty(deathBool))
                animator.SetBool(deathBool, false);

            // Idle 클립이 일부 자식 transform(다리 등)을 keyframe 하지 않으면 Die 의 마지막 자세가
            // 그대로 남는다. WriteDefaultValues() 로 모든 애니메이트 속성을 기본값(스폰 직후)으로
            // 강제 복원해 누운 자세 등 잔재 제거.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.WriteDefaultValues();
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
            // 기본 스프라이트가 오른쪽 향함 → 오른쪽 진행 시 양의 X. 왼쪽 향한 스프라이트면 부호 반대.
            bool facingRight = dirX > 0f;
            bool wantPositive = spriteFacesRight ? facingRight : !facingRight;
            scale.x = wantPositive ? abs : -abs;
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
