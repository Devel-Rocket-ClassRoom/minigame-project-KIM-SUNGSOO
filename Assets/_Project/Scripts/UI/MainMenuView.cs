using KRTD.Audio;
using KRTD.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 타이틀 씬 플로우 코디네이터. 3단계 패널을 SetActive 로 전환한다.
    ///   ① 로그인 → ② 환영([게임 시작]→메뉴) → ③ 메인메뉴([게임 시작]→게임씬/설정/로그아웃)
    /// 로그아웃 시 ①로 복귀. ③ 메인메뉴 패널/버튼은 인스펙터에서 직접 연결(셋업 툴은 ①②만 생성).
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [Header("씬")]
        [SerializeField] private string gameSceneName = "TileMapeScene";

        [Header("① 로그인")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private LoginView loginView;
        [SerializeField] private Button quitButton;

        [Header("② 환영")]
        [SerializeField] private GameObject welcomePanel;
        [SerializeField] private TMP_Text welcomeText;
        [SerializeField] private Button proceedButton;
        [SerializeField] private Button welcomeLogoutButton;

        [Header("③ 메인 메뉴 (기존 패널 — 직접 연결)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
            if (proceedButton != null) proceedButton.onClick.AddListener(ShowMainMenu);
            if (welcomeLogoutButton != null) welcomeLogoutButton.onClick.AddListener(HandleLogout);
            if (startButton != null) startButton.onClick.AddListener(HandleStart);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (logoutButton != null) logoutButton.onClick.AddListener(HandleLogout);

            if (loginView != null) loginView.OnLoginComplete += HandleLoginComplete;
            if (AuthManager.Instance != null) AuthManager.Instance.OnSignedOut += HandleSignedOut;

            ShowLogin();
        }

        private void OnDestroy()
        {
            if (loginView != null) loginView.OnLoginComplete -= HandleLoginComplete;
            if (AuthManager.Instance != null) AuthManager.Instance.OnSignedOut -= HandleSignedOut;
        }

        private void ShowLogin()
        {
            SetPanels(login: true, welcome: false, menu: false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void ShowWelcome() => SetPanels(login: false, welcome: true, menu: false);
        private void ShowMainMenu() => SetPanels(login: false, welcome: false, menu: true);

        private void SetPanels(bool login, bool welcome, bool menu)
        {
            if (loginPanel != null) loginPanel.SetActive(login);
            if (welcomePanel != null) welcomePanel.SetActive(welcome);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(menu);
        }

        private void HandleLoginComplete(PlayerData data)
        {
            ShowWelcome();
            if (welcomeText != null)
                welcomeText.text = data != null ? $"{data.nickname} 님 환영합니다!" : "환영합니다!";

            // 계정에 저장된 볼륨을 로컬에 적용 (기기 간 동기화).
            if (data != null) AudioManager.Instance?.ApplyVolumes(data.bgmVolume, data.sfxVolume);
        }

        private void HandleSignedOut() => ShowLogin();

        private void HandleStart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void HandleLogout() => AuthManager.Instance?.SignOut();

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
