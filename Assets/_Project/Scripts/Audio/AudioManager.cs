using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KRTD.Audio
{
    /// <summary>
    /// 씬에 단 하나 존재하는 오디오 매니저. <see cref="DontDestroyOnLoad"/> 로 씬 전환에도 살아남는다.
    ///
    /// 책임:
    ///   - 씬 이름별 BGM 자동 전환 (sceneBgms 매핑)
    ///   - BGM 페이드 인/아웃 크로스 swap (bgmFadeDuration > 0 일 때)
    ///   - UI 버튼 클릭 등 SFX 일회성 재생 (PlayOneShot)
    ///   - PlayerPrefs(bgmVolume / sfxVolume) 자동 로드 + 슬라이더 변경 즉시 반영
    ///
    /// 사용:
    ///   1) MainMenu 씬에 빈 GameObject 만들고 이 컴포넌트 부착.
    ///   2) sceneBgms 에 씬 이름 → BGM 클립 매핑 등록 (예: "MainMenu", "TileMapeScene").
    ///   3) buttonClickSfx 에 클릭 효과음 클립 할당.
    ///   4) 버튼 프리팹에는 <see cref="ButtonClickSfx"/> 컴포넌트 드롭.
    ///
    /// 다른 씬에서 시작해도 첫 씬에 매니저가 없으면 BGM 이 안 나오므로 MainMenu 진입을
    /// 기본으로 잡아두는 게 안전. (또는 모든 씬에 매니저를 두고 중복 인스턴스를 정리하는
    /// 본 Awake 로직에 맡겨도 됨.)
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [System.Serializable]
        public class SceneBgm
        {
            [Tooltip("Scene asset 이름 (확장자 제외). 예: MainMenu, TileMapeScene.")]
            public string sceneName;
            [Tooltip("이 씬에서 재생할 BGM 클립.")]
            public AudioClip clip;
        }

        [Header("씬별 BGM")]
        [Tooltip("씬 이름 → 재생할 BGM 매핑. 매핑이 없으면 BGM 중지(무음).")]
        [SerializeField] private SceneBgm[] sceneBgms;

        [Header("SFX")]
        [Tooltip("UI 버튼 클릭 공용 효과음. ButtonClickSfx 컴포넌트가 이 클립을 재생.")]
        [SerializeField] private AudioClip buttonClickSfx;

        [Header("AudioSource (비우면 자동 생성)")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("BGM 크로스페이드")]
        [Tooltip("BGM 전환 시 fade out → swap → fade in 의 한쪽 페이드 시간(초). " +
            "0 이면 즉시 swap. 0.3~0.8 권장.")]
        [SerializeField] private float bgmFadeDuration = 0.5f;

        // PlayerPrefs 키 — SettingsPanelView 의 키와 일치.
        public const string BgmVolumeKey = "bgmVolume";
        public const string SfxVolumeKey = "sfxVolume";
        private const float DefaultVolume = 0.8f;

        private Coroutine fadeCoroutine;
        private float targetBgmVolume; // 슬라이더가 정한 "현재 볼륨". 페이드 진행 중에도 보존.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatic() { Instance = null; }

        private void Awake()
        {
            // 단일 인스턴스 — 두 번째가 들어오면 자기 자신 제거.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureSources();

            // 저장된 볼륨 로드.
            targetBgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume);
            float sfxVol = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);
            bgmSource.volume = targetBgmVolume;
            sfxSource.volume = sfxVol;

            SceneManager.activeSceneChanged += OnSceneChanged;

            // 매니저가 처음 깬 씬에 대해 BGM 즉시 적용 (페이드 in 만).
            ApplyBgmForScene(SceneManager.GetActiveScene().name, fadeIn: true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.activeSceneChanged -= OnSceneChanged;
                Instance = null;
            }
        }

        private void EnsureSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.spatialBlend = 0f; // 2D
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        private void OnSceneChanged(Scene prev, Scene next)
        {
            ApplyBgmForScene(next.name, fadeIn: false);
        }

        /// <summary>
        /// 지정된 씬 이름에 매핑된 BGM 으로 전환.
        /// 이미 같은 클립이 재생 중이면 아무 일도 안 함.
        /// </summary>
        /// <param name="fadeIn">true 면 fade-out 생략하고 곧장 fade-in (첫 씬 진입용).</param>
        private void ApplyBgmForScene(string sceneName, bool fadeIn)
        {
            AudioClip target = ResolveBgmForScene(sceneName);
            if (bgmSource.clip == target && bgmSource.isPlaying) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            if (bgmFadeDuration > 0f)
            {
                fadeCoroutine = StartCoroutine(FadeSwap(target, skipFadeOut: fadeIn));
            }
            else
            {
                bgmSource.Stop();
                bgmSource.clip = target;
                bgmSource.volume = targetBgmVolume;
                if (target != null) bgmSource.Play();
            }
        }

        private AudioClip ResolveBgmForScene(string sceneName)
        {
            if (sceneBgms == null) return null;
            for (int i = 0; i < sceneBgms.Length; i++)
            {
                if (sceneBgms[i] != null && sceneBgms[i].sceneName == sceneName)
                    return sceneBgms[i].clip;
            }
            return null;
        }

        private IEnumerator FadeSwap(AudioClip target, bool skipFadeOut)
        {
            // 1) Fade out (필요 시).
            if (!skipFadeOut && bgmSource.isPlaying)
            {
                float startVol = bgmSource.volume;
                float t = 0f;
                while (t < bgmFadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, t / bgmFadeDuration);
                    yield return null;
                }
            }

            // 2) Swap.
            bgmSource.Stop();
            bgmSource.clip = target;

            if (target == null)
            {
                bgmSource.volume = targetBgmVolume;
                fadeCoroutine = null;
                yield break;
            }

            // 3) Fade in.
            bgmSource.volume = 0f;
            bgmSource.Play();
            float t2 = 0f;
            while (t2 < bgmFadeDuration)
            {
                t2 += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, targetBgmVolume, t2 / bgmFadeDuration);
                yield return null;
            }
            bgmSource.volume = targetBgmVolume;
            fadeCoroutine = null;
        }

        // --- 공개 API ----------------------------------------------------------

        /// <summary>버튼 클릭 효과음을 일회 재생.</summary>
        public void PlayButtonClick()
        {
            if (buttonClickSfx == null || sfxSource == null) return;
            sfxSource.PlayOneShot(buttonClickSfx);
        }

        /// <summary>임의 SFX 일회 재생 (효과음 추가될 때 유용).</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        /// <summary>BGM 볼륨을 즉시 반영 (페이드 진행 중이면 다음 페이드 목표 볼륨으로 저장).</summary>
        public void SetBgmVolume(float v)
        {
            targetBgmVolume = Mathf.Clamp01(v);
            // 페이드가 안 돌고 있을 때만 즉시 반영 — 페이드 중에는 목표값만 갱신.
            if (fadeCoroutine == null) bgmSource.volume = targetBgmVolume;
        }

        /// <summary>SFX 볼륨을 즉시 반영.</summary>
        public void SetSfxVolume(float v)
        {
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(v);
        }
    }
}
