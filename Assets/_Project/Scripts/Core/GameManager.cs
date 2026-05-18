using UnityEngine;

namespace KRTD.Core
{
    /// <summary>
    /// 게임 전체의 흐름(상태/씬/일시정지/승패 판정)을 총괄하는 싱글턴 매니저.
    /// 다른 매니저(Wave/Gold/UI)는 여기서 참조를 가져가지 말고
    /// EventBus를 통해 상호작용하는 것을 권장.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Boot,
            MainMenu,
            Playing,
            Paused,
            Victory,
            Defeat
        }

        [Header("Runtime")]
        [SerializeField] private GameState currentState = GameState.Boot;

        public GameState CurrentState => currentState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ChangeState(GameState.Playing); // 임시: 바로 플레이 시작
        }

        public void ChangeState(GameState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            EventBus.Raise(new GameStateChangedEvent(newState));
        }

        public void Pause() => ChangeState(GameState.Paused);
        public void Resume() => ChangeState(GameState.Playing);
        public void Victory() => ChangeState(GameState.Victory);
        public void Defeat() => ChangeState(GameState.Defeat);
    }

    public readonly struct GameStateChangedEvent
    {
        public readonly GameManager.GameState State;
        public GameStateChangedEvent(GameManager.GameState s) { State = s; }
    }
}
