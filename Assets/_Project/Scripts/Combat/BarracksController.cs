using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using KRTD.Map;

namespace KRTD.Combat
{
    /// <summary>
    /// 배럭 건물의 행동 로직.
    /// 자신이 소유한 spawn 위치 N 개에 보병 1명씩 유지하며,
    /// 보병이 죽으면 일정 시간 뒤 흙먼지 이펙트 → 새 보병 등장 순으로 부활시킨다.
    ///
    /// 배치 정책:
    ///   - 기본: EnemyPath 의 "가장 가까운 점" 을 찾고, 그 점의 경로 진행 방향에
    ///           수직으로 N 명을 균등 분산한다 (방어선 형태).
    ///   - useNearestPath=false 이거나 EnemyPath 가 없으면 인스펙터의 spawnPoints[] 를 폴백으로 사용.
    ///
    /// 구조 권장:
    ///   Barracks (이 컴포넌트, BuildSpot 의 buildingPrefab 으로 인스턴스화)
    ///   ├─ Body              (SpriteRenderer - Barracks 그림)
    ///   └─ (선택) SpawnPoint_1 ... SpawnPoint_3   ← 폴백용. path 모드에서는 무시됨.
    /// </summary>
    public class BarracksController : MonoBehaviour, ISelectableTower
    {
        [Header("유닛")]
        [Tooltip("이 배럭에서 소환할 보병 프리팹 (Soldier 컴포넌트 포함).")]
        [SerializeField] private Soldier soldierPrefab;

        [Tooltip("동시에 운영할 보병 수. path 모드에서 형태 분산에도 사용된다.")]
        [Min(1)]
        [SerializeField] private int soldierCount = 3;

        [Header("티어 (배럭 Lv별 보병 강화)")]
        [Tooltip("스폰된 보병의 HP 배율. Lv1=1.0, Lv2=1.5, Lv3=2.0 권장.")]
        [SerializeField] private float soldierHpMultiplier = 1f;
        [Tooltip("스폰된 보병의 데미지 배율. Lv1=1.0, Lv2=1.5, Lv3=2.0 권장.")]
        [SerializeField] private float soldierDamageMultiplier = 1f;

        [Header("경로 기반 배치 (권장)")]
        [Tooltip("켜져 있으면 EnemyPath 의 가장 가까운 지점을 자동으로 찾아 그 주변에 보병을 분산 배치한다.")]
        [SerializeField] private bool useNearestPath = true;

        [Tooltip("명시적으로 사용할 EnemyPath. 비워두면 씬에서 자동 탐색.")]
        [SerializeField] private EnemyPath path;

        [Tooltip("랠리 포인트로부터 각 보병까지의 거리 (월드 단위). " +
            "3명 이상이면 정다각형의 외접원 반지름(꼭짓점 0번은 적이 오는 쪽). " +
            "1명이면 무시, 2명이면 좌우 분산 폭의 절반.")]
        [SerializeField] private float formationSpacing = 0.4f;

        [Header("배치 사거리")]
        [Tooltip("배럭이 보병을 배치할 수 있는 최대 반지름 (월드 단위). " +
            "범위 내에 경로가 있으면 그 지점에 스폰, 범위 밖이면 배럭 위치에 스폰 (이 배럭은 경로를 못 막는다는 시각적 표시).")]
        [SerializeField] private float deploymentRange = 5f;

        [Tooltip("사거리 원의 색 (에디터 기즈모).")]
        [SerializeField] private Color rangeGizmoColor = new Color(0.3f, 0.6f, 1f, 0.85f);

        [Header("랠리 가능 타일맵")]
        [Tooltip("이 Tilemap 의 타일이 있는 셀에만 랠리 가능. " +
            "비워두면 씬의 PathTilemapMarker 를 자동 탐색. 둘 다 없으면 PathTile 제약 해제(사거리만 검사).")]
        [SerializeField] private Tilemap pathTilemap;

        [Header("폴백 (path 모드가 꺼졌거나 EnemyPath 가 없을 때)")]
        [Tooltip("폴백 시 사용될 위치들. soldierCount 보다 적으면 그만큼만 소환된다.")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("부활")]
        [Tooltip("보병 사망 후 새 보병이 등장하기까지의 시간(초).")]
        [SerializeField] private float respawnDelay = 8f;

        [Tooltip("등장 직전에 spawn 위치에 잠시 띄울 흙먼지 이펙트 프리팹. null 이면 생략.")]
        [SerializeField] private GameObject spawnDustPrefab;

        [Tooltip("흙먼지 이펙트 자체의 생존 시간(초). 보통 애니메이션 길이와 동일.")]
        [SerializeField] private float dustDuration = 0.7f;

        [Tooltip("흙먼지가 먼저 살짝 보이고 그 뒤에 보병이 등장하기까지의 텀(초).")]
        [SerializeField] private float dustToSoldierLead = 0.15f;

        // 런타임 캐시: 슬롯별 실제 스폰 위치 (월드 좌표).
        private Vector3[] resolvedSpawnPositions;
        // 슬롯별로 현재 살아있는 보병 (사망 시 null).
        private Soldier[] activeSoldiers;

        // 사용자가 지정한 커스텀 랠리 (사거리 안에 있을 때만 유효).
        // 비어있으면 자동 결정(가장 가까운 경로 지점) 사용.
        private Vector3? customRally;

        // ISelectableTower 용 사거리 원 LineRenderer (필요 시 자동 생성).
        private LineRenderer rangeCircle;

        // --- 공용 API (BarracksRallyController 등 외부가 호출) ----------------

        public float DeploymentRange => deploymentRange;
        public Vector3 BarracksPosition => transform.position;

        /// <summary>사용자가 지정한 커스텀 랠리(있다면). 업그레이드 시 새 인스턴스로 이식하기 위한 용도.</summary>
        public Vector3? CustomRally => customRally;

        /// <summary>worldPos 가 사거리 안인지 검사.</summary>
        public bool IsInRange(Vector3 worldPos)
        {
            return (worldPos - transform.position).sqrMagnitude <= deploymentRange * deploymentRange;
        }

        /// <summary>
        /// 랠리로 유효한 위치인지: 사거리 안 AND PathTilemap 위에 타일 있음.
        /// PathTilemapMarker 가 씬에 없으면 PathTile 제약 없이 사거리만 본다 (안전망).
        /// 미리보기 색과 SetCustomRally 둘 다 이걸로 검증.
        /// </summary>
        public bool IsValidRally(Vector3 worldPos)
        {
            if (!IsInRange(worldPos)) return false;
            var map = ResolvePathTilemap();
            if (map == null) return true;
            var cell = map.WorldToCell(worldPos);
            return map.HasTile(cell);
        }

        private Tilemap cachedPathTilemap;
        private Tilemap ResolvePathTilemap()
        {
            // 우선순위: 인스펙터 직접 드래그 → PathTilemapMarker 자동 탐색 → null
            if (pathTilemap != null) return pathTilemap;
            if (cachedPathTilemap != null) return cachedPathTilemap;

            var marker = Object.FindFirstObjectByType<PathTilemapMarker>();
            cachedPathTilemap = marker != null ? marker.Tilemap : null;
            return cachedPathTilemap;
        }

        /// <summary>
        /// 사용자가 새 랠리 포인트를 지정. 사거리 밖이거나 PathTile 위가 아니면 무시(false 반환).
        /// 활성 보병들 모두 새 위치 기준으로 SetRallyPoint 갱신.
        /// </summary>
        public bool SetCustomRally(Vector3 worldPos)
        {
            if (!IsValidRally(worldPos)) return false;

            customRally = worldPos;
            RebuildFormationToCustom();
            return true;
        }

        /// <summary>커스텀 랠리 해제 → 자동 결정(가장 가까운 경로 지점)으로 복귀.</summary>
        public void ClearCustomRally()
        {
            customRally = null;
            // 자동 모드로 복귀. resolvedSpawnPositions 재계산 + 활성 보병 갱신.
            if (path != null) ApplyFormation(ComputePathFormation(path));
        }

        // 커스텀 랠리 중심으로 분산 배치 재계산하고 활성 보병에게 알린다.
        private void RebuildFormationToCustom()
        {
            if (customRally == null) return;
            Vector3 rally = customRally.Value;
            Vector3 dir = path != null && path.Count >= 2
                ? ComputePathDirectionAt(path, rally)
                : Vector3.right;
            ApplyFormation(ComputeFormationPositions(rally, dir));
        }

        // 새 위치 배열로 resolvedSpawnPositions 를 갱신하고, 활성 보병의 SetRallyPoint 도 호출.
        private void ApplyFormation(Vector3[] positions)
        {
            resolvedSpawnPositions = positions;
            if (activeSoldiers == null) return;

            int min = Mathf.Min(activeSoldiers.Length, positions.Length);
            for (int i = 0; i < min; i++)
            {
                if (activeSoldiers[i] != null)
                    activeSoldiers[i].SetRallyPoint(positions[i]);
            }
        }

        // --- ISelectableTower ------------------------------------------------

        public void SetRangeVisible(bool visible)
        {
            if (!visible)
            {
                if (rangeCircle != null) rangeCircle.gameObject.SetActive(false);
                return;
            }

            if (rangeCircle == null)
            {
                var go = new GameObject("BarracksRange (auto)");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                rangeCircle = go.AddComponent<LineRenderer>();
                rangeCircle.material = new Material(Shader.Find("Sprites/Default"));
                rangeCircle.startWidth = 0.06f;
                rangeCircle.endWidth = 0.06f;
                rangeCircle.sortingOrder = 50;
                rangeCircle.useWorldSpace = false;
                rangeCircle.loop = true;
                rangeCircle.startColor = rangeGizmoColor;
                rangeCircle.endColor = rangeGizmoColor;

                const int segments = 48;
                rangeCircle.positionCount = segments;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * 2f * Mathf.PI / segments;
                    rangeCircle.SetPosition(i, new Vector3(
                        Mathf.Cos(a) * deploymentRange,
                        Mathf.Sin(a) * deploymentRange, 0f));
                }
            }
            rangeCircle.gameObject.SetActive(true);
        }

        private void Start()
        {
            if (soldierPrefab == null)
            {
                Debug.LogWarning($"[BarracksController] {name}: soldierPrefab 이 비어있다.");
                return;
            }

            resolvedSpawnPositions = ResolveSpawnPositions();
            if (resolvedSpawnPositions == null || resolvedSpawnPositions.Length == 0)
            {
                Debug.LogWarning($"[BarracksController] {name}: 스폰 위치를 결정할 수 없다 (EnemyPath 도 없고 spawnPoints 도 비어있다).");
                return;
            }

            activeSoldiers = new Soldier[resolvedSpawnPositions.Length];
            for (int i = 0; i < resolvedSpawnPositions.Length; i++)
                SpawnImmediate(i);
        }

        /// <summary>
        /// 스폰 위치 결정.
        ///   1. path 가 인스펙터로 명시돼 있으면 그걸 사용 (디자이너 오버라이드)
        ///   2. 아니면 씬의 모든 EnemyPath 중 "이 배럭과 가장 가까운 지점을 가진" 것을 자동 선택
        ///   3. 둘 다 실패 시 spawnPoints[] 폴백
        /// </summary>
        private Vector3[] ResolveSpawnPositions()
        {
            if (useNearestPath)
            {
                EnemyPath bestPath = (path != null && path.Count >= 1) ? path : null;

                // 인스펙터 미지정 → 씬 전체 EnemyPath 중 자기 위치에서 가장 가까운 것 선택
                if (bestPath == null)
                {
                    var all = Object.FindObjectsByType<EnemyPath>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);

                    float bestDistSq = float.MaxValue;
                    foreach (var ep in all)
                    {
                        if (ep == null || ep.Count < 1) continue;
                        Vector3 p = FindNearestPointOnPath(ep, transform.position);
                        float d = (p - transform.position).sqrMagnitude;
                        if (d < bestDistSq)
                        {
                            bestPath = ep;
                            bestDistSq = d;
                        }
                    }

                    // 부활 시 같은 경로 재선택 비용 줄이기 위해 캐시
                    if (bestPath != null) path = bestPath;
                }

                if (bestPath != null)
                    return ComputePathFormation(bestPath);

                Debug.LogWarning($"[BarracksController] {name}: " +
                    "씬에서 유효한 EnemyPath (자식 웨이포인트 1개 이상) 를 찾을 수 없다. spawnPoints 폴백 사용.");
            }

            // 폴백
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var list = new List<Vector3>();
                foreach (var p in spawnPoints)
                    if (p != null) list.Add(p.position);
                return list.ToArray();
            }

            return null;
        }

        /// <summary>
        /// 랠리 포인트(보병들이 모일 중심점)를 결정한다.
        /// 우선순위:
        ///   1. 사용자가 SetCustomRally 로 지정한 위치 (사거리 안일 때만 유효)
        ///   2. 가장 가까운 경로 지점이 사거리 내 → 그 경로 지점 사용
        ///   3. 사거리 밖 → 배럭 위치에 그대로
        /// </summary>
        private Vector3 ResolveRallyPoint(EnemyPath p)
        {
            // 사용자 지정 우선 (이미 SetCustomRally 가 사거리 검사를 한 뒤 저장).
            if (customRally.HasValue) return customRally.Value;

            Vector3 barracksPos = transform.position;
            Vector3 nearestOnPath = FindNearestPointOnPath(p, barracksPos);
            float distance = Vector3.Distance(nearestOnPath, barracksPos);

            return distance <= deploymentRange ? nearestOnPath : barracksPos;
        }

        /// <summary>
        /// 랠리 포인트를 중심으로 정다각형 방사 배치한다 (3명 → 정삼각형).
        /// </summary>
        private Vector3[] ComputePathFormation(EnemyPath p)
        {
            Vector3 rally = ResolveRallyPoint(p);
            Vector3 dir = ComputePathDirectionAt(p, rally);
            return ComputeFormationPositions(rally, dir);
        }

        /// <summary>
        /// 랠리를 중심으로 보병을 방사 배치한다.
        ///   N=1 → 랠리 정확히
        ///   N=2 → 경로 수직축 좌우 분산 (다각형이 모이지 않는 케이스)
        ///   N≥3 → 정다각형. 꼭짓점 0번은 "적이 오는 방향(-pathDir)" 을 향한다 (첨병).
        /// 외접원 반지름 = formationSpacing.
        /// </summary>
        private Vector3[] ComputeFormationPositions(Vector3 rally, Vector3 pathDir)
        {
            int n = Mathf.Max(1, soldierCount);
            var positions = new Vector3[n];

            if (n == 1)
            {
                positions[0] = rally;
                return positions;
            }

            Vector3 dir = pathDir.sqrMagnitude > 1e-6f ? pathDir.normalized : Vector3.right;
            // 2D 90° 회전: (x, y) → (-y, x). 경로 진행 방향에 수직인 단위벡터.
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            if (n == 2)
            {
                positions[0] = rally + perp * formationSpacing;
                positions[1] = rally - perp * formationSpacing;
                return positions;
            }

            // N≥3: 정다각형. 꼭짓점 0번을 "적이 오는 쪽(-dir)" 으로 두기 위해 facing = -dir.
            // angle=0 → facing 방향, angle=2π/N → 그 다음 꼭짓점.
            Vector3 facing = -dir;
            for (int i = 0; i < n; i++)
            {
                float angle = i * 2f * Mathf.PI / n;
                Vector3 offset = facing * Mathf.Cos(angle) + perp * Mathf.Sin(angle);
                positions[i] = rally + offset * formationSpacing;
            }
            return positions;
        }

        private void SpawnImmediate(int slot)
        {
            var rally = resolvedSpawnPositions[slot];
            // 보병은 배럭 위치에서 등장해서 랠리까지 걸어간다 — 그 동안은 Soldier.IsDeploying = true
            // 이라 적과 상호작용하지 않는다 (issue #57: 배치 중 \"걸어가다 죽는\" 현상 방지).
            var soldier = Instantiate(soldierPrefab, transform.position, Quaternion.identity);
            // 티어 배율 적용 (Lv1 은 1.0/1.0 이라 변화 없음, Lv2/Lv3 에서 강화됨)
            soldier.ApplyTier(soldierHpMultiplier, soldierDamageMultiplier);
            soldier.SetRallyPoint(rally);
            int capturedSlot = slot;
            soldier.OnDeath += _ => StartCoroutine(RespawnAfterDelay(capturedSlot));
            activeSoldiers[slot] = soldier;
        }

        /// <summary>
        /// 배럭이 파괴/판매될 때, 이 배럭이 소환했던 보병들도 함께 정리한다.
        /// (보병은 spawn 시 부모 없이 씬 루트에 만들어지므로 배럭이 사라져도 살아남는다.)
        /// </summary>
        private void OnDestroy()
        {
            if (activeSoldiers == null) return;
            for (int i = 0; i < activeSoldiers.Length; i++)
            {
                if (activeSoldiers[i] != null)
                {
                    Destroy(activeSoldiers[i].gameObject);
                    activeSoldiers[i] = null;
                }
            }
        }

        private IEnumerator RespawnAfterDelay(int slot)
        {
            activeSoldiers[slot] = null;

            yield return new WaitForSeconds(respawnDelay);

            var pos = resolvedSpawnPositions[slot];

            // 1) 흙먼지 펑
            if (spawnDustPrefab != null)
            {
                var dust = Instantiate(spawnDustPrefab, pos, Quaternion.identity);
                if (dustDuration > 0f) Destroy(dust, dustDuration);
            }

            // 2) 살짝 텀 두고
            if (dustToSoldierLead > 0f)
                yield return new WaitForSeconds(dustToSoldierLead);

            // 3) 보병 등장 — 단, 그 사이 배럭이 파괴/판매됐을 수도 있으니 살아있는지 확인
            if (this != null) SpawnImmediate(slot);
        }

        // --- 경로 기하학 헬퍼 -------------------------------------------------

        /// <summary>경로의 모든 세그먼트 중 from 과 가장 가까운 점을 반환.</summary>
        private static Vector3 FindNearestPointOnPath(EnemyPath path, Vector3 from)
        {
            int n = path.Count;
            if (n == 0) return from;
            if (n == 1) return path.GetPoint(0);

            Vector3 best = path.GetPoint(0);
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = path.GetPoint(i);
                Vector3 b = path.GetPoint(i + 1);
                Vector3 p = ClosestPointOnSegment(a, b, from);
                float d = (p - from).sqrMagnitude;
                if (d < bestDistSq) { bestDistSq = d; best = p; }
            }
            return best;
        }

        /// <summary>point 가 위치한 세그먼트의 진행 방향 단위벡터. 세그먼트가 모호하면 가장 가까운 세그먼트 기준.</summary>
        private static Vector3 ComputePathDirectionAt(EnemyPath path, Vector3 point)
        {
            int n = path.Count;
            if (n < 2) return Vector3.right;

            float bestDistSq = float.MaxValue;
            Vector3 bestDir = Vector3.right;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a = path.GetPoint(i);
                Vector3 b = path.GetPoint(i + 1);
                Vector3 p = ClosestPointOnSegment(a, b, point);
                float d = (p - point).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    Vector3 dir = b - a;
                    if (dir.sqrMagnitude > 1e-6f) bestDir = dir.normalized;
                }
            }
            return bestDir;
        }

        /// <summary>선분 ab 위에서 p 와 가장 가까운 점.</summary>
        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float sqLen = ab.sqrMagnitude;
            if (sqLen < 1e-6f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / sqLen);
            return a + ab * t;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 사거리 원 (배치 가능 범위 시각화)
            Gizmos.color = rangeGizmoColor;
            DrawWireCircle(transform.position, deploymentRange, 48);

            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.85f);

            // 런타임: 실제 결정된 위치를 그린다.
            if (Application.isPlaying && resolvedSpawnPositions != null)
            {
                foreach (var p in resolvedSpawnPositions)
                {
                    Gizmos.DrawWireSphere(p, 0.25f);
                    Gizmos.DrawLine(transform.position, p);
                }
                return;
            }

            // 에디터 미리보기: 현재 설정으로 어디에 스폰될지 예상.
            if (useNearestPath)
            {
                var previewPath = path != null ? path : Object.FindFirstObjectByType<EnemyPath>();
                if (previewPath != null && previewPath.Count >= 1)
                {
                    var preview = ComputePathFormation(previewPath);
                    foreach (var p in preview)
                    {
                        Gizmos.DrawWireSphere(p, 0.25f);
                        Gizmos.DrawLine(transform.position, p);
                    }
                    return;
                }
            }

            if (spawnPoints != null)
            {
                foreach (var p in spawnPoints)
                {
                    if (p == null) continue;
                    Gizmos.DrawWireSphere(p.position, 0.25f);
                    Gizmos.DrawLine(transform.position, p.position);
                }
            }
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            if (segments < 8) segments = 8;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
