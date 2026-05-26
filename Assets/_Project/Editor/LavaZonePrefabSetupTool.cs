#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KRTD.Abilities;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Create LavaZone Prefab` 를 실행하면 Tiny Swords 의 Fire 애니메이션을
    /// 사용해 LavaZone 프리팹을 한 번에 생성하고, 씬에 Ability_LavaZone 이 있으면
    /// 그 슬롯에 자동 연결까지 한다.
    ///
    /// 결과물:
    ///   Assets/Prefabs/LavaZone.prefab
    ///     LavaZone_Prefab (LavaZone 컴포넌트, visualRoot 자동 연결)
    ///     └─ VisualRoot
    ///        ├─ Puddle        (SpriteRenderer, 주황 반투명, scale 3.6)
    ///        ├─ Flame_Center  (SpriteRenderer + Animator: Fire 1.controller)
    ///        ├─ Flame_NE
    ///        └─ Flame_SW
    ///
    /// 정책:
    ///   - 같은 경로의 프리팹이 있으면 덮어쓴다 (SaveAsPrefabAsset 기본 동작).
    ///   - Tiny Swords 자산 경로가 바뀌어 있으면 에러 로그 남기고 중단.
    /// </summary>
    public static class LavaZonePrefabSetupTool
    {
        private const string MenuPath = "KRTD/Create LavaZone Prefab";
        private const string OutputPath = "Assets/Prefabs/LavaZone.prefab";

        private const string FireSpritePath = "Assets/Imported/Tiny Swords/Particle FX/Fire_01.png";
        private const string FireControllerPath = "Assets/Imported/Tiny Swords/Particle FX/Fire 1 Animation/Fire 1.controller";

        [MenuItem(MenuPath)]
        public static void CreateLavaZonePrefab()
        {
            // --- 1. 외부 자산 로드 --------------------------------------------
            Sprite fireSprite = LoadFirstSprite(FireSpritePath);
            if (fireSprite == null)
            {
                Debug.LogError($"[LavaZonePrefabSetupTool] {FireSpritePath} 에서 Sprite 를 찾지 못함. " +
                    "Tiny Swords 자산이 다른 경로에 있다면 이 스크립트의 FireSpritePath 상수를 수정하세요.");
                return;
            }

            var fireController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FireControllerPath);
            if (fireController == null)
            {
                Debug.LogError($"[LavaZonePrefabSetupTool] {FireControllerPath} 에서 AnimatorController 를 찾지 못함.");
                return;
            }

            // --- 2. 빌드 ------------------------------------------------------
            var root = new GameObject("LavaZone_Prefab");
            var lavaZone = root.AddComponent<LavaZone>();

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            // Puddle (반투명 주황색 바닥. radius 1.8 × 2 = 지름 3.6)
            CreateSpriteChild(visualRoot.transform, "Puddle", fireSprite,
                color: new Color(1f, 0.5f, 0.2f, 0.55f),
                position: Vector3.zero,
                scale: new Vector3(3.6f, 3.6f, 1f),
                orderInLayer: 1);

            // Flame 3개 — 살짝 위치/스케일/애니속도 다르게 둬서 동시 깜빡임 방지
            CreateFlameChild(visualRoot.transform, "Flame_Center", fireSprite, fireController,
                position: new Vector3(0f, 0.3f, 0f),
                scale: new Vector3(1.3f, 1.3f, 1f),
                animSpeed: 1.0f, orderInLayer: 2);
            CreateFlameChild(visualRoot.transform, "Flame_NE", fireSprite, fireController,
                position: new Vector3(0.55f, 0.5f, 0f),
                scale: new Vector3(0.9f, 0.9f, 1f),
                animSpeed: 0.9f, orderInLayer: 2);
            CreateFlameChild(visualRoot.transform, "Flame_SW", fireSprite, fireController,
                position: new Vector3(-0.6f, 0f, 0f),
                scale: new Vector3(1.0f, 1.0f, 1f),
                animSpeed: 1.1f, orderInLayer: 2);

            // --- 3. LavaZone.visualRoot 슬롯 연결 -----------------------------
            var so = new SerializedObject(lavaZone);
            so.FindProperty("visualRoot").objectReferenceValue = visualRoot.transform;
            so.ApplyModifiedProperties();

            // --- 4. 출력 폴더 보장 + 프리팹 저장 -------------------------------
            var dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            Object.DestroyImmediate(root);

            if (prefab == null)
            {
                Debug.LogError("[LavaZonePrefabSetupTool] 프리팹 저장 실패. 경로 권한이나 잠금 상태를 확인하세요.");
                return;
            }

            // --- 5. (옵션) 씬의 Ability_LavaZone 슬롯에 자동 연결 -------------
            var ability = Object.FindFirstObjectByType<LavaZoneAbility>();
            if (ability != null)
            {
                var prefabLavaZone = prefab.GetComponent<LavaZone>();
                var aso = new SerializedObject(ability);
                aso.FindProperty("lavaZonePrefab").objectReferenceValue = prefabLavaZone;
                aso.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(ability.gameObject.scene);

                Debug.Log($"[LavaZonePrefabSetupTool] 프리팹 생성 완료: {OutputPath}\n" +
                    $"Ability_LavaZone.lavaZonePrefab 슬롯에도 자동 연결.");
            }
            else
            {
                Debug.Log($"[LavaZonePrefabSetupTool] 프리팹 생성 완료: {OutputPath}\n" +
                    "(씬에 Ability_LavaZone 이 없어서 자동 연결은 스킵. " +
                    "Ability_LavaZone.lavaZonePrefab 슬롯에 수동으로 드래그하세요.)");
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        /// <summary>
        /// 단일 Sprite 든 멀티 Sprite 시트든 첫 Sprite 를 가져온다.
        /// </summary>
        private static Sprite LoadFirstSprite(string path)
        {
            var single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (single != null) return single;

            // 시트라면 sub-asset 중 첫 Sprite 사용.
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
            {
                if (a is Sprite s) return s;
            }
            return null;
        }

        private static void CreateSpriteChild(Transform parent, string name, Sprite sprite,
            Color color, Vector3 position, Vector3 scale, int orderInLayer)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = orderInLayer;
        }

        private static void CreateFlameChild(Transform parent, string name, Sprite sprite,
            RuntimeAnimatorController controller, Vector3 position, Vector3 scale,
            float animSpeed, int orderInLayer)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(Animator));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;             // 에디터 미리보기용 — 런타임엔 Animator 가 덮어씀
            sr.sortingOrder = orderInLayer;

            var anim = go.GetComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.speed = animSpeed;
        }
    }
}
#endif
