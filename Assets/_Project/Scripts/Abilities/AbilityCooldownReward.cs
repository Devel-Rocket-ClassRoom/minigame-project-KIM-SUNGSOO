using System.Collections.Generic;
using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// "다음 웨이브 일찍 부르기" 보상 — WaveDirector.OnWaveSkipped 를 구독해
    /// 등록된 SpecialAbility 들의 쿨다운을 일괄 단축한다.
    ///
    /// 책임 분리:
    ///   - WaveDirector 는 "스킵 발생했다" 만 알리고,
    ///   - 이 컴포넌트가 "어떻게 보상할지" 를 정한다 (능력 목록 / 감소량 정책).
    /// </summary>
    public class AbilityCooldownReward : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("이벤트를 구독할 WaveDirector. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private WaveDirector director;

        [Tooltip("쿨다운을 단축해줄 능력들. 비워두면 씬의 모든 SpecialAbility 자동 수집.")]
        [SerializeField] private List<SpecialAbility> abilities = new List<SpecialAbility>();

        [Header("보상")]
        [Tooltip("스킵 1회당 각 능력의 쿨다운을 줄여줄 초.")]
        [Min(0f)]
        [SerializeField] private float reductionSeconds = 5f;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<WaveDirector>();
            if (abilities.Count == 0)
            {
                // 비워뒀으면 씬에서 전부 수집 (비활성 포함하려면 includeInactive=true)
                var found = FindObjectsOfType<SpecialAbility>(includeInactive: true);
                abilities.AddRange(found);
            }
        }

        private void OnEnable()
        {
            if (director != null) director.OnWaveSkipped += HandleWaveSkipped;
        }

        private void OnDisable()
        {
            if (director != null) director.OnWaveSkipped -= HandleWaveSkipped;
        }

        // savedSeconds 인자는 추후 "절약 시간 비례" 정책으로 바꾸고 싶을 때 활용 가능.
        private void HandleWaveSkipped(float savedSeconds)
        {
            _ = savedSeconds;
            if (reductionSeconds <= 0f) return;

            foreach (var ab in abilities)
            {
                if (ab == null) continue;
                ab.ReduceCooldown(reductionSeconds);
            }
        }
    }
}
