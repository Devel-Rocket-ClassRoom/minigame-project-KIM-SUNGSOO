using KRTD.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 타이틀(MainMenu) 씬의 플로우 코디네이터. 3단계 패널을 SetActive 로 전환한다.
    ///
    ///   ① LoginPanel    — 로그인 폼(LoginView) + [게임 종료]
    ///   ② WelcomePanel  — "○○님 환영합니다" + [게임 시작](→ 메인 메뉴) + [로그아웃]
    ///   ③ MainMenuPanel — 기존 메인 메뉴(손으로 만든 것). [게임 시작](→게임씬) / [설정] / [로그아웃]
    ///
    /// 흐름:
    ///   씬 시작 → ① → (로그인/회원가입 또는 세션 자동 복원) → ②
    ///          → ②의 [게임 시작] → ③ → ③의 [게임 시작] → 게임씬 로드.
    ///   로그아웃(② 또는 ③) → AuthManager.OnSignedOut → 다시 ①.
    ///
    /// 주의:
    ///   - ③ MainMenuPanel 은 기존 패널을 그대로 쓴다. mainMenuPanel/startButton/settingsButton/
    ///     logoutButton/settingsPanel 필드는 인스펙터에서 직접 연결해야 한다(셋업 툴은 ①②만 생성).
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [Header("씬")]
        [Tooltip("메인 메뉴 [게임 시작] 이 로드할 본 게임 씬 이름. Build Settings 에 등록되어 있어야 함.")]
        [SerializeField] private string gameSceneName = "TileMapeScene";

        [Header("① 로그인")]
        [SerializeField] private GameObject loginPanel;
        [Tooltip("loginPanel 안의 LoginView. 로그인 완료 이벤트를 구독한다.")]
        [SerializeField] private LoginView loginView;
        [Tooltip("로그인 패널의 [게임 종료] 버튼. 앱을 종료한다.")]
        [SerializeField] private Button quitButton;

        [Header("② 환영")]
        [SerializeField] private GameObject welcomePanel;
        [Tooltip("로그인된 계정의 닉네임을 보여줄 환영 문구.")]
        [SerializeField] private TMP_Text welcomeText;
        [Tooltip("환영 화면 [게임 시작] — 메인 메뉴로 진입.")]
        [SerializeField] private Button proceedButton;
        [Tooltip("환영 화면 [로그아웃].")]
        [SerializeField] private Button welcomeLogoutButton;

        [Header("③ 메인 메뉴 (기존 패널 — 직접 연결)")]
        [SerializeField] private GameObject mainMenuPanel;
        [Tooltip("메인 메뉴 [게임 시작] — 게임씬 로드.")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [Tooltip("메인 메뉴 [로그아웃].")]
        [SerializeField] private Button logoutButton;
        [Tooltip("[설정] 버튼이 여는 패널 루트. 시작 시 비활성.")]
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

            // 항상 로그인부터. 세션이 살아있으면 LoginView 가 곧 OnLoginComplete 를 쏴 환영 화면으로 전환.
            ShowLogin();
        }

        private void OnDestroy()
        {
            if (loginView != null) loginView.OnLoginComplete -= HandleLoginComplete;
            if (AuthManager.Instance != null) AuthManager.Instance.OnSignedOut -= HandleSignedOut;
        }

        // --- 패널 전환 ---------------------------------------------------------

        private void ShowLogin()
        {
            SetPanels(login: true, welcome: false, menu: false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void ShowWelcome()
        {
            SetPanels(login: false, welcome: true, menu: false);
        }

        // proceedButton(②의 "게임 시작")에서 호출 — 기존 메인 메뉴로 진입.
        private void ShowMainMenu()
        {
            SetPanels(login: false, welcome: false, menu: true);
        }

        private void SetPanels(bool login, bool welcome, bool menu)
        {
            if (loginPanel != null) loginPanel.SetActive(login);
            if (welcomePanel != null) welcomePanel.SetActive(welcome);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(menu);
        }

        // --- 이벤트/콜백 -------------------------------------------------------

        private void HandleLoginComplete(PlayerData data)
        {
            ShowWelcome();
            if (welcomeText != null)
                welcomeText.text = data != null ? $"{data.nickname} 님 환영합니다!" : "환영합니다!";
        }

        private void HandleSignedOut()
        {
            ShowLogin();
        }

        private void HandleStart()
        {
            // ③의 [게임 시작] — 다음 씬에서 정상 속도로 시작.
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void HandleLogout()
        {
            // AuthManager.OnSignedOut → HandleSignedOut → 로그인 패널 복귀.
            AuthManager.Instance?.SignOut();
        }

        private void HandleQuit()
        {
            // ①의 [게임 종료] — 진짜 앱 종료.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
