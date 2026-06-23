using System;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Audio;
using KRTD.Cloud;

namespace KRTD.UI
{
    /// <summary>
    /// 설정 패널. BGM/SFX 슬라이더 → PlayerPrefs + AudioManager 즉시 반영.
    /// 닫을 때 로그인 상태면 볼륨을 계정에도 동기화한다.
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
            AudioManager.Instance?.SetBgmVolume(value);
        }

        private void HandleSfxChanged(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
            AudioManager.Instance?.SetSfxVolume(value);
        }

        private void HandleClose()
        {
            PlayerPrefs.Save();

            // 드래그마다가 아니라 닫을 때 한 번만 계정에 동기화 (부분 업데이트).
            var svc = PlayerDataService.Instance;
            var am = AudioManager.Instance;
            if (svc != null && am != null && svc.CanUse)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                svc.SaveVolumes(am.BgmVolume, am.SfxVolume, now);
            }

            gameObject.SetActive(false);
        }
    }
}
