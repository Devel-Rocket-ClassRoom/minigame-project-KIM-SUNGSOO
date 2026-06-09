using UnityEngine;
using UnityEngine.UI;
using KRTD.Audio;

namespace KRTD.UI
{
    /// <summary>
    /// 설정 모달 패널. BGM/SFX 볼륨 슬라이더와 닫기 버튼을 다룬다.
    /// 값은 PlayerPrefs 에 즉시 저장 (bgmVolume / sfxVolume, 둘 다 0~1 float)
    /// 되며, 동시에 <see cref="AudioManager"/> 에 즉시 반영되어 재생 중인 BGM/SFX 볼륨이 실시간 변경된다.
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
            var am = AudioManager.Instance;
            if (am != null) am.SetBgmVolume(value);
        }

        private void HandleSfxChanged(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
            var am = AudioManager.Instance;
            if (am != null) am.SetSfxVolume(value);
        }

        private void HandleClose()
        {
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }
    }
}
