using System;
using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>
    /// Per-booster owned count, persisted in PlayerPrefs keyed by booster id.
    /// </summary>
    public static class PlayerBoosters
    {
        private const string Prefix = "PixelShoot.Booster.";

        /// <summary>Fired after a booster count changes. Payload = (id, new count).</summary>
        public static event Action<string, int> OnChanged;

        public static int Count(string id)
            => string.IsNullOrEmpty(id) ? 0 : PlayerPrefs.GetInt(Prefix + id, 0);

        public static void Add(string id, int n = 1)
        {
            if (string.IsNullOrEmpty(id) || n == 0) return;
            SetCount(id, Count(id) + n);
        }

        /// <summary>Spend one (or n) if available. Returns false and changes nothing otherwise.</summary>
        public static bool TryConsume(string id, int n = 1)
        {
            if (string.IsNullOrEmpty(id)) return false;
            int c = Count(id);
            if (c < n) return false;
            SetCount(id, c - n);
            LevelAnalytics.RecordBooster(); // no-op unless a level is active; counts every booster path
            return true;
        }

        // ── One-time default grant ───────────────────────────────────────────
        private const string GrantedPrefix = "PixelShoot.BoosterGranted.";
        /// <summary>Grant `amount` free boosters once per device for this id.</summary>
        public static void GrantDefaultOnce(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return;
            if (PlayerPrefs.GetInt(GrantedPrefix + id, 0) == 1) return;
            PlayerPrefs.SetInt(GrantedPrefix + id, 1);
            Add(id, amount); // Add() saves
        }

        // ── Tutorial-seen flag ───────────────────────────────────────────────
        private const string TutorialPrefix = "PixelShoot.BoosterTutorial.";
        public static bool IsTutorialShown(string id)
            => !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(TutorialPrefix + id, 0) == 1;
        public static void MarkTutorialShown(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            PlayerPrefs.SetInt(TutorialPrefix + id, 1);
            PlayerPrefs.Save();
        }

        private static void SetCount(string id, int value)
        {
            value = Mathf.Max(0, value);
            PlayerPrefs.SetInt(Prefix + id, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke(id, value);
        }
    }
}
