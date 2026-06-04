using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 씬에 단일로 배치되는 영웅 스포너. Start 시점에 HeroData.heroPrefab 을 자기 위치에 1회 인스턴스화한다.
    ///
    /// 권장 배치: 각 EnemyPath 의 마지막 웨이포인트 근처(디펜스 라인).
    /// 영웅이 부활 시 \"시작 위치\" 로 돌아오게 하려면 HeroData.respawnAtLastRally 를 false 로.
    /// </summary>
    public class HeroSpawner : MonoBehaviour
    {
        [Tooltip("스폰할 영웅의 HeroData. heroPrefab 이 채워져 있어야 한다.")]
        [SerializeField] private HeroData data;

        private void Start()
        {
            if (data == null || data.heroPrefab == null)
            {
                Debug.LogWarning("[HeroSpawner] HeroData 또는 heroPrefab 이 비어있어 스폰하지 않는다.");
                return;
            }

            // 이미 활성 Hero 가 있다면 중복 스폰 금지 (씬 재진입/스포너 중복 방지).
            if (Hero.Instance != null) return;

            Instantiate(data.heroPrefab, transform.position, Quaternion.identity);
        }
    }
}
