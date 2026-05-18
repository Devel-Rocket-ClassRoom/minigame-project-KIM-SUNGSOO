using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Data;
using KRTD.Towers;

namespace KRTD.UI
{
    /// <summary>
    /// 빈 슬롯 클릭 시 뜨는 라디얼/팝업 메뉴.
    /// LevelData.allowedTowers를 기준으로 버튼을 동적으로 생성.
    /// </summary>
    public class TowerBuildMenu : MonoBehaviour
    {
        public static TowerBuildMenu Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private Transform buttonParent;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private LevelData levelData;

        private TowerSlot currentSlot;

        private void Awake()
        {
            Instance = this;
            root.SetActive(false);
        }

        public void Open(TowerSlot slot)
        {
            currentSlot = slot;
            root.transform.position = slot.transform.position;
            BuildButtons();
            root.SetActive(true);
        }

        public void Close()
        {
            currentSlot = null;
            root.SetActive(false);
        }

        private void BuildButtons()
        {
            for (int i = buttonParent.childCount - 1; i >= 0; i--)
                Destroy(buttonParent.GetChild(i).gameObject);

            foreach (var tower in levelData.allowedTowers)
            {
                var btn = Instantiate(buttonPrefab, buttonParent);
                var img = btn.GetComponent<Image>();
                if (img != null && tower.icon != null) img.sprite = tower.icon;

                var captured = tower;
                btn.onClick.AddListener(() =>
                {
                    if (currentSlot != null && currentSlot.TryBuild(captured)) Close();
                });
            }
        }
    }
}
