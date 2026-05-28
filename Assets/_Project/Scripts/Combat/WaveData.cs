using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Combat
{
    /// <summary>
    /// 한 웨이브의 스폰 명세.
    /// 각 SpawnEntry 는 startOffset(웨이브 시작 후 N초) 시점에 시작된다.
    /// 같은 startOffset 값의 entry 들은 병렬로 진행되며, pathId 로 어느 경로로 갈지 지정한다.
    ///
    /// 예시:
    ///   - SpawnEntry { pathId:"L",  startOffset:0,  enemy:Goblin, count:8, interval:0.8 }
    ///   - SpawnEntry { pathId:"R",  startOffset:0,  enemy:Goblin, count:8, interval:0.8 }   // 좌우 동시 발진
    ///   - SpawnEntry { pathId:"L",  startOffset:8,  enemy:Orc,    count:3, interval:1.5 }   // 8초 뒤 좌측 오크 추가
    /// </summary>
    [CreateAssetMenu(fileName = "Wave_New", menuName = "KRTD/Wave Data")]
    public class WaveData : ScriptableObject
    {
        [Header("표시 정보")]
        public string waveName = "Wave";

        [Header("스폰 항목")]
        [Tooltip("entry 들은 startOffset 시점에 병렬로 시작된다.")]
        public List<SpawnEntry> entries = new List<SpawnEntry>();

        [Header("타이밍")]
        [Tooltip("이 웨이브가 시작되기까지의 추가 대기 시간 (초). 0 이면 즉시 시작.")]
        public float startDelay = 0f;

        [Serializable]
        public class SpawnEntry
        {
            [Tooltip("이 묶음이 사용할 경로의 ID. WaveDirector 에 등록된 EnemySpawner.PathId 와 매칭된다. 빈 값이면 첫 등록 스포너로 fallback.")]
            public string pathId = "";

            [Tooltip("웨이브 시작 후 이 묶음이 시작되기까지의 대기 시간 (초). 같은 값을 가진 entry 들은 병렬로 진행된다.")]
            [Min(0f)]
            public float startOffset = 0f;

            public EnemyData enemy;

            [Min(1)]
            public int count = 1;

            [Tooltip("이 묶음 내에서 적 사이의 스폰 간격 (초)")]
            [Min(0f)]
            public float interval = 1f;
        }
    }
}
