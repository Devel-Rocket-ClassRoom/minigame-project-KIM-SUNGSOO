using System;
using UnityEngine;
using UnityEngine.UI;
using KRTD.Game;
using KRTD.Cloud;

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
    ///   │   ├─ Stars (Star0, Star1, Star2 — Image 3개를 starIcons 에 좌→우 순서로 연결)
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

        [Header("Win 패널 성급(별점)")]
        [Tooltip("WinPanel 안에 배치한 별 Image 3개. 좌→우 순서. 길이가 3 이 아니면 성급 표시를 건너뛴다.")]
        [SerializeField] private Image[] starIcons;
        [Tooltip("켜진(획득) 별 스프라이트. 비워두면 색만 바꿔서 표현.")]
        [SerializeField] private Sprite starOnSprite;
        [Tooltip("꺼진(미획득) 별 스프라이트. 비워두면 색만 바꿔서 표현.")]
        [SerializeField] private Sprite starOffSprite;
        [Tooltip("스프라이트가 비어있을 때 사용할 켜진 별 색.")]
        [SerializeField] private Color starOnColor = Color.white;
        [Tooltip("스프라이트가 비어있을 때 사용할 꺼진 별 색.")]
        [SerializeField] private Color starOffColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [Tooltip("★★★ 임계값. 남은 라이프가 이 값 이상이면 별 3개. 기본 18 (시작 라이프 20 기준 거의 무피해).")]
        [SerializeField] private int threeStarMinLife = 18;
        [Tooltip("★★☆ 임계값. 남은 라이프가 이 값 이상이면 별 2개. 그 미만(1 이상)은 별 1개.")]
        [SerializeField] private int twoStarMinLife = 7;

        [Header("동작 정책")]
        [Tooltip("승/패 시 Time.timeScale 을 0 으로 만들어 씬 전체를 멈춘다. 끄면 UI 만 뜨고 씬은 계속 진행.")]
        [SerializeField] private bool freezeTimeOnEnd = true;

        [Header("클라우드 저장")]
        [Tooltip("이 씬이 나타내는 스테이지 번호. 클리어 시 이 ID 로 별점을 클라우드에 기록.")]
        [SerializeField] private int stageId = 1;
        [Tooltip("켜면 승리 시 로그인된 계정에 스테이지 별점을 자동 저장. 로그인 안 되어 있으면 조용히 건너뜀.")]
        [SerializeField] private bool saveResultToCloud = true;

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
            int stars = ApplyStarRating(state);
            if (winPanel != null) winPanel.SetActive(true);
            FreezeIfNeeded();

            if (saveResultToCloud) RecordOutcomeToCloud(won: true, stars: stars);
        }

        // 이번 판 결과를 로그인 계정에 기록 (미로그인 시 건너뜀).
        private void RecordOutcomeToCloud(bool won, int stars)
        {
            var svc = PlayerDataService.Instance;
            if (svc == null || !svc.CanUse) return;

            svc.Load(data =>
            {
                // 로드 실패 시 저장 스킵 — 빈 데이터로 덮어써 닉네임/기록이 날아가는 것 방지.
                if (data == null)
                {
                    Debug.LogWarning("[GameOutcomeView] 데이터 로드 실패 — 결과 저장 건너뜀.");
                    return;
                }
                data.RecordStageOutcome(stageId, won, stars);
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                svc.Save(data, now, ok =>
                {
                    if (!ok) Debug.LogWarning("[GameOutcomeView] 스테이지 결과 클라우드 저장 실패.");
                });
            });
        }

        /// <summary>
        /// 남은 라이프 절대값으로 별 0~3개를 결정해 starIcons 에 반영하고, 결정된 별 개수를 반환.
        /// 기준: Life ≥ threeStarMinLife → 3, ≥ twoStarMinLife → 2, ≥ 1 → 1, 그 외 → 0.
        /// starIcons 가 3 개가 아니어도 별 개수 계산·반환은 정상 수행(아이콘 반영만 건너뜀).
        /// </summary>
        private int ApplyStarRating(GameState gs)
        {
            if (gs == null) return 0;

            int life = gs.Life;
            int stars;
            if (life >= threeStarMinLife) stars = 3;
            else if (life >= twoStarMinLife) stars = 2;
            else if (life >= 1) stars = 1;
            else stars = 0;

            if (starIcons == null || starIcons.Length != 3) return stars;

            for (int i = 0; i < starIcons.Length; i++)
            {
                var icon = starIcons[i];
                if (icon == null) continue;
                bool on = i < stars;
                if (starOnSprite != null && starOffSprite != null)
                {
                    icon.sprite = on ? starOnSprite : starOffSprite;
                    icon.color = Color.white;
                }
                else
                {
                    icon.color = on ? starOnColor : starOffColor;
                }
            }
            return stars;
        }

        private void HandleLose()
        {
            if (losePanel != null) losePanel.SetActive(true);
            FreezeIfNeeded();

            // 패배도 도전 횟수에 포함 (클리어/별점은 갱신 안 함).
            if (saveResultToCloud) RecordOutcomeToCloud(won: false, stars: 0);
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
            // "게임 끝내기" 는 앱 종료가 아니라 MainMenu 복귀.
            // 실제 앱 종료는 MainMenu 의 종료 버튼만 담당.
            var pause = PauseController.Instance;
            if (pause != null) { pause.ReturnToMainMenu(); return; }
            // 폴백 — PauseController 가 없으면 직접 씬 로드.
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
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
