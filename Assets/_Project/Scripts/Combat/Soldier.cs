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
    public class Soldier : MonoBehaviour
    {
        [Header("스탯")]
        [SerializeField] private float maxHp = 8f;
        [SerializeField] private float damage = 2f;
        [SerializeField] private float attackInterval = 1.0f;
        [Tooltip("이 거리 안의 적만 공격 대상으로 한다.")]
        [SerializeField] private float attackRange = 1.2f;

        [Header("애니메이션 (선택)")]
        [Tooltip("공격/사망 트리거를 호출할 Animator. 비워두면 무시.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string attackTrigger = "Attack1";
        [SerializeField] private string deathTrigger = "Death";

        [Header("사망 처리")]
        [Tooltip("Die() 호출 후 GameObject 가 파괴되기까지 대기할 시간 (사망 애니 길이).")]
        [SerializeField] private float deathLingerSeconds = 0.6f;

        private float currentHp;
        private float nextAttackTime;
        private Enemy currentTarget;
        private bool isDead;

        public bool IsDead => isDead;
        public Vector3 Position => transform.position;

        /// <summary>
        /// 죽음 순간 한 번 호출. BarracksController 가 구독해서 부활 카운트다운 시작.
        /// </summary>
        public event Action<Soldier> OnDeath;

        private void Awake()
        {
            currentHp = maxHp;
        }

        private void Update()
        {
            if (isDead) return;

            // 대상 갱신: null/사망/사거리 이탈이면 다시 찾는다.
            if (currentTarget == null || currentTarget.IsDead || !IsInRange(currentTarget))
                currentTarget = FindNearestEnemyInRange();

            if (currentTarget == null) return;

            // 정해진 간격마다 공격
            if (Time.time >= nextAttackTime)
            {
                Attack(currentTarget);
                nextAttackTime = Time.time + attackInterval;
            }
        }

        /// <summary>
        /// 외부(적 등)에서 데미지를 입힌다.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (isDead) return;

            currentHp -= amount;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        private void Attack(Enemy target)
        {
            if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                animator.SetTrigger(attackTrigger);

            target.TakeDamage(damage);
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

        private bool IsInRange(Enemy enemy)
        {
            return (enemy.Position - transform.position).sqrMagnitude <= attackRange * attackRange;
        }

        private Enemy FindNearestEnemyInRange()
        {
            Vector3 origin = transform.position;
            float rangeSq = attackRange * attackRange;
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
