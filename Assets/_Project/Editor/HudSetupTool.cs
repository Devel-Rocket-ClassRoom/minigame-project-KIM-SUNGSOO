#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KRTD.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Setup HUD in Active Scene` 를 실행하면
    /// 현재 씬에 킹덤러시 스타일 HUD 를 자동 생성한다.
    ///
    /// 레이아웃:
    ///   - 좌측 상단: LifeWidget + GoldWidget (세로 스택)
    ///   - 중앙 상단: WaveWidget
    ///
    /// 책임:
    ///   - HUD Canvas (Screen Space - Overlay)
    ///   - 좌측/중앙 컨테이너 + 위젯 (Icon + TMP_Text)
    ///   - HudController 자동 연결
    ///
    /// 정책:
    ///   - 같은 이름의 "HUD Canvas" 가 이미 있으면 먼저 제거(Undo 등록) 후 새로 생성.
    ///   - 아이콘 Sprite 는 비워둠 — 사용자가 직접 끌어 넣어야 함.
    /// </summary>
    public static class HudSetupTool
    {
        private const string MenuPath = "KRTD/Setup HUD in Active Scene";
        private const string CanvasName = "HUD Canvas";

        // 시각 상수 (킹덤러시 톤: 큰 아이콘 + 굵은 텍스트)
        private const float ICON_SIZE = 56f;
        private const float FONT_SIZE = 42f;
        private const float WIDGET_HEIGHT = 64f;
        private const float WIDGET_WIDTH_LEFT = 220f;
        private const float WIDGET_WIDTH_WAVE = 260f;
        private const float EDGE_PADDING = 20f;

        [MenuItem(MenuPath)]
        public static void SetupHud()
        {
            // 기존 HUD 가 있으면 제거 (Undo 가능)
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            // --- Canvas --------------------------------------------------------
            var canvasGO = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HudController));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem 없으면 생성
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // --- 좌측 상단: Life + Gold 스택 -----------------------------------
            var topLeft = CreateAnchoredContainer(
                canvasGO.transform,
                "TopLeft",
                anchor: new Vector2(0f, 1f),       // 좌상
                pivot: new Vector2(0f, 1f),
                anchoredPosition: new Vector2(EDGE_PADDING, -EDGE_PADDING),
                size: new Vector2(WIDGET_WIDTH_LEFT, WIDGET_HEIGHT * 2f + 8f));

            var vLayout = topLeft.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8f;
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.childControlWidth = false;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = false;
            vLayout.childForceExpandHeight = false;
            vLayout.childScaleWidth = false;
            vLayout.childScaleHeight = false;

            TMP_Text lifeText = CreateWidget(topLeft.transform, "LifeWidget", WIDGET_WIDTH_LEFT, "20");
            TMP_Text goldText = CreateWidget(topLeft.transform, "GoldWidget", WIDGET_WIDTH_LEFT, "100");

            // --- 중앙 상단: Wave ----------------------------------------------
            var topCenter = CreateAnchoredContainer(
                canvasGO.transform,
                "TopCenter",
                anchor: new Vector2(0.5f, 1f),     // 상단 중앙
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -EDGE_PADDING),
                size: new Vector2(WIDGET_WIDTH_WAVE, WIDGET_HEIGHT));

            TMP_Text waveText = CreateWidget(topCenter.transform, "WaveWidget", WIDGET_WIDTH_WAVE, "0 / 0", center: true);

            // --- HudController 슬롯 연결 --------------------------------------
            var hudController = canvasGO.GetComponent<HudController>();
            var so = new SerializedObject(hudController);
            so.FindProperty("lifeText").objectReferenceValue = lifeText;
            so.FindProperty("waveText").objectReferenceValue = waveText;
            so.FindProperty("goldText").objectReferenceValue = goldText;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Selection.activeGameObject = canvasGO;

            Debug.Log("[HudSetupTool] HUD 재생성 완료. 좌측상단=Life/Gold, 중앙상단=Wave. " +
                "각 위젯 Icon 의 Sprite 슬롯에 아이콘을 끌어 넣어주세요.");
        }

        /// <summary>앵커 기반의 빈 컨테이너 RectTransform 을 만든다.</summary>
        private static GameObject CreateAnchoredContainer(
            Transform parent, string name,
            Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            return go;
        }

        /// <summary>위젯 1개 생성. (Icon + Value) 가로 정렬.</summary>
        private static TMP_Text CreateWidget(Transform parent, string name, float width, string initialText, bool center = false)
        {
            var widget = new GameObject(name,
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            widget.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(widget, "Create " + name);

            var rt = widget.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, WIDGET_HEIGHT);

            var layout = widget.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = center ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            // Icon
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(widget.transform, false);
            var iconRT = icon.GetComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);
            var iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;

            // Value (TMP)
            var value = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            value.transform.SetParent(widget.transform, false);
            var valueRT = value.GetComponent<RectTransform>();
            valueRT.sizeDelta = new Vector2(width - ICON_SIZE - 12f, ICON_SIZE);

            var tmp = value.GetComponent<TextMeshProUGUI>();
            tmp.text = initialText;
            tmp.fontSize = FONT_SIZE;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = center ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            // 가독성: 외곽선 — 어두운 배경에서도 잘 보이도록
            tmp.outlineColor = Color.black;
            tmp.outlineWidth = 0.2f;

            return tmp;
        }
    }
}
#endif
