#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using KRTD.Combat;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 메뉴 `KRTD > Create Healer Enemy` 실행 시 힐러(Monk) 적 한 종을 자동 생성한다.
    ///
    /// 동작:
    ///   1. Assets/Prefabs/Enemy/BlackWarrior.prefab 를 복사해 BlackMonk.prefab 생성
    ///   2. 복사본의 Body 자식의 SpriteRenderer.sprite + Animator.runtimeAnimatorController 를
    ///      Black Monk 자산으로 스왑
    ///   3. Assets/EnemyData/Monk.asset (EnemyData) 생성, 힐러 스탯 주입
    ///   4. 프리팹의 Enemy 컴포넌트:
    ///        - data 슬롯 = 새 Monk.asset
    ///        - fallback 스탯도 동일한 값으로 동기화 (data 없이 직접 배치돼도 동작)
    ///        - Monk 컨트롤러에는 파라미터가 없으므로 isAttacking/heal/death 트리거 이름은 비운다.
    ///   5. EnemyData.enemyPrefab = 새 BlackMonk.prefab
    ///
    /// 정책:
    ///   - 같은 경로의 자산이 있으면 덮어쓴다.
    ///   - Tiny Swords 자산 경로가 바뀌어 있으면 에러 로그 남기고 중단.
    ///   - 힐러는 공격하지 않고(attackDamage=0) healInterval 마다 사거리 안에서
    ///     가장 많이 다친 아군 적을 healAmount 만큼 회복한다(이동하면서 시전).
    /// </summary>
    public static class MonkHealerSetupTool
    {
        private const string MenuPath = "KRTD/Create Healer Enemy";

        // 원본 / 출력 경로
        private const string SourcePrefabPath = "Assets/Prefabs/Enemy/BlackWarrior.prefab";
        private const string OutputPrefabPath = "Assets/Prefabs/Enemy/BlackMonk.prefab";
        private const string OutputDataPath = "Assets/EnemyData/Monk.asset";

        // Tiny Swords Black Monk 자산
        private const string MonkSpritePath = "Assets/Imported/Tiny Swords/Units/Black Units/Monk/Idle.png";
        private const string MonkControllerPath = "Assets/Imported/Tiny Swords/Units/Black Units/Monk/Monk Black Animations/Monk_Black.controller";

        // 힐러 스탯 (디자인 메모: 공격하지 않는 지원형. 다친 아군을 회복하므로 우선 처치 대상이 되도록
        // 골드 보상은 높이고 체력은 중간 이하로 둔다.)
        private const float HEAL_HP = 45f;
        private const float HEAL_MOVE_SPEED = 0.6f;
        private const int HEAL_GOLD = 40;
        private const int HEAL_LIFE_DMG = 1;
        private const float HEAL_PHYS_DEF = 1f;
        private const float HEAL_MAGIC_DEF = 1f;
        private const float HEAL_MIN_DMG = 1f;
        private const float HEAL_ATK_DMG = 0f;     // 공격 안 함
        private const float HEAL_ATK_RANGE = 0.8f;
        private const float HEAL_DETECTION_RANGE = 2f;
        private const float HEAL_ATK_INTERVAL = 1f;
        private const float HEAL_AMOUNT = 6f;
        private const float HEAL_RANGE = 2.5f;
        private const float HEAL_INTERVAL = 1.5f;

        [MenuItem(MenuPath)]
        public static void CreateHealer()
        {
            // --- 1. 자산 로드 -------------------------------------------------
            Sprite monkSprite = LoadFirstSprite(MonkSpritePath);
            if (monkSprite == null)
            {
                Debug.LogError($"[MonkHealerSetupTool] {MonkSpritePath} 에서 Sprite 를 찾지 못함. " +
                    "Tiny Swords 자산이 다른 경로에 있다면 이 스크립트의 MonkSpritePath 를 수정하세요.");
                return;
            }
            var monkController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MonkControllerPath);
            if (monkController == null)
            {
                Debug.LogError($"[MonkHealerSetupTool] {MonkControllerPath} 에서 AnimatorController 를 찾지 못함.");
                return;
            }
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError($"[MonkHealerSetupTool] 원본 프리팹을 찾지 못함: {SourcePrefabPath}");
                return;
            }

            // --- 2. 프리팹 복사 ----------------------------------------------
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null)
                AssetDatabase.DeleteAsset(OutputPrefabPath);
            EnsureFolder(Path.GetDirectoryName(OutputPrefabPath));
            if (!AssetDatabase.CopyAsset(SourcePrefabPath, OutputPrefabPath))
            {
                Debug.LogError($"[MonkHealerSetupTool] 프리팹 복사 실패: {SourcePrefabPath} → {OutputPrefabPath}");
                return;
            }
            AssetDatabase.Refresh();

            // --- 3. EnemyData 생성 (먼저 만들어두고 프리팹에 주입) ----------
            EnsureFolder(Path.GetDirectoryName(OutputDataPath));
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(OutputDataPath);
            if (existing != null) AssetDatabase.DeleteAsset(OutputDataPath);

            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = "Healer (Monk)";
            data.maxHp = HEAL_HP;
            data.moveSpeed = HEAL_MOVE_SPEED;
            data.goldReward = HEAL_GOLD;
            data.lifeDamage = HEAL_LIFE_DMG;
            data.physicalDefense = HEAL_PHYS_DEF;
            data.magicDefense = HEAL_MAGIC_DEF;
            data.minDamage = HEAL_MIN_DMG;
            data.attackDamage = HEAL_ATK_DMG;
            data.attackRange = HEAL_ATK_RANGE;
            data.detectionRange = HEAL_DETECTION_RANGE;
            data.attackInterval = HEAL_ATK_INTERVAL;
            data.attackType = AttackType.Physical;
            data.arrowPrefab = null;
            data.healAmount = HEAL_AMOUNT;
            data.healRange = HEAL_RANGE;
            data.healInterval = HEAL_INTERVAL;
            AssetDatabase.CreateAsset(data, OutputDataPath);

            // --- 4. 복사된 프리팹 수정 ---------------------------------------
            var copiedRoot = PrefabUtility.LoadPrefabContents(OutputPrefabPath);
            try
            {
                copiedRoot.name = "BlackMonk";

                // Body 자식의 시각요소 스왑
                Transform body = copiedRoot.transform.Find("Body");
                if (body == null)
                {
                    Debug.LogError("[MonkHealerSetupTool] 복사된 프리팹에서 Body 자식을 찾지 못함.");
                    return;
                }
                var sr = body.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = monkSprite;
                var anim = body.GetComponent<Animator>();
                if (anim != null) anim.runtimeAnimatorController = monkController;

                // Enemy 컴포넌트의 data + fallback 스탯 동기화
                var enemy = copiedRoot.GetComponent<Enemy>();
                if (enemy != null)
                {
                    var so = new SerializedObject(enemy);
                    so.FindProperty("data").objectReferenceValue = data;
                    so.FindProperty("maxHp").floatValue = HEAL_HP;
                    so.FindProperty("moveSpeed").floatValue = HEAL_MOVE_SPEED;
                    so.FindProperty("goldReward").intValue = HEAL_GOLD;
                    so.FindProperty("lifeDamage").intValue = HEAL_LIFE_DMG;
                    so.FindProperty("physicalDefense").floatValue = HEAL_PHYS_DEF;
                    so.FindProperty("magicDefense").floatValue = HEAL_MAGIC_DEF;
                    so.FindProperty("minDamage").floatValue = HEAL_MIN_DMG;
                    so.FindProperty("attackDamage").floatValue = HEAL_ATK_DMG;
                    so.FindProperty("attackRange").floatValue = HEAL_ATK_RANGE;
                    so.FindProperty("detectionRange").floatValue = HEAL_DETECTION_RANGE;
                    so.FindProperty("attackInterval").floatValue = HEAL_ATK_INTERVAL;
                    so.FindProperty("attackType").enumValueIndex = (int)AttackType.Physical;
                    var arrowProp = so.FindProperty("arrowPrefab");
                    if (arrowProp != null) arrowProp.objectReferenceValue = null;
                    so.FindProperty("healAmount").floatValue = HEAL_AMOUNT;
                    so.FindProperty("healRange").floatValue = HEAL_RANGE;
                    so.FindProperty("healInterval").floatValue = HEAL_INTERVAL;
                    // Monk 컨트롤러에는 파라미터가 없으므로 애니 트리거 이름을 비워 경고 방지
                    var isAtk = so.FindProperty("isAttackingBool");
                    if (isAtk != null) isAtk.stringValue = "";
                    var deathTrig = so.FindProperty("deathTrigger");
                    if (deathTrig != null) deathTrig.stringValue = "";
                    var healTrig = so.FindProperty("healTrigger");
                    if (healTrig != null) healTrig.stringValue = "";
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

            Debug.Log("[MonkHealerSetupTool] 생성 완료\n" +
                $"  - 프리팹: {OutputPrefabPath}\n" +
                $"  - 데이터: {OutputDataPath}\n" +
                "남은 작업:\n" +
                "  1) Wave 의 entries 에 Monk(Healer) EnemyData 를 추가 (탱커/일반 적과 함께 등장시켜야 힐이 의미 있음)\n" +
                "  2) 인스펙터에서 스탯 조정 (현재 HP 45 / 힐량 6 / 힐 사거리 2.5 / 힐 텀 1.5s)");
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
