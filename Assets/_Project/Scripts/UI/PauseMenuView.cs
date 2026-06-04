using UnityEngine;
using UnityEngine.UI;
using KRTD.Game;

namespace KRTD.UI
{
    /// <summary>
    /// 일시정지 UI. 우측 상단 PauseButton + 모달 패널(계속하기 / 재시작 / 나가기).
    /// 버튼 클릭은 PauseController 의 메서드를 호출하고 패널 활성 상태를 토글한다.
    ///
    /// 구조 권장 (HudSetupTool 같은 자동 셋업 도구로 만듦):
    ///   HUD Canvas
    ///   └─ PauseMenuRoot (이 컴포넌트)
    ///       ├─ PauseButton  (우측 상단)
    ///       └─ MenuPanel    (전체 화면 모달, 시작 시 비활성)
    ///           └─ CenterBox
    ///               ├─ Title "일시정지"
    ///               ├─ ResumeButton  "계속하기"
    ///               ├─ RestartButton "재시작"
    ///               └─ QuitButton    "나가기"
    /// </summary>
    public class PauseMenuView : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private Button pauseButton;
        [Tooltip("일시정지 시 활성, 재개 시 비활성으로 토글되는 모달 패널 루트.")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        private void Start()
        {
            if (menuPanel != null) menuPanel.SetActive(false);

            if (pauseButton != null) pauseButton.onClick.AddListener(HandlePause);
            if (resumeButton != null) resumeButton.onClick.AddListener(HandleResume);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        }

        private static PauseController Controller => PauseController.Instance;

        private void HandlePause()
        {
            if (Controller != null) Controller.Pause();
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        private void HandleResume()
        {
            if (Controller != null) Controller.Resume();
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        private void HandleRestart()
        {
            if (Controller != null) Controller.RestartScene();
        }

        private void HandleQuit()
        {
            // 일시정지 메뉴의 "나가기" 는 앱 종료가 아니라 MainMenu 복귀.
            // 실제 앱 종료는 MainMenu 의 종료 버튼이 담당.
            if (Controller != null) Controller.ReturnToMainMenu();
        }
    }
}
