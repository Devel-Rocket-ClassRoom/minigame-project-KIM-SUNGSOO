using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KRTD.Game
{
    /// <summary>
    /// 게임의 일시정지 / 재개 / 재시작 / 종료를 총괄.
    /// TimeScale 기반이라 Animator/Coroutine/Update 까지 일괄 정지된다.
    ///
    /// 책임:
    ///   - IsPaused 상태 보관 + 변경 이벤트
    ///   - Pause/Resume/Toggle
    ///   - RestartScene (현재 씬 리로드)
    ///   - Quit (빌드: Application.Quit / 에디터: Play 모드 정지)
    ///
    /// 정책:
    ///   - 씬 전환/종료 시 항상 TimeScale 을 1 로 되돌린다 (다음 씬이 멈춘 채 시작하지 않게).
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        public static PauseController Instance { get; private set; }

        public bool IsPaused { get; private set; }
        public event Action<bool> OnPauseStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // 안전망: 컨트롤러가 사라져도 다음 씬은 정상 속도로
                Time.timeScale = 1f;
                Instance = null;
            }
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            OnPauseStateChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            OnPauseStateChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        /// <summary>현재 씬을 다시 로드. (씬은 Build Settings 에 등록돼 있어야 함.)</summary>
        public void RestartScene()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

        /// <summary>빌드에선 어플리케이션 종료, 에디터에선 Play 모드 정지.</summary>
        public void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
