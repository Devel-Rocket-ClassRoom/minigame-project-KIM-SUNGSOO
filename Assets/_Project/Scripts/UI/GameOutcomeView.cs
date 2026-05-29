using UnityEngine;
using UnityEngine.UI;
using KRTD.Game;

namespace KRTD.UI
{
    /// <summary>
    /// 게임 종료(승리/패배) 모달 UI.
    /// GameState.OnGameWon → WinPanel 활성, OnGameOver → LosePanel 활성.
    /// 활성화 즉시 Time.timeScale=0 으로 씬 정지 (PauseController 와 동일한 방식).
    ///
    /// 인스펙터 구조 권장:
    ///   GameOutcomeRoot (이 컴포넌트, 시작 시 두 패널 모두 비활성)
    ///   ├─ WinPanel
    ///   │   ├─ Title "Stage Clear"
    ///   │   ├─ NextStageButton  "다음 스테이지"     (TODO 동작)
    ///   │   ├─ StageSelectButton "스테이지 선택"   (TODO 동작)
    ///   │   └─ QuitButton        "게임 끝내기"
    ///   └─ LosePanel
    ///       ├─ Title "Game Over"
    ///       ├─ RestartButton      "재시작"
    ///       ├─ StageSelectButton2 "스테이지 선택"   (TODO 동작)
    ///       └─ QuitButton2        "게임 끝내기"
    /// </summary>
    public class GameOutcomeView : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        [Header("Win 패널 버튼")]
        [SerializeField] private Button winNextStageButton;
        [SerializeField] private Button winStageSelectButton;
        [SerializeField] private Button winQuitButton;

        [Header("Lose 패널 버튼")]
        [SerializeField] private Button loseRestartButton;
        [SerializeField] private Button loseStageSelectButton;
        [SerializeField] private Button loseQuitButton;

        [Header("동작 정책")]
        [Tooltip("승/패 시 Time.timeScale 을 0 으로 만들어 씬 전체를 멈춘다. 끄면 UI 만 뜨고 씬은 계속 진행.")]
        [SerializeField] private bool freezeTimeOnEnd = true;

        private GameState state;

        private void Awake()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);

            // Win 버튼 와이어링
            if (winNextStageButton != null) winNextStageButton.onClick.AddListener(HandleNextStage);
            if (winStageSelectButton != null) winStageSelectButton.onClick.AddListener(HandleStageSelect);
            if (winQuitButton != null) winQuitButton.onClick.AddListener(HandleQuit);

            // Lose 버튼 와이어링
            if (loseRestartButton != null) loseRestartButton.onClick.AddListener(HandleRestart);
            if (loseStageSelectButton != null) loseStageSelectButton.onClick.AddListener(HandleStageSelect);
            if (loseQuitButton != null) loseQuitButton.onClick.AddListener(HandleQuit);
        }

        private void OnEnable()
        {
            state = GameState.Instance;
            if (state != null)
            {
                state.OnGameWon += HandleWin;
                state.OnGameOver += HandleLose;
            }
        }

        private void Start()
        {
            // OnEnable 시점에 GameState 가 아직 없었던 경우(스크립트 실행 순서) 한 번 더 시도.
            if (state == null)
            {
                state = GameState.Instance;
                if (state != null)
                {
                    state.OnGameWon += HandleWin;
                    state.OnGameOver += HandleLose;
                }
                else
                {
                    Debug.LogError("[GameOutcomeView] Start 에서도 GameState.Instance 가 null — 씬에 GameState 가 없음.");
                }
            }
        }

        private void OnDisable()
        {
            if (state != null)
            {
                state.OnGameWon -= HandleWin;
                state.OnGameOver -= HandleLose;
            }
        }

        private void HandleWin()
        {
            if (winPanel != null) winPanel.SetActive(true);
            FreezeIfNeeded();
        }

        private void HandleLose()
        {
            if (losePanel != null) losePanel.SetActive(true);
            FreezeIfNeeded();
        }

        private void FreezeIfNeeded()
        {
            if (freezeTimeOnEnd) Time.timeScale = 0f;
        }

        // --- 버튼 콜백 ---------------------------------------------------------

        private void HandleRestart()
        {
            var pause = PauseController.Instance;
            if (pause != null) { pause.RestartScene(); return; }
            // 폴백
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void HandleQuit()
        {
            var pause = PauseController.Instance;
            if (pause != null) { pause.Quit(); return; }
            // 폴백
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleNextStage()
        {
            // TODO(스테이지 선택 씬/다음 스테이지 씬이 생기면 SceneManager.LoadScene 으로 연결.
            //       지금은 빌드2 마일스톤 — 단일 스테이지라 일단 placeholder 로그.)
            Debug.Log("[GameOutcomeView] 다음 스테이지 — 미구현 (스테이지 선택 씬 작성 후 연결).");
        }

        private void HandleStageSelect()
        {
            // TODO(스테이지 선택 씬이 생기면 SceneManager.LoadScene("StageSelect") 등으로 연결.)
            Debug.Log("[GameOutcomeView] 스테이지 선택 — 미구현 (스테이지 선택 씬 작성 후 연결).");
        }
    }
}
