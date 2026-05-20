using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 적 한 종류의 정적 데이터.
    /// Enemy 컴포넌트는 인스턴스화 직후 이 데이터를 받아 자신의 스탯을 초기화한다.
    /// 시각(스프라이트, 애니메이터)은 enemyPrefab 안에 포함시킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy_New", menuName = "KRTD/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("표시 정보")]
        public string enemyName = "Enemy";

        [Header("프리팹")]
        [Tooltip("스포너가 인스턴스화할 프리팹. Enemy 컴포넌트가 붙어 있어야 한다.")]
        public GameObject enemyPrefab;

        [Header("스탯")]
        [Tooltip("최대 체력")]
        public float maxHp = 10f;

        [Tooltip("초당 이동 속도 (월드 유닛/초)")]
        public float moveSpeed = 1.5f;

        [Header("보상")]
        [Tooltip("처치 시 플레이어가 얻는 골드")]
        public int goldReward = 5;

        [Tooltip("골인했을 때 플레이어가 잃는 생명")]
        public int lifeDamage = 1;
    }
}
