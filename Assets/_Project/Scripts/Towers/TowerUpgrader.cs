using UnityEngine;
using KRTD.Data;
using KRTD.Core;
using KRTD.Economy;

namespace KRTD.Towers
{
    /// <summary>
    /// 타워 티어 진행을 관리.
    /// - Initialize 시 Tier 1 적용
    /// - Upgrade(branchIndex) 호출 시 다음 티어로 진행 (분기 인덱스로 트리 처리)
    /// </summary>
    public class TowerUpgrader : MonoBehaviour
    {
        private TowerController owner;
        private TowerData data;

        public TowerUpgradeData CurrentTier { get; private set; }
        public int CurrentTierIndex { get; private set; } = -1;

        public void Initialize(TowerController controller, TowerData towerData)
        {
            owner = controller;
            data = towerData;
            if (data != null && data.upgradeChain.Count > 0)
                ApplyTier(data.upgradeChain[0], 0);
        }

        /// <summary>
        /// 다음 티어로 업그레이드. branchIndex는 분기 트리에서 어떤 가지로 갈지 선택.
        /// 단일 라인이면 0으로 고정.
        /// </summary>
        public bool TryUpgrade(int branchIndex = 0)
        {
            if (CurrentTier == null) return false;
            if (CurrentTier.IsFinalTier) return false;
            if (branchIndex < 0 || branchIndex >= CurrentTier.nextUpgrades.Count) return false;

            var next = CurrentTier.nextUpgrades[branchIndex];
            if (!GoldManager.Instance.TrySpend(next.upgradeCost)) return false;

            ApplyTier(next, CurrentTierIndex + 1);
            return true;
        }

        private void ApplyTier(TowerUpgradeData tier, int index)
        {
            CurrentTier = tier;
            CurrentTierIndex = index;

            // 비주얼 스왑
            SwapVisual(tier.visualPrefab);

            // 사거리/공격 컴포넌트에 반영
            owner.TargetFinder.ApplyTier(tier);
            if (owner.Shooter != null) owner.Shooter.ApplyTier(tier);

            EventBus.Raise(new TowerUpgradedEvent(owner, tier));
        }

        private void SwapVisual(GameObject visualPrefab)
        {
            var root = owner.VisualRoot;
            if (root == null || visualPrefab == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);

            Instantiate(visualPrefab, root);
        }
    }

    public readonly struct TowerUpgradedEvent
    {
        public readonly TowerController Tower;
        public readonly TowerUpgradeData NewTier;
        public TowerUpgradedEvent(TowerController t, TowerUpgradeData tier) { Tower = t; NewTier = tier; }
    }
}
