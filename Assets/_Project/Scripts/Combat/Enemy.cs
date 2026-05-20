using UnityEngine;
using KRTD.Game;
using KRTD.Map;

namespace KRTD.Combat
{
    /// <summary>
    /// 적 유닛. EnemyPath 의 웨이포인트를 순서대로 따라 이동하며,
    /// 골인 시 GameState 의 생명을 깎고, 처치되면 골드를 보상한다.
    ///
    /// 초기화 흐름:
    ///   EnemySpawner.Spawn(...) → Instantiate → Enemy.Init(data, path)
    ///   Init 이 호출되지 않은 경우(에디터 직접 배치) 인스펙터 fallback 값으로 동작.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        [Header("데이터 (없으면 아래 fallback 사용)")]
        [SerializeField] private EnemyData data;

        [Header("Fallback 스탯 (data 가 없을 때만 사용)")]
        [SerializeField] private float maxHp = 10f;
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private int goldReward = 5;
        [SerializeField] private int lifeDamage = 1;

        [Header("이동")]
        [Tooltip("웨이포인트에 이만큼 가까워지면 다음 점으로 진행")]
        [SerializeField] private float waypointReachRadius = 0.05f;

        private float currentHp;
        private EnemyPath path;
        private int nextWaypointIndex;
        private bool reachedEnd;

        public bool IsDead => currentHp <= 0f;
        public Vector3 Position => transform.position;

        private void Awake()
        {
            // 데이터가 있으면 스탯/시각 동기화. 없으면 fallback 값 그대로.
            ApplyDataIfPresent();
            currentHp = ResolveMaxHp();
        }

        /// <summary>
        /// 스포너에서 호출. 데이터와 경로를 주입하고 스폰 위치로 이동.
        /// </summary>
        public void Init(EnemyData data, EnemyPath path)
        {
            this.data = data;
            this.path = path;

            ApplyDataIfPresent();
            currentHp = ResolveMaxHp();
            nextWaypointIndex = 0;
            reachedEnd = false;

            // 스폰 위치 = 경로 시작점
            if (path != null && path.Count > 0)
            {
                transform.position = path.SpawnPoint;
                // 첫 프레임에 곧바로 1번 웨이포인트를 향해 출발하도록 인덱스를 1로 둔다.
                nextWaypointIndex = Mathf.Min(1, path.Count - 1);
            }
        }

        private void Update()
        {
            if (IsDead || reachedEnd || path == null) return;

            Vector3 target = path.GetPoint(nextWaypointIndex);
            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;
            float step = ResolveMoveSpeed() * Time.deltaTime;

            if (dist <= step + waypointReachRadius)
            {
                // 이 웨이포인트 도착: 다음으로 진행하거나, 마지막이면 골인 처리.
                transform.position = target;
                if (nextWaypointIndex >= path.Count - 1)
                {
                    ReachEnd();
                    return;
                }
                nextWaypointIndex++;
                return;
            }

            transform.position += toTarget / dist * step;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || reachedEnd) return;

            currentHp -= damage;
            if (currentHp <= 0f)
            {
                currentHp = 0f;
                Die();
            }
        }

        private void Die()
        {
            // 처치 보상.
            var state = GameState.Instance;
            if (state != null) state.AddGold(ResolveGoldReward());

            // TODO: 사망 연출 (애니메이터 트리거, 파티클 등)
            Destroy(gameObject);
        }

        private void ReachEnd()
        {
            reachedEnd = true;

            var state = GameState.Instance;
            if (state != null) state.LoseLife(ResolveLifeDamage());

            // 골인한 적은 보상 없이 사라진다.
            Destroy(gameObject);
        }

        // --- 데이터/Fallback 해석 헬퍼 -----------------------------------------

        private void ApplyDataIfPresent()
        {
            if (data == null) return;
            // 데이터가 있는 경우 fallback 필드를 데이터 값으로 갱신해 두면
            // 인스펙터에서도 현재 값이 보여 디버깅이 쉽다.
            maxHp = data.maxHp;
            moveSpeed = data.moveSpeed;
            goldReward = data.goldReward;
            lifeDamage = data.lifeDamage;
        }

        private float ResolveMaxHp() => data != null ? data.maxHp : maxHp;
        private float ResolveMoveSpeed() => data != null ? data.moveSpeed : moveSpeed;
        private int ResolveGoldReward() => data != null ? data.goldReward : goldReward;
        private int ResolveLifeDamage() => data != null ? data.lifeDamage : lifeDamage;
    }
}
