#if UNITY_EDITOR
using KRTD.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KRTD.EditorTools
{
    /// <summary>
    /// GameOutcomeView 의 WinPanel 하위에 별(Star) 3개를 자동 생성하고,
    /// 컴포넌트의 starIcons 배열까지 와이어링하는 1-클릭 셋업 도구.
    ///
    /// 메뉴: KRTD > Setup GameOutcome Stars
    ///
    /// 사용법:
    ///   1) GameOutcomeView 가 들어있는 씬(예: TileMapeScene)을 연다.
    ///   2) 상단 메뉴 KRTD > Setup GameOutcome Stars 클릭.
    ///   3) Ctrl+S 로 씬 저장 → Play 로 확인.
    ///
    /// 동작:
    ///   - 활성 씬에서 GameOutcomeView 를 찾고, 인스펙터에 연결된 winPanel 아래에
    ///     "Stars" 컨테이너(Horizontal Layout Group) + Star0/1/2 (Image, Knob 스프라이트) 생성.
    ///   - GameOutcomeView.starIcons 배열에 좌→우 순서로 할당.
    ///   - 같은 이름의 자식이 이미 있으면 재사용(재실행 안전).
    ///   - WinPanel 활성/비활성 상태는 건드리지 않는다(시작 시 비활성 정책 유지).
    /// </summary>
    public static class GameOutcomeStarsSetupTool
    {
        private const string MenuPath = "KRTD/Setup GameOutcome Stars";
        private const string StarsContainerName = "Stars";
        private const string StarNamePrefix = "Star";
        private const int StarCount = 3;
        private static readonly Vector2 StarSize = new Vector2(64f, 64f);
        private const float StarSpacing = 12f;

        // Stars 컨테이너 기본 배치 — WinPanel 상단에서 약간 아래쪽 가운데.
        // 이미 존재하는 컨테이너의 위치/크기는 덮어쓰지 않는다.
        private static readonly Vector2 StarsAnchoredPosition = new Vector2(0f, -160f);

        // 색 모드(스프라이트 미설정 시) 의 기본 ON 색 — 앰버 톤.
        private static readonly Color DefaultStarOnColor = new Color(1f, 0.78f, 0.16f, 1f);

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            // 비활성 객체도 찾도록 includeInactive=true.
#if UNITY_2023_1_OR_NEWER
            var view = Object.FindFirstObjectByType<GameOutcomeView>(FindObjectsInactive.Include);
#else
            var view = Object.FindObjectOfType<GameOutcomeView>(true);
#endif
            if (view == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup GameOutcome Stars",
                    "현재 씬에서 GameOutcomeView 컴포넌트를 찾을 수 없습니다.\n" +
                    "TileMapeScene 처럼 GameOutcomeView 가 포함된 씬을 먼저 열어주세요.",
                    "확인");
                return;
            }

            var so = new SerializedObject(view);
            so.Update();

            var winPanelProp = so.FindProperty("winPanel");
            if (winPanelProp == null || winPanelProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup GameOutcome Stars",
                    "GameOutcomeView.winPanel 슬롯이 비어 있습니다.\n" +
                    "먼저 인스펙터에서 WinPanel GameObject 를 할당해주세요.",
                    "확인");
                return;
            }

            var winPanel = (GameObject)winPanelProp.objectReferenceValue;

            // 1) Stars 컨테이너 — 있으면 재사용, 없으면 생성.
            var starsT = winPanel.transform.Find(StarsContainerName);
            GameObject starsGo;
            if (starsT == null)
            {
                starsGo = new GameObject(StarsContainerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(starsGo, "Create Stars container");
                Undo.SetTransformParent(starsGo.transform, winPanel.transform, "Parent Stars");

                var rt = (RectTransform)starsGo.transform;
                rt.localScale = Vector3.one;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = StarsAnchoredPosition;
                rt.sizeDelta = new Vector2(
                    StarSize.x * StarCount + StarSpacing * (StarCount - 1),
                    StarSize.y);
            }
            else
            {
                starsGo = starsT.gameObject;
            }

            // 2) HorizontalLayoutGroup 보장.
            var hlg = starsGo.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null)
            {
                hlg = Undo.AddComponent<HorizontalLayoutGroup>(starsGo);
                hlg.spacing = StarSpacing;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
            }

            // 3) Knob 내장 스프라이트(원형) 로드 — 별 아이콘 임시 플레이스홀더.
            //    못 찾아도 진행(색만으로도 동작 확인 가능).
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // 4) Star0/1/2 생성/재사용.
            var icons = new Image[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                var starName = StarNamePrefix + i;
                var existing = starsGo.transform.Find(starName);

                GameObject starGo;
                if (existing == null)
                {
                    starGo = new GameObject(
                        starName,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    Undo.RegisterCreatedObjectUndo(starGo, "Create " + starName);
                    Undo.SetTransformParent(starGo.transform, starsGo.transform, "Parent " + starName);

                    var rt = (RectTransform)starGo.transform;
                    rt.localScale = Vector3.one;
                    rt.sizeDelta = StarSize;
                }
                else
                {
                    starGo = existing.gameObject;
                }

                var img = starGo.GetComponent<Image>();
                if (img == null) img = Undo.AddComponent<Image>(starGo);
                if (img.sprite == null && knob != null) img.sprite = knob;
                // 색은 런타임에 ApplyStarRating 이 덮어쓰므로 여기선 ON 색으로 깔끔하게 노출.
                img.color = DefaultStarOnColor;

                icons[i] = img;
            }

            // 5) GameOutcomeView.starIcons 배열 와이어링.
            var starIconsProp = so.FindProperty("starIcons");
            if (starIconsProp == null)
            {
                Debug.LogError(
                    "[GameOutcomeStarsSetupTool] starIcons SerializedProperty 를 찾지 못했습니다. " +
                    "GameOutcomeView 의 필드 이름이 바뀐 건 아닌지 확인하세요.");
                return;
            }
            starIconsProp.arraySize = StarCount;
            for (int i = 0; i < StarCount; i++)
            {
                starIconsProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);

            Selection.activeGameObject = starsGo;
            EditorGUIUtility.PingObject(starsGo);

            Debug.Log(
                "[GameOutcomeStarsSetupTool] WinPanel 하위에 별 " + StarCount +
                "개 셋업 + starIcons 와이어링 완료. Ctrl+S 로 씬 저장 후 Play 로 확인하세요.");
        }
    }
}
#endif
