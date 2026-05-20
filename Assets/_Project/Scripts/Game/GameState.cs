using System;
using UnityEngine;

namespace KRTD.Game
{
    /// <summary>
    /// 게임 전역 상태(생명/재화/웨이브)를 보관하고 변화 이벤트를 발사한다.
    /// 씬에 하나만 존재하며 다른 시스템(Spawner, Enemy, UI)이 Instance 로 접근한다.
    ///
    /// 책임:
    ///   - 데이터 보관 (Life, Gold, CurrentWave, TotalWave)
    ///   - 변경 API 제공 (LoseLife / AddGold / SpendGold / SetWave)
    ///   - 변경 이벤트 브로드캐스트 (UI 가 구독)
    ///
    /// 정책:
    ///   - 생명이 0 이하가 되면 OnGameOver 를 한 번만 발사.
    ///   - 골드/생명은 음수가 되지 않도록 클램프.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("시작 값")]
        [SerializeField] private int startingLife = 20;
        [SerializeField] private int startingGold = 100;
        [Tooltip("이번 스테이지의 총 웨이브 수. Spawner 가 자동으로 세팅할 수도 있다.")]
        [SerializeField] private int totalWave = 1;

        public int Life { get; private set; }
        public int Gold { get; private set; }
        public int CurrentWave { get; private set; }
        public int TotalWave => totalWave;
        public bool IsGameOver { get; private set; }

        public event Action<int> OnLifeChanged;
        public event Action<int> OnGoldChanged;
        /// <summary>(current, total) 순서로 호출.</summary>
        public event Action<int, int> OnWaveChanged;
        public event Action OnGameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Life = startingLife;
            Gold = startingGold;
            CurrentWave = 0;
        }

        private void Start()
        {
            // 시작 값 UI 동기화용 첫 브로드캐스트.
            OnLifeChanged?.Invoke(Life);
            OnGoldChanged?.Invoke(Gold);
            OnWaveChanged?.Invoke(CurrentWave, totalWave);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>적이 골인했을 때 호출.</summary>
        public void LoseLife(int amount = 1)
        {
            if (IsGameOver || amount <= 0) return;

            Life = Mathf.Max(0, Life - amount);
            OnLifeChanged?.Invoke(Life);

            if (Life == 0)
            {
                IsGameOver = true;
                OnGameOver?.Invoke();
            }
        }

        /// <summary>적 처치 보상 등 골드 획득.</summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            OnGoldChanged?.Invoke(Gold);
        }

        /// <summary>건설/업그레이드 비용 차감. 부족하면 false 반환하고 상태는 변경하지 않는다.</summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Gold < amount) return false;
            Gold -= amount;
            OnGoldChanged?.Invoke(Gold);
            return true;
        }

        /// <summary>Spawner 가 웨이브 진행 시 호출.</summary>
        public void SetWave(int current, int total)
        {
            CurrentWave = current;
            totalWave = total;
            OnWaveChanged?.Invoke(CurrentWave, totalWave);
        }

        /// <summary>현재 웨이브만 갱신. 총 웨이브 수는 그대로.</summary>
        public void SetCurrentWave(int current)
        {
            CurrentWave = current;
            OnWaveChanged?.Invoke(CurrentWave, totalWave);
        }
    }
}
