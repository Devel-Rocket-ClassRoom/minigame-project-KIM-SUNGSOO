using System;
using System.Collections;
using UnityEngine;
using KRTD.Data;
using KRTD.Enemies;
using KRTD.Map;
using KRTD.Pooling;

namespace KRTD.Waves
{
    /// <summary>
    /// 단일 경로에 대한 적 스폰 처리.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public bool IsSpawning { get; private set; }

        public IEnumerator SpawnGroup(SpawnGroup group, Path path, Action onSpawned)
        {
            IsSpawning = true;
            if (group.startOffset > 0f) yield return new WaitForSeconds(group.startOffset);

            for (int i = 0; i < group.count; i++)
            {
                SpawnOne(group.enemy, path);
                onSpawned?.Invoke();
                if (i < group.count - 1)
                    yield return new WaitForSeconds(group.intervalBetween);
            }
            IsSpawning = false;
        }

        private void SpawnOne(EnemyData data, Path path)
        {
            if (data == null || data.enemyPrefab == null || path == null) return;
            var spawnPos = path.Waypoints.Length > 0 ? path.Waypoints[0].position : transform.position;
            var go = ObjectPool.Instance.Spawn(data.enemyPrefab, spawnPos, Quaternion.identity);
            var enemy = go.GetComponent<EnemyController>();
            enemy.Spawn(data, path);
        }
    }
}
