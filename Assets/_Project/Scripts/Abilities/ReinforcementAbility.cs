using UnityEngine;
using UnityEngine.Tilemaps;
using KRTD.Combat;
using KRTD.Map;

namespace KRTD.Abilities
{
    /// <summary>
    /// 지원군 소환: 클릭 지점 주변에 보병 N 명을 즉시 스폰한다.
    /// 보병은 평소처럼 자기 사거리 내 적을 자동 교전하며, 죽으면 사라진다 (부활 없음).
    ///
    /// 배치 형태: 클릭 지점 중심, 수평으로 균등 분산 (formationSpacing 간격).
    /// </summary>
    public class ReinforcementAbility : SpecialAbility, IAbilityPreviewRadius
    {
        [Header("지원군 스폰")]
        [Tooltip("스폰할 보병 프리팹. Soldier 컴포넌트가 붙어있어야 한다.")]
        [SerializeField] private Soldier soldierPrefab;

        [Tooltip("한 번 발동에 스폰될 보병 수.")]
        [Min(1)]
        [SerializeField] private int soldierCount = 3;

        [Tooltip("보병 간 간격 (월드 단위). 수평 일렬로 분산.")]
        [SerializeField] private float formationSpacing = 0.5f;

        [Header("스탯 배율 (지원군 강화)")]
        [Tooltip("보병 기본 HP 에 곱할 배율. 1 = 원본 그대로.")]
        [SerializeField] private float hpMultiplier = 1f;

        [Tooltip("보병 기본 데미지에 곱할 배율. 1 = 원본 그대로.")]
        [SerializeField] private float damageMultiplier = 1f;

        [Header("시한부 (만료)")]
        [Tooltip("스폰 후 이 시간(초)이 지나면 지원군이 자동으로 사망한다. 0 이면 영구(만료 없음).")]
        [Min(0f)]
        [SerializeField] private float lifetimeSeconds = 10f;

        [Header("배치 가능 영역")]
        [Tooltip("이 Tilemap 위에 타일이 있는 셀에만 지원군 배치 가능. " +
            "비워두면 어디든 OK (이전 동작). 보통 씬의 PathTileMap 을 드래그.")]
        [SerializeField] private Tilemap pathTilemap;

        public float PreviewRadius => Mathf.Max(0.3f, (soldierCount - 1) * formationSpacing * 0.5f + 0.3f);

        /// <summary>
        /// 지원군은 경로 타일 위에만 배치 가능.
        /// 인스펙터 pathTilemap 우선, 비어있으면 PathTilemapMarker 자동 탐색.
        /// 둘 다 못 찾으면 안전망으로 항상 OK (이전 동작).
        /// </summary>
        public override bool IsValidPlacement(Vector3 worldPos)
        {
            var map = ResolveTilemap();
            if (map == null) return true;
            var cell = map.WorldToCell(worldPos);
            return map.HasTile(cell);
        }

        // 인스펙터 참조 → 마커 자동 탐색 → null 순. 결과를 캐싱해 재탐색 비용 줄임.
        private Tilemap cachedResolved;
        private Tilemap ResolveTilemap()
        {
            if (pathTilemap != null) return pathTilemap;
            if (cachedResolved != null) return cachedResolved;

            // Stage prefab 동적 인스턴스화 대비 — 매 호출마다 한 번씩 가볍게 시도하지 않고,
            // 인스턴스가 사라진 경우 다시 탐색하기 위해 매번 조회 (FindFirst 는 한 번 호출 비용 작음).
            var marker = Object.FindFirstObjectByType<PathTilemapMarker>();
            cachedResolved = marker != null ? marker.Tilemap : null;
            return cachedResolved;
        }

        protected override void Perform(Vector3 worldPos)
        {
            if (soldierPrefab == null)
            {
                Debug.LogWarning($"[ReinforcementAbility] {name}: soldierPrefab 이 비어있다.");
                return;
            }

            // 클릭 지점 기준으로 좌우 균등 분산. count=1 이면 정확히 클릭 지점.
            float half = (soldierCount - 1) * formationSpacing * 0.5f;
            for (int i = 0; i < soldierCount; i++)
            {
                Vector3 pos = worldPos + new Vector3(-half + i * formationSpacing, 0f, 0f);
                Soldier s = Instantiate(soldierPrefab, pos, Quaternion.identity);
                s.SetRallyPoint(pos);
                if (!Mathf.Approximately(hpMultiplier, 1f) || !Mathf.Approximately(damageMultiplier, 1f))
                    s.ApplyTier(hpMultiplier, damageMultiplier);

                // 시한부 만료 타이머 부착 (lifetimeSeconds > 0 일 때만).
                if (lifetimeSeconds > 0f)
                {
                    var lifetime = s.gameObject.AddComponent<ReinforcementLifetime>();
                    lifetime.Setup(lifetimeSeconds);
                }
            }
        }
    }
}
