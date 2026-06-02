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
        private const string PurchasePrefix = "PixelShoot.Purchase.";
        private const string FirstLaunchKey = "PixelShoot.FirstLaunchUtc"; // ISO-8601 UTC string
        private const string AnyPurchaseKey = "PixelShoot.AnyPurchaseMade"; // 0/1

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

        // ── Purchase ledger (for one-time IAP offers) ─────────────────────
        public static bool HasPurchased(string offerId)
            => !string.IsNullOrEmpty(offerId) && PlayerPrefs.GetInt(PurchasePrefix + offerId, 0) == 1;

        public static void MarkPurchased(string offerId)
        {
            if (string.IsNullOrEmpty(offerId)) return;
            PlayerPrefs.SetInt(PurchasePrefix + offerId, 1);
            PlayerPrefs.Save();
        }

        public static void ClearPurchase(string offerId)
        {
            if (string.IsNullOrEmpty(offerId)) return;
            PlayerPrefs.DeleteKey(PurchasePrefix + offerId);
            PlayerPrefs.Save();
        }

        // ── First-launch + global "any purchase" flag (drives starter offers) ─
        public static System.DateTime FirstLaunchUtc
        {
            get
            {
                string s = PlayerPrefs.GetString(FirstLaunchKey, "");
                if (string.IsNullOrEmpty(s)) return System.DateTime.MinValue;
                if (System.DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
                return System.DateTime.MinValue;
            }
        }

        /// <summary>Stamp first-launch on first call; later calls are no-ops.</summary>
        public static void StampFirstLaunchIfMissing()
        {
            if (PlayerPrefs.HasKey(FirstLaunchKey)) return;
            PlayerPrefs.SetString(FirstLaunchKey, System.DateTime.UtcNow.ToString("o"));
            PlayerPrefs.Save();
        }

        public static bool IsWithinFirstDays(int days)
        {
            var first = FirstLaunchUtc;
            if (first == System.DateTime.MinValue) return true; // not stamped yet → treat as fresh
            return (System.DateTime.UtcNow - first).TotalDays < Mathf.Max(0, days);
        }

        public static bool HasMadeAnyPurchase => PlayerPrefs.GetInt(AnyPurchaseKey, 0) == 1;

        public static void MarkAnyPurchaseMade()
        {
            PlayerPrefs.SetInt(AnyPurchaseKey, 1);
            PlayerPrefs.Save();
        }

        private static void SetBalance(int value)
        {
            PlayerPrefs.SetInt(BalanceKey, value);
            PlayerPrefs.Save();
            OnBalanceChanged?.Invoke(value);
        }
    }
}
