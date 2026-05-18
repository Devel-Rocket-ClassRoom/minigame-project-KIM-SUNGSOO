using UnityEngine;
using KRTD.Data;

namespace KRTD.Enemies
{
    /// <summary>
    /// 적 프리펩 루트. 자식 컴포넌트들을 묶는 역할.
    /// 외부에서는 이 컴포넌트만 참조하면 됨(체력, 이동, 데이터 접근 일원화).
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyMovement))]
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyData enemyData;

        public EnemyData Data => enemyData;
        public EnemyHealth Health { get; private set; }
        public EnemyMovement Movement { get; private set; }

        public bool IsAlive => Health != null && Health.CurrentHP > 0f;
        public float CurrentHP => Health != null ? Health.CurrentHP : 0f;
        public float PathProgress => Movement != null ? Movement.NormalizedProgress : 0f;

        private void Awake()
        {
            Health = GetComponent<EnemyHealth>();
            Movement = GetComponent<EnemyMovement>();
        }

        public void Spawn(EnemyData data, KRTD.Map.Path path)
        {
            enemyData = data;
            Health.Initialize(data);
            Movement.Initialize(data, path);
        }
    }
}
