using UnityEngine;
using UnityEngine.EventSystems;
using KRTD.Combat;
using KRTD.Map;

namespace KRTD.UI
{
    /// <summary>
    /// 영웅 랠리 지정 모드의 입력/조준 컨트롤러.
    /// HeroPortrait 의 토글 버튼이 BeginTargeting 으로 진입.
    ///
    /// 흐름 (BarracksRallyController 패턴 + 경로 스냅 차이):
    ///   - 진입 시 마우스 위치 근처에 마커 표시
    ///   - 매 프레임 씬의 모든 EnemyPath 를 훑어 마우스에 가장 가까운 경로 세그먼트 위 점을 찾음
    ///   - 스냅 거리(snapThreshold) 안이면 그 점에 녹색 마커, 밖이면 빨간 마커
    ///   - 유효 위치 클릭 → Hero.SetRally(snappedPoint), 조준 종료
    ///   - 무효 위치 클릭 → 무시(조준 유지)
    ///   - ESC/우클릭 → 취소
    /// </summary>
    public class HeroPathRallyController : MonoBehaviour
    {
        public static HeroPathRallyController Instance { get; private set; }

        [Header("선택 사항")]
        [Tooltip("월드 좌표를 얻을 카메라. 비우면 Camera.main 사용.")]
        [SerializeField] private Camera worldCamera;

        [Header("스냅 설정")]
        [Tooltip("마우스가 경로 세그먼트로부터 이만큼 떨어진 곳까지를 \"경로 위 클릭\" 으로 인정.")]
        [Min(0.01f)]
        [SerializeField] private float snapThreshold = 0.8f;

        [Header("미리보기 색상")]
        [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.5f, 0.95f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.95f);

        [Header("미리보기 마커")]
        [Tooltip("랠리 지정 미리보기 LineRenderer 프리팹. null 이면 자동 생성.")]
        [SerializeField] private LineRenderer markerPrefab;
        [Tooltip("자동 생성 시 그릴 원 마커의 반지름.")]
        [SerializeField] private float markerRadius = 0.35f;

        private Hero pendingHero;
        private LineRenderer markerInstance;
        // 진입 프레임 — 같은 클릭이 Portrait 버튼 → SetRally 로 흘러들어오는 것 방지.
        private int enteredFrame = -1;

        // 매 프레임 계산된 스냅 위치 / 유효 여부 — 클릭 시 그대로 사용.
        private Vector3 snappedPos;
        private bool snappedValid;

        public bool IsTargeting => pendingHero != null;
        public Hero PendingHero => pendingHero;

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

        /// <summary>HeroPortrait 가 호출 — 영웅이 살아있을 때만 진입.</summary>
        public void BeginTargeting(Hero hero)
        {
            if (hero == null || hero.IsDead) return;
            CancelTargeting();
            pendingHero = hero;
            enteredFrame = Time.frameCount;
            ShowMarker();
        }

        public void CancelTargeting()
        {
            pendingHero = null;
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

            // 모든 EnemyPath 를 훑어 가장 가까운 경로 위 점을 찾는다.
            ComputeNearestPathPoint(mouseWorld, out snappedPos, out float dist);
            snappedValid = dist <= snapThreshold;

            // 마커는 유효하면 스냅 점에, 무효면 마우스 위치(빨강) 에 표시.
            if (markerInstance != null)
            {
                markerInstance.transform.position = snappedValid ? snappedPos : mouseWorld;
                var c = snappedValid ? validColor : invalidColor;
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
                // Portrait 버튼을 누른 그 클릭이 같은 프레임에 여기로 흘러들어오는 것을 한 프레임 무시.
                if (Time.frameCount == enteredFrame) return;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                if (!snappedValid) return; // 경로 밖 클릭은 흘려보냄

                pendingHero.SetRally(snappedPos);
                CancelTargeting();
            }
        }

        /// <summary>
        /// 씬의 모든 EnemyPath 를 훑어 mouseWorld 에 가장 가까운 segment 위 점을 찾는다.
        /// 결과: nearestPoint = 그 점의 월드 좌표, distance = mouseWorld 와의 거리.
        /// 경로가 하나도 없으면 nearestPoint = mouseWorld, distance = float.MaxValue.
        /// </summary>
        private static void ComputeNearestPathPoint(Vector3 mouseWorld, out Vector3 nearestPoint, out float distance)
        {
            nearestPoint = mouseWorld;
            distance = float.MaxValue;

            // NOTE: 매 프레임 FindObjectsByType 호출 — 경로 수가 적어(보통 4) 부담 적음.
            var paths = Object.FindObjectsByType<EnemyPath>(FindObjectsSortMode.None);
            foreach (var path in paths)
            {
                if (path == null || path.Count < 2) continue;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 a = path.GetPoint(i);
                    Vector3 b = path.GetPoint(i + 1);
                    Vector3 p = ClosestPointOnSegment(a, b, mouseWorld);
                    float d = Vector3.Distance(p, mouseWorld);
                    if (d < distance)
                    {
                        distance = d;
                        nearestPoint = p;
                    }
                }
            }
        }

        /// <summary>선분 ab 위에서 점 p 에 가장 가까운 점을 t∈[0,1] 클램프로 구한다.</summary>
        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) return a; // 사실상 점
            float t = Vector3.Dot(p - a, ab) / lenSq;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        // --- 마커 ---------------------------------------------------------------

        private void ShowMarker()
        {
            if (markerPrefab != null)
            {
                markerInstance = Instantiate(markerPrefab);
            }
            else
            {
                var go = new GameObject("HeroRallyMarker (auto)");
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
