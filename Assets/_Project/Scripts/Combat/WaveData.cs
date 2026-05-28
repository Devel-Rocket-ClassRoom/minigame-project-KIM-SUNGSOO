using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 한 웨이브의 스폰 명세.
    /// 여러 개의 SpawnEntry 가 순차적으로 진행된다 (entry A 완료 후 entry B 시작).
    ///
    /// 예시:
    ///   - SpawnEntry { enemy: Goblin, count: 8, interval: 0.8 }   // 고블린 8마리, 0.8초 간격
    ///   - SpawnEntry { enemy: Orc,    count: 3, interval: 1.5 }   // 그 뒤 오크 3마리
    /// </summary>
    [CreateAssetMenu(fileName = "Wave_New", menuName = "KRTD/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("표시 정보")]
        public string waveName = "Wave";

        [Header("스폰 항목")]
        [Tooltip("순서대로 진행될 스폰 묶음들.")]
        public List<SpawnEntry> entries = new List<SpawnEntry>();

        [Header("타이밍")]
        [Tooltip("이 웨이브가 시작되기까지의 추가 대기 시간 (초). 0 이면 즉시 시작.")]
        public float startDelay = 0f;

        [Serializable]
        public class SpawnEntry
        {
            public EnemyData enemy;

            [Min(1)]
            public int count = 1;

            [Tooltip("이 묶음 내에서 적 사이의 스폰 간격 (초)")]
            [Min(0f)]
            public float interval = 1f;
        }
    }
}
