using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Data
{
    /// <summary>
    /// 타워 한 종류의 전체 정의.
    /// Tier 1 → Tier 2 → Tier 3 까지의 업그레이드 체인을 List로 보관해,
    /// 새 타워 추가 시 SO만 만들면 코드 수정 없이 게임에 반영됨.
    ///
    /// 분기 트리가 필요하면 TowerUpgradeData.nextUpgrades 에 여러 항목을 넣으면 됨.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerData", menuName = "KRTD/Tower Data", order = 0)]
    public class TowerData : ScriptableObject
    {
        [Header("Identity")]
        public string towerId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Build")]
        public int buildCost = 50;
        public GameObject towerPrefab;          // _Base 타워 프리펩 (TowerController 포함)

        [Header("Upgrade Chain (Tier 1 → 2 → 3)")]
        public List<TowerUpgradeData> upgradeChain = new();
    }
}
