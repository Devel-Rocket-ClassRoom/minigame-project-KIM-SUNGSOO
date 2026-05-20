using UnityEngine;
using KRTD.UI;

namespace KRTD.Map
{
    /// <summary>
    /// 타워(건물)를 설치할 수 있는 빈 스팟.
    /// 3x3 타일 영역의 중심에 배치되며, 클릭하면 건설 메뉴를 호출한다.
    ///
    /// 배치 규칙:
    ///   - Transform.position 은 3x3 영역의 "중심"
    ///   - 1 unit = 1 tile 기준이므로, 셀 격자에 맞추려면 좌표가 정수 또는 정수+0.5 가 되도록 스냅한다.
    ///   - 인접 스팟과는 최소 1칸의 여백을 둔다 (= 중심 간 거리 4 이상).
    ///
    /// 시각 구조:
    ///   BuildSpot (이 컴포넌트, BoxCollider2D)
    ///   ├─ SpotMarker          (빈 스팟 표시 - 반투명 Black Tower)
    ///   └─ [건물 인스턴스]      (Play 시 BuildingData.buildingPrefab 이 인스턴스화)
    ///
    /// 정책:
    ///   - 에디터에서는 자동 미리보기를 하지 않는다 (씬에 잔여 인스턴스가 누적되는 문제 방지).
    ///   - Play 진입 시 Awake 에서 currentBuilding/isOccupied 를 보고 한 번만 인스턴스화.
    ///   - 에디터에서 currentBuilding 을 바꿔본 결과를 보고 싶다면 그냥 Play 누르기.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BuildSpot : MonoBehaviour
    {
        [Header("스팟 크기 (타일 단위)")]
        [SerializeField] private Vector2Int size = new Vector2Int(3, 3);

        [Header("자식 시각 요소")]
        [Tooltip("빈 스팟일 때 보이는 반투명 표지. 건물 설치 시 자동으로 숨김.")]
        [SerializeField] private SpriteRenderer spotMarker;

        [Header("상태")]
        [SerializeField] private bool isOccupied = false;
        [SerializeField] private BuildingData currentBuilding;

        // 런타임 인스턴스 추적 (직렬화 안 함)
        [System.NonSerialized] private GameObject currentBuildingInstance;

        public bool IsOccupied => isOccupied;
        public Vector2Int Size => size;
        public Vector3 CenterWorld => transform.position;
        public BuildingData CurrentBuilding => currentBuilding;

        /// <summary>
        /// 현재 설치된 건물 인스턴스의 컴포넌트(또는 인터페이스 구현체)를 가져온다.
        /// 미설치/미일치 시 null.
        /// </summary>
        public T GetBuildingComponent<T>() where T : class
        {
            if (currentBuildingInstance == null) return null;
            return currentBuildingInstance.GetComponent<T>();
        }

        private void Awake()
        {
            if (currentBuilding != null && isOccupied)
                ApplyBuildingVisual(currentBuilding);
            else
                ClearBuildingVisual();
        }

        /// <summary>
        /// 데이터 기반으로 건물을 설치한다.
        /// </summary>
        public bool PlaceBuilding(BuildingData data)
        {
            if (isOccupied || data == null) return false;

            isOccupied = true;
            currentBuilding = data;
            ApplyBuildingVisual(data);
            return true;
        }

        /// <summary>
        /// 설치된 건물을 제거하고 빈 스팟으로 되돌린다.
        /// </summary>
        public void RemoveBuilding()
        {
            isOccupied = false;
            currentBuilding = null;
            ClearBuildingVisual();
        }

        /// <summary>
        /// 현재 건물을 즉시 다른 단계로 교체한다 (업그레이드 등).
        /// PlaceBuilding 은 isOccupied 일 때 거부하므로 업그레이드 경로용 분리 메서드.
        /// </summary>
        public bool ReplaceBuilding(BuildingData next)
        {
            if (next == null) return false;
            RemoveBuilding();
            return PlaceBuilding(next);
        }

        private void ApplyBuildingVisual(BuildingData data)
        {
            if (spotMarker != null) spotMarker.enabled = false;

            // 잔여/중복 자식 모두 정리 (이전 세션에 누적된 인스턴스 청소)
            ClearAllBuildingChildren();

            if (data.buildingPrefab == null) return;

            currentBuildingInstance = Instantiate(data.buildingPrefab, transform);
            currentBuildingInstance.transform.localPosition = Vector3.zero;
        }

        private void ClearBuildingVisual()
        {
            if (spotMarker != null) spotMarker.enabled = true;
            ClearAllBuildingChildren();
        }

        /// <summary>
        /// SpotMarker 를 제외한 모든 자식을 제거한다.
        /// 에디터 잔여/런타임 누적 모두 정리.
        /// </summary>
        private void ClearAllBuildingChildren()
        {
            currentBuildingInstance = null;
            var spotMarkerGO = spotMarker != null ? spotMarker.gameObject : null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (child == spotMarkerGO) continue;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private void OnMouseDown()
        {
            var menu = BuildMenuController.Instance;
            if (menu == null)
            {
                Debug.LogWarning("[BuildSpot] 씬에 BuildMenuController 가 없다. UI 호출을 스킵.");
                return;
            }

            if (isOccupied)
                menu.ShowManageMenu(this);
            else
                menu.ShowBuildMenu(this);
        }

        /// <summary>
        /// 누적된 잔여 자식들을 수동으로 청소하고 싶을 때 사용.
        /// 컴포넌트 헤더 우클릭 → "Clear Stale Children".
        /// </summary>
        [ContextMenu("Clear Stale Children")]
        private void ClearStaleChildren()
        {
            ClearAllBuildingChildren();
            if (spotMarker != null) spotMarker.enabled = !isOccupied;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Vector3 area = new Vector3(size.x, size.y, 0.01f);

            Gizmos.color = isOccupied
                ? new Color(1f, 0.3f, 0.3f, 0.2f)
                : new Color(0.3f, 1f, 0.3f, 0.2f);
            Gizmos.DrawCube(transform.position, area);

            Gizmos.color = isOccupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.position, area);
        }

        private void OnValidate()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2(size.x, size.y);
                col.offset = Vector2.zero;
            }
            // 자동 미리보기 제거: Play 시에만 인스턴스화됨 (잔여 누적 방지)
        }
#endif
    }
}
