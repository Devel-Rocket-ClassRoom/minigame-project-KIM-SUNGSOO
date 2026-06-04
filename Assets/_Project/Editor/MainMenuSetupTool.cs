#if UNITY_EDITOR
using System.Linq;
using KRTD.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 빈 MainMenu 씬을 한 번 클릭으로 표준 구조(타이틀 + 3버튼 + 설정 패널)로 셋업한다.
    /// 메뉴: KRTD > Setup MainMenu Scene
    ///
    /// 사용법:
    ///   1) MainMenu.unity 씬을 연다.
    ///   2) 상단 메뉴 KRTD > Setup MainMenu Scene 클릭.
    ///   3) Ctrl+S 로 씬 저장 → Play 로 확인.
    ///
    /// 동작:
    ///   - 현재 활성 씬에 EventSystem / Canvas / Title / 3버튼 / SettingsPanel 생성.
    ///   - MainMenuView, SettingsPanelView 컴포넌트 부착 + 인스펙터 슬롯 자동 와이어링.
    ///   - 같은 이름의 자식이 이미 있으면 재사용(재실행 안전).
    /// </summary>
    public static class MainMenuSetupTool
    {
        private const string WoodTableSpritePath =
            "Assets/Imported/Tiny Swords/UI Elements/Wood Table/WoodTable.png";
        // 9개 sub-sprite 중 메뉴 보드로 가장 적합한 세로형 큰 보드.
        // 못 찾으면 가장 큰 sprite 자동 폴백.
        private const string PreferredBoardSpriteName = "WoodTable_7";

        private const string BannerSpritePath =
            "Assets/Imported/Tiny Swords/UI Elements/Banners/Banner.png";

        [MenuItem("KRTD/Setup MainMenu Scene")]
        public static void Setup()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "MainMenu")
            {
                if (!EditorUtility.DisplayDialog(
                    "Setup MainMenu Scene",
                    $"현재 활성 씬이 '{activeScene.name}' 입니다.\n그래도 이 씬에 MainMenu UI 를 셋업할까요?",
                    "진행", "취소"))
                {
                    return;
                }
            }

            EnsureEventSystem();
            var canvas = EnsureCanvas();
            var canvasT = canvas.transform;

            CreateSolidBackground(canvasT);
            CreateBoardBackground(canvasT);
            CreateTitle(canvasT);
            var startBtn = CreateButton(canvasT, "StartButton", "시작", new Vector2(0, 60));
            var settingsBtn = CreateButton(canvasT, "SettingsButton", "설정", new Vector2(0, -60));
            var quitBtn = CreateButton(canvasT, "QuitButton", "종료", new Vector2(0, -180));

            var rootGo = GetOrCreateChild(canvasT, "MainMenuRoot");
            var mainMenuView = rootGo.GetComponent<MainMenuView>() ?? rootGo.AddComponent<MainMenuView>();

            var panelGo = CreateSettingsPanel(canvasT);
            var bgmSlider = CreateSlider(panelGo.transform, "BgmSlider", new Vector2(0, 80));
            var sfxSlider = CreateSlider(panelGo.transform, "SfxSlider", new Vector2(0, 0));
            var closeBtn = CreateButton(panelGo.transform, "CloseButton", "닫기", new Vector2(0, -120));
            var settingsView = panelGo.GetComponent<SettingsPanelView>() ?? panelGo.AddComponent<SettingsPanelView>();

            CreateLabelAbove(bgmSlider.transform, "BGM");
            CreateLabelAbove(sfxSlider.transform, "SFX");

            WireMainMenuView(mainMenuView, startBtn, settingsBtn, quitBtn, panelGo);
            WireSettingsPanelView(settingsView, bgmSlider, sfxSlider, closeBtn);

            panelGo.SetActive(false);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = rootGo;
            Debug.Log("[MainMenuSetupTool] MainMenu 셋업 완료. Ctrl+S 로 씬 저장하세요.");
        }

        // --- 빌딩 블록 ---------------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;

            var go = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateSolidBackground(Transform parent)
        {
            var go = GetOrCreateChild(parent, "SolidBackground");
            go.transform.SetAsFirstSibling();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.11f, 0.08f, 1f); // 어두운 갈색 — 보드 뒤에 비치는 분위기
            img.raycastTarget = false;
        }

        private static void CreateBoardBackground(Transform parent)
        {
            var go = GetOrCreateChild(parent, "BoardBackground");
            go.transform.SetSiblingIndex(1); // SolidBackground 바로 뒤, 다른 UI 보다 앞

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(720, 940); // 화면 중앙 세로 보드

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            var sprite = LoadWoodTableSprite();
            if (sprite != null) img.sprite = sprite;
            else Debug.LogWarning($"[MainMenuSetupTool] WoodTable sprite 를 못 찾았습니다: {WoodTableSpritePath}");
        }

        private static Sprite LoadWoodTableSprite()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(WoodTableSpritePath);
            var sprites = all.OfType<Sprite>().ToArray();
            if (sprites.Length == 0) return null;

            var preferred = sprites.FirstOrDefault(s => s.name == PreferredBoardSpriteName);
            return preferred ?? sprites.OrderByDescending(s => s.rect.width * s.rect.height).First();
        }

        private static void CreateTitle(Transform parent)
        {
            var go = GetOrCreateChild(parent, "TitleText");

            // 이전 셋업에서 TitleText 자체에 직접 부착돼있던 TMP 는 제거 (배너 Image 로 교체).
            var oldTmp = go.GetComponent<TextMeshProUGUI>();
            if (oldTmp != null) Object.DestroyImmediate(oldTmp);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -40);
            rect.sizeDelta = new Vector2(900, 260);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            var banner = LoadBannerSprite();
            if (banner != null) img.sprite = banner;
            else Debug.LogWarning($"[MainMenuSetupTool] Banner sprite 를 못 찾았습니다: {BannerSpritePath}");

            // 배너 위에 얹히는 텍스트 (자식 Label).
            var labelGo = GetOrCreateChild(go.transform, "Label");
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.30f);
            labelRect.anchorMax = new Vector2(1, 0.80f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var tmp = labelGo.GetComponent<TextMeshProUGUI>() ?? labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Kingdom Rush TD";
            tmp.fontSize = 64;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        private static Sprite LoadBannerSprite()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(BannerSpritePath);
            var sprites = all.OfType<Sprite>().ToArray();
            if (sprites.Length == 0) return null;
            // 9개 sub-sprite 중 가장 큰 디자인 자동 선택 (보통 메인 깃발).
            return sprites.OrderByDescending(s => s.rect.width * s.rect.height).First();
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
        {
            var go = GetOrCreateChild(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(360, 90);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = GetOrCreateChild(go.transform, "Label");
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>() ?? labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btn;
        }

        private static GameObject CreateSettingsPanel(Transform parent)
        {
            var panelGo = GetOrCreateChild(parent, "SettingsPanel");
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.85f);
            img.raycastTarget = true;
            return panelGo;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos)
        {
            var go = GetOrCreateChild(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(600, 40);

            var slider = go.GetComponent<Slider>() ?? go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;
            slider.direction = Slider.Direction.LeftToRight;

            BuildSliderVisuals(slider);
            return slider;
        }

        private static void BuildSliderVisuals(Slider slider)
        {
            var sliderT = slider.transform;

            var bgGo = GetOrCreateChild(sliderT, "Background");
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>() ?? bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);

            var fillAreaGo = GetOrCreateChild(sliderT, "Fill Area");
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-15, 0);

            var fillGo = GetOrCreateChild(fillAreaGo.transform, "Fill");
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = new Vector2(10, 0);
            var fillImg = fillGo.GetComponent<Image>() ?? fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.55f, 0.85f, 1f);

            var handleAreaGo = GetOrCreateChild(sliderT, "Handle Slide Area");
            var handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handleGo = GetOrCreateChild(handleAreaGo.transform, "Handle");
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            var handleImg = handleGo.GetComponent<Image>() ?? handleGo.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
        }

        private static void CreateLabelAbove(Transform sliderT, string text)
        {
            var labelGo = GetOrCreateChild(sliderT, "Label");
            var rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(0, 6);
            rect.sizeDelta = new Vector2(120, 36);

            var tmp = labelGo.GetComponent<TextMeshProUGUI>() ?? labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        // --- 와이어링 (SerializedObject 로 private SerializeField 직접 채우기) -------

        private static void WireMainMenuView(MainMenuView view, Button start, Button settings, Button quit, GameObject panel)
        {
            var so = new SerializedObject(view);
            so.FindProperty("startButton").objectReferenceValue = start;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("settingsPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSettingsPanelView(SettingsPanelView view, Slider bgm, Slider sfx, Button close)
        {
            var so = new SerializedObject(view);
            so.FindProperty("bgmSlider").objectReferenceValue = bgm;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
