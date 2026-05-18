using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KRTD.Core;
using KRTD.Data;
using KRTD.Enemies;
using KRTD.Map;

namespace KRTD.Waves
{
    /// <summary>
    /// 레벨의 웨이브 진행을 총괄.
    /// LevelData.waves 순서대로 EnemySpawner에게 SpawnGroup을 위임.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private LevelData levelData;
        [SerializeField] private List<EnemySpawner> spawners;   // pathIndex와 일치하는 순서
        [SerializeField] private List<Path> paths;              // pathIndex와 일치하는 순서

        private int currentWaveIndex = -1;
        private int aliveEnemies = 0;
        private bool waveInProgress = false;

        public int CurrentWaveIndex => currentWaveIndex;
        public int TotalWaves => levelData != null ? levelData.waves.Count : 0;

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Subscribe<EnemyReachedGoalEvent>(OnEnemyReachedGoal);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Unsubscribe<EnemyReachedGoalEvent>(OnEnemyReachedGoal);
        }

        public void StartNextWave()
        {
            if (waveInProgress) return;
            currentWaveIndex++;
            if (currentWaveIndex >= TotalWaves)
            {
                GameManager.Instance.Victory();
                return;
            }
            StartCoroutine(RunWave(levelData.waves[currentWaveIndex]));
        }

        private IEnumerator RunWave(WaveData wave)
        {
            waveInProgress = true;
            yield return new WaitForSeconds(wave.startDelay);

            foreach (var group in wave.spawnGroups)
            {
                int pathIdx = Mathf.Clamp(group.pathIndex, 0, spawners.Count - 1);
                StartCoroutine(spawners[pathIdx].SpawnGroup(group, paths[pathIdx], () => aliveEnemies++));
            }

            // 모든 스폰 완료 + 살아있는 적 0 까지 대기
            yield return new WaitUntil(() => aliveEnemies == 0 && AllSpawnersIdle());

            waveInProgress = false;
            EventBus.Raise(new WaveClearedEvent(currentWaveIndex));
        }

        private bool AllSpawnersIdle()
        {
            foreach (var s in spawners) if (s.IsSpawning) return false;
            return true;
        }

        private void OnEnemyDied(EnemyDiedEvent _)              { aliveEnemies = Mathf.Max(0, aliveEnemies - 1); }
        private void OnEnemyReachedGoal(EnemyReachedGoalEvent _) { aliveEnemies = Mathf.Max(0, aliveEnemies - 1); }
    }

    public readonly struct WaveClearedEvent
    {
        public readonly int WaveIndex;
        public WaveClearedEvent(int i) { WaveIndex = i; }
    }
}
