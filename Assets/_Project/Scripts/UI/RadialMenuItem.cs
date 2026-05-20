using System;
using UnityEngine;

namespace KRTD.UI
{
    /// <summary>
    /// 라디얼 메뉴의 버튼 한 개. SpriteRenderer + BoxCollider2D 기반이라
    /// Canvas/EventSystem 없이도 OnMouseDown 으로 클릭 처리한다.
    ///
    /// 구조:
    ///   RadialMenuItem (이 컴포넌트 + BoxCollider2D)
    ///   ├─ Background (SpriteRenderer, 원형 배경 - 선택)
    ///   └─ Icon       (SpriteRenderer, BuildingData.icon)
    ///
    /// 위치/등장 애니메이션은 부모 RadialMenu 가 제어한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RadialMenuItem : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer backgroundRenderer;

        private Action onClick;
        private bool interactable;

        public void Setup(Sprite icon, Action onClickCallback)
        {
            if (iconRenderer != null) iconRenderer.sprite = icon;
            onClick = onClickCallback;
            interactable = true;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        public void SetTint(Color iconTint, Color backgroundTint)
        {
            if (iconRenderer != null) iconRenderer.color = iconTint;
            if (backgroundRenderer != null) backgroundRenderer.color = backgroundTint;
        }

        private void OnMouseDown()
        {
            if (!interactable) return;
            onClick?.Invoke();
        }
    }
}
