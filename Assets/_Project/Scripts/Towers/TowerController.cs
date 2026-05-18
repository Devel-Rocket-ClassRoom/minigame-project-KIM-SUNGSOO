using UnityEngine;
using KRTD.Data;

namespace KRTD.Towers
{
    /// <summary>
    /// 타워 프리펩의 루트 컴포넌트.
    /// 자식 컴포넌트(TargetFinder, TowerShooter, TowerUpgrader)를 묶어주는 역할만 함.
    /// 구체적인 공격 행동은 TowerShooter 파생 클래스에 위임.
    /// </summary>
    [RequireComponent(typeof(TargetFinder))]
    [RequireComponent(typeof(TowerUpgrader))]
    public class TowerController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TowerData towerData;

        [Header("Refs")]
        [SerializeField] private Transform visualRoot;  // 외형(스프라이트/메쉬) swap 대상

        public TowerData TowerData => towerData;
        public TargetFinder TargetFinder { get; private set; }
        public TowerShooter Shooter { get; private set; }
        public TowerUpgrader Upgrader { get; private set; }
        public Transform VisualRoot => visualRoot;

        private void Awake()
        {
            TargetFinder = GetComponent<TargetFinder>();
            Shooter      = GetComponent<TowerShooter>(); // 어떤 Shooter든 1개
            Upgrader     = GetComponent<TowerUpgrader>();
        }

        private void Start()
        {
            Upgrader.Initialize(this, towerData);       // 초기 티어 적용
        }

        /// <summary>외부에서 타워 데이터 주입(슬롯에서 건설 시 호출).</summary>
        public void SetTowerData(TowerData data)
        {
            towerData = data;
        }
    }
}
