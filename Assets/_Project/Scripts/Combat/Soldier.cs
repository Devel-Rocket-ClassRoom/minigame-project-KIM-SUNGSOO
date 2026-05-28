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
    public class Soldier : MonoBehaviour, IDamageable
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

        public bool IsDead => isDead;
        public Vector3 Position => transform.position;

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
        }

        /// <summary>BarracksController 등 외부에서 명시적으로 랠리 위치를 지정.</summary>
        public void SetRallyPoint(Vector3 worldPos)
        {
            rallyPoint = worldPos;
            hasRallyPoint = true;
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

            // 1. 탐지 범위 내 적 갱신 (없거나 사망했거나 탐지 이탈이면 재탐색).
            if (currentTarget == null || currentTarget.IsDead || !IsInDetection(currentTarget))
                currentTarget = FindNearestEnemyInDetection();

            bool nextIsAttacking = false;
            bool nextIsRunning = false;

            if (currentTarget != null)
            {
                // 항상 적 방향을 본다 (이동 중이든 공격 중이든)
                UpdateFacing(currentTarget.Position.x - transform.position.x);

                if (IsInAttackRange(currentTarget))
                {
                    // 2. 공격 범위 안: 멈춰서 공격
                    nextIsAttacking = true;
                    if (Time.time >= nextAttackTime)
                    {
                        Attack(currentTarget);
                        nextAttackTime = Time.time + attackInterval;
                    }
                }
                else
                {
                    // 3. 탐지는 됐지만 사거리 밖 → 적을 향해 이동
                    nextIsRunning = true;
                    MoveToward(currentTarget.Position);
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

            // Animator 동기화: 매 프레임 정확한 상태로
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(runBool)) animator.SetBool(runBool, nextIsRunning);
                if (!string.IsNullOrEmpty(attackBool)) animator.SetBool(attackBool, nextIsAttacking);
            }
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
            currentTarget = null;

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
