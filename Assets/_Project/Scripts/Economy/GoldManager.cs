using UnityEngine;
using KRTD.Core;

namespace KRTD.Economy
{
    public class GoldManager : MonoBehaviour
    {
        public static GoldManager Instance { get; private set; }

        [SerializeField] private int currentGold;
        public int CurrentGold => currentGold;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetInitial(int amount) { currentGold = amount; EventBus.Raise(new GoldChangedEvent(currentGold)); }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            currentGold += amount;
            EventBus.Raise(new GoldChangedEvent(currentGold));
        }

        public bool TrySpend(int amount)
        {
            if (amount > currentGold) return false;
            currentGold -= amount;
            EventBus.Raise(new GoldChangedEvent(currentGold));
            return true;
        }
    }

    public readonly struct GoldChangedEvent
    {
        public readonly int Amount;
        public GoldChangedEvent(int a) { Amount = a; }
    }
}
