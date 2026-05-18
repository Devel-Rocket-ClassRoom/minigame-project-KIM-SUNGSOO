using UnityEngine;

namespace KRTD.Map
{
    /// <summary>
    /// 경로(웨이포인트 컬렉션). 자식 Transform들을 순서대로 경로로 사용.
    /// </summary>
    public class Path : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private bool autoFromChildren = true;

        public Transform[] Waypoints => waypoints;
        public float TotalLength { get; private set; }

        private void Awake()
        {
            if (autoFromChildren) GatherFromChildren();
            RecalculateLength();
        }

        private void GatherFromChildren()
        {
            int n = transform.childCount;
            waypoints = new Transform[n];
            for (int i = 0; i < n; i++) waypoints[i] = transform.GetChild(i);
        }

        private void RecalculateLength()
        {
            TotalLength = 0f;
            if (waypoints == null) return;
            for (int i = 0; i < waypoints.Length - 1; i++)
                TotalLength += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null) continue;
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                Gizmos.DrawSphere(waypoints[i].position, 0.15f);
            }
        }
    }
}
