using UnityEngine;
using KRTD.Map;

namespace KRTD.Combat
{
    /// <summary>
    /// 한 경로의 "스폰 머신". 위치/경로 책임만 가진다.
    ///
    /// 책임:
    ///   - 자신의 PathId 노출 (WaveDirector 가 SpawnEntry.pathId 와 매칭)
    ///   - SpawnEnemy(data, hpMultiplier) 호출 시 path.SpawnPoint 에 Instantiate
    ///
    /// 비책임 (WaveDirector 가 담당):
    ///   - 웨이브 진행 / 자동 시작 / 갭 대기
    ///   - GameState 동기화
    ///   - 난이도 배율 계산
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("식별")]
        [Tooltip("WaveDirector 의 SpawnEntry.pathId 와 매칭. 예: \"L\", \"CL\", \"CR\", \"R\"")]
        [SerializeField] private string pathId = "";

        [Header("참조")]
        [Tooltip("적이 따라갈 경로. 비워두면 자식/부모에서 EnemyPath 자동 탐색.")]
        [SerializeField] private EnemyPath path;

        public string PathId => pathId;
        public EnemyPath Path => path;

        private void Awake()
        {
            if (path == null)
            {
                path = GetComponentInChildren<EnemyPath>();
                if (path == null) path = GetComponentInParent<EnemyPath>();
            }
        }

        /// <summary>
        /// 적 1마리를 이 스포너의 경로에 스폰한다. hpMultiplier 는 WaveDirector 의 난이도 곡선에서 전달.
        /// </summary>
        public void SpawnEnemy(EnemyData data, float hpMultiplier = 1f)
        {
            if (data == null || data.enemyPrefab == null)
            {
                Debug.LogWarning($"[EnemySpawner:{pathId}] EnemyData 또는 prefab 이 비어있다.");
                return;
            }
            if (path == null)
            {
                Debug.LogWarning($"[EnemySpawner:{pathId}] EnemyPath 가 비어있다. 스폰 불가.");
                return;
            }

            var go = Instantiate(data.enemyPrefab, path.SpawnPoint, Quaternion.identity);

            var enemy = go.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogWarning($"[EnemySpawner:{pathId}] 프리팹에 Enemy 컴포넌트가 없다: {data.enemyPrefab.name}");
                return;
            }
            enemy.Init(data, path, hpMultiplier);
        }
    }
}
