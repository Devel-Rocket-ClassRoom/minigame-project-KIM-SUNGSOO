using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// 용암 지대: 클릭 지점에 일정 시간 지속되는 원형 장판을 깐다. 장판 안에 들어온
    /// 적은 tickInterval 마다 damagePerTick 만큼 마법 데미지를 입는다.
    ///
    /// 경로 위에 깔아 적이 통과하도록 만드는 전술이 주된 용법.
    /// </summary>
    public class LavaZoneAbility : SpecialAbility, IAbilityPreviewRadius
    {
        [Header("장판 형태")]
        [Tooltip("장판 반경 (월드 단위). 미리보기 원과 OverlapCircle 반경에 모두 사용.")]
        [SerializeField] private float radius = 1.8f;

        [Tooltip("총 지속 시간(초).")]
        [SerializeField] private float duration = 5f;

        [Tooltip("틱 간격(초). 작을수록 빠르게 데미지가 들어온다.")]
        [SerializeField] private float tickInterval = 0.4f;

        [Tooltip("틱 1회 데미지. Enemy 의 방어력 계산에 attackType 이 적용된다.")]
        [SerializeField] private float damagePerTick = 1.5f;

        [Tooltip("공격 유형. 기본 Magic.")]
        [SerializeField] private AttackType attackType = AttackType.Magic;

        [Header("프리팹 (선택)")]
        [Tooltip("LavaZone 컴포넌트가 붙은 시각 프리팹. 비워두면 코드가 빈 GameObject 에 LavaZone 만 붙여 자동 외곽선으로 표시한다.")]
        [SerializeField] private LavaZone lavaZonePrefab;

        public float PreviewRadius => radius;

        protected override void Perform(Vector3 worldPos)
        {
            LavaZone zone;
            if (lavaZonePrefab != null)
            {
                zone = Instantiate(lavaZonePrefab, worldPos, Quaternion.identity);
            }
            else
            {
                var go = new GameObject("LavaZone");
                go.transform.position = worldPos;
                zone = go.AddComponent<LavaZone>();
            }
            zone.Init(radius, damagePerTick, tickInterval, duration, attackType);
        }
    }
}
