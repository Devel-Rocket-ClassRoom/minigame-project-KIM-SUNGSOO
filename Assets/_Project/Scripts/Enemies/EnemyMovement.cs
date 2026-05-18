using UnityEngine;
using KRTD.Core;
using KRTD.Data;
using KRTD.Map;
using KRTD.Pooling;

namespace KRTD.Enemies
{
    /// <summary>
    /// 웨이포인트 기반 이동. Path의 노드를 순서대로 따라간다.
    /// 골인 도달 시 라이프 차감 + 풀로 반환.
    /// </summary>
    public class EnemyMovement : MonoBehaviour
    {
        private Path path;
        private float speed;
        private int currentNode;
        private float totalDistance;
        private float traveled;
        private EnemyController owner;
        private int livesPenalty;

        /// <summary>0~1, 시작=0 골인=1. TargetFinder의 First/Last 우선순위에 사용.</summary>
        public float NormalizedProgress => totalDistance > 0f ? traveled / totalDistance : 0f;

        private void Awake() { owner = GetComponent<EnemyController>(); }

        public void Initialize(EnemyData data, Path p)
        {
            path = p;
            speed = data.moveSpeed;
            livesPenalty = data.livesOnReach;
            currentNode = 0;
            traveled = 0f;
            totalDistance = path != null ? path.TotalLength : 1f;

            if (path != null && path.Waypoints.Length > 0)
                transform.position = path.Waypoints[0].position;
        }

        private void Update()
        {
            if (path == null || currentNode >= path.Waypoints.Length - 1) return;

            var current = path.Waypoints[currentNode].position;
            var next = path.Waypoints[currentNode + 1].position;
            var dir = (next - transform.position);
            float step = speed * Time.deltaTime;

            if (dir.sqrMagnitude <= step * step)
            {
                traveled += dir.magnitude;
                transform.position = next;
                currentNode++;
                if (currentNode >= path.Waypoints.Length - 1)
                {
                    ReachGoal();
                }
            }
            else
            {
                dir.Normalize();
                transform.position += dir * step;
                traveled += step;
            }
        }

        private void ReachGoal()
        {
            EventBus.Raise(new EnemyReachedGoalEvent(owner, livesPenalty));
            ObjectPool.Instance.Despawn(gameObject);
        }
    }

    public readonly struct EnemyReachedGoalEvent
    {
        public readonly EnemyController Enemy;
        public readonly int LivesPenalty;
        public EnemyReachedGoalEvent(EnemyController e, int p) { Enemy = e; LivesPenalty = p; }
    }
}
