using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// In-game settings popup (Sound / Music / Vibration toggles + Quit / Resume). Opened from the
    /// Game HUD via <c>PopupService.Instance.Create&lt;IngameSettingsPopup&gt;()</c>.
    ///
    /// <para><b>Shares state with the menu <see cref="SettingsPopup"/>.</b> Both read and write the
    /// SAME <see cref="PlayerWallet"/> flags (SfxEnabled / MusicEnabled / VibrationEnabled), so a
    /// change here is reflected there and vice-versa — there is no separate in-game state. The toggle
    /// visual reuses <see cref="SettingsPopup.ToggleVisual"/> so both popups behave identically.</para>
    /// </summary>
    public class IngameSettingsPopup : BasePopup
    {
        [Header("Toggles (Sound / Music / Vibration)")]
        [SerializeField] private SettingsPopup.ToggleVisual sound;
        [SerializeField] private SettingsPopup.ToggleVisual music;
        [SerializeField] private SettingsPopup.ToggleVisual vibration;

        [Header("Buttons")]
        [SerializeField] private Button quitButton;
        [SerializeField] private Button resumeButton;

        protected override void OnInit()
        {
            sound.Wire(ToggleSound);
            music.Wire(ToggleMusic);
            vibration.Wire(ToggleVibration);

            if (quitButton   != null) { quitButton.onClick.RemoveAllListeners();   quitButton.onClick.AddListener(OnQuit); }
            if (resumeButton != null) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(OnResume); }

            RefreshAll();
        }

        private void RefreshAll()
        {
            sound.Apply(PlayerWallet.SfxEnabled);
            music.Apply(PlayerWallet.MusicEnabled);
            vibration.Apply(PlayerWallet.VibrationEnabled);
        }

        // Same PlayerWallet flags the menu SettingsPopup uses → the two stay in sync automatically.
        private void ToggleSound()
        {
            bool on = !PlayerWallet.SfxEnabled;
            PlayerWallet.SfxEnabled = on; // AudioManager reacts via the change event
            sound.Apply(on);
        }

        private void ToggleMusic()
        {
            bool on = !PlayerWallet.MusicEnabled;
            PlayerWallet.MusicEnabled = on;
            music.Apply(on);
        }

        private void ToggleVibration()
        {
            bool on = !PlayerWallet.VibrationEnabled;
            PlayerWallet.VibrationEnabled = on; // persisted stub — hook real haptics later
            vibration.Apply(on);
        }

        // Resume → just close the popup and keep playing.
        private void OnResume() => Close();

        // Quit → close, then hand off to the Game-scene quit flow (streak-loss → life → menu).
        private void OnQuit()
        {
            Close();
            if (QuitFlowController.Instance != null) QuitFlowController.Instance.BeginQuitFlow();
            else Debug.LogWarning("[IngameSettings] No QuitFlowController in the Game scene — can't quit.");
        }
    }
}
