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
            return true;
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
