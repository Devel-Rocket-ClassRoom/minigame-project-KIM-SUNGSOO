using UnityEngine;

namespace KRTD.Map
{
    /// <summary>
    /// 건물 한 종류의 정적 데이터.
    /// BuildSpot에 설치되면 buildingPrefab 이 인스턴스화된다.
    /// 시각, Animator, 스크립트(공격 로직 등)는 모두 prefab 안에 포함시킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "Building_New", menuName = "KRTD/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("표시 정보")]
        public string buildingName = "Building";

        [Tooltip("건설 메뉴(UI)용 작은 아이콘")]
        public Sprite icon;

        [Header("실제 건물")]
        [Tooltip("BuildSpot에 설치될 때 인스턴스화되는 프리팹. 시각/애니메이션/공격 로직 모두 이 안에 둔다.")]
        public GameObject buildingPrefab;

        [Header("기타")]
        [Tooltip("건설 비용 (자원 시스템 도입 후 사용)")]
        public int cost = 0;
    }
}
