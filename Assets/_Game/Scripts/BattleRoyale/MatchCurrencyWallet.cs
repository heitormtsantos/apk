using System;
using UnityEngine;

namespace BattleRoyale
{
    public sealed class MatchCurrencyWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int currentCurrency;

        public event Action<int> OnCurrencyChanged;
        public int CurrentCurrency => currentCurrency;

        public bool CanAfford(int amount) => amount >= 0 && currentCurrency >= amount;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || !CanAfford(amount)) return false;
            SetCurrency(currentCurrency - amount);
            return true;
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0) return;
            SetCurrency((int)Math.Min(int.MaxValue, (long)currentCurrency + amount));
        }

        public void SetCurrency(int amount)
        {
            var next = Mathf.Max(0, amount);
            if (next == currentCurrency) return;
            currentCurrency = next;
            OnCurrencyChanged?.Invoke(currentCurrency);
        }

        public void ResetCurrency() => SetCurrency(0);
    }
}
