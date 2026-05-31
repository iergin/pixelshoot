using System;
using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Persistent coin wallet backed by PlayerPrefs. Raises OnBalanceChanged
    /// every time the balance moves so UI labels can refresh without polling.
    /// </summary>
    public static class PlayerWallet
    {
        private const string BalanceKey = "PixelShoot.Coins";

        /// <summary>Fired AFTER the new balance is persisted. Payload = new balance.</summary>
        public static event Action<int> OnBalanceChanged;

        public static int Balance => PlayerPrefs.GetInt(BalanceKey, 0);

        /// <summary>True the very first time the wallet is touched on this device.</summary>
        public static bool IsUnset => !PlayerPrefs.HasKey(BalanceKey);

        /// <summary>
        /// Seeds the wallet with the configured initial balance on first run.
        /// Idempotent — does nothing if a balance is already saved.
        /// </summary>
        public static void EnsureInitialized(int initial)
        {
            if (!IsUnset) return;
            SetBalance(Mathf.Max(0, initial));
        }

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            SetBalance(Balance + amount);
        }

        /// <summary>True iff the player can afford `amount`.</summary>
        public static bool CanAfford(int amount) => Balance >= Mathf.Max(0, amount);

        /// <summary>
        /// Attempt to spend `amount`. Returns true on success and deducts; returns
        /// false on insufficient funds and leaves the balance untouched.
        /// </summary>
        public static bool TrySpend(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (Balance < amount) return false;
            SetBalance(Balance - amount);
            return true;
        }

        public static void Reset(int initial = 0) => SetBalance(Mathf.Max(0, initial));

        private static void SetBalance(int value)
        {
            PlayerPrefs.SetInt(BalanceKey, value);
            PlayerPrefs.Save();
            OnBalanceChanged?.Invoke(value);
        }
    }
}
