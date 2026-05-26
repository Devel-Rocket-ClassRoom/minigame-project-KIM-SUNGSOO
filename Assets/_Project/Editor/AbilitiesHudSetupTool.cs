#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KRTD.Abilities;
using KRTD.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Setup Ability HUD in Active Scene` 를 실행하면
    /// 현재 씬에 특수능력 시스템(컨트롤러 + 능력 + UI 버튼) 을 한 번에 셋업한다.
    ///
    /// 생성 구조:
    ///   Scene root
    ///   ├─ Abilities                       (런타임 로직 묶음 — 타일맵과 무관)
    ///   │  ├─ SpecialAbilityController
    ///   │  ├─ Ability_Reinforcement        (ReinforcementAbility)
    ///   │  └─ Ability_LavaZone             (LavaZoneAbility)
    ///   └─ Ability HUD Canvas              (Screen Space - Overlay, 화면 고정)
    ///      └─ BottomRight (HorizontalLayoutGroup)
    ///         ├─ Btn_Reinforcement
    ///         │  ├─ SelectionHighlight (강조 테두리, 처음엔 비활성)
    ///         │  ├─ Icon
    ///         │  ├─ CooldownMask (Filled Radial360)
    ///         │  └─ CooldownText (TMP)
    ///         └─ Btn_LavaZone              (동일 구조)
    ///
    /// 정책:
    ///   - 같은 이름의 루트가 있으면 먼저 제거 (Undo 등록) 후 새로 생성.
    ///   - 보병/유성 프리팹 등 자산 슬롯은 비워둠 — 사용자가 직접 끌어 넣어야 함.
    ///   - AbilityButton.ability 슬롯은 SerializedObject 로 자동 연결.
    /// </summary>
    public static class AbilitiesHudSetupTool
    {
        private const string MenuPath = "KRTD/Setup Ability HUD in Active Scene";
        private const string CanvasName = "Ability HUD Canvas";
        private const string RootName = "Abilities";

        // 시각 상수
        private const float BUTTON_SIZE = 128f;
        private const float HIGHLIGHT_PAD = 12f;   // 강조 테두리가 버튼보다 이만큼 더 큼
        private const float ICON_INSET = 16f;      // 아이콘이 버튼보다 안쪽으로 이만큼
        private const float SPACING = 16f;         // 버튼 사이 간격
        private const float EDGE_PADDING = 32f;    // 화면 끝에서 떨어진 거리
        private const float COOLDOWN_FONT = 48f;

        [MenuItem(MenuPath)]
        public static void SetupAbilityHud()
        {
            // 기존 셋업 제거
            var existingCanvas = GameObject.Find(CanvasName);
            if (existingCanvas != null) Undo.DestroyObjectImmediate(existingCanvas);
            var existingRoot = GameObject.Find(RootName);
            if (existingRoot != null) Undo.DestroyObjectImmediate(existingRoot);

            // --- 런타임 로직 루트 ---------------------------------------------
            var abilitiesRoot = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(abilitiesRoot, "Create Abilities Root");

            var controllerGO = new GameObject("SpecialAbilityController", typeof(SpecialAbilityController));
            controllerGO.transform.SetParent(abilitiesRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(controllerGO, "Create SpecialAbilityController");

            var reinforcementGO = new GameObject("Ability_Reinforcement", typeof(ReinforcementAbility));
            reinforcementGO.transform.SetParent(abilitiesRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(reinforcementGO, "Create ReinforcementAbility");

            var lavaZoneGO = new GameObject("Ability_LavaZone", typeof(LavaZoneAbility));
            lavaZoneGO.transform.SetParent(abilitiesRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(lavaZoneGO, "Create LavaZoneAbility");

            // --- Canvas -------------------------------------------------------
            var canvasGO = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Ability HUD Canvas");

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // HUD Canvas (sortingOrder=10) 보다 위에 그려서 능력 버튼이 절대 가려지지 않도록.
            canvas.sortingOrder = 11;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }

            // 우하단 컨테이너 (오른쪽에서 왼쪽 순으로 버튼 정렬)
            float containerWidth = BUTTON_SIZE * 2f + SPACING;
            var bottomRight = CreateAnchoredContainer(
                canvasGO.transform,
                "BottomRight",
                anchor: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f),
                anchoredPosition: new Vector2(-EDGE_PADDING, EDGE_PADDING),
                size: new Vector2(containerWidth, BUTTON_SIZE));

            var hLayout = bottomRight.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = SPACING;
            hLayout.childAlignment = TextAnchor.MiddleRight;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;
            hLayout.childScaleWidth = false;
            hLayout.childScaleHeight = false;

            // --- 버튼 2개 -----------------------------------------------------
            var reinforcementAbility = reinforcementGO.GetComponent<ReinforcementAbility>();
            var lavaZoneAbility = lavaZoneGO.GetComponent<LavaZoneAbility>();

            CreateAbilityButton(bottomRight.transform, "Btn_Reinforcement", reinforcementAbility,
                highlightColor: new Color(1f, 0.85f, 0.3f, 0.95f),     // 금색
                buttonTint: new Color(0.85f, 0.95f, 1f, 0.9f));        // 시원한 톤
            CreateAbilityButton(bottomRight.transform, "Btn_LavaZone", lavaZoneAbility,
                highlightColor: new Color(1f, 0.85f, 0.3f, 0.95f),
                buttonTint: new Color(1f, 0.55f, 0.3f, 0.9f));         // 용암 톤

            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            Selection.activeGameObject = canvasGO;

            Debug.Log("[AbilitiesHudSetupTool] 셋업 완료.\n" +
                "  - 런타임: Abilities/{Controller, Reinforcement, LavaZone}\n" +
                "  - UI: Ability HUD Canvas/BottomRight/Btn_*\n" +
                "남은 작업:\n" +
                "  1) Ability_Reinforcement.soldierPrefab 에 Soldier 프리팹 드래그\n" +
                "  2) (선택) Ability_LavaZone.lavaZonePrefab 에 LavaZone 프리팹 드래그 — 비워두면 자동 LineRenderer 외곽선으로 표시\n" +
                "  3) 각 능력의 Icon 슬롯에 아이콘 스프라이트 드래그 (선택사항)");
        }

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

        /// <summary>
        /// 능력 버튼 1개 생성.
        /// 자식 순서: SelectionHighlight(뒤) → Icon → CooldownMask → CooldownText (앞).
        /// </summary>
        private static void CreateAbilityButton(
            Transform parent, string name, SpecialAbility ability,
            Color highlightColor, Color buttonTint)
        {
            var btn = new GameObject(name,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(AbilityButton));
            btn.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(btn, "Create " + name);

            var btnRT = btn.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(BUTTON_SIZE, BUTTON_SIZE);

            // 버튼 배경 (Image 가 raycast 타겟이라 클릭 영역도 됨)
            var btnImage = btn.GetComponent<Image>();
            btnImage.color = buttonTint;

            // --- 1. SelectionHighlight (뒤에 깔리는 금색 후광) -----------------
            var highlight = new GameObject("SelectionHighlight",
                typeof(RectTransform), typeof(Image));
            highlight.transform.SetParent(btn.transform, false);
            highlight.transform.SetAsFirstSibling();   // 버튼 뒤로 보내기

            var hlRT = highlight.GetComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0f, 0f);
            hlRT.anchorMax = new Vector2(1f, 1f);
            hlRT.offsetMin = new Vector2(-HIGHLIGHT_PAD, -HIGHLIGHT_PAD);
            hlRT.offsetMax = new Vector2(HIGHLIGHT_PAD, HIGHLIGHT_PAD);

            var hlImage = highlight.GetComponent<Image>();
            hlImage.color = highlightColor;
            hlImage.raycastTarget = false;
            highlight.SetActive(false);                // 처음엔 꺼둠

            // --- 2. Icon ------------------------------------------------------
            var icon = new GameObject("Icon",
                typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(btn.transform, false);

            var iconRT = icon.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(1f, 1f);
            iconRT.offsetMin = new Vector2(ICON_INSET, ICON_INSET);
            iconRT.offsetMax = new Vector2(-ICON_INSET, -ICON_INSET);

            var iconImage = icon.GetComponent<Image>();
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            // --- 3. CooldownMask (Filled Radial360) ---------------------------
            var mask = new GameObject("CooldownMask",
                typeof(RectTransform), typeof(Image));
            mask.transform.SetParent(btn.transform, false);

            var maskRT = mask.GetComponent<RectTransform>();
            maskRT.anchorMin = new Vector2(0f, 0f);
            maskRT.anchorMax = new Vector2(1f, 1f);
            maskRT.offsetMin = Vector2.zero;
            maskRT.offsetMax = Vector2.zero;

            var maskImage = mask.GetComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.6f);     // 반투명 어두움
            maskImage.raycastTarget = false;
            maskImage.type = Image.Type.Filled;
            maskImage.fillMethod = Image.FillMethod.Radial360;
            maskImage.fillOrigin = (int)Image.Origin360.Top;
            maskImage.fillClockwise = true;
            maskImage.fillAmount = 0f;                          // 처음엔 사용 가능 상태

            // --- 4. CooldownText (TMP) ----------------------------------------
            var labelGO = new GameObject("CooldownText",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(btn.transform, false);

            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var label = labelGO.GetComponent<TextMeshProUGUI>();
            label.text = "";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = COOLDOWN_FONT;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.outlineColor = Color.black;
            label.outlineWidth = 0.25f;
            label.raycastTarget = false;
            label.enabled = false;                              // 처음엔 숨김

            // --- AbilityButton 슬롯 자동 연결 ---------------------------------
            var abilityButton = btn.GetComponent<AbilityButton>();
            var so = new SerializedObject(abilityButton);
            so.FindProperty("ability").objectReferenceValue = ability;
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("cooldownMask").objectReferenceValue = maskImage;
            so.FindProperty("cooldownLabel").objectReferenceValue = label;
            so.FindProperty("selectionHighlight").objectReferenceValue = highlight;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
