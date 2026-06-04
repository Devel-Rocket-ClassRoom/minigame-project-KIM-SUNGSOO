using UnityEngine;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 설정 모달 패널. BGM/SFX 볼륨 슬라이더와 닫기 버튼을 다룬다.
    /// 값은 PlayerPrefs 에 즉시 저장 (bgmVolume / sfxVolume, 둘 다 0~1 float).
    ///
    /// 본 이슈(#55) 범위: PlayerPrefs 저장까지만. AudioMixer 적용은 후속 이슈에서.
    /// (저장된 값은 후속 작업에서 AudioMixer.SetFloat("BGM", Linear2dB(v)) 패턴으로 읽으면 됨.)
    /// </summary>
    public class SettingsPanelView : MonoBehaviour
    {
        public const string BgmVolumeKey = "bgmVolume";
        public const string SfxVolumeKey = "sfxVolume";
        private const float DefaultVolume = 0.8f;

        [Header("슬라이더")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("버튼")]
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            // 저장된 값 로드 (없으면 기본값).
            float bgm = PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume);
            float sfx = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);

            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value = bgm;
                bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.value = sfx;
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            }

            if (closeButton != null) closeButton.onClick.AddListener(HandleClose);
        }

        private void HandleBgmChanged(float value)
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, value);
        }

        private void HandleSfxChanged(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
        }

        private void HandleClose()
        {
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}
