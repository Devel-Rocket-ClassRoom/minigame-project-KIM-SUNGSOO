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

        [Header("방어력 (공격 유형별 flat 차감)")]
        [Tooltip("물리 공격을 받을 때 데미지에서 차감되는 방어력.")]
        public float physicalDefense = 0f;

        [Tooltip("마법 공격을 받을 때 데미지에서 차감되는 방어력.")]
        public float magicDefense = 0f;

        [Tooltip("방어력 적용 후 최소 데미지. 0 이면 면역도 가능, 1 이면 항상 최소 1 데미지 들어감.")]
        public float minDamage = 1f;

        [Header("공격 (보병/지원군에 가하는 공격)")]
        [Tooltip("0 이면 공격 안 함 (지나가기만 하는 적). 보병을 만나면 멈춰서 이 데미지로 때린다.")]
        public float attackDamage = 0f;

        [Tooltip("이 거리 안의 보병만 공격한다. 근접은 0.6~1.2, 원거리(BlackArcher 등) 는 3 이상.")]
        public float attackRange = 0.8f;

        [Tooltip("이 거리 안에 보병이 들어오면 적이 멈춘다 (공격은 attackRange 가 되어야 시작). " +
            "근접 적은 attackRange 보다 크게 설정해 '보병이 다가올 때까지 대기' 가능. " +
            "0 이거나 attackRange 이하면 attackRange 와 동일하게 동작.")]
        public float detectionRange = 2.5f;

        [Tooltip("연속 공격 사이의 텀(초).")]
        public float attackInterval = 1f;

        [Tooltip("적의 공격 유형. 보병 방어력 계산에 사용.")]
        public AttackType attackType = AttackType.Physical;

        [Tooltip("원거리 공격에 사용할 화살(혹은 마법) 투사체 프리팹. " +
            "비워두면 즉시 데미지(근접). 설정하면 적 위치에서 보병까지 날아간 뒤 데미지.")]
        public Arrow arrowPrefab;
    }
}
