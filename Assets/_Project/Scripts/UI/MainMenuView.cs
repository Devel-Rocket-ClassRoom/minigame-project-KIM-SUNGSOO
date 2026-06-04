using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KRTD.UI
{
    /// <summary>
    /// 타이틀(MainMenu) 씬의 루트 컨트롤러.
    /// Start / 설정 / 종료 세 버튼을 와이어링하고, 설정 패널 토글을 책임진다.
    ///
    /// 구조 권장:
    ///   MainMenu Canvas
    ///   └─ MainMenuRoot (이 컴포넌트)
    ///       ├─ TitleText  "Kingdom Rush TD"  (혹은 로고 Image)
    ///       ├─ StartButton    "시작"
    ///       ├─ SettingsButton "설정"
    ///       ├─ QuitButton     "종료"
    ///       └─ SettingsPanel (SettingsPanelView, 시작 시 비활성)
    /// </summary>
    public class MainMenuView : MonoBehaviour
    {
        [Header("씬")]
        [Tooltip("Start 버튼이 로드할 본 게임 씬 이름. Build Settings 에 등록되어 있어야 함.")]
        [SerializeField] private string gameSceneName = "TileMapeScene";

        [Header("버튼")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("패널")]
        [Tooltip("설정 버튼 클릭 시 활성화되는 패널 루트. 시작 시 비활성.")]
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);

            if (startButton != null) startButton.onClick.AddListener(HandleStart);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        }

        private void HandleStart()
        {
            // 다음 씬에서 정상 속도로 시작.
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName);
        }

        private void HandleSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void HandleQuit()
        {
            // MainMenu 의 종료 버튼만 "진짜" 앱 종료를 담당한다.
            // (일시정지 메뉴의 '나가기' 와 게임 종료 화면의 '게임 끝내기' 는 MainMenu 복귀로 동작.)
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
