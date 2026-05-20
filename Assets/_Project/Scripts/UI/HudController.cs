using TMPro;
using UnityEngine;
using KRTD.Game;

namespace KRTD.UI
{
    /// <summary>
    /// 화면 상단 HUD. GameState 의 변화 이벤트에 구독해 생명/재화/웨이브를 표시한다.
    ///
    /// 책임:
    ///   - GameState.Instance 의 이벤트 구독/해제
    ///   - 세 TMP_Text 필드 갱신
    ///
    /// 정책:
    ///   - Start 시점에 현재 값을 즉시 한 번 동기화한다 (구독 전에 발사된 초기 브로드캐스트를 놓쳐도 안전).
    ///   - 필드가 비어 있어도 NullRef 없이 무시한다 (부분 HUD 구성 가능).
    ///
    /// 구조 권장:
    ///   Canvas (Screen Space - Overlay)
    ///   └─ TopHUD (HorizontalLayoutGroup)
    ///       ├─ LifeWidget  (Icon + TMP_Text → lifeText)
    ///       ├─ WaveWidget  (Icon + TMP_Text → waveText)
    ///       └─ GoldWidget  (Icon + TMP_Text → goldText)
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("표시 텍스트")]
        [SerializeField] private TMP_Text lifeText;
        [SerializeField] private TMP_Text goldText;
        [Tooltip("웨이브 표시 텍스트. 포맷은 waveFormat 에 정의.")]
        [SerializeField] private TMP_Text waveText;

        [Header("포맷")]
        [Tooltip("웨이브 텍스트 포맷. {0}=현재, {1}=총합")]
        [SerializeField] private string waveFormat = "{0} / {1}";

        private GameState state;

        private void Start()
        {
            state = GameState.Instance;
            if (state == null)
            {
                Debug.LogWarning("[HudController] 씬에 GameState 가 없다. HUD 가 갱신되지 않는다.");
                return;
            }

            state.OnLifeChanged += HandleLifeChanged;
            state.OnGoldChanged += HandleGoldChanged;
            state.OnWaveChanged += HandleWaveChanged;

            // 초기 동기화: GameState.Start 의 첫 브로드캐스트가 우리보다 앞서 발생했어도
            // 현재 값을 직접 읽어 한 번 갱신해 둔다.
            HandleLifeChanged(state.Life);
            HandleGoldChanged(state.Gold);
            HandleWaveChanged(state.CurrentWave, state.TotalWave);
        }

        private void OnDestroy()
        {
            if (state == null) return;
            state.OnLifeChanged -= HandleLifeChanged;
            state.OnGoldChanged -= HandleGoldChanged;
            state.OnWaveChanged -= HandleWaveChanged;
        }

        private void HandleLifeChanged(int life)
        {
            if (lifeText != null) lifeText.text = life.ToString();
        }

        private void HandleGoldChanged(int gold)
        {
            if (goldText != null) goldText.text = gold.ToString();
        }

        private void HandleWaveChanged(int current, int total)
        {
            if (waveText != null) waveText.text = string.Format(waveFormat, current, total);
        }
    }
}
