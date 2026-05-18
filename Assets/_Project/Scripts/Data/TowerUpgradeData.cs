using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Data
{
    /// <summary>
    /// 타워의 한 티어(Tier) 데이터.
    /// 공격력/사거리/쿨다운/투사체 등 전투 능력치 + 비주얼 차이를 담음.
    /// nextUpgrades 가 비어 있으면 최종 티어로 간주.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerTier", menuName = "KRTD/Tower Upgrade (Tier)", order = 1)]
    public class TowerUpgradeData : ScriptableObject
    {
        [Header("Tier Info")]
        public int tier = 1;                // 1, 2, 3
        public string displayName;          // 예: "Archer Keep"
        public Sprite icon;
        public int upgradeCost = 75;

        [Header("Combat Stats")]
        public float damage = 5f;
        public float range = 4f;
        public float fireRate = 1f;         // 초당 발사 횟수
        public DamageType damageType = DamageType.Physical;
        [Tooltip("이 타워가 공격할 수 있는 적의 분류 (비워두면 전체)")]
        public List<EnemyArmorType> canTarget = new();

        [Header("Splash (옵션)")]
        public float splashRadius = 0f;     // 0이면 단일 타겟

        [Header("Visual / Behavior")]
        public GameObject visualPrefab;     // 티어별 외형(보통 메인 프리펩 안의 _Visual에 swap)
        public GameObject projectilePrefab; // 발사할 투사체 (근접타워면 null)

        [Header("Branching (옵션)")]
        [Tooltip("다음 티어 후보들. 1개면 일직선, 2개 이상이면 분기 트리.")]
        public List<TowerUpgradeData> nextUpgrades = new();

        public bool IsFinalTier => nextUpgrades == null || nextUpgrades.Count == 0;
    }
}
