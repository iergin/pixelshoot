using System;
using UnityEngine;

namespace PixelShoot.Game
{
    /// <summary>The start-of-level powerups the player can own and spend: the same effects the streak
    /// grants for free (Bomb = start-of-level bombs, Paint = start-of-level free paint).</summary>
    public enum PowerupType { Bomb, Paint }

    /// <summary>
    /// Owned counts of the two start-of-level powerups, persisted in PlayerPrefs (like boosters). The
    /// player picks which ones to use for the NEXT level in the PlayPopup (selection UI added later);
    /// the selected ones are consumed and applied at level start by <see cref="PixelShoot.UI.StreakGiftController"/>
    /// on top of any streak gift. Amount granted per powerup comes from a PowerupsConfig.
    /// </summary>
    public static class PlayerPowerups
    {
        private const string OwnedPrefix    = "PixelShoot.Powerup.";
        private const string SelectedPrefix = "PixelShoot.PowerupSelected.";

        /// <summary>Fired after an owned count changes. Payload = (type, new count).</summary>
        public static event Action<PowerupType, int> OnChanged;
        /// <summary>Fired after a selection toggles. Payload = (type, selected).</summary>
        public static event Action<PowerupType, bool> OnSelectionChanged;

        // ── Owned counts ─────────────────────────────────────────────────────
        public static int Owned(PowerupType t) => PlayerPrefs.GetInt(OwnedPrefix + t, 0);

        public static void Add(PowerupType t, int n)
        {
            if (n == 0) return;
            SetOwned(t, Owned(t) + n);
        }

        /// <summary>Spend one if available. Returns false and changes nothing otherwise.</summary>
        public static bool TryConsume(PowerupType t)
        {
            int c = Owned(t);
            if (c <= 0) return false;
            SetOwned(t, c - 1);
            return true;
        }

        // ── Next-level selection (set in the PlayPopup) ──────────────────────
        public static bool IsSelected(PowerupType t) => PlayerPrefs.GetInt(SelectedPrefix + t, 0) == 1;

        public static void SetSelected(PowerupType t, bool on)
        {
            PlayerPrefs.SetInt(SelectedPrefix + t, on ? 1 : 0);
            PlayerPrefs.Save();
            OnSelectionChanged?.Invoke(t, on);
        }

        /// <summary>Clear all next-level selections (called after they're applied at level start).</summary>
        public static void ClearSelections()
        {
            SetSelected(PowerupType.Bomb, false);
            SetSelected(PowerupType.Paint, false);
        }

        private static void SetOwned(PowerupType t, int value)
        {
            value = Mathf.Max(0, value);
            PlayerPrefs.SetInt(OwnedPrefix + t, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke(t, value);
        }
    }
}
