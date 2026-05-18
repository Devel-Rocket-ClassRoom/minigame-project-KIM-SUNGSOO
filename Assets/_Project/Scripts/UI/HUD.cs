using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KRTD.Core;
using KRTD.Economy;
using KRTD.Waves;

namespace KRTD.UI
{
    /// <summary>
    /// 상단/하단 HUD. 골드/라이프/웨이브 표시 + Start Wave 버튼.
    /// EventBus만 구독하므로 UI는 매니저와 강결합되지 않음.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text livesText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private Button startWaveButton;
        [SerializeField] private WaveManager waveManager;

        private void OnEnable()
        {
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Subscribe<WaveClearedEvent>(OnWaveCleared);
            startWaveButton.onClick.AddListener(StartWave);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
            startWaveButton.onClick.RemoveListener(StartWave);
        }

        private void StartWave() { waveManager.StartNextWave(); }

        private void OnGoldChanged(GoldChangedEvent e)   { if (goldText)  goldText.text  = $"Gold: {e.Amount}"; }
        private void OnLivesChanged(LivesChangedEvent e) { if (livesText) livesText.text = $"Lives: {e.Amount}"; }
        private void OnWaveCleared(WaveClearedEvent e)   { if (waveText)  waveText.text  = $"Wave: {e.WaveIndex + 1}/{waveManager.TotalWaves}"; }
    }
}
