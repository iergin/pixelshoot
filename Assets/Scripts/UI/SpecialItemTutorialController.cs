using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;

namespace PixelShoot.UI
{
    /// <summary>Persisted "player has seen this special item's tutorial" flags.</summary>
    public static class SpecialItemTutorialState
    {
        private const string Prefix = "PixelShoot.SpecialItemTutorial.";
        public static bool IsShown(SpecialItem item) => PlayerPrefs.GetInt(Prefix + item, 0) == 1;
        public static void MarkShown(SpecialItem item) { PlayerPrefs.SetInt(Prefix + item, 1); PlayerPrefs.Save(); }
        public static void ResetAll()
        {
            foreach (SpecialItem i in Enum.GetValues(typeof(SpecialItem))) PlayerPrefs.DeleteKey(Prefix + i);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Shows a one-time info panel (title / description / icon) the FIRST time a level
    /// contains a given special item (Surprise / Link / LockKey / Bomb). Tapping anywhere on
    /// the panel closes it; if several first-time items appear in one level they queue up and
    /// show one after another. Call <see cref="CheckLevel"/> once the level is built.
    /// </summary>
    public class SpecialItemTutorialController : MonoBehaviour
    {
        [SerializeField] private SpecialItemTutorialData config;

        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image iconImage;
        [Tooltip("Button covering the panel — a tap anywhere on it closes the tutorial.")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button BGcloseButton;

        private readonly Queue<SpecialItem> queue = new Queue<SpecialItem>();
        private SpecialItem current;
        private bool showing;
        private bool wired;

        private void Awake()
        {
            Wire();
            if (panel != null) panel.SetActive(false);
        }

        private void Wire()
        {
            if (wired) return;
            wired = true;
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(Advance); }
            if (BGcloseButton != null) { BGcloseButton.onClick.RemoveAllListeners(); BGcloseButton.onClick.AddListener(Advance); }

        }

        /// <summary>Scan a freshly-built level and queue a tutorial for each special item the
        /// player is seeing for the first time.</summary>
        public void CheckLevel(LevelData data)
        {
            if (data == null || config == null) return;
            Wire();

            foreach (SpecialItem item in Enum.GetValues(typeof(SpecialItem)))
            {
                if (SpecialItemTutorialState.IsShown(item)) continue;
                if (!LevelHas(data, item)) continue;
                if (config.Get(item) == null) continue; // no authored panel → skip
                if (!queue.Contains(item)) queue.Enqueue(item);
            }
            if (!showing) ShowNext();
        }

        // The tap-to-close handler (also advances to the next queued item).
        public void Advance()
        {
            if (!showing) return;
            SpecialItemTutorialState.MarkShown(current);
            ShowNext();
        }

        private void ShowNext()
        {
            if (queue.Count == 0)
            {
                showing = false;
                if (panel != null) panel.SetActive(false);
                return;
            }

            current = queue.Dequeue();
            var e = config.Get(current);
            if (e == null) { ShowNext(); return; }

            if (titleLabel != null) titleLabel.text = e.title;
            if (descriptionLabel != null) descriptionLabel.text = e.description;
            if (iconImage != null) { iconImage.sprite = e.icon; iconImage.enabled = e.icon != null; }
            if (panel != null) panel.SetActive(true);
            showing = true;
        }

        // ── Presence detection from the level data ───────────────────────────
        private static bool LevelHas(LevelData data, SpecialItem item)
        {
            switch (item)
            {
                case SpecialItem.Surprise: return AnyShooter(data, s => s.IsSurprise);
                case SpecialItem.Link:     return AnyShooter(data, s => s.LinkGroupId != 0);
                case SpecialItem.LockKey:  return AnyShooter(data, s => s.IsLock) || AnyCell(data, c => c.KeyId != 0);
                case SpecialItem.Bomb:     return AnyCell(data, c => c.IsBomb);
                default: return false;
            }
        }

        private static bool AnyShooter(LevelData data, Func<ShooterData, bool> pred)
        {
            if (data.Columns == null) return false;
            foreach (var col in data.Columns)
            {
                if (col == null) continue;
                foreach (var s in col.Shooters)
                    if (s != null && pred(s)) return true;
            }
            return false;
        }

        private static bool AnyCell(LevelData data, Func<BoxCellData, bool> pred)
        {
            var cells = data.Grid != null ? data.Grid.Cells : null;
            if (cells == null) return false;
            foreach (var c in cells)
                if (c != null && pred(c)) return true;
            return false;
        }
    }
}
