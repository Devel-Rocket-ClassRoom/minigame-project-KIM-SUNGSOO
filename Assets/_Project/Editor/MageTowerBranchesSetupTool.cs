#if UNITY_EDITOR
using KRTD.Combat;
using KRTD.Map;
using UnityEditor;
using UnityEngine;

namespace KRTD.EditorTools
{
    /// <summary>
    /// 마법사 타워(MageTower_Lv3) 의 분기 진화 — Pyromancer(광역 AoE) / Frost Mage(둔화) — 자동 셋업.
    ///
    /// 메뉴: KRTD > Setup MageTower Branches
    ///
    /// 동작:
    ///   1) Magic.prefab 을 복제해 Magic_Fireball / Magic_Frostbolt prefab 생성.
    ///      - Fireball: splashRadius 채움 (광역).
    ///      - Frostbolt: slowAmount + slowDuration 채움 (둔화).
    ///   2) MageTower_Lv3.prefab 을 복제해 MageTower_Pyromancer / MageTower_FrostMage prefab 생성.
    ///      - 각자 magicPrefab 슬롯을 위에서 만든 Magic variant 로 교체.
    ///      - Pyromancer 는 광역이라 단발 damage 를 약간 낮춤(밸런스).
    ///   3) TowerData(BuildingData) 두 개 생성 (Pyromancer/FrostMage).
    ///   4) MageTower_Lv3.asset.nextBranches 에 두 TowerData 와이어링.
    ///
    /// 재실행 안전 — 같은 경로의 자산이 있으면 재사용해 값만 갱신.
    /// 스프라이트/색상/이펙트는 손대지 않으니 추후 인스펙터에서 자유롭게 차별화 가능.
    /// </summary>
    public static class MageTowerBranchesSetupTool
    {
        // 베이스 자산 경로
        private const string BaseMagicPrefabPath   = "Assets/Prefabs/Magic.prefab";
        private const string BaseMageLv3PrefabPath = "Assets/Prefabs/MageTower_Lv3.prefab";
        private const string MageLv3DataPath       = "Assets/TowerData/MageTower_Lv3.asset";

        // 생성될 자산 경로
        private const string FireballPrefabPath    = "Assets/Prefabs/Magic_Fireball.prefab";
        private const string FrostboltPrefabPath   = "Assets/Prefabs/Magic_Frostbolt.prefab";
        private const string PyromancerPrefabPath  = "Assets/Prefabs/MageTower_Pyromancer.prefab";
        private const string FrostMagePrefabPath   = "Assets/Prefabs/MageTower_FrostMage.prefab";
        private const string PyromancerDataPath    = "Assets/TowerData/MageTower_Pyromancer.asset";
        private const string FrostMageDataPath     = "Assets/TowerData/MageTower_FrostMage.asset";

        // 분기 튜닝 값 — 추후 EditorWindow 로 빼서 조절 가능하게 만들 수 있음.
        private const float FireballSplashRadius   = 1.5f;
        private const float FireballDamageMul      = 0.8f;   // 광역이라 단발 데미지 약간 낮춤
        private const float FrostboltSlowAmount    = 0.5f;   // 50% 감속
        private const float FrostboltSlowDuration  = 2f;     // 2초 지속

        private const int PyromancerCost = 280;
        private const int FrostMageCost  = 280;

        [MenuItem("KRTD/Setup MageTower Branches")]
        public static void Setup()
        {
            var baseMagic       = AssetDatabase.LoadAssetAtPath<GameObject>(BaseMagicPrefabPath);
            var baseMageLv3     = AssetDatabase.LoadAssetAtPath<GameObject>(BaseMageLv3PrefabPath);
            var mageLv3Data     = AssetDatabase.LoadAssetAtPath<BuildingData>(MageLv3DataPath);

            if (baseMagic == null || baseMageLv3 == null || mageLv3Data == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup MageTower Branches",
                    "베이스 자산을 찾을 수 없습니다.\n\n" +
                    "- " + BaseMagicPrefabPath + "\n" +
                    "- " + BaseMageLv3PrefabPath + "\n" +
                    "- " + MageLv3DataPath,
                    "확인");
                return;
            }

            if (baseMageLv3.GetComponent<MageTower>() == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup MageTower Branches",
                    "베이스 타워 prefab 에 MageTower 컴포넌트가 없습니다: " + BaseMageLv3PrefabPath,
                    "확인");
                return;
            }
            if (baseMagic.GetComponent<Magic>() == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup MageTower Branches",
                    "베이스 마법 prefab 에 Magic 컴포넌트가 없습니다: " + BaseMagicPrefabPath,
                    "확인");
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                // 1) Magic variant 두 개
                var fireballPrefab  = EnsureMagicVariant(baseMagic, FireballPrefabPath,
                    splashRadius: FireballSplashRadius, slowAmount: 0f, slowDuration: 0f);
                var frostboltPrefab = EnsureMagicVariant(baseMagic, FrostboltPrefabPath,
                    splashRadius: 0f, slowAmount: FrostboltSlowAmount, slowDuration: FrostboltSlowDuration);

                // 2) MageTower variant 두 개 (magicPrefab 교체 + 일부 스탯 튜닝)
                var pyromancerPrefab = EnsureTowerVariant(baseMageLv3, PyromancerPrefabPath,
                    magicPrefab: fireballPrefab.GetComponent<Magic>(),
                    damageMultiplier: FireballDamageMul);
                var frostMagePrefab  = EnsureTowerVariant(baseMageLv3, FrostMagePrefabPath,
                    magicPrefab: frostboltPrefab.GetComponent<Magic>(),
                    damageMultiplier: 1f);

                // 3) TowerData 두 개
                var pyromancerData = EnsureTowerData(PyromancerDataPath, "MageTower_Pyromancer",
                    pyromancerPrefab, PyromancerCost);
                var frostMageData  = EnsureTowerData(FrostMageDataPath, "MageTower_FrostMage",
                    frostMagePrefab, FrostMageCost);

                // 4) MageTower_Lv3.nextBranches 와이어링
                WireBranches(mageLv3Data, pyromancerData, frostMageData);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var pingTarget = AssetDatabase.LoadAssetAtPath<BuildingData>(PyromancerDataPath);
            if (pingTarget != null) EditorGUIUtility.PingObject(pingTarget);

            Debug.Log("[MageTowerBranchesSetupTool] 분기 진화 셋업 완료 — Pyromancer / FrostMage prefab + TowerData + Lv3 nextBranches 와이어링.");
        }

        // --- Helpers --------------------------------------------------------

        /// <summary>
        /// Magic variant prefab 을 보장한다. 없으면 베이스로 생성, 있으면 재사용 후 값만 갱신.
        /// </summary>
        private static GameObject EnsureMagicVariant(GameObject baseMagic, string path,
            float splashRadius, float slowAmount, float slowDuration)
        {
            EnsurePrefabCloned(baseMagic, path);

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var magic = contents.GetComponent<Magic>();
                var so = new SerializedObject(magic);
                SetFloatIfExists(so, "splashRadius", splashRadius);
                SetFloatIfExists(so, "slowAmount", slowAmount);
                SetFloatIfExists(so, "slowDuration", slowDuration);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// MageTower variant prefab 을 보장. magicPrefab 슬롯 교체 + 데미지 배율 튜닝.
        /// </summary>
        private static GameObject EnsureTowerVariant(GameObject baseTower, string path,
            Magic magicPrefab, float damageMultiplier)
        {
            // damage 배율을 적용하려면 베이스 값을 알아야 한다 — 재실행 시 누적 곱셈을 피하려고
            // 항상 베이스 prefab 의 damage 를 기준으로 새로 계산한다.
            float baseDamage = baseTower.GetComponent<MageTower>() != null
                ? new SerializedObject(baseTower.GetComponent<MageTower>()).FindProperty("damage").floatValue
                : 0f;

            EnsurePrefabCloned(baseTower, path);

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tower = contents.GetComponent<MageTower>();
                var so = new SerializedObject(tower);
                so.FindProperty("magicPrefab").objectReferenceValue = magicPrefab;
                if (baseDamage > 0f) so.FindProperty("damage").floatValue = baseDamage * damageMultiplier;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// path 에 prefab 이 없으면 baseObj 를 복제해 만든다. 있으면 no-op.
        /// (값 갱신은 호출자가 LoadPrefabContents 로 처리.)
        /// </summary>
        private static void EnsurePrefabCloned(GameObject baseObj, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseObj);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(inst, path);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        /// <summary>
        /// TowerData(BuildingData) ScriptableObject 를 보장. 없으면 생성, 있으면 필드 갱신.
        /// nextBranches 는 빈 배열(이 타워가 종착점).
        /// </summary>
        private static BuildingData EnsureTowerData(string path, string buildingName,
            GameObject prefab, int cost)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            BuildingData data;
            if (existing != null)
            {
                data = existing;
            }
            else
            {
                data = ScriptableObject.CreateInstance<BuildingData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            so.FindProperty("buildingName").stringValue = buildingName;
            so.FindProperty("buildingPrefab").objectReferenceValue = prefab;
            so.FindProperty("cost").intValue = cost;
            so.FindProperty("nextUpgrade").objectReferenceValue = null;
            var branchesProp = so.FindProperty("nextBranches");
            branchesProp.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void WireBranches(BuildingData mageLv3Data,
            BuildingData branch1, BuildingData branch2)
        {
            var so = new SerializedObject(mageLv3Data);
            var branchesProp = so.FindProperty("nextBranches");
            branchesProp.arraySize = 2;
            branchesProp.GetArrayElementAtIndex(0).objectReferenceValue = branch1;
            branchesProp.GetArrayElementAtIndex(1).objectReferenceValue = branch2;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mageLv3Data);
        }

        /// <summary>
        /// SerializedProperty 가 없는 빌드/씬에서도 깨지지 않게 안전하게 set.
        /// (Magic.cs 의 필드 이름이 바뀌면 조용히 스킵 — 셋업이 통째로 fail 하는 것보단 낫다.)
        /// </summary>
        private static void SetFloatIfExists(SerializedObject so, string propName, float value)
        {
            var p = so.FindProperty(propName);
            if (p == null) return;
            p.floatValue = value;
        }
    }
}
#endif
