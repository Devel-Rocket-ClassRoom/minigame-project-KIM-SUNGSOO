using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Pooling
{
    /// <summary>
    /// 단순한 프리펩 기반 오브젝트 풀.
    /// 적/투사체/이펙트처럼 자주 생성·파괴되는 오브젝트는 모두 이 풀을 통해 처리.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
        private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            GameObject go;
            if (queue.Count > 0)
            {
                go = queue.Dequeue();
                go.transform.SetPositionAndRotation(position, rotation);
                if (parent != null) go.transform.SetParent(parent, true);
                go.SetActive(true);
            }
            else
            {
                go = Instantiate(prefab, position, rotation, parent);
                instanceToPrefab[go] = prefab;
            }

            return go;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;
            if (!instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                Destroy(instance); // 풀에 등록되지 않은 객체는 그냥 파괴
                return;
            }
            instance.SetActive(false);
            instance.transform.SetParent(transform, true);
            pools[prefab].Enqueue(instance);
        }
    }
}
