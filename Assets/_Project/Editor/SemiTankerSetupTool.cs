#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using KRTD.Combat;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Create SemiTanker Enemy` 실행 시 Semi-Tanker 적 한 종을 자동 생성한다.
    ///
    /// 동작:
    ///   1. Assets/Prefabs/Enemy/BlackWarrior.prefab 를 복사해 BlackLancer.prefab 생성
    ///   2. 복사본의 Body 자식의 SpriteRenderer.sprite + Animator.runtimeAnimatorController 를
    ///      Black Lancer 자산으로 스왑
    ///   3. Assets/EnemyData/Lancer.asset (EnemyData) 생성, 탱커 스탯 주입
    ///   4. 프리팹의 Enemy 컴포넌트:
    ///        - data 슬롯 = 새 Lancer.asset
    ///        - fallback 스탯도 동일한 값으로 동기화 (data 없이 직접 배치돼도 동작)
    ///   5. EnemyData.enemyPrefab = 새 BlackLancer.prefab
    ///
    /// 정책:
    ///   - 같은 경로의 자산이 있으면 덮어쓴다.
    ///   - Tiny Swords 자산 경로가 바뀌어 있으면 에러 로그 남기고 중단.
    /// </summary>
    public static class SemiTankerSetupTool
    {
        private const string MenuPath = "KRTD/Create SemiTanker Enemy";

        // 원본 / 출력 경로
        private const string SourcePrefabPath = "Assets/Prefabs/Enemy/BlackWarrior.prefab";
        private const string OutputPrefabPath = "Assets/Prefabs/Enemy/BlackLancer.prefab";
        private const string OutputDataPath = "Assets/EnemyData/Lancer.asset";

        // Tiny Swords Black Lancer 자산
        private const string LancerSpritePath = "Assets/Imported/Tiny Swords/Units/Black Units/Lancer/Lancer_Idle.png";
        private const string LancerControllerPath = "Assets/Imported/Tiny Swords/Units/Black Units/Lancer/Lancer Black Animations/Lancer_Black.controller";

        // Semi-Tanker 스탯 (디자인 메모: maxHp 와 minDamage 클램프 로 탱킹.
        // physicalDefense 가 너무 크면 모든 타워가 minDamage 로 깎이므로 중간값.)
        private const float TANK_HP = 80f;
        private const float TANK_MOVE_SPEED = 0.5f;
        private const int TANK_GOLD = 35;
        private const int TANK_LIFE_DMG = 2;
        private const float TANK_PHYS_DEF = 3f;
        private const float TANK_MAGIC_DEF = 3f;
        private const float TANK_MIN_DMG = 1f;
        private const float TANK_ATK_DMG = 5f;
        private const float TANK_ATK_RANGE = 1.0f;
        private const float TANK_DETECTION_RANGE = 2f;
        private const float TANK_ATK_INTERVAL = 1.2f;

        [MenuItem(MenuPath)]
        public static void CreateSemiTanker()
        {
            // --- 1. 자산 로드 -------------------------------------------------
            Sprite lancerSprite = LoadFirstSprite(LancerSpritePath);
            if (lancerSprite == null)
            {
                Debug.LogError($"[SemiTankerSetupTool] {LancerSpritePath} 에서 Sprite 를 찾지 못함. " +
                    "Tiny Swords 자산이 다른 경로에 있다면 이 스크립트의 LancerSpritePath 를 수정하세요.");
                return;
            }
            var lancerController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LancerControllerPath);
            if (lancerController == null)
            {
                Debug.LogError($"[SemiTankerSetupTool] {LancerControllerPath} 에서 AnimatorController 를 찾지 못함.");
                return;
            }
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[SemiTankerSetupTool] 원본 프리팹을 찾지 못함: {SourcePrefabPath}");
                return;
            }

            // --- 2. 프리팹 복사 ----------------------------------------------
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null)
                AssetDatabase.DeleteAsset(OutputPrefabPath);
            EnsureFolder(Path.GetDirectoryName(OutputPrefabPath));
            if (!AssetDatabase.CopyAsset(SourcePrefabPath, OutputPrefabPath))
            {
                Debug.LogError($"[SemiTankerSetupTool] 프리팹 복사 실패: {SourcePrefabPath} → {OutputPrefabPath}");
                return;
            }
            AssetDatabase.Refresh();

            // --- 3. EnemyData 생성 (먼저 만들어두고 프리팹에 주입) ----------
            EnsureFolder(Path.GetDirectoryName(OutputDataPath));
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(OutputDataPath);
            if (existing != null) AssetDatabase.DeleteAsset(OutputDataPath);

            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = "Semi-Tanker (Lancer)";
            data.maxHp = TANK_HP;
            data.moveSpeed = TANK_MOVE_SPEED;
            data.goldReward = TANK_GOLD;
            data.lifeDamage = TANK_LIFE_DMG;
            data.physicalDefense = TANK_PHYS_DEF;
            data.magicDefense = TANK_MAGIC_DEF;
            data.minDamage = TANK_MIN_DMG;
            data.attackDamage = TANK_ATK_DMG;
            data.attackRange = TANK_ATK_RANGE;
            data.detectionRange = TANK_DETECTION_RANGE;
            data.attackInterval = TANK_ATK_INTERVAL;
            data.attackType = AttackType.Physical;
            data.arrowPrefab = null;   // 근접
            AssetDatabase.CreateAsset(data, OutputDataPath);

            // --- 4. 복사된 프리팹 수정 ---------------------------------------
            var copiedRoot = PrefabUtility.LoadPrefabContents(OutputPrefabPath);
            try
            {
                copiedRoot.name = "BlackLancer";

                // Body 자식의 시각요소 스왑
                Transform body = copiedRoot.transform.Find("Body");
                if (body == null)
                {
                    Debug.LogError("[SemiTankerSetupTool] 복사된 프리팹에서 Body 자식을 찾지 못함.");
                    return;
                }
                var sr = body.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = lancerSprite;
                var anim = body.GetComponent<Animator>();
                if (anim != null) anim.runtimeAnimatorController = lancerController;

                // Enemy 컴포넌트의 data + fallback 스탯 동기화
                var enemy = copiedRoot.GetComponent<Enemy>();
                if (enemy != null)
                {
                    var so = new SerializedObject(enemy);
                    so.FindProperty("data").objectReferenceValue = data;
                    so.FindProperty("maxHp").floatValue = TANK_HP;
                    so.FindProperty("moveSpeed").floatValue = TANK_MOVE_SPEED;
                    so.FindProperty("goldReward").intValue = TANK_GOLD;
                    so.FindProperty("lifeDamage").intValue = TANK_LIFE_DMG;
                    so.FindProperty("physicalDefense").floatValue = TANK_PHYS_DEF;
                    so.FindProperty("magicDefense").floatValue = TANK_MAGIC_DEF;
                    so.FindProperty("minDamage").floatValue = TANK_MIN_DMG;
                    so.FindProperty("attackDamage").floatValue = TANK_ATK_DMG;
                    so.FindProperty("attackRange").floatValue = TANK_ATK_RANGE;
                    so.FindProperty("detectionRange").floatValue = TANK_DETECTION_RANGE;
                    so.FindProperty("attackInterval").floatValue = TANK_ATK_INTERVAL;
                    so.FindProperty("attackType").enumValueIndex = (int)AttackType.Physical;
                    var arrowProp = so.FindProperty("arrowPrefab");
                    if (arrowProp != null) arrowProp.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(copiedRoot, OutputPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(copiedRoot);
            }

            // --- 5. EnemyData.enemyPrefab 에 새 프리팹 연결 -------------------
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
            var dataSO = new SerializedObject(data);
            dataSO.FindProperty("enemyPrefab").objectReferenceValue = savedPrefab;
            dataSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);

            Debug.Log("[SemiTankerSetupTool] 생성 완료\n" +
                $"  - 프리팹: {OutputPrefabPath}\n" +
                $"  - 데이터: {OutputDataPath}\n" +
                "남은 작업:\n" +
                "  1) Wave_1 (혹은 신규 Wave) 의 entries 에 Lancer EnemyData 를 추가\n" +
                "  2) 인스펙터에서 스탯 조정 (현재 HP 80 / 방어력 3 / 이동속도 0.5)");
        }

        private static Sprite LoadFirstSprite(string path)
        {
            var single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (single != null) return single;
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
                if (a is Sprite s) return s;
            return null;
        }

        private static void EnsureFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            if (Directory.Exists(dir)) return;
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
    }
}
#endif
