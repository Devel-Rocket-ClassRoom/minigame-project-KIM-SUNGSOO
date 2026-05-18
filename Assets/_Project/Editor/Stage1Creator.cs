using UnityEditor;
using UnityEngine;
using KRTD.Data;
using KRTD.Map;
using KRTD.Towers;
using KRTD.Waves;

namespace KRTD.Editor
{
    /// <summary>
    /// 상단 메뉴 KRTD > Create Stage 1 Assets 를 실행하면
    /// Stage1_Map 프리팹 + LevelData + WaveData 3개를 자동 생성.
    /// </summary>
    public static class Stage1Creator
    {
        private const string PrefabsMapPath = "Assets/_Project/Prefabs/Map";
        private const string SOLevelsPath   = "Assets/_Project/ScriptableObjects/Levels";
        private const string SOWavesPath    = "Assets/_Project/ScriptableObjects/Waves";

        // S자 경로 웨이포인트 (좌→우)
        //
        //  Y= 4:        ┌───────────┐
        //  Y= 0: ───────┘           │
        //  Y=-4:                    └──────── (Goal)
        //
        private static readonly Vector3[] WaypointPositions =
        {
            new(-12f,  0f, 0f),   // WP_00: 스폰
            new( -5f,  0f, 0f),   // WP_01
            new( -5f,  4f, 0f),   // WP_02: 위로 꺾임
            new(  5f,  4f, 0f),   // WP_03
            new(  5f, -4f, 0f),   // WP_04: 아래로 꺾임
            new( 12f, -4f, 0f),   // WP_05: 골
        };

        // 경로 옆 타워 슬롯 위치 (6개)
        private static readonly Vector3[] SlotPositions =
        {
            new(-8.5f, -2.5f, 0f),  // Slot 01: 진입로 아래
            new(-8.5f,  2.5f, 0f),  // Slot 02: 진입로 위
            new( -1f,   6.5f, 0f),  // Slot 03: 상단 경로 좌측 상
            new(  1f,   6.5f, 0f),  // Slot 04: 상단 경로 우측 상
            new(  8.5f, -2f,  0f),  // Slot 05: 출구 경로 위
            new(  8.5f, -6.5f,0f),  // Slot 06: 출구 경로 아래
        };

        [MenuItem("KRTD/Create Stage 1 Assets")]
        public static void CreateStage1Assets()
        {
            EnsureFolders();

            var wave1 = CreateWaveData("Stage1_Wave01", "Wave 1", count: 8,  interval: 0.9f, delay: 3f);
            var wave2 = CreateWaveData("Stage1_Wave02", "Wave 2", count: 12, interval: 0.75f, delay: 3f);
            var wave3 = CreateWaveData("Stage1_Wave03", "Wave 3", count: 18, interval: 0.6f, delay: 3f);

            var levelData = CreateLevelData(wave1, wave2, wave3);
            CreateMapPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Stage1Creator] ✅ Stage 1 에셋 생성 완료!\n" +
                      "  → Prefabs/Map/Stage1_Map.prefab\n" +
                      "  → ScriptableObjects/Levels/Level_01.asset\n" +
                      "  → ScriptableObjects/Waves/Stage1_Wave01~03.asset\n\n" +
                      "⚠ 남은 작업:\n" +
                      "  1. EnemyData 에셋을 만든 뒤 각 WaveData의 SpawnGroup.enemy 에 연결\n" +
                      "  2. Level_01.asset 의 allowedTowers 에 사용할 타워 등록\n" +
                      "  3. WaveManager 인스펙터에서 Path_01 / EnemySpawner 연결");
        }

        // ──────────────────────────────────────────────────────────────────────

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(SOLevelsPath))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Levels");
            if (!AssetDatabase.IsValidFolder(SOWavesPath))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Waves");
        }

        private static WaveData CreateWaveData(string filename, string waveName, int count, float interval, float delay)
        {
            var wave = ScriptableObject.CreateInstance<WaveData>();
            wave.waveName   = waveName;
            wave.startDelay = delay;
            wave.spawnGroups.Add(new SpawnGroup
            {
                count           = count,
                intervalBetween = interval,
                startOffset     = 0f,
                pathIndex       = 0,
                // enemy: EnemyData 에셋 생성 후 인스펙터에서 연결
            });
            AssetDatabase.CreateAsset(wave, $"{SOWavesPath}/{filename}.asset");
            return wave;
        }

        private static LevelData CreateLevelData(params WaveData[] waves)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.levelId     = "Level_01";
            level.displayName = "1스테이지 - 숲길";
            level.startGold   = 200;
            level.startLives  = 20;
            foreach (var w in waves) level.waves.Add(w);
            // allowedTowers: TowerData 에셋 생성 후 인스펙터에서 추가
            AssetDatabase.CreateAsset(level, $"{SOLevelsPath}/Level_01.asset");
            return level;
        }

        private static void CreateMapPrefab()
        {
            var root = new GameObject("Stage1_Map");

            // ── 경로 ──────────────────────────────────────────────────────────
            var pathGO = new GameObject("Path_01");
            pathGO.transform.SetParent(root.transform);
            pathGO.AddComponent<Path>();   // autoFromChildren = true → 자식 자동 수집

            for (int i = 0; i < WaypointPositions.Length; i++)
            {
                var wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(pathGO.transform);
                wp.transform.position = WaypointPositions[i];
            }

            // ── 골 마커 ───────────────────────────────────────────────────────
            var goal = new GameObject("GoalMarker");
            goal.transform.SetParent(root.transform);
            goal.transform.position = WaypointPositions[^1];

            // ── 적 스포너 (스폰 위치에 배치) ──────────────────────────────────
            var spawnerGO = new GameObject("EnemySpawner");
            spawnerGO.transform.SetParent(root.transform);
            spawnerGO.transform.position = WaypointPositions[0];
            spawnerGO.AddComponent<EnemySpawner>();

            // ── 타워 슬롯 ─────────────────────────────────────────────────────
            var slotsParent = new GameObject("TowerSlots");
            slotsParent.transform.SetParent(root.transform);

            for (int i = 0; i < SlotPositions.Length; i++)
            {
                var slotGO = new GameObject($"TowerSlot_{i + 1:00}");
                slotGO.transform.SetParent(slotsParent.transform);
                slotGO.transform.position = SlotPositions[i];

                var slot = slotGO.AddComponent<TowerSlot>();

                // spawnAnchor는 Reset()이 인스펙터에서만 자동 설정되므로 직접 주입
                var so = new SerializedObject(slot);
                so.FindProperty("spawnAnchor").objectReferenceValue = slotGO.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── 프리팹 저장 ───────────────────────────────────────────────────
            string prefabPath = $"{PrefabsMapPath}/Stage1_Map.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success)
                Debug.LogError("[Stage1Creator] 프리팹 저장 실패!");
        }
    }
}
