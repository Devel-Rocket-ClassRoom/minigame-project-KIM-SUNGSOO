#if UNITY_EDITOR
using KRTD.Cloud;
using KRTD.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// MainMenu 씬에 로그인 플로우 UI(① 로그인 / ② 환영)를 자동 생성·연결하는 일회성 에디터 툴.
    ///
    /// 메뉴: <b>KRTD ▸ Setup ▸ 로그인 + 메뉴 UI 생성</b>
    ///
    /// 하는 일:
    ///   1) 씬의 Canvas / EventSystem 보장 (없으면 생성)
    ///   2) FirebaseManagers 오브젝트 (FirebaseInit + AuthManager + CloudSaveManager) — 이미 있으면 재사용
    ///   3) ① LoginPanel  : 이메일/비밀번호/닉네임 입력 + [로그인]/[회원가입]/[게임 종료] + 상태 텍스트 → LoginView
    ///      ② WelcomePanel: 환영 텍스트 + [게임 시작](메인 메뉴로 진입) + [로그아웃]
    ///   4) LoginView / MainMenuView 의 ①② 관련 SerializeField 자동 연결
    ///   5) 생성한 모든 TMP 텍스트에 Pretendard-Regular SDF 폰트 적용
    ///
    /// ③ MainMenuPanel(기존 메인 메뉴)은 건드리지 않는다. MainMenuView 의
    ///   mainMenuPanel / startButton / settingsButton / logoutButton / settingsPanel 은
    ///   인스펙터에서 기존 패널·버튼에 직접 연결할 것.
    ///
    /// 주의:
    ///   - 기능 배치용 "뼈대" UI 다. 색/위치는 생성 후 에디터에서 다듬으면 된다.
    ///   - 다시 실행하면 기존 LoginPanel/WelcomePanel 을 지우고 새로 만든다(멱등).
    /// </summary>
    public static class LoginMenuUiSetup
    {
        private const float ColumnWidth = 480f;
        private const float ControlHeight = 64f;
        private const string FontAssetPath = "Assets/Font/Pretendard-Regular SDF.asset";

        private static readonly Color PanelBg = new Color(0f, 0f, 0f, 0.78f);
        private static readonly TMP_DefaultControls.Resources TmpRes = new TMP_DefaultControls.Resources();

        // Build() 동안 생성 TMP 에 적용할 폰트. 못 찾으면 null(기본 폰트 유지).
        private static TMP_FontAsset _font;

        [MenuItem("KRTD/Setup/로그인 + 메뉴 UI 생성")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            var canvas = EnsureCanvas();
            EnsureEventSystem();
            EnsureManagers();

            // 재실행 멱등 — ①② 기존 생성물 정리. (구버전 "MenuPanel"/"MainMenuPanel" 이름도 함께 제거.)
            DestroyChildByName(canvas.transform, "LoginPanel");
            DestroyChildByName(canvas.transform, "WelcomePanel");
            DestroyChildByName(canvas.transform, "MenuPanel");
            DestroyChildByName(canvas.transform, "MainMenuPanel");

            // --- ① LoginPanel ---
            GameObject loginPanel = CreateFullScreenPanel("LoginPanel", canvas.transform);
            RectTransform loginCol = CreateColumn(loginPanel.transform);
            CreateText(loginCol, "로그인", 48f);
            TMP_InputField emailInput = CreateInput(loginCol, "이메일", false);
            TMP_InputField passwordInput = CreateInput(loginCol, "비밀번호 (6자 이상)", true);
            TMP_InputField nicknameInput = CreateInput(loginCol, "닉네임 (회원가입 시)", false);
            Button loginButton = CreateButton(loginCol, "로그인");
            Button signUpButton = CreateButton(loginCol, "회원가입");
            Button quitButton = CreateButton(loginCol, "게임 종료");
            TMP_Text loginStatus = CreateText(loginCol, "", 26f);
            var loginView = loginPanel.AddComponent<LoginView>();

            // --- ② WelcomePanel ---
            GameObject welcomePanel = CreateFullScreenPanel("WelcomePanel", canvas.transform);
            RectTransform welcomeCol = CreateColumn(welcomePanel.transform);
            TMP_Text welcomeText = CreateText(welcomeCol, "환영합니다!", 40f);
            Button proceedButton = CreateButton(welcomeCol, "게임 시작");
            Button welcomeLogoutButton = CreateButton(welcomeCol, "로그아웃");
            welcomePanel.SetActive(false);

            // --- MainMenuView (기존 재사용 or 신규) ---
            MainMenuView mainMenu = Object.FindFirstObjectByType<MainMenuView>(FindObjectsInactive.Include);
            if (mainMenu == null)
            {
                var rootGo = new GameObject("MainMenuRoot", typeof(RectTransform));
                GameObjectUtility.SetParentAndAlign(rootGo, canvas.gameObject);
                StretchFull(rootGo.GetComponent<RectTransform>());
                mainMenu = rootGo.AddComponent<MainMenuView>();
            }

            // --- 와이어링 (①② 만. ③ 메인메뉴 필드는 건드리지 않음) ---
            Wire(loginView, "emailInput", emailInput);
            Wire(loginView, "passwordInput", passwordInput);
            Wire(loginView, "nicknameInput", nicknameInput);
            Wire(loginView, "loginButton", loginButton);
            Wire(loginView, "signUpButton", signUpButton);
            Wire(loginView, "statusText", loginStatus);

            Wire(mainMenu, "loginPanel", loginPanel);
            Wire(mainMenu, "loginView", loginView);
            Wire(mainMenu, "quitButton", quitButton);
            Wire(mainMenu, "welcomePanel", welcomePanel);
            Wire(mainMenu, "welcomeText", welcomeText);
            Wire(mainMenu, "proceedButton", proceedButton);
            Wire(mainMenu, "welcomeLogoutButton", welcomeLogoutButton);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = loginPanel;

            string fontMsg = _font != null ? "" : $" ⚠ 폰트 '{FontAssetPath}' 를 못 찾아 기본 폰트 사용.";
            Debug.Log("[LoginMenuUiSetup] 완료 — ① LoginPanel / ② WelcomePanel + FirebaseManagers 생성·연결됨. " +
                      "③ 메인 메뉴 패널/버튼은 인스펙터에서 MainMenuView 에 직접 연결하세요 " +
                      "(mainMenuPanel/startButton/settingsButton/logoutButton/settingsPanel). " +
                      "씬 저장(Ctrl+S) 후 Firebase 콘솔 설정도 확인." + fontMsg);
        }

        // --- 구성 요소 보장 ----------------------------------------------------

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) return canvas;

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        private static void EnsureManagers()
        {
            var existing = Object.FindFirstObjectByType<FirebaseInit>(FindObjectsInactive.Include);
            GameObject go = existing != null ? existing.gameObject : null;
            if (go == null)
            {
                go = new GameObject("FirebaseManagers");
                go.AddComponent<FirebaseInit>();
                Undo.RegisterCreatedObjectUndo(go, "Create FirebaseManagers");
            }
            if (go.GetComponent<AuthManager>() == null) go.AddComponent<AuthManager>();
            if (go.GetComponent<CloudSaveManager>() == null) go.AddComponent<CloudSaveManager>();
        }

        // --- UI 생성 헬퍼 ------------------------------------------------------

        private static GameObject CreateFullScreenPanel(string name, Transform parent)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            GameObjectUtility.SetParentAndAlign(panel, parent.gameObject);
            StretchFull(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = PanelBg;
            return panel;
        }

        private static RectTransform CreateColumn(Transform parent)
        {
            var col = new GameObject("Container",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            GameObjectUtility.SetParentAndAlign(col, parent.gameObject);

            var rt = col.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ColumnWidth, 0f);

            var vlg = col.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            var fitter = col.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rt;
        }

        private static TMP_InputField CreateInput(Transform parent, string placeholder, bool password)
        {
            var go = TMP_DefaultControls.CreateInputField(TmpRes);
            go.transform.SetParent(parent, false);
            var input = go.GetComponent<TMP_InputField>();
            if (password) input.contentType = TMP_InputField.ContentType.Password;
            ApplyFont(input.textComponent as TMP_Text);
            if (input.placeholder is TMP_Text ph) { ph.text = placeholder; ApplyFont(ph); }
            SetHeight(go, ControlHeight);
            return input;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes);
            go.transform.SetParent(parent, false);
            var t = go.GetComponentInChildren<TMP_Text>();
            if (t != null) { t.text = label; ApplyFont(t); }
            SetHeight(go, ControlHeight);
            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateText(Transform parent, string text, float fontSize)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes);
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TMP_Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            ApplyFont(t);
            SetHeight(go, fontSize + 18f);
            return t;
        }

        private static void ApplyFont(TMP_Text t)
        {
            if (t != null && _font != null) t.font = _font;
        }

        // --- 저수준 유틸 -------------------------------------------------------

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        private static void DestroyChildByName(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static void Wire(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[LoginMenuUiSetup] 필드 '{fieldName}' 를 {target.GetType().Name} 에서 못 찾음.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
