using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Data
{
    /// <summary>
    /// 한 웨이브의 구성. 여러 SpawnGroup으로 이뤄지며, 각 그룹마다 적/수량/간격을 지정.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "KRTD/Wave Data", order = 20)]
    public class WaveData : ScriptableObject
    {
        [Header("Wave")]
        public string waveName;
        public float startDelay = 3f;     // 웨이브 시작 전 대기
        public List<SpawnGroup> spawnGroups = new();
    }

    [Serializable]
    public class SpawnGroup
    {
        public EnemyData enemy;
        public int count = 5;
        public float intervalBetween = 0.8f;  // 한 마리 간격
        public float startOffset = 0f;        // 웨이브 시작 후 이 그룹이 도는 지연
        public int pathIndex = 0;             // 멀티패스 맵 대비
    }
}
