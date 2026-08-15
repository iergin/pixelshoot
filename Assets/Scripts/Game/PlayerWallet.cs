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

        /// <summary>Grant coins. <paramref name="source"/> tags the earn for the <c>coin</c> analytics
        /// event (e.g. "level_win", "reward_bundle"). Fires the event with a POSITIVE amount.</summary>
        public static void Add(int amount, string source = "unknown")
        {
            if (amount <= 0) return;
            int start = Balance;
            SetBalance(start + amount);
            PixelShoot.Analytics.AnalyticsEvents.TrackCoin(source, amount, start, start + amount);
        }

        /// <summary>True iff the player can afford `amount`.</summary>
        public static bool CanAfford(int amount) => Balance >= Mathf.Max(0, amount);

        /// <summary>
        /// Attempt to spend `amount`. Returns true on success and deducts; returns
        /// false on insufficient funds and leaves the balance untouched.
        /// </summary>
        public static bool TrySpend(int amount, string source = "unknown")
        {
            amount = Mathf.Max(0, amount);
            if (Balance < amount) return false;
            int start = Balance;
            SetBalance(start - amount);
            if (amount > 0) PixelShoot.Analytics.AnalyticsEvents.TrackCoin(source, -amount, start, start - amount); // spend = negative
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

        // ── Session counter & "first ad seen" tracker (for promo triggers) ───
        private const string SessionCountKey = "PixelShoot.SessionCount";
        private const string FirstAdSeenKey  = "PixelShoot.FirstAdSeen"; // 0/1
        private const string FirstLevelDoneThisSessionKey = "PixelShoot.FirstLevelDoneThisSession"; // 0/1
        private const string NoAdsKey = "PixelShoot.NoAds"; // 0/1

        public static int SessionCount => PlayerPrefs.GetInt(SessionCountKey, 0);

        /// <summary>
        /// Bump the session count by one and reset per-session flags. Call once
        /// at app start (LevelLoader does this for you).
        /// </summary>
        public static void BeginSession()
        {
            PlayerPrefs.SetInt(SessionCountKey, SessionCount + 1);
            PlayerPrefs.SetInt(FirstLevelDoneThisSessionKey, 0);
            PlayerPrefs.Save();
        }

        public static bool FirstLevelDoneThisSession => PlayerPrefs.GetInt(FirstLevelDoneThisSessionKey, 0) == 1;
        public static void MarkFirstLevelDoneThisSession()
        {
            PlayerPrefs.SetInt(FirstLevelDoneThisSessionKey, 1);
            PlayerPrefs.Save();
        }

        public static bool HasSeenFirstAd => PlayerPrefs.GetInt(FirstAdSeenKey, 0) == 1;
        public static void MarkFirstAdSeen()
        {
            if (HasSeenFirstAd) return;
            PlayerPrefs.SetInt(FirstAdSeenKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Fired after No-Ads is bought (the flag flips to owned). UI listens to hide the
        /// "No Ads" button and close the promo popup.</summary>
        public static event Action OnNoAdsChanged;

        public static bool HasNoAds => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;
        public static void MarkNoAdsBought()
        {
            PlayerPrefs.SetInt(NoAdsKey, 1);
            PlayerPrefs.Save();
            OnNoAdsChanged?.Invoke();
        }

        // ── Sound mute (Settings) ────────────────────────────────────────────
        private const string MutedKey = "PixelShoot.Muted";
        public static bool SoundMuted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        // ── Settings toggles (independent, all default ON) ────────────────────
        private const string MusicKey        = "PixelShoot.MusicEnabled";
        private const string SfxKey          = "PixelShoot.SfxEnabled";
        private const string VibrationKey    = "PixelShoot.VibrationEnabled";
        private const string NotificationsKey = "PixelShoot.NotificationsEnabled";

        /// <summary>Fired after the music flag changes. Payload = new enabled state.</summary>
        public static event Action<bool> OnMusicEnabledChanged;
        /// <summary>Fired after the effects (sound) flag changes. Payload = new enabled state.</summary>
        public static event Action<bool> OnSfxEnabledChanged;
        /// <summary>Fired after the vibration/haptics flag changes. Payload = new enabled state.</summary>
        public static event Action<bool> OnVibrationEnabledChanged;
        /// <summary>Fired after the notifications flag changes. Payload = new enabled state.</summary>
        public static event Action<bool> OnNotificationsEnabledChanged;

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnMusicEnabledChanged?.Invoke(value);
            }
        }

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnSfxEnabledChanged?.Invoke(value);
            }
        }

        /// <summary>Vibration / haptics toggle. Persisted; wire real haptics off
        /// <see cref="OnVibrationEnabledChanged"/> when the feature lands.</summary>
        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnVibrationEnabledChanged?.Invoke(value);
            }
        }

        /// <summary>Notifications toggle. Persisted; wire real push-notification opt-in off
        /// <see cref="OnNotificationsEnabledChanged"/> when the feature lands.</summary>
        public static bool NotificationsEnabled
        {
            get => PlayerPrefs.GetInt(NotificationsKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(NotificationsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                OnNotificationsEnabledChanged?.Invoke(value);
            }
        }

        private static void SetBalance(int value)
        {
            PlayerPrefs.SetInt(BalanceKey, value);
            PlayerPrefs.Save();
            OnBalanceChanged?.Invoke(value);
        }
    }
}
