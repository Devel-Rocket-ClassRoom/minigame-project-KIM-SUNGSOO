using UnityEngine;
using UnityEngine.EventSystems;
using KRTD.Combat;

namespace KRTD.UI
{
    /// <summary>
    /// 배럭 랠리 포인트 지정 모드의 입력/조준 컨트롤러.
    /// BuildMenuController 의 관리 메뉴에서 Rally 엔트리를 누르면 BeginTargeting 으로 진입.
    ///
    /// 흐름:
    ///   - 진입 시 배럭 사거리 원 표시 + 마우스 위치에 미리보기 LineRenderer 표시
    ///   - 매 프레임 BarracksController.IsInRange 검사로 미리보기 색 갱신
    ///   - 유효 위치 클릭 → SetCustomRally, 조준 종료
    ///   - 무효 위치 클릭 → 무시 (조준 유지)
    ///   - ESC/우클릭 → 취소
    /// </summary>
    public class BarracksRallyController : MonoBehaviour
    {
        public static BarracksRallyController Instance { get; private set; }

        [Header("선택 사항")]
        [Tooltip("월드 좌표를 얻을 카메라. 비우면 Camera.main 사용.")]
        [SerializeField] private Camera worldCamera;

        [Header("미리보기 색상")]
        [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.5f, 0.95f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        [Header("미리보기 마커")]
        [Tooltip("랠리 지정 미리보기 LineRenderer 프리팹. null 이면 자동 생성.")]
        [SerializeField] private LineRenderer markerPrefab;
        [Tooltip("자동 생성 시 그릴 X자/원 마커의 반지름.")]
        [SerializeField] private float markerRadius = 0.35f;

        private BarracksController pendingBarracks;
        private LineRenderer markerInstance;
        // 진입 프레임 — 같은 클릭이 Rally 버튼 → SetCustomRally 로 흘러들어오는 것 방지.
        private int enteredFrame = -1;

        public bool IsTargeting => pendingBarracks != null;
        public BarracksController PendingBarracks => pendingBarracks;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>외부(BuildMenuController) 가 호출 — 이 배럭의 랠리 변경 모드 진입.</summary>
        public void BeginTargeting(BarracksController barracks)
        {
            if (barracks == null) return;

            CancelTargeting();

            pendingBarracks = barracks;
            enteredFrame = Time.frameCount;
            barracks.SetRangeVisible(true);   // 사거리 원 켬
            ShowMarker();
        }

        public void CancelTargeting()
        {
            if (pendingBarracks != null)
                pendingBarracks.SetRangeVisible(false);
            pendingBarracks = null;
            HideMarker();
        }

        private void Update()
        {
            if (!IsTargeting) return;

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
                if (worldCamera == null) return;
            }

            Vector3 mouseWorld = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            // 사거리 + PathTile 둘 다 만족해야 유효 (정확한 규칙은 BarracksController 가 가진다).
            bool validNow = pendingBarracks.IsValidRally(mouseWorld);

            if (markerInstance != null)
            {
                markerInstance.transform.position = mouseWorld;
                var c = validNow ? validColor : invalidColor;
                markerInstance.startColor = c;
                markerInstance.endColor = c;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelTargeting();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                // Rally 버튼을 누른 그 클릭이 같은 프레임에 여기로 흘러들어오는 것을 한 프레임 무시.
                if (Time.frameCount == enteredFrame) return;

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

                // 무효 위치 클릭은 흘려보내 모드 유지.
                if (!validNow) return;

                pendingBarracks.SetCustomRally(mouseWorld);
                CancelTargeting();
            }
        }

        // --- 미리보기 마커 ----------------------------------------------------

        private void ShowMarker()
        {
            if (markerPrefab != null)
            {
                markerInstance = Instantiate(markerPrefab);
            }
            else
            {
                var go = new GameObject("RallyMarker (auto)");
                markerInstance = go.AddComponent<LineRenderer>();
                markerInstance.material = new Material(Shader.Find("Sprites/Default"));
                markerInstance.startWidth = 0.08f;
                markerInstance.endWidth = 0.08f;
                markerInstance.sortingOrder = 100;
                markerInstance.useWorldSpace = false;
                markerInstance.loop = true;

                const int segments = 24;
                markerInstance.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * 2f * Mathf.PI / segments;
                    markerInstance.SetPosition(i, new Vector3(
                        Mathf.Cos(a) * markerRadius,
                        Mathf.Sin(a) * markerRadius, 0f));
                }
            }
        }

        private void HideMarker()
        {
            if (markerInstance != null)
            {
                Destroy(markerInstance.gameObject);
                markerInstance = null;
            }
        }
    }
}
