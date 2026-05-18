using UnityEngine;

namespace KRTD.Data
{
    /// <summary>
    /// 적 한 종류의 정의.
    /// 새 적을 만들 때: EnemyData SO만 만들고 enemyPrefab을 연결하면 끝.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "KRTD/Enemy Data", order = 10)]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string enemyId;
        public string displayName;

        [Header("Stats")]
        public float maxHP = 50f;
        public float moveSpeed = 2f;
        public EnemyArmorType armorType = EnemyArmorType.Light;
        public float physicalResist = 0f;   // 0~1, 데미지 감소 비율
        public float magicResist = 0f;

        [Header("Reward")]
        public int goldOnKill = 5;
        public int livesOnReach = 1;        // 골인 시 플레이어가 잃을 라이프

        [Header("Visual")]
        public GameObject enemyPrefab;      // 적 본체 프리펩
    }
}
