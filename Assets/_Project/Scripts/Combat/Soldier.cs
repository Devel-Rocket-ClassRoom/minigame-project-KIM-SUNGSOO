using System;
using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 배럭에서 소환되는 근접 보병.
    /// 사거리 내 가장 가까운 적을 찾아 정해진 간격마다 데미지를 입힌다.
    ///
    /// 구조 권장:
    ///   Soldier (이 컴포넌트 + Collider2D for hitbox)
    ///   └─ Body (SpriteRenderer + Animator)
    ///
    /// BarracksController 가 인스턴스화 → OnDeath 이벤트로 부활 신호를 받는다.
    /// </summary>
    public class Soldier : MonoBehaviour, IEnemyEngageable
    {
        [Header("스탯")]
        [SerializeField] private float maxHp = 8f;
        [SerializeField] private float damage = 2f;
        [SerializeField] private float attackInterval = 1.0f;
        [Tooltip("이 거리 안의 적만 공격 대상으로 한다.")]
        [SerializeField] private float attackRange = 1.2f;
        [Tooltip("이 보병의 공격 유형. 기본 Physical.")]
        [SerializeField] private AttackType attackType = AttackType.Physical;

        [Header("방어력 (공격 유형별 flat 차감)")]
        [SerializeField] private float physicalDefense = 0f;
        [SerializeField] private float magicDefense = 0f;
        [SerializeField] private float minDamage = 1f;

        [Header("이동/탐지")]
        [Tooltip("이 거리 안의 적을 발견하면 그 적을 향해 이동한다.")]
        [SerializeField] private float detectionRange = 4f;
        [Tooltip("적을 향해 이동하거나 랠리로 복귀할 때 사용하는 속도.")]
        [SerializeField] private float moveSpeed = 2f;
        [Tooltip("랠리(스폰) 위치 근처 이만큼 안이면 복귀 완료로 본다.")]
        [SerializeField] private float rallyArriveRadius = 0.1f;

        [Header("측면 교전 (옆자리 슬라이드)")]
        [Tooltip("적을 잡으면 그 적의 좌/우 옆자리(Y 동일)로 이동한 뒤 공격한다. " +
            "이 값은 적의 위치에서 보병이 멈출 X 오프셋 — attackRange 의 50~80% 권장. " +
            "0 이면 슬라이드 비활성(기존 동작).")]
        [Min(0f)]
        [SerializeField] private float sideEngageOffset = 0.7f;
        [Tooltip("측면 슬롯에 도달했다고 판정할 반경. rallyArriveRadius 보다 작아도 된다.")]
        [Min(0.01f)]
        [SerializeField] private float engageArriveRadius = 0.05f;

        [Header("좌우 방향 반전")]
        [Tooltip("진행/타겟 방향에 따라 좌우 반전할 시각 Transform (보통 자식 Body). " +
            "비워두면 자기 자신 transform. 기본 스프라이트가 오른쪽(+X) 향한다고 가정.")]
        [SerializeField] private Transform visualRoot;

        [Header("애니메이션 (선택)")]
        [Tooltip("Idle / Run / Attack 세 상태를 가진 Animator. 비워두면 무시.")]
        [SerializeField] private Animator animator;
        [Tooltip("이동 중(추격 또는 랠리 복귀) 일 때 true 가 되는 Bool 파라미터 이름. " +
            "Idle ↔ Run 전환 조건.")]
        [SerializeField] private string runBool = "isRunning";
        [Tooltip("공격 사거리 안에서 교전 중일 때 true 가 되는 Bool 파라미터 이름. " +
            "Idle/Run ↔ Attack 전환 조건.")]
        [SerializeField] private string attackBool = "isAttacking";
        [Tooltip("(선택) 매 공격 스윙마다 Trigger 도 발사하고 싶을 때 이름 지정. " +
            "Attack state 가 루핑이면 비워둬도 됨.")]
        [SerializeField] private string attackTrigger = "";
        [SerializeField] private string deathTrigger = "Death";

        [Header("사망 처리")]
        [Tooltip("Die() 호출 후 GameObject 가 파괴되기까지 대기할 시간 (사망 애니 길이).")]
        [SerializeField] private float deathLingerSeconds = 0.6f;

        private float currentHp;
        private float nextAttackTime;
        private Enemy currentTarget;
        private bool isDead;
        private Vector3 rallyPoint;
        private bool hasRallyPoint;
        // 현재 랠리에 도달한 상태인지. false 인 동안엔 "배치 중" — 적과 무관.
        // 일반적으로 한 번 true 가 되면 그대로 유지되지만, SetRallyPoint 로 랠리가
        // 멀리 옮겨지면 다시 false 로 돌아간다 (새 랠리로 걸어가는 동안 무방비 방지).
        private bool hasArrivedAtRally;

        // 1:1 페어 락. 이 보병을 currentEngageTarget 으로 잡고 있는 적이 있다면 그 인스턴스.
        // null 이면 자유 상태 — 다른 적이 후보로 잡을 수 있다.
        // Enemy 측에서 SetTargetedBy 로 설정/해제한다.
        private Enemy targetedBy;

        // 현재 페어에서 적의 어느 쪽 옆자리를 잡았는지. 페어가 끊기면 None.
        // 페어 형성 시점의 X 비교로 결정해 페어 동안 유지(좌우 깜빡임 방지).
        private EngageSlot engageSlot = EngageSlot.None;

        private enum EngageSlot { None, Left, Right }

        public bool IsDead => isDead;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 배치 중(아직 랠리에 도달 못한 상태) — 적과의 모든 상호작용을 무시한다.
        /// 보병 자신도 적 탐지/공격 안 함, 적 측에서도 이 보병을 타겟에 넣지 않는다.
        /// 랠리 반경 안에 들어온 시점에 해제. 외부에서 랠리를 멀리 옮기면 다시 배치 중으로 돌아감.
        /// </summary>
        public bool IsDeploying => !isDead && !hasArrivedAtRally;

        /// <summary>
        /// 이 보병을 노리고 있는 적(있다면). 1:1 페어 정책: 다른 적은 이 보병을 후보에 넣지 않는다.
        /// null 이면 자유.
        /// </summary>
        public Enemy TargetedBy => targetedBy;

        /// <summary>
        /// Enemy 측에서 \"이 보병을 내 currentEngageTarget 으로 잡았다 / 풀었다\" 알릴 때 호출.
        /// null 을 넣으면 페어 해제. Enemy.SetCurrentEngageTarget 이 자동으로 갱신한다 — 외부 직접 호출 비권장.
        /// </summary>
        public void SetTargetedBy(Enemy e) { targetedBy = e; }

        /// <summary>보병은 1:1 페어 lock — 한 명의 보병에 한 적만 붙는다.</summary>
        public bool AcceptsMultipleAttackers => false;

        /// <summary>보병은 sideEngage 슬라이드로 적에게 다가간다 — 적은 멈춰서 기다린다.</summary>
        public bool ApproachesEnemies => true;

        /// <summary>
        /// 죽음 순간 한 번 호출. BarracksController 가 구독해서 부활 카운트다운 시작.
        /// </summary>
        public event Action<Soldier> OnDeath;

        private void Awake()
        {
            currentHp = maxHp;
            // 스폰 위치를 자동으로 rallyPoint 로 설정 (BarracksController 가 SetRallyPoint 로 덮어쓸 수 있음).
            if (!hasRallyPoint)
            {
                rallyPoint = transform.position;
                hasRallyPoint = true;
            }
            // 스폰 위치가 이미 랠리 반경 안이면 즉시 활성화 (랠리=스폰위치인 기존 동작 호환).
            // 배럭에서 랠리까지 걸어오는 새 동작은 BarracksController 가 별도 위치에 스폰하므로
            // 이 시점엔 rallyArriveRadius 밖이고 hasArrivedAtRally 는 false 로 유지된다.
            if ((rallyPoint - transform.position).sqrMagnitude <= rallyArriveRadius * rallyArriveRadius)
                hasArrivedAtRally = true;
        }

        /// <summary>BarracksController 등 외부에서 명시적으로 랠리 위치를 지정.</summary>
        public void SetRallyPoint(Vector3 worldPos)
        {
            rallyPoint = worldPos;
            hasRallyPoint = true;
            // 새 랠리가 현재 위치에서 멀면 다시 배치 중(deploying) 으로 — 도달까지 적과 무관.
            // 가까우면 그대로 active 유지(이미 도착 상태이거나 자체 스폰 케이스).
            if ((rallyPoint - transform.position).sqrMagnitude > rallyArriveRadius * rallyArriveRadius)
                hasArrivedAtRally = false;
            else
                hasArrivedAtRally = true;
        }

        /// <summary>
        /// 배럭 티어별 스탯 배율 적용. BarracksController 가 스폰 직후 1회 호출.
        /// HP/데미지 가 인스펙터 기본값에서 배율만큼 증폭된다.
        /// </summary>
        public void ApplyTier(float hpMultiplier, float damageMultiplier)
        {
            maxHp = Mathf.Max(1f, maxHp * hpMultiplier);
            currentHp = maxHp;
            damage = Mathf.Max(0f, damage * damageMultiplier);
        }

        private void Update()
        {
            if (isDead) return;

            bool nextIsAttacking = false;
            bool nextIsRunning = false;

            // 0. 배치 중(랠리 첫 도달 전): 적과의 상호작용 없이 랠리로만 이동.
            //    Enemy.FindNearestEngageableInDetection 도 IsDeploying 인 보병을 건너뛰므로
            //    이 구간 동안 보병은 적에게도 보이지 않는다.
            if (!hasArrivedAtRally)
            {
                if (hasRallyPoint)
                {
                    Vector3 toRally = rallyPoint - transform.position;
                    float arriveSq = rallyArriveRadius * rallyArriveRadius;
                    if (toRally.sqrMagnitude <= arriveSq)
                    {
                        hasArrivedAtRally = true;
                        // 도착한 첫 프레임은 idle 로 두고 다음 프레임부터 평상시 로직 진입.
                    }
                    else
                    {
                        nextIsRunning = true;
                        UpdateFacing(toRally.x);
                        MoveToward(rallyPoint);
                    }
                }
                else
                {
                    // 랠리 정보가 없으면 deploying 의미가 없다 — 즉시 활성화.
                    hasArrivedAtRally = true;
                }

                SyncAnimator(nextIsRunning, nextIsAttacking);
                return;
            }

            // 1. 탐지 범위 내 적 갱신 (없거나 사망했거나 탐지 이탈이면 재탐색).
            if (currentTarget == null || currentTarget.IsDead || !IsInDetection(currentTarget))
                SetCurrentTarget(FindNearestEnemyInDetection());

            if (currentTarget != null)
            {
                // 적의 좌/우 옆자리(같은 Y) 슬롯으로 이동한 뒤 공격한다.
                // 슬라이드 도중엔 슬롯 방향(=결국 적이 있는 방향)으로 facing, 공격 중엔 적 방향 그대로.
                Vector3 engagePos = ComputeEngagePosition(currentTarget);
                Vector3 toEngage = engagePos - transform.position;
                bool atEngageSlot = toEngage.sqrMagnitude <= engageArriveRadius * engageArriveRadius;

                if (atEngageSlot && IsInAttackRange(currentTarget))
                {
                    // 2. 슬롯 도달 + 사거리 안: 멈춰서 공격. 적 방향 facing.
                    UpdateFacing(currentTarget.Position.x - transform.position.x);
                    nextIsAttacking = true;
                    if (Time.time >= nextAttackTime)
                    {
                        Attack(currentTarget);
                        nextAttackTime = Time.time + attackInterval;
                    }
                }
                else
                {
                    // 3. 슬롯 미도달 또는 사거리 밖 → 슬롯으로 슬라이드.
                    //    적이 움직이면 매 프레임 슬롯도 따라 움직이므로 자연스럽게 \"옆에 붙어다님\".
                    nextIsRunning = true;
                    UpdateFacing(toEngage.x);
                    MoveToward(engagePos);
                }
            }
            else if (hasRallyPoint)
            {
                // 4. 탐지 적 없음 → 랠리로 복귀
                Vector3 toRally = rallyPoint - transform.position;
                if (toRally.sqrMagnitude > rallyArriveRadius * rallyArriveRadius)
                {
                    nextIsRunning = true;
                    UpdateFacing(toRally.x);
                    MoveToward(rallyPoint);
                }
                // 랠리 도착 → Idle (둘 다 false 상태). facing 은 마지막 방향 유지.
            }

            SyncAnimator(nextIsRunning, nextIsAttacking);
        }

        private void SyncAnimator(bool running, bool attacking)
        {
            if (animator == null) return;
            if (!string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, running);
            if (!string.IsNullOrEmpty(attackBool)) animator.SetBool(attackBool, attacking);
        }

        /// <summary>
        /// currentTarget 갱신. 이전 적의 TargetedBy 를 풀고, 새 적의 TargetedBy 를 this 로 설정해
        /// 1:1 페어 lock 을 유지한다. 모든 currentTarget 변경은 이 메서드로만 한다.
        /// 새 페어 형성 시 engageSlot 도 함께 결정(보병 X 가 적보다 작으면 적의 왼쪽 슬롯).
        /// internal: Enemy.Die/ReachEnd 의 cascading 정리에서 호출.
        /// </summary>
        internal void SetCurrentTarget(Enemy newTarget)
        {
            if (currentTarget == newTarget) return;
            // 이전 페어 해제 — 단, 그 적이 정말로 나를 lock 하고 있을 때만 (방어적).
            if (currentTarget != null && currentTarget.TargetedBy == this)
                currentTarget.SetTargetedBy(null);
            currentTarget = newTarget;
            if (currentTarget != null)
            {
                currentTarget.SetTargetedBy(this);
                // 페어 형성 시점의 X 비교로 슬롯 결정 — 페어 동안 유지.
                engageSlot = transform.position.x < currentTarget.Position.x
                    ? EngageSlot.Left
                    : EngageSlot.Right;
            }
            else
            {
                engageSlot = EngageSlot.None;
            }
        }

        /// <summary>
        /// 페어된 적의 좌/우 옆자리(같은 Y) 좌표를 계산. sideEngageOffset 만큼 X 로 떨어진 지점.
        /// sideEngageOffset 이 0 이면 적 위치 그대로 반환 — 기존 동작과 동일(슬라이드 없음).
        /// </summary>
        private Vector3 ComputeEngagePosition(Enemy target)
        {
            float dx = engageSlot == EngageSlot.Left ? -sideEngageOffset : sideEngageOffset;
            Vector3 p = target.Position;
            p.x += dx;
            return p;
        }

        private void MoveToward(Vector3 worldPos)
        {
            Vector3 toTarget = worldPos - transform.position;
            float dist = toTarget.magnitude;
            if (dist < 1e-4f) return;

            float step = moveSpeed * Time.deltaTime;
            transform.position += toTarget / dist * Mathf.Min(step, dist);
        }

        /// <summary>
        /// 진행/타겟 방향의 X 부호로 visualRoot 의 localScale.x 를 ±|x| 로 설정.
        /// 기본 스프라이트가 오른쪽 향한다고 가정 — 음수 X 면 좌우반전.
        /// </summary>
        private void UpdateFacing(float dirX)
        {
            if (Mathf.Abs(dirX) < 1e-4f) return;
            Transform t = visualRoot != null ? visualRoot : transform;
            Vector3 scale = t.localScale;
            float abs = Mathf.Abs(scale.x);
            scale.x = dirX > 0 ? abs : -abs;
            t.localScale = scale;
        }

        /// <summary>
        /// 외부(적 등)에서 데미지를 입힌다.
        /// 공격 유형에 따라 방어력(flat) 적용 후 minDamage 로 클램프.
        /// </summary>
        public void TakeDamage(float amount, AttackType attackType)
        {
            if (isDead) return;

            float defense = attackType == AttackType.Magic ? magicDefense : physicalDefense;
            float effective = Mathf.Max(minDamage, amount - defense);

            currentHp -= effective;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        /// <summary>공격 유형이 명시되지 않은 호출 호환용. Physical 로 간주.</summary>
        public void TakeDamage(float amount) => TakeDamage(amount, AttackType.Physical);

        private void Attack(Enemy target)
        {
            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            target.TakeDamage(damage, attackType);
        }

        private void Die()
        {
            isDead = true;
            // 1:1 페어 lock 해제 — 내가 노리던 적의 TargetedBy 풀기.
            SetCurrentTarget(null);
            // 나를 노리던 적도 자기 currentEngageTarget 을 즉시 풀어 다른 후보를 잡을 수 있게.
            if (targetedBy != null) targetedBy.SetCurrentEngageTarget(null);

            if (animator != null && !string.IsNullOrEmpty(deathTrigger))
                animator.SetTrigger(deathTrigger);

            // 이 시점에 OnDeath 를 발사. BarracksController 는 이걸 받아 즉시 카운트다운 시작.
            // (시각적 GameObject 는 잠깐 더 남아서 사망 애니메이션 재생.)
            OnDeath?.Invoke(this);

            Destroy(gameObject, Mathf.Max(0f, deathLingerSeconds));
        }

        /// <summary>
        /// 외부에서 시간 만료 등으로 보병을 사망시킬 때 호출. 일반 사망과 동일하게 처리(애니/Destroy).
        /// 이미 죽은 상태면 무시.
        /// </summary>
        public void Expire()
        {
            if (isDead) return;
            Die();
        }

        private bool IsInAttackRange(Enemy enemy)
        {
            return (enemy.Position - transform.position).sqrMagnitude <= attackRange * attackRange;
        }

        private bool IsInDetection(Enemy enemy)
        {
            float r = Mathf.Max(detectionRange, attackRange);
            return (enemy.Position - transform.position).sqrMagnitude <= r * r;
        }

        private Enemy FindNearestEnemyInDetection()
        {
            Vector3 origin = transform.position;
            float r = Mathf.Max(detectionRange, attackRange);
            float rangeSq = r * r;
            Enemy nearest = null;
            float bestDistSq = float.MaxValue;

            // NOTE: ArcherTower 와 동일하게 매 프레임 검색. 적 수가 늘면 매니저 등록 방식으로 교체할 것.
            Enemy[] enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == null || e.IsDead) continue;
                // 공중 유닛은 보병이 공격할 수 없다 — 후보에서 제외 (ArcherTower/MageTower 가 담당).
                if (e.IsFlying) continue;
                // 1:1 페어 정책: 이미 다른 보병이 잡고 있는 적은 후보에서 제외.
                if (e.TargetedBy != null && e.TargetedBy != this) continue;

                float distSq = (e.Position - origin).sqrMagnitude;
                if (distSq > rangeSq) continue;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    nearest = e;
                }
            }
            return nearest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, attackRange);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
