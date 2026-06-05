using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 영웅 한 종류의 정적 데이터.
    /// Hero 컴포넌트가 인스턴스화 직후 이 데이터를 읽어 스탯을 초기화한다.
    /// 시각(스프라이트, 애니메이터)은 heroPrefab 안에 포함.
    /// </summary>
    [CreateAssetMenu(fileName = "Hero_New", menuName = "KRTD/Hero Data")]
    public class HeroData : ScriptableObject
    {
        [Header("표시 정보")]
        public string heroName = "Hero";

        [Header("프리팹")]
        [Tooltip("HeroSpawner 가 인스턴스화할 프리팹. Hero 컴포넌트가 붙어 있어야 한다.")]
        public GameObject heroPrefab;

        [Header("스탯 (일반 보병보다 한 단계 위 권장)")]
        public float maxHp = 30f;
        public float damage = 5f;

        [Tooltip("이 거리 안의 적만 공격 대상으로 한다.")]
        public float attackRange = 1.5f;

        [Tooltip("연속 공격 사이의 텀(초).")]
        public float attackInterval = 0.8f;

        [Tooltip("랠리 이동 속도(월드 유닛/초).")]
        public float moveSpeed = 2.5f;

        public AttackType attackType = AttackType.Physical;

        [Header("방어력")]
        public float physicalDefense = 1f;
        public float magicDefense = 1f;
        public float minDamage = 1f;

        [Header("사망/부활")]
        [Tooltip("HP 0 이 되면 이 시간 후 시작 위치(또는 마지막 랠리)에 부활. 게임오버 X.")]
        [Min(0f)]
        public float respawnDelay = 5f;

        [Tooltip("부활 위치 — true 면 마지막 랠리, false 면 HeroSpawner 의 시작 위치.")]
        public bool respawnAtLastRally = true;

        [Header("HP 자연 회복 (비전투 시)")]
        [Tooltip("마지막 전투 후 이 시간이 지나면 HP 자연 회복 시작. 0 이면 회복 비활성.")]
        [Min(0f)]
        public float combatOutOfTime = 5f;

        [Tooltip("비전투 시 초당 회복량.")]
        [Min(0f)]
        public float hpRegenPerSec = 5f;
    }
}
