using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Combat;
using KRTD.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 영웅 셋업을 한 클릭으로 끝내는 에디터 메뉴.
    ///
    /// GameObject 메뉴에 추가됨 (Hierarchy 우클릭 또는 상단 GameObject 메뉴):
    ///   - KRTD/Hero/Spawner                  → HeroSpawner GO 생성, HeroData 자동 연결 시도
    ///   - KRTD/Hero/Path Rally Controller    → HeroPathRallyController GO 생성
    ///   - KRTD/Hero/Portrait (UI)            → HUD Canvas 우상단에 HeroPortrait UI 계층 생성
    ///
    /// 다 Undo 지원 (Ctrl+Z 로 취소 가능) + 생성 직후 Selection 으로 잡혀 Inspector 가 열림.
    /// </summary>
    public static class HeroSetupMenu
    {
        // priority 가 같은 항목들은 메뉴 분리선 없이 묶임. 11 차이로 두면 같은 그룹.
        private const int MenuPriority = 10;

        [MenuItem("GameObject/KRTD/Hero/Spawner", false, MenuPriority)]
        private static void CreateHeroSpawner(MenuCommand menuCommand)
        {
            var go = new GameObject("HeroSpawner");
            var spawner = go.AddComponent<HeroSpawner>();

            // 부모/Scene 정렬 — 우클릭 컨텍스트 GameObject 가 있으면 그 아래에, 없으면 Scene 루트에.
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            // HeroData 자동 연결 — 프로젝트에 HeroData 가 있다면 첫 번째 것을 data 슬롯에.
            TryAutoAssignHeroData(spawner);

            Undo.RegisterCreatedObjectUndo(go, "Create HeroSpawner");
            Selection.activeObject = go;

            Debug.Log($"[HeroSetupMenu] HeroSpawner 생성. 위치 조정 후 data 가 비어 있으면 HeroData 에셋을 직접 드래그하세요.", go);
        }

        [MenuItem("GameObject/KRTD/Hero/Path Rally Controller", false, MenuPriority + 1)]
        private static void CreateHeroPathRallyController(MenuCommand menuCommand)
        {
            // 이미 씬에 하나 있으면 그 인스턴스로 점프 — 중복 생성 방지.
            var existing = Object.FindAnyObjectByType<HeroPathRallyController>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[HeroSetupMenu] HeroPathRallyController 가 이미 씬에 있습니다. 그 인스턴스를 선택합니다.", existing);
                return;
            }

            var go = new GameObject("HeroPathRallyController");
            go.AddComponent<HeroPathRallyController>();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            Undo.RegisterCreatedObjectUndo(go, "Create HeroPathRallyController");
            Selection.activeObject = go;
        }

        /// <summary>
        /// 프로젝트의 HeroData 중 첫 번째를 spawner.data 슬롯에 세팅.
        /// 여러 개가 있다면 무엇이든 첫 번째 — 인스펙터에서 사용자가 바꿀 수 있음.
        /// </summary>
        private static void TryAutoAssignHeroData(HeroSpawner spawner)
        {
            var guids = AssetDatabase.FindAssets("t:HeroData");
            if (guids == null || guids.Length == 0) return;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var data = AssetDatabase.LoadAssetAtPath<HeroData>(path);
            if (data == null) return;

            // 직렬화된 private 필드 \"data\" 에 접근하려면 SerializedObject 가 정석.
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("data");
            if (prop != null)
            {
                prop.objectReferenceValue = data;
                so.ApplyModifiedProperties();
            }
        }

        // -------------------------------------------------------------------
        // HeroPortrait (UI)
        // -------------------------------------------------------------------

        [MenuItem("GameObject/KRTD/Hero/Portrait (UI)", false, MenuPriority + 2)]
        private static void CreateHeroPortrait(MenuCommand menuCommand)
        {
            // 중복 방지 — 이미 있으면 그 인스턴스로 점프.
            var existing = Object.FindAnyObjectByType<HeroPortrait>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[HeroSetupMenu] HeroPortrait 가 이미 씬에 있습니다. 그 인스턴스를 선택합니다.", existing);
                return;
            }

            // HUD 후보 Canvas 찾기 — \"HUD Canvas\" 이름 우선, 없으면 HudController 가 붙은 Canvas, 그래도 없으면 임의 Canvas.
            Canvas canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[HeroSetupMenu] 씬에 Canvas 가 없습니다. UI → Canvas 를 먼저 만들어 주세요.");
                return;
            }

            var root = BuildHeroPortraitHierarchy(canvas);
            Undo.RegisterCreatedObjectUndo(root, "Create HeroPortrait");
            Selection.activeObject = root;
            Debug.Log($"[HeroSetupMenu] HeroPortrait 를 \"{canvas.name}\" 아래 우상단에 생성. " +
                $"필요하면 Portrait 자식의 Source Image 에 영웅 얼굴 스프라이트를 드래그하세요.", root);
        }

        private static Canvas FindHudCanvas()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            // 우선순위: "HUD Canvas" 이름 → HudController 가 있는 Canvas → 임의 Canvas
            foreach (var c in canvases)
                if (c.name == "HUD Canvas") return c;
            foreach (var c in canvases)
                if (c.GetComponentInChildren<HudController>() != null) return c;
            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static GameObject BuildHeroPortraitHierarchy(Canvas canvas)
        {
            // --- Root: HeroPortrait (Image 배경 + Button + HeroPortrait 컴포넌트) ---
            var root = new GameObject("HeroPortrait",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);

            var rootRT = (RectTransform)root.transform;
            rootRT.anchorMin = new Vector2(1f, 1f);
            rootRT.anchorMax = new Vector2(1f, 1f);
            rootRT.pivot = new Vector2(1f, 1f);
            rootRT.anchoredPosition = new Vector2(-10f, -10f);
            rootRT.sizeDelta = new Vector2(100f, 130f);

            var rootBg = root.GetComponent<Image>();
            rootBg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

            // --- Portrait (얼굴 자리 — 위쪽 영역) ---
            var portrait = CreateChildImage(root.transform, "Portrait");
            var portraitRT = (RectTransform)portrait.transform;
            portraitRT.anchorMin = new Vector2(0f, 1f);
            portraitRT.anchorMax = new Vector2(1f, 1f);
            portraitRT.pivot = new Vector2(0.5f, 1f);
            portraitRT.anchoredPosition = new Vector2(0f, -5f);
            portraitRT.sizeDelta = new Vector2(-10f, 90f); // 좌우 5px 여백, 높이 90
            portrait.GetComponent<Image>().preserveAspect = true;

            // --- HpBar 컨테이너 (아래쪽) ---
            var hpBar = new GameObject("HpBar", typeof(RectTransform));
            hpBar.transform.SetParent(root.transform, false);
            var hpBarRT = (RectTransform)hpBar.transform;
            hpBarRT.anchorMin = new Vector2(0f, 0f);
            hpBarRT.anchorMax = new Vector2(1f, 0f);
            hpBarRT.pivot = new Vector2(0.5f, 0f);
            hpBarRT.anchoredPosition = new Vector2(0f, 5f);
            hpBarRT.sizeDelta = new Vector2(-10f, 22f);

            // Background
            var hpBg = CreateChildImage(hpBar.transform, "Background");
            SetStretchAll((RectTransform)hpBg.transform);
            hpBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            // Fill (Filled Horizontal Left)
            var hpFill = CreateChildImage(hpBar.transform, "Fill");
            SetStretchAll((RectTransform)hpFill.transform);
            var fillImage = hpFill.GetComponent<Image>();
            fillImage.color = new Color(0.32f, 0.85f, 0.32f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            // --- DeathOverlay (부활 중 회색) ---
            var deathOverlay = CreateChildImage(root.transform, "DeathOverlay");
            SetStretchAll((RectTransform)deathOverlay.transform);
            deathOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            deathOverlay.SetActive(false);

            // --- CountdownText (남은 초) ---
            var countdown = new GameObject("CountdownText",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            countdown.transform.SetParent(root.transform, false);
            SetStretchAll((RectTransform)countdown.transform);
            var tmp = countdown.GetComponent<TextMeshProUGUI>();
            tmp.text = "5";
            tmp.fontSize = 40;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            countdown.SetActive(false);

            // --- HeroPortrait 컴포넌트 + 슬롯 연결 ---
            var portraitComp = root.AddComponent<HeroPortrait>();
            var so = new SerializedObject(portraitComp);
            so.FindProperty("fillImage").objectReferenceValue = fillImage;
            so.FindProperty("deathOverlay").objectReferenceValue = deathOverlay;
            so.FindProperty("countdownText").objectReferenceValue = tmp;
            // clickButton 은 HeroPortrait.Awake 에서 GetComponent<Button>() 으로 자동 인식.
            so.ApplyModifiedProperties();

            return root;
        }

        private static GameObject CreateChildImage(Transform parent, string name)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetStretchAll(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
