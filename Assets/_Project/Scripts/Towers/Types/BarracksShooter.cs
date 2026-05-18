using UnityEngine;

namespace KRTD.Towers.Types
{
    /// <summary>
    /// 막사 타워: 발사 대신 병사 유닛을 소환해 경로를 막는 구조.
    /// MVP에서는 스폰만 처리하고 실제 병사 AI는 별도 컴포넌트로 확장.
    /// </summary>
    public class BarracksShooter : TowerShooter
    {
        [SerializeField] private Transform[] rallyPoints;
        [SerializeField] private int maxSoldiers = 3;
        [SerializeField] private float respawnTime = 5f;

        protected override void Update()
        {
            // 막사는 자동 발사가 아닌 "수동 배치 + 자동 리스폰" 패턴.
            // 스폰 사이클은 별도 코루틴/타이머로 처리할 예정. (스켈레톤 단계)
        }

        protected override void Fire(KRTD.Enemies.EnemyController target)
        {
            // 막사는 Fire를 사용하지 않음.
        }
    }
}
