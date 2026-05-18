using UnityEngine;
using KRTD.Core;
using KRTD.Enemies;

namespace KRTD.Economy
{
    public class LivesManager : MonoBehaviour
    {
        public static LivesManager Instance { get; private set; }

        [SerializeField] private int currentLives;
        public int CurrentLives => currentLives;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()  { EventBus.Subscribe<EnemyReachedGoalEvent>(OnEnemyReachedGoal); }
        private void OnDisable() { EventBus.Unsubscribe<EnemyReachedGoalEvent>(OnEnemyReachedGoal); }

        public void SetInitial(int lives) { currentLives = lives; EventBus.Raise(new LivesChangedEvent(currentLives)); }

        private void OnEnemyReachedGoal(EnemyReachedGoalEvent e)
        {
            currentLives -= e.LivesPenalty;
            EventBus.Raise(new LivesChangedEvent(currentLives));
            if (currentLives <= 0) GameManager.Instance.Defeat();
        }
    }

    public readonly struct LivesChangedEvent
    {
        public readonly int Amount;
        public LivesChangedEvent(int a) { Amount = a; }
    }
}
