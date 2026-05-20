using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KRTD.Game;
using KRTD.Map;

namespace KRTD.Combat
{
    /// <summary>
    /// 웨이브 리스트를 순서대로 진행하며 EnemyPath 를 따라 적을 스폰한다.
    ///
    /// 책임:
    ///   - 시작 시(또는 외부 트리거 시) 첫 웨이브부터 진행
    ///   - 한 웨이브의 모든 entry 를 순차 스폰
    ///   - GameState 에 (CurrentWave, TotalWave) 동기화
    ///
    /// 정책:
    ///   - 다음 웨이브 시작 트리거는 "이전 웨이브의 모든 적이 스폰 완료" 시점.
    ///     (현재는 처치 완료 대기 없음. 필요시 EnemyManager 도입 후 확장.)
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("적이 따라갈 경로. 비워두면 자식 EnemyPath 자동 탐색.")]
        [SerializeField] private EnemyPath path;

        [Header("웨이브")]
        [SerializeField] private List<WaveData> waves = new List<WaveData>();

        [Header("시작 동작")]
        [Tooltip("플레이 시작과 함께 자동으로 첫 웨이브를 시작할지 여부.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("자동 시작 시 첫 웨이브까지의 대기 시간 (초)")]
        [SerializeField] private float initialDelay = 1.5f;

        [Tooltip("웨이브 사이의 추가 대기 시간 (초). WaveData.startDelay 와 합산된다.")]
        [SerializeField] private float gapBetweenWaves = 3f;

        private bool isRunning;

        private void Awake()
        {
            if (path == null) path = GetComponentInChildren<EnemyPath>();
        }

        private void Start()
        {
            // 총 웨이브 수를 GameState 에 미리 알린다 (UI 표시용).
            var state = GameState.Instance;
            if (state != null) state.SetWave(0, waves.Count);

            if (autoStart) StartWaves();
        }

        /// <summary>외부에서(예: 시작 버튼) 호출하면 첫 웨이브부터 진행한다.</summary>
        public void StartWaves()
        {
            if (isRunning) return;
            if (path == null)
            {
                Debug.LogWarning("[EnemySpawner] EnemyPath 가 비어있다. 적을 스폰할 수 없다.");
                return;
            }
            isRunning = true;
            StartCoroutine(RunWaves());
        }

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, initialDelay));

            for (int i = 0; i < waves.Count; i++)
            {
                var wave = waves[i];
                if (wave == null) continue;

                // GameState 갱신 (UI 가 구독)
                var state = GameState.Instance;
                if (state != null) state.SetWave(i + 1, waves.Count);

                yield return new WaitForSeconds(Mathf.Max(0f, wave.startDelay));
                yield return RunWave(wave);

                // 마지막 웨이브가 아니면 갭만큼 대기
                if (i < waves.Count - 1)
                    yield return new WaitForSeconds(Mathf.Max(0f, gapBetweenWaves));
            }

            isRunning = false;
        }

        private IEnumerator RunWave(WaveData wave)
        {
            foreach (var entry in wave.entries)
            {
                if (entry == null || entry.enemy == null || entry.count <= 0) continue;

                for (int n = 0; n < entry.count; n++)
                {
                    SpawnOne(entry.enemy);
                    if (n < entry.count - 1)
                        yield return new WaitForSeconds(Mathf.Max(0f, entry.interval));
                }
            }
        }

        private void SpawnOne(EnemyData data)
        {
            if (data == null || data.enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] EnemyData 또는 prefab 이 비어있다.");
                return;
            }

            Vector3 spawnPos = path.SpawnPoint;
            var go = Instantiate(data.enemyPrefab, spawnPos, Quaternion.identity);

            var enemy = go.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogWarning($"[EnemySpawner] 프리팹에 Enemy 컴포넌트가 없다: {data.enemyPrefab.name}");
                return;
            }
            enemy.Init(data, path);
        }
    }
}
