using UnityEngine;
using UnityEngine.UI;
using KRTD.Towers;

namespace KRTD.UI
{
    /// <summary>
    /// 이미 건설된 타워 클릭 시 뜨는 업그레이드/판매 메뉴.
    /// 분기 트리가 있으면 nextUpgrades.Count 만큼 버튼이 동적으로 생김.
    /// </summary>
    public class TowerUpgradeMenu : MonoBehaviour
    {
        public static TowerUpgradeMenu Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private Transform branchButtonParent;
        [SerializeField] private Button branchButtonPrefab;
        [SerializeField] private Button sellButton;

        private TowerController currentTower;

        private void Awake()
        {
            Instance = this;
            root.SetActive(false);
        }

        public void Open(TowerController tower)
        {
            currentTower = tower;
            root.transform.position = tower.transform.position;
            BuildBranchButtons();
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(() =>
            {
                var slot = tower.GetComponentInParent<TowerSlot>();
                slot?.Sell();
                Close();
            });
            root.SetActive(true);
        }

        public void Close()
        {
            currentTower = null;
            root.SetActive(false);
        }

        private void BuildBranchButtons()
        {
            for (int i = branchButtonParent.childCount - 1; i >= 0; i--)
                Destroy(branchButtonParent.GetChild(i).gameObject);

            var cur = currentTower.Upgrader.CurrentTier;
            if (cur == null || cur.IsFinalTier) return;

            for (int i = 0; i < cur.nextUpgrades.Count; i++)
            {
                int idx = i;
                var next = cur.nextUpgrades[i];
                var btn = Instantiate(branchButtonPrefab, branchButtonParent);
                var img = btn.GetComponent<Image>();
                if (img != null && next.icon != null) img.sprite = next.icon;

                btn.onClick.AddListener(() =>
                {
                    if (currentTower.Upgrader.TryUpgrade(idx)) BuildBranchButtons();
                });
            }
        }
    }
}
