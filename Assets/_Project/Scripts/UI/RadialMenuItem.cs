using System;
using TMPro;
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
    ///   ├─ Icon       (SpriteRenderer, BuildingData.icon)
    ///   └─ (CostLabel) — 인스펙터에서 TextMeshPro 직접 할당하거나,
    ///                    비워두면 런타임에 WorldSpace Canvas + TMP_UGUI 가 자동 생성됨.
    ///
    /// 위치/등장 애니메이션은 부모 RadialMenu 가 제어한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RadialMenuItem : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [Tooltip("비용 표시 라벨. 비워두면 런타임에 WorldSpace Canvas + TextMeshProUGUI 가 자동 생성됨.")]
        [SerializeField] private TextMeshPro costLabel;

        [Header("자동 생성 라벨 설정 (costLabel 이 비어있을 때만 적용)")]
        [Tooltip("자동 생성 라벨의 로컬 좌표 (보통 아이콘 아래).")]
        [SerializeField] private Vector3 autoLabelLocalPosition = new Vector3(0f, -0.8f, -0.1f);
        [Tooltip("자동 생성 라벨의 폰트 크기 (Canvas 단위, 36~72 추천).")]
        [SerializeField] private float autoLabelFontSize = 48f;
        [Tooltip("최소 sortingOrder 값. 실제 적용 시 아이콘/배경 sortingOrder + 10 과 비교해 더 큰 값으로.")]
        [SerializeField] private int autoLabelSortingOrder = 32000;
        [Tooltip("Canvas RectTransform 의 sizeDelta (Canvas 단위).")]
        [SerializeField] private Vector2 autoLabelCanvasSize = new Vector2(240f, 80f);
        [Tooltip("WorldSpace Canvas 의 스케일. 0.01 = 100 canvas unit ≈ 1 world unit.")]
        [SerializeField] private float autoLabelCanvasScale = 0.01f;

        // 색상: 비용(차감) / 환급
        private static readonly Color CostColor = new Color(1f, 0.92f, 0.4f);   // 노란 톤
        private static readonly Color RefundColor = new Color(0.55f, 1f, 0.5f); // 녹색 톤

        private Action onClick;
        private bool interactable;
        // 인스펙터로 할당된 3D TMP, 혹은 자동 생성된 TMP_UGUI 가 들어감
        private TMP_Text resolvedLabel;

        public void Setup(Sprite icon, Action onClickCallback, int? cost = null)
        {
            if (iconRenderer != null) iconRenderer.sprite = icon;
            onClick = onClickCallback;
            interactable = true;
            ApplyCost(cost);
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

        // --- 비용 라벨 ---------------------------------------------------------

        private void ApplyCost(int? cost)
        {
            // 표시할 게 없음 → 라벨이 있으면 끄기만
            if (!cost.HasValue || cost.Value == 0)
            {
                if (resolvedLabel != null) resolvedLabel.gameObject.SetActive(false);
                return;
            }

            var label = GetOrCreateCostLabel();
            if (label == null) return;
            label.gameObject.SetActive(true);

            int value = cost.Value;
            if (value > 0)
            {
                label.text = value.ToString();
                label.color = CostColor;
            }
            else
            {
                label.text = "+" + (-value).ToString();
                label.color = RefundColor;
            }
        }

        /// <summary>
        /// 라벨을 가져오거나 생성한다.
        /// 우선순위: 인스펙터에 할당된 costLabel(3D TMP) → 없으면 WorldSpace Canvas + TMP_UGUI 자동 생성.
        /// </summary>
        private TMP_Text GetOrCreateCostLabel()
        {
            if (resolvedLabel != null) return resolvedLabel;

            // 인스펙터에 미리 할당된 3D TMP 사용
            if (costLabel != null)
            {
                resolvedLabel = costLabel;
                return resolvedLabel;
            }

            // 자동 생성: WorldSpace Canvas + TextMeshProUGUI
            // (2D 게임에서 SpriteRenderer 와 가장 호환 잘 됨)
            var canvasGO = new GameObject("CostLabelCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.transform.localPosition = autoLabelLocalPosition;
            canvasGO.transform.localScale = Vector3.one * autoLabelCanvasScale;

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // SpriteRenderer 들과 동일한 sorting layer 에서 위에 그리도록
            if (backgroundRenderer != null)
                canvas.sortingLayerID = backgroundRenderer.sortingLayerID;
            else if (iconRenderer != null)
                canvas.sortingLayerID = iconRenderer.sortingLayerID;

            // sortingOrder: 아이콘/배경의 sortingOrder 보다 무조건 위에 오도록 계산
            int finalSortingOrder = autoLabelSortingOrder;
            if (iconRenderer != null)
                finalSortingOrder = Mathf.Max(finalSortingOrder, iconRenderer.sortingOrder + 10);
            if (backgroundRenderer != null)
                finalSortingOrder = Mathf.Max(finalSortingOrder, backgroundRenderer.sortingOrder + 10);
            canvas.sortingOrder = finalSortingOrder;

            // WorldSpace Canvas 의 카메라 명시 (none 으로 두어도 보통 작동하지만, 명시가 더 안전)
            if (Camera.main != null) canvas.worldCamera = Camera.main;

            var canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = autoLabelCanvasSize;
            canvasRT.pivot = new Vector2(0.5f, 0.5f);

            // TMP_UGUI 자식 — CanvasRenderer 명시 추가 (RequireComponent 의존 X)
            var labelGO = new GameObject("CostLabel",
                typeof(RectTransform), typeof(CanvasRenderer));
            labelGO.transform.SetParent(canvasGO.transform, false);

            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = autoLabelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = CostColor;
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.2f;
            tmp.raycastTarget = false;

            // Font Asset 다단계 폴백 — 어느 단계라도 폰트를 잡아야 텍스트가 그려짐
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
                tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            resolvedLabel = tmp;
            return resolvedLabel;
        }
    }
}
