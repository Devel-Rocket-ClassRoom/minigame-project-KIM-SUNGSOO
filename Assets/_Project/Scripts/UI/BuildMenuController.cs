using System.Collections.Generic;
using UnityEngine;
using KRTD.Combat;
using KRTD.Map;

namespace KRTD.UI
{
    /// <summary>
    /// 씬에 하나 존재하는 라디얼 메뉴 컨트롤러.
    /// BuildSpot 의 OnMouseDown 에서 ShowBuildMenu / ShowManageMenu 를 호출한다.
    ///
    /// 책임:
    ///   - 라디얼 메뉴 인스턴스를 1개만 유지 (열려있으면 먼저 닫기)
    ///   - 메뉴 영역 밖 클릭 시 자동으로 닫기
    /// </summary>
    public class BuildMenuController : MonoBehaviour
    {
        public static BuildMenuController Instance { get; private set; }

        [Header("프리팹/설정")]
        [Tooltip("라디얼 메뉴 본체 프리팹. RadialMenu 컴포넌트 + itemPrefab 이 세팅돼 있어야 함.")]
        [SerializeField] private RadialMenu menuPrefab;

        [Tooltip("빈 스팟 클릭 시 보여줄 건물 목록.")]
        [SerializeField] private BuildMenuConfig buildConfig;

        [Header("관리 메뉴 아이콘 (점유된 스팟용)")]
        [Tooltip("업그레이드 버튼 아이콘.")]
        [SerializeField] private Sprite upgradeIcon;

        [Tooltip("판매 버튼 아이콘.")]
        [SerializeField] private Sprite sellIcon;

        private RadialMenu currentMenu;
        private Camera mainCam;
        private int openedFrame = -1;

        // 관리 메뉴에서 선택 중인 타워. 사거리 원을 메뉴와 함께 끄기 위해 보관.
        private ISelectableTower selectedTower;

        // 관리 메뉴 고정 위치 (도). 0 = 12시, 180 = 6시.
        private const float UpgradeAngleDeg = 0f;
        private const float SellAngleDeg = 180f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            mainCam = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 빈 스팟 위에 라디얼 건설 메뉴를 연다.
        /// </summary>
        public void ShowBuildMenu(BuildSpot spot)
        {
            if (spot == null || buildConfig == null) return;

            var entries = new List<RadialMenu.Entry>();
            foreach (var data in buildConfig.buildableBuildings)
            {
                if (data == null) continue;
                var captured = data;
                entries.Add(new RadialMenu.Entry(captured.icon, () =>
                {
                    spot.PlaceBuilding(captured);
                }));
            }

            OpenMenuAt(spot.CenterWorld, entries);
        }

        /// <summary>
        /// 점유된 스팟 위에 관리(업그레이드/판매) 라디얼 메뉴를 연다.
        /// 메뉴가 열려있는 동안에는 해당 타워의 사거리 원이 표시된다.
        /// 자리 배치는 고정: 업그레이드 = 12시, 판매 = 6시.
        /// </summary>
        public void ShowManageMenu(BuildSpot spot)
        {
            if (spot == null) return;

            var entries = new List<RadialMenu.Entry>();
            var current = spot.CurrentBuilding;

            // 업그레이드: 다음 단계가 정의된 경우만 노출 (12시 고정)
            if (current != null && current.CanUpgrade)
            {
                var next = current.nextUpgrade;
                entries.Add(new RadialMenu.Entry(upgradeIcon, () =>
                {
                    spot.ReplaceBuilding(next);
                }, overrideAngleDeg: UpgradeAngleDeg));
            }

            // 판매 (6시 고정)
            entries.Add(new RadialMenu.Entry(sellIcon, () =>
            {
                spot.RemoveBuilding();
            }, overrideAngleDeg: SellAngleDeg));

            // 메뉴를 여는 동안 타워의 사거리 원을 표시
            ShowTowerRange(spot);

            OpenMenuAt(spot.CenterWorld, entries);
        }

        private void ShowTowerRange(BuildSpot spot)
        {
            // 이전 선택의 사거리가 남아있으면 먼저 끈다
            HideTowerRange();

            selectedTower = spot.GetBuildingComponent<ISelectableTower>();
            selectedTower?.SetRangeVisible(true);
        }

        private void HideTowerRange()
        {
            // 판매로 타워 인스턴스가 이미 파괴됐을 수 있으므로 UnityEngine.Object 캐스트로 살아있는지 확인.
            // (인터페이스 참조는 C# 기본 동등성을 따라 Unity 의 == 오버로드를 우회한다.)
            var asObj = selectedTower as Object;
            if (asObj != null)
            {
                selectedTower.SetRangeVisible(false);
            }
            selectedTower = null;
        }

        public void CloseMenu()
        {
            HideTowerRange();
            if (currentMenu != null)
            {
                currentMenu.Close();
                currentMenu = null;
            }
        }

        private void OpenMenuAt(Vector3 worldPos, List<RadialMenu.Entry> entries)
        {
            if (menuPrefab == null)
            {
                Debug.LogWarning("[BuildMenuController] menuPrefab 이 비어있다.");
                return;
            }
            if (entries.Count == 0) return;

            // 이미 열려있으면 즉시 정리
            if (currentMenu != null)
            {
                Destroy(currentMenu.gameObject);
                currentMenu = null;
            }

            currentMenu = Instantiate(menuPrefab, worldPos, Quaternion.identity);
            openedFrame = Time.frameCount;
            currentMenu.Open(entries, onClosedCallback: () =>
            {
                // 자기 자신이 정상 종료된 경우에만 참조 해제
                currentMenu = null;
                // 메뉴가 끝까지 닫힌 시점에 사거리 원도 함께 끈다
                HideTowerRange();
            });
        }

        private void Update()
        {
            if (currentMenu == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 메뉴를 막 연 프레임에서는 같은 클릭으로 닫지 않는다.
            if (Time.frameCount == openedFrame) return;

            // 카메라 캐싱이 null 일 수 있음 (씬 전환 등)
            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) return;
            }

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 p = mouseWorld;

            // 아이템 위 클릭은 RadialMenuItem 의 OnMouseDown 이 처리한다.
            // 우리는 "메뉴 밖" 클릭만 잡아서 닫는다.
            if (currentMenu.ContainsWorldPoint(p)) return;

            // BuildSpot 위 클릭은 BuildSpot.OnMouseDown 이 새 메뉴를 열도록 위임 →
            // 여기서는 그냥 닫기만 한다 (그 다음 프레임에 BuildSpot 이 다시 열어줌).
            CloseMenu();
        }
    }
}
