using UnityEngine;
using KRTD.Data;
using KRTD.Economy;

namespace KRTD.Towers
{
    /// <summary>
    /// 맵에 미리 배치되는 "고정 건설 슬롯".
    /// 비어 있을 땐 UI(라디얼 메뉴)를 띄워서 타워 선택을 받고,
    /// 선택되면 해당 타워 프리펩을 자신의 위치에 인스턴스화.
    /// </summary>
    public class TowerSlot : MonoBehaviour
    {
        [SerializeField] private Transform spawnAnchor;

        public TowerController CurrentTower { get; private set; }
        public bool IsEmpty => CurrentTower == null;

        private void Reset()
        {
            spawnAnchor = transform;
        }

        /// <summary>UI에서 타워가 선택되면 호출.</summary>
        public bool TryBuild(TowerData data)
        {
            if (!IsEmpty || data == null || data.towerPrefab == null) return false;
            if (!GoldManager.Instance.TrySpend(data.buildCost)) return false;

            var go = Instantiate(data.towerPrefab, spawnAnchor.position, spawnAnchor.rotation, transform);
            var ctrl = go.GetComponent<TowerController>();
            ctrl.SetTowerData(data);
            CurrentTower = ctrl;
            return true;
        }

        /// <summary>타워 판매(언제든 슬롯을 다시 빈 상태로).</summary>
        public void Sell()
        {
            if (CurrentTower == null) return;
            GoldManager.Instance.Add(Mathf.RoundToInt(CurrentTower.TowerData.buildCost * 0.5f));
            Destroy(CurrentTower.gameObject);
            CurrentTower = null;
        }

        private void OnMouseDown()
        {
            // 임시: 슬롯 클릭 시 UI 매니저에 알림 (UI 시스템과 연결 필요)
            UI.TowerBuildMenu.Instance?.Open(this);
        }
    }
}
