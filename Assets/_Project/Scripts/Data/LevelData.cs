using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Data
{
    /// <summary>
    /// 한 스테이지 정의: 시작 골드/라이프 + 웨이브 리스트 + 건설 가능한 타워 목록.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "KRTD/Level Data", order = 30)]
    public class LevelData : ScriptableObject
    {
        [Header("Stage Info")]
        public string levelId;
        public string displayName;

        [Header("Player Start")]
        public int startGold = 200;
        public int startLives = 20;

        [Header("Waves")]
        public List<WaveData> waves = new();

        [Header("Buildable Towers")]
        [Tooltip("이 맵에서 슬롯에서 선택 가능한 타워들")]
        public List<TowerData> allowedTowers = new();
    }
}
