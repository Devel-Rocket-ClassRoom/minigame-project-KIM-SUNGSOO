using UnityEngine;

namespace KRTD.Map
{
    /// <summary>
    /// 기기마다 화면 비율이 달라도 타일맵 전체가 항상 화면 안에 들어오도록
    /// 런타임에 카메라 OrthographicSize를 자동 조정한다.
    ///
    /// 동작 원리:
    ///   - 맵의 "기준 너비"와 "기준 높이"를 설정해 두면,
    ///     실제 화면 비율에 따라 그 중 더 큰 쪽을 기준으로 orthoSize를 계산한다.
    ///   - 가로가 좁은 폰(세로형) → 너비 기준으로 축소 → 맵 좌우가 잘리지 않음
    ///   - 가로가 넓은 태블릿    → 높이 기준 → 맵 상하가 잘리지 않음
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraAspectFitter : MonoBehaviour
    {
        [Header("맵 기준 크기 (월드 단위 = 타일 수)")]
        [SerializeField] private float mapWidth  = 26f;
        [SerializeField] private float mapHeight = 13f;

        [Header("카메라 중심 (타일맵 중앙 좌표)")]
        [SerializeField] private Vector2 mapCenter = new Vector2(0f, 0.5f);

        [Header("여백 비율 (0.05 = 5%)")]
        [Range(0f, 0.2f)]
        [SerializeField] private float padding = 0.05f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ApplyFit();
        }

        private void Start()
        {
            ApplyFit();
        }

        public void ApplyFit()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float mapAspect    = mapWidth / mapHeight;

            float orthoSize;
            if (screenAspect >= mapAspect)
                orthoSize = mapHeight * 0.5f;          // 가로형 → 높이 기준
            else
                orthoSize = (mapWidth * 0.5f) / screenAspect;  // 세로형 → 너비 기준

            cam.orthographicSize = orthoSize * (1f + padding);

            Vector3 pos = transform.position;
            pos.x = mapCenter.x;
            pos.y = mapCenter.y;
            transform.position = pos;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                cam = GetComponent<Camera>();
                if (cam != null) ApplyFit();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawCube(new Vector3(mapCenter.x, mapCenter.y, 0f), new Vector3(mapWidth, mapHeight, 0.01f));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(mapCenter.x, mapCenter.y, 0f), new Vector3(mapWidth, mapHeight, 0.01f));
        }
#endif
    }
}
