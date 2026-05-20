using System.Collections.Generic;
using UnityEngine;
using KRTD.Map;

namespace KRTD.UI
{
    /// <summary>
    /// 빈 BuildSpot 을 클릭했을 때 라디얼 메뉴에 표시할 건물 목록.
    /// 씬의 BuildMenuController 에 한 번 꽂아두면 모든 빈 스팟이 공유한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildMenuConfig", menuName = "KRTD/Build Menu Config")]
    public class BuildMenuConfig : ScriptableObject
    {
        [Tooltip("라디얼 메뉴에 등장하는 순서대로 위에서부터 시계방향으로 배치된다.")]
        public List<BuildingData> buildableBuildings = new List<BuildingData>();
    }
}
