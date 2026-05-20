using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Map
{
    /// <summary>
    /// 적이 따라갈 웨이포인트 경로.
    /// 빈 GameObject 에 이 컴포넌트를 붙이고, 자식 Transform 들을 경로 순서대로 둔다.
    ///
    /// 구조 예시:
    ///   EnemyPath (이 컴포넌트)
    ///   ├─ WP_0  (스폰 지점)
    ///   ├─ WP_1
    ///   ├─ WP_2
    ///   └─ WP_End (골인 지점)
    ///
    /// 정책:
    ///   - 자식의 hierarchy 순서가 경로 순서.
    ///   - 시작점은 Waypoints[0], 골인점은 Waypoints[Count-1].
    ///   - Z 좌표는 무시하지 않는다 (필요시 SetZ 로 보정).
    /// </summary>
    public class EnemyPath : MonoBehaviour
    {
        [Header("기즈모")]
        [SerializeField] private Color lineColor = new Color(1f, 0.8f, 0.2f, 0.9f);
        [SerializeField] private Color pointColor = new Color(1f, 0.5f, 0.1f, 1f);
        [SerializeField] private float pointRadius = 0.15f;

        private readonly List<Transform> cachedPoints = new List<Transform>();

        public int Count
        {
            get
            {
                RefreshIfNeeded();
                return cachedPoints.Count;
            }
        }

        public Vector3 SpawnPoint => GetPoint(0);
        public Vector3 EndPoint => GetPoint(Count - 1);

        /// <summary>i 번째 웨이포인트의 월드 좌표.</summary>
        public Vector3 GetPoint(int index)
        {
            RefreshIfNeeded();
            if (cachedPoints.Count == 0) return transform.position;
            int clamped = Mathf.Clamp(index, 0, cachedPoints.Count - 1);
            return cachedPoints[clamped].position;
        }

        private void RefreshIfNeeded()
        {
            // 자식 수가 바뀌었거나 캐시에 null 이 들어있으면 다시 수집한다.
            if (cachedPoints.Count != transform.childCount || HasNull())
                Rebuild();
        }

        private bool HasNull()
        {
            for (int i = 0; i < cachedPoints.Count; i++)
                if (cachedPoints[i] == null) return true;
            return false;
        }

        private void Rebuild()
        {
            cachedPoints.Clear();
            for (int i = 0; i < transform.childCount; i++)
                cachedPoints.Add(transform.GetChild(i));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            int n = transform.childCount;
            if (n == 0) return;

            // 점
            Gizmos.color = pointColor;
            for (int i = 0; i < n; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                Gizmos.DrawSphere(child.position, pointRadius);
            }

            // 선
            Gizmos.color = lineColor;
            for (int i = 0; i < n - 1; i++)
            {
                var a = transform.GetChild(i);
                var b = transform.GetChild(i + 1);
                if (a == null || b == null) continue;
                Gizmos.DrawLine(a.position, b.position);
            }
        }
#endif
    }
}
