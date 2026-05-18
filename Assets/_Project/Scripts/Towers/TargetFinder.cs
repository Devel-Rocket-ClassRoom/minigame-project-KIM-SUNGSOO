using System.Collections.Generic;
using UnityEngine;
using KRTD.Data;
using KRTD.Enemies;

namespace KRTD.Towers
{
    /// <summary>
    /// 사거리 내 적을 찾아 가장 우선순위가 높은 타겟을 반환.
    /// 우선순위 정책(First/Closest/Strongest/Weakest)을 SO/Enum으로 확장하기 좋음.
    /// </summary>
    public class TargetFinder : MonoBehaviour
    {
        public enum Priority { First, Last, Closest, Strongest, Weakest }

        [SerializeField] private Priority priority = Priority.First;
        [SerializeField] private float range = 4f;
        [SerializeField] private LayerMask enemyMask;

        private static readonly Collider[] buffer = new Collider[32]; // 3D 기준. 2D면 Collider2D + OverlapCircle 사용.

        public float Range
        {
            get => range;
            set => range = value;
        }

        public void ApplyTier(TowerUpgradeData tier)
        {
            if (tier != null) range = tier.range;
        }

        /// <summary>현재 우선순위에 맞는 타겟을 찾는다. 없으면 null.</summary>
        public EnemyController FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, buffer, enemyMask);
            if (count == 0) return null;

            EnemyController best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                var enemy = buffer[i].GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.IsAlive) continue;

                float score = ScoreFor(enemy);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        private float ScoreFor(EnemyController e)
        {
            switch (priority)
            {
                case Priority.First:     return e.PathProgress;
                case Priority.Last:      return -e.PathProgress;
                case Priority.Closest:   return -Vector3.SqrMagnitude(e.transform.position - transform.position);
                case Priority.Strongest: return e.CurrentHP;
                case Priority.Weakest:   return -e.CurrentHP;
            }
            return 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}
