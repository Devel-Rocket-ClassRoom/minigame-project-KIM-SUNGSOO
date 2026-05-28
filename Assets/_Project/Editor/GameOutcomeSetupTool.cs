#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KRTD.Combat;
using KRTD.Game;
using KRTD.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Setup Game Outcome UI in Active Scene` 실행 시
    /// 현재 씬에 GameOutcomeWatcher + GameOutcomeRoot(Win/Lose 패널 포함) 를 자동 생성.
    ///
    /// 정책:
    ///   - HUD Canvas 가 있으면 거기에 추가, 없으면 새로 만든다.
    ///   - 같은 이름의 GameOutcomeRoot 가 이미 있으면 제거 후 재생성.
    ///   - WinPanel / LosePanel 모두 시작 시 비활성 (코드도 Awake 에 SetActive(false)).
    /// </summary>
    public static class GameOutcomeSetupTool
    {
        private const string MenuPath = "KRTD/Setup Game Outcome UI in Active Scene";
        private const string HudCanvasName = "HUD Canvas";
        private const string RootName = "GameOutcomeRoot";
        private const string WatcherName = "GameOutcomeWatcher";

        [MenuItem(MenuPath)]
        public static void SetupGameOutcomeUI()
        {
            // 1. GameOutcomeWatcher 보장
            if (Object.FindFirstObjectByType<GameOutcomeWatcher>() == null)
            {
                var watcherGO = new GameObject(WatcherName, typeof(GameOutcomeWatcher));
                Undo.RegisterCreatedObjectUndo(watcherGO, "Create GameOutcomeWatcher");
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

            // 4. 기존 GameOutcomeRoot 가 있으면 제거 (재실행 시 깨끗)
            var existingRoot = canvas.transform.Find(RootName);
            if (existingRoot != null) Undo.DestroyObjectImmediate(existingRoot.gameObject);

            // 5. GameOutcomeRoot 생성 (Canvas 전체 덮는 컨테이너 + GameOutcomeView)
            var root = new GameObject(RootName,
                typeof(RectTransform), typeof(GameOutcomeView));
            root.transform.SetParent(canvas.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create GameOutcomeRoot");

            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // 6. WinPanel
            var winPanel = CreatePanel(
                parent: root.transform,
                name: "WinPanel",
                titleText: "STAGE CLEAR",
                titleColor: new Color(0.95f, 0.85f, 0.35f, 1f), // 노란빛
                buttonSpecs: new[]
                {
                    new ButtonSpec("NextStageButton",  "다음 스테이지",  new Color(0.30f, 0.55f, 0.35f, 1f)),
                    new ButtonSpec("StageSelectButton","스테이지 선택",  new Color(0.30f, 0.45f, 0.65f, 1f)),
                    new ButtonSpec("QuitButton",       "게임 끝내기",    new Color(0.65f, 0.30f, 0.30f, 1f)),
                });

            // 7. LosePanel
            var losePanel = CreatePanel(
                parent: root.transform,
                name: "LosePanel",
                titleText: "GAME OVER",
                titleColor: new Color(0.95f, 0.40f, 0.35f, 1f), // 붉은빛
                buttonSpecs: new[]
                {
                    new ButtonSpec("RestartButton",     "재시작",        new Color(0.30f, 0.55f, 0.35f, 1f)),
                    new ButtonSpec("StageSelectButton", "스테이지 선택",  new Color(0.30f, 0.45f, 0.65f, 1f)),
                    new ButtonSpec("QuitButton",        "게임 끝내기",    new Color(0.65f, 0.30f, 0.30f, 1f)),
                });

            // 8. GameOutcomeView 슬롯 연결
            var view = root.GetComponent<GameOutcomeView>();
            var so = new SerializedObject(view);
            so.FindProperty("winPanel").objectReferenceValue = winPanel.panel;
            so.FindProperty("losePanel").objectReferenceValue = losePanel.panel;
            so.FindProperty("winNextStageButton").objectReferenceValue   = winPanel.buttons[0];
            so.FindProperty("winStageSelectButton").objectReferenceValue = winPanel.buttons[1];
            so.FindProperty("winQuitButton").objectReferenceValue        = winPanel.buttons[2];
            so.FindProperty("loseRestartButton").objectReferenceValue     = losePanel.buttons[0];
            so.FindProperty("loseStageSelectButton").objectReferenceValue = losePanel.buttons[1];
            so.FindProperty("loseQuitButton").objectReferenceValue        = losePanel.buttons[2];
            so.FindProperty("freezeTimeOnEnd").boolValue = true;
            so.ApplyModifiedProperties();

            // 9. 시작 시 두 패널 모두 숨김
            winPanel.panel.SetActive(false);
            losePanel.panel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log("[GameOutcomeSetupTool] GameOutcomeUI 생성 완료. " +
                "씬 저장(Ctrl+S) 후 commit 잊지 마세요.");
        }

        // --- 패널 생성 ---------------------------------------------------------

        private struct ButtonSpec
        {
            public string name;
            public string label;
            public Color bg;
            public ButtonSpec(string n, string l, Color c) { name = n; label = l; bg = c; }
        }

        private struct PanelHandle
        {
            public GameObject panel;
            public Button[] buttons;
        }

        private static PanelHandle CreatePanel(Transform parent, string name, string titleText,
            Color titleColor, ButtonSpec[] buttonSpecs)
        {
            // 패널 자체 = 화면 덮는 backdrop (반투명 검정)
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero;
            pRT.anchorMax = Vector2.one;
            pRT.offsetMin = Vector2.zero;
            pRT.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // CenterBox (메뉴 컨테이너)
            var centerBox = new GameObject("CenterBox",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            centerBox.transform.SetParent(panel.transform, false);

            var cRT = centerBox.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0.5f);
            cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = new Vector2(460f, 420f);
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

            // 타이틀
            CreateLabel(centerBox.transform, "Title", titleText, 56f, titleColor);

            // 버튼들
            var buttons = new Button[buttonSpecs.Length];
            for (int i = 0; i < buttonSpecs.Length; i++)
            {
                var s = buttonSpecs[i];
                var go = CreateButton(centerBox.transform, s.name, s.label, s.bg);
                SetButtonHeight(go, 64f);
                buttons[i] = go.GetComponent<Button>();
            }

            return new PanelHandle { panel = panel, buttons = buttons };
        }

        // --- 공용 헬퍼들 -------------------------------------------------------

        private static Canvas FindOrCreateHudCanvas()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
                if (c.name == HudCanvasName) return c;

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

        private static void CreateLabel(Transform parent, string name, string text, float size, Color color)
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
            tmp.color = color;
            if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }
    }
}
#endif
