using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelShoot.UI
{
    /// <summary>
    /// Reusable "feature gated by level" block for a popup: shows a LOCK (with the unlock level) below
    /// the threshold, the real UI at/after it, and a one-time TUTORIAL the first time it unlocks
    /// (dismissed by a tap anywhere via a full-screen catcher). Drop one per gated feature in the
    /// inspector and call <see cref="Apply"/> with the current level + that feature's unlock level.
    /// </summary>
    [Serializable]
    public class LockedFeature
    {
        [Tooltip("The feature's real UI (e.g. the powerup select button). Shown only when unlocked.")]
        public GameObject root;
        [Tooltip("Lock overlay shown while still locked.")]
        public GameObject lockRoot;
        [Tooltip("Label on the lock showing the unlock level. {0} = the level number.")]
        public TMP_Text unlockLabel;
        public string unlockFormat = "Level {0}";
        [Tooltip("Tutorial shown ONCE the first time this feature unlocks. Tap anywhere to close.")]
        public GameObject tutorial;
        [Tooltip("Full-screen (transparent) button over everything that catches a tap ANYWHERE to dismiss the tutorial.")]
        public Button tutorialTapCatcher;
        [Tooltip("Unique key so each feature's 'tutorial seen' flag is independent (e.g. 'Paint', 'Bomb').")]
        public string tutorialKey = "Feature";

        private string ShownKey => "PixelShoot.FeatureTutorialShown." + tutorialKey;

        /// <summary>Show lock vs. real UI for <paramref name="currentDisplayLevel"/> and, on the first
        /// unlock, the tutorial.</summary>
        public void Apply(int currentDisplayLevel, int unlockLevel)
        {
            bool unlocked = currentDisplayLevel >= unlockLevel;

            if (root != null) root.SetActive(unlocked);
            if (lockRoot != null) lockRoot.SetActive(!unlocked);

            if (!unlocked)
            {
                if (unlockLabel != null) unlockLabel.text = string.Format(string.IsNullOrEmpty(unlockFormat) ? "{0}" : unlockFormat, unlockLevel);
                HideTutorial();
                return;
            }
            MaybeShowTutorial();
        }

        private void MaybeShowTutorial()
        {
            if (tutorial == null) return;
            if (PlayerPrefs.GetInt(ShownKey, 0) == 1) { HideTutorial(); return; }

            tutorial.SetActive(true);
            if (tutorialTapCatcher != null)
            {
                tutorialTapCatcher.gameObject.SetActive(true);
                tutorialTapCatcher.onClick.RemoveAllListeners();
                tutorialTapCatcher.onClick.AddListener(Dismiss);
            }
        }

        private void Dismiss()
        {
            PlayerPrefs.SetInt(ShownKey, 1);
            PlayerPrefs.Save();
            HideTutorial();
        }

        private void HideTutorial()
        {
            if (tutorial != null) tutorial.SetActive(false);
            if (tutorialTapCatcher != null) tutorialTapCatcher.gameObject.SetActive(false);
        }
    }
}
