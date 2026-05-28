using System;
using UnityEngine;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 월드 좌표에 묶여 그려지는 단일 UI 버튼.
    /// "다음 웨이브가 여기서 나옵니다" 표시 + 클릭 시 스킵 트리거.
    ///
    /// 부모는 Screen Space Overlay Canvas. 매 프레임 cam.WorldToScreenPoint 로
    /// rectTransform.position 을 갱신해 카메라 줌/패닝에 따라가게 한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(RectTransform))]
    public class WaveCallButton : MonoBehaviour
    {
        [Header("화면 가장자리 클램프")]
        [Tooltip("월드 좌표가 화면 밖일 때 가장자리에 붙일지 여부. 끄면 그대로 화면 밖에 그려진다.")]
        [SerializeField] private bool clampToScreen = true;
        [Tooltip("가장자리 클램프 시 여백 (픽셀).")]
        [SerializeField] private float screenMargin = 40f;

        private RectTransform rect;
        private Camera cam;
        private Vector3 worldPos;
        private Action onClick;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            GetComponent<Button>().onClick.AddListener(HandleClick);
        }

        /// <summary>
        /// 스포너 매니저가 호출. 어느 월드 위치에 묶일지와 클릭 콜백을 전달.
        /// </summary>
        public void Bind(Vector3 worldPosition, Camera camera, Action onClickCallback)
        {
            worldPos = worldPosition;
            cam = camera != null ? camera : Camera.main;
            onClick = onClickCallback;
            UpdateScreenPosition();
        }

        private void LateUpdate()
        {
            // 카메라가 움직이지 않는 TD 라도 매 프레임 갱신해 두면 추후 카메라 추가 시 자동 대응.
            UpdateScreenPosition();
        }

        private void UpdateScreenPosition()
        {
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(worldPos);

            // 카메라 뒤(z<0)에 있으면 좌표가 뒤집힘 → 안전하게 반전.
            if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            if (clampToScreen)
            {
                sp.x = Mathf.Clamp(sp.x, screenMargin, Screen.width - screenMargin);
                sp.y = Mathf.Clamp(sp.y, screenMargin, Screen.height - screenMargin);
            }

            rect.position = sp;
        }

        private void HandleClick()
        {
            onClick?.Invoke();
        }
    }
}
