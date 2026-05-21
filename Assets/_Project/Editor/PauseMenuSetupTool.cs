#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KRTD.Game;
using KRTD.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Setup Pause Menu in Active Scene` 실행 시
    /// 현재 씬에 PauseController + 우측 상단 일시정지 버튼 + 일시정지 모달 패널을 자동 생성.
    ///
    /// 정책:
    ///   - HUD Canvas 가 있으면 거기에 추가, 없으면 새로 만든다.
    ///   - 같은 이름의 PauseMenuRoot 가 이미 있으면 제거 후 재생성.
    ///   - 시작 시 MenuPanel 은 비활성 (Pause 버튼 누르면 활성).
    /// </summary>
    public static class PauseMenuSetupTool
    {
        private const string MenuPath = "KRTD/Setup Pause Menu in Active Scene";
        private const string HudCanvasName = "HUD Canvas";
        private const string PauseMenuRootName = "PauseMenuRoot";

        [MenuItem(MenuPath)]
        public static void SetupPauseMenu()
        {
            // 1. PauseController 가 없으면 생성
            var existingController = Object.FindFirstObjectByType<PauseController>();
            if (existingController == null)
            {
                var controllerGO = new GameObject("PauseController", typeof(PauseController));
                Undo.RegisterCreatedObjectUndo(controllerGO, "Create PauseController");
            }

            // 2. HUD Canvas 찾기/생성
            Canvas canvas = FindOrCreateHudCanvas();

            // 3. EventSystem 보장
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // 4. 기존 PauseMenuRoot 가 있으면 제거 (재실행 시 깨끗)
            var existingRoot = canvas.transform.Find(PauseMenuRootName);
            if (existingRoot != null) Undo.DestroyObjectImmediate(existingRoot.gameObject);

            // 5. PauseMenuRoot 생성 (Canvas 전체를 덮는 컨테이너)
            var menuRoot = new GameObject(PauseMenuRootName,
                typeof(RectTransform), typeof(PauseMenuView));
            menuRoot.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(menuRoot, "Create PauseMenuRoot");

            var rootRT = menuRoot.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // 5-1. 우측 상단 PauseButton
            var pauseBtnGO = CreateButton(menuRoot.transform, "PauseButton", "II",
                new Color(0.2f, 0.2f, 0.25f, 0.9f));
            var pauseBtnRT = pauseBtnGO.GetComponent<RectTransform>();
            pauseBtnRT.anchorMin = new Vector2(1f, 1f);
            pauseBtnRT.anchorMax = new Vector2(1f, 1f);
            pauseBtnRT.pivot = new Vector2(1f, 1f);
            pauseBtnRT.anchoredPosition = new Vector2(-20f, -20f);
            pauseBtnRT.sizeDelta = new Vector2(72f, 72f);

            // 5-2. MenuPanel (전체 화면 backdrop + CenterBox)
            var menuPanel = new GameObject("MenuPanel",
                typeof(RectTransform), typeof(Image));
            menuPanel.transform.SetParent(menuRoot.transform, false);
            var panelRT = menuPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            menuPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f); // 반투명 백드롭

            // 5-3. CenterBox (메뉴 컨테이너)
            var centerBox = new GameObject("CenterBox",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            centerBox.transform.SetParent(menuPanel.transform, false);

            var centerRT = centerBox.GetComponent<RectTransform>();
            centerRT.anchorMin = new Vector2(0.5f, 0.5f);
            centerRT.anchorMax = new Vector2(0.5f, 0.5f);
            centerRT.pivot = new Vector2(0.5f, 0.5f);
            centerRT.anchoredPosition = Vector2.zero;
            centerRT.sizeDelta = new Vector2(420f, 380f);
            centerBox.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.22f, 0.96f);

            var layout = centerBox.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            CreateLabel(centerBox.transform, "Title", "일시정지", 52f);
            var resumeBtn  = CreateButton(centerBox.transform, "ResumeButton",  "계속하기", new Color(0.30f, 0.55f, 0.35f, 1f));
            var restartBtn = CreateButton(centerBox.transform, "RestartButton", "재시작",   new Color(0.30f, 0.45f, 0.65f, 1f));
            var quitBtn    = CreateButton(centerBox.transform, "QuitButton",    "나가기",   new Color(0.65f, 0.30f, 0.30f, 1f));
            SetButtonHeight(resumeBtn, 64f);
            SetButtonHeight(restartBtn, 64f);
            SetButtonHeight(quitBtn, 64f);

            // 6. PauseMenuView 슬롯 연결
            var view = menuRoot.GetComponent<PauseMenuView>();
            var so = new SerializedObject(view);
            so.FindProperty("pauseButton").objectReferenceValue = pauseBtnGO.GetComponent<Button>();
            so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn.GetComponent<Button>();
            so.FindProperty("restartButton").objectReferenceValue = restartBtn.GetComponent<Button>();
            so.FindProperty("quitButton").objectReferenceValue = quitBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();

            // 7. 시작 시 패널 숨김
            menuPanel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(menuRoot.scene);
            Selection.activeGameObject = menuRoot;

            Debug.Log("[PauseMenuSetupTool] PauseMenu 생성 완료. " +
                "씬을 Build Settings 에 등록해야 재시작 버튼이 동작합니다.");
        }

        // --- 헬퍼들 -----------------------------------------------------------

        private static Canvas FindOrCreateHudCanvas()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
                if (c.name == HudCanvasName) return c;

            // 없으면 새로 만든다
            var canvasGO = new GameObject(HudCanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Color bg)
        {
            var btnGO = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(parent, false);
            btnGO.GetComponent<Image>().color = bg;

            // 라벨 (자식)
            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(btnGO.transform, false);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 32f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            return btnGO;
        }

        private static void SetButtonHeight(GameObject btnGO, float height)
        {
            var le = btnGO.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }

        private static void CreateLabel(Transform parent, string name, string text, float size)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = size + 16f;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
#endif
