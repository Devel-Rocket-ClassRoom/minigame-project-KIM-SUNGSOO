using UnityEngine;
using KRTD.Data;
using KRTD.Economy;
using KRTD.Waves;

namespace KRTD.Map
{
    /// <summary>
    /// 한 스테이지 씬의 초기 세팅(골드/라이프/웨이브 데이터 주입).
    /// 씬마다 LevelData를 다르게 연결하면 새 맵이 됨.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        [SerializeField] private LevelData levelData;
        [SerializeField] private GoldManager goldManager;
        [SerializeField] private LivesManager livesManager;
        [SerializeField] private WaveManager waveManager;

        private void Start()
        {
            if (goldManager != null)  goldManager.SetInitial(levelData.startGold);
            if (livesManager != null) livesManager.SetInitial(levelData.startLives);
            // WaveManager에 LevelData를 inspector에서 직접 연결하거나, 여기서 주입.
        }
    }
}
