using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// Soldier 에 부착되어 일정 시간 후 Expire() 를 호출하는 만료 타이머.
    /// 지원군(Reinforcement) 처럼 시한부 보병에 사용.
    /// 보병이 그 전에 적에게 죽으면 OnDestroy 로 자동 정리.
    /// </summary>
    [RequireComponent(typeof(Soldier))]
    public class ReinforcementLifetime : MonoBehaviour
    {
        [Tooltip("이 시간(초) 경과 후 보병이 자동으로 만료 사망한다.")]
        [Min(0.1f)]
        [SerializeField] private float lifetimeSeconds = 10f;

        private Soldier soldier;
        private float expireAt;

        /// <summary>
        /// 외부(예: ReinforcementAbility) 가 스폰 직후 호출해 만료 시간을 설정.
        /// 호출하지 않으면 인스펙터의 lifetimeSeconds 가 그대로 적용된다.
        /// </summary>
        public void Setup(float seconds)
        {
            lifetimeSeconds = Mathf.Max(0.1f, seconds);
            expireAt = Time.time + lifetimeSeconds;
        }

        private void Awake()
        {
            soldier = GetComponent<Soldier>();
        }

        private void Start()
        {
            // Setup 이 안 호출됐을 경우 인스펙터 값 기준으로 시작 시점 고정.
            if (expireAt <= 0f) expireAt = Time.time + lifetimeSeconds;
        }

        private void Update()
        {
            if (soldier == null || soldier.IsDead) return;
            if (Time.time >= expireAt) soldier.Expire();
        }
    }
}
