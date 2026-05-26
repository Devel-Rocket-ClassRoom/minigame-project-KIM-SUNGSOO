using System.Collections;
using UnityEngine;
using KRTD.Combat;

namespace KRTD.Abilities
{
    /// <summary>
    /// 유성우: 클릭 지점을 중심으로 castRadius 반경 안에 meteorCount 개의 유성을
    /// intervalSeconds 간격으로 차례차례 떨어뜨린다. 각 유성은 임팩트 시 AoE 데미지를 가한다.
    /// </summary>
    public class MeteorShowerAbility : SpecialAbility, IAbilityPreviewRadius
    {
        [Header("유성우 형태")]
        [Tooltip("발사할 유성 프리팹 (Meteor 컴포넌트 포함).")]
        [SerializeField] private Meteor meteorPrefab;

        [Tooltip("총 떨어질 유성 개수.")]
        [Min(1)]
        [SerializeField] private int meteorCount = 5;

        [Tooltip("클릭 지점 중심의 살포 반경 (월드 단위). 이 안 어딘가에 랜덤 임팩트.")]
        [SerializeField] private float castRadius = 2.5f;

        [Tooltip("유성 사이 간격 (초). 0 이면 한꺼번에 떨어짐.")]
        [Min(0f)]
        [SerializeField] private float intervalSeconds = 0.18f;

        [Header("개별 유성 임팩트")]
        [Tooltip("각 유성 1발의 AoE 반경.")]
        [SerializeField] private float impactRadius = 1.2f;

        [Tooltip("각 유성 1발의 데미지.")]
        [SerializeField] private float damagePerMeteor = 6f;

        [Tooltip("공격 유형. Enemy 의 방어력 계산에 사용. 기본 Magic.")]
        [SerializeField] private AttackType attackType = AttackType.Magic;

        public float PreviewRadius => castRadius;

        protected override void Perform(Vector3 worldPos)
        {
            if (meteorPrefab == null)
            {
                Debug.LogWarning($"[MeteorShowerAbility] {name}: meteorPrefab 이 비어있다.");
                return;
            }

            StartCoroutine(DropSequence(worldPos));
        }

        private IEnumerator DropSequence(Vector3 center)
        {
            for (int i = 0; i < meteorCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * castRadius;
                Vector3 impact = center + new Vector3(offset.x, offset.y, 0f);

                Meteor m = Instantiate(meteorPrefab);
                m.Init(impact, damagePerMeteor, impactRadius, attackType);

                if (intervalSeconds > 0f && i < meteorCount - 1)
                    yield return new WaitForSeconds(intervalSeconds);
            }
        }
    }
}
