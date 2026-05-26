using System.Collections.Generic;
using UnityEngine;
using KRTD.Combat;
using KRTD.Game;
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

        [Header("경제")]
        [Tooltip("타워 판매 시 누적 투자 금액의 몇 % 를 환급할지 (0~1). 예: 0.7 = 70% 환급.")]
        [Range(0f, 1f)]
        [SerializeField] private float sellRefundRate = 0.7f;

        private RadialMenu currentMenu;
        private Camera mainCam;
        private int openedFrame = -1;

        // 관리 메뉴에서 선택 중인 타워. 사거리 원을 메뉴와 함께 끄기 위해 보관.
        private ISelectableTower selectedTower;

        // 메뉴가 열려있는 동안 클릭을 막아둘 스팟. 메뉴 버튼을 빗나간 클릭이
        // BuildSpot.OnMouseDown 으로 다시 들어와 메뉴가 재생성되는 것을 방지.
        private BuildSpot openSpot;

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
                    if (!TrySpendGold(captured.cost, "건설")) return;
                    spot.PlaceBuilding(captured);
                }, cost: captured.cost));
            }

            OpenMenuAt(spot, entries);
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
                    if (!TrySpendGold(next.cost, "업그레이드")) return;
                    spot.ReplaceBuilding(next);
                }, overrideAngleDeg: UpgradeAngleDeg, cost: next.cost));
            }

            // 판매 (6시 고정). 환급 예상액을 음수 cost 로 넘겨 라벨에 "+N" 으로 표시.
            int sellRefund = Mathf.RoundToInt(spot.TotalInvested * sellRefundRate);
            entries.Add(new RadialMenu.Entry(sellIcon, () =>
            {
                SellSpot(spot);
            }, overrideAngleDeg: SellAngleDeg, cost: -sellRefund));

            // 메뉴를 여는 동안 타워의 사거리 원을 표시
            ShowTowerRange(spot);

            OpenMenuAt(spot, entries);
        }

        /// <summary>
        /// 골드를 차감하고 성공 여부를 반환. GameState 가 없으면 경제 무시(true 반환).
        /// 부족 시 콘솔 로그만 남기고 false.
        /// </summary>
        private bool TrySpendGold(int amount, string actionLabel)
        {
            if (amount <= 0) return true;
            var state = GameState.Instance;
            if (state == null) return true;
            if (state.SpendGold(amount)) return true;

            Debug.Log($"[BuildMenuController] {actionLabel} 골드 부족 — 필요: {amount}, 현재: {state.Gold}");
            return false;
        }

        /// <summary>
        /// 스팟의 누적 투자 금액의 sellRefundRate 만큼 환급하고 건물을 제거한다.
        /// </summary>
        private void SellSpot(BuildSpot spot)
        {
            if (spot == null) return;

            var state = GameState.Instance;
            if (state != null)
            {
                int refund = Mathf.RoundToInt(spot.TotalInvested * sellRefundRate);
                if (refund > 0) state.AddGold(refund);
            }
            spot.RemoveBuilding();
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
            // 메뉴 인스턴스가 이미 사라졌거나 강제 종료된 경우에도 스팟은 다시 클릭 가능해야 한다.
            ReleaseOpenSpot();
        }

        private void OpenMenuAt(BuildSpot spot, List<RadialMenu.Entry> entries)
        {
            if (menuPrefab == null)
            {
                Debug.LogWarning("[BuildMenuController] menuPrefab 이 비어있다.");
                return;
            }
            if (entries.Count == 0) return;

            // 이미 열려있으면 즉시 정리. 이전 스팟이 비활성화된 채로 남지 않도록 콜라이더 복원.
            if (currentMenu != null)
            {
                Destroy(currentMenu.gameObject);
                currentMenu = null;
                ReleaseOpenSpot();
            }

            currentMenu = Instantiate(menuPrefab, spot.CenterWorld, Quaternion.identity);
            openedFrame = Time.frameCount;

            // 메뉴가 떠 있는 동안에는 이 스팟의 클릭을 막는다.
            openSpot = spot;
            spot.SetClickable(false);

            currentMenu.Open(entries, onClosedCallback: () =>
            {
                // 자기 자신이 정상 종료된 경우에만 참조 해제
                currentMenu = null;
                // 메뉴가 끝까지 닫힌 시점에 사거리 원과 스팟 콜라이더를 함께 되돌린다
                HideTowerRange();
                ReleaseOpenSpot();
            });
        }

        private void ReleaseOpenSpot()
        {
            if (openSpot != null)
            {
                openSpot.SetClickable(true);
                openSpot = null;
            }
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
