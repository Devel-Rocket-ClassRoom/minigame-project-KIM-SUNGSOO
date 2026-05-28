using UnityEngine;
using UnityEngine.Tilemaps;

namespace KRTD.Map
{
    /// <summary>
    /// 경로 타일맵 식별 마커. PathTileMap GameObject 에 한 번 부착해두면
    /// 능력(예: ReinforcementAbility) 이 런타임에 자동 탐색할 수 있다.
    ///
    /// Stage prefab 으로 묶어도 인스턴스화 시 마커가 함께 따라오므로,
    /// 능력 인스펙터의 직접 참조가 깨져도 fallback 으로 동작한다.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class PathTilemapMarker : MonoBehaviour
    {
        private Tilemap cached;

        public Tilemap Tilemap
        {
            get
            {
                if (cached == null) cached = GetComponent<Tilemap>();
                return cached;
            }
        }
    }
}
