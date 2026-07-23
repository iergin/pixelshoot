using System;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Settings popup matching the in-game layout: a Notifications switch, three icon-button toggles
    /// (Sound / Music / Vibration) under "Audio &amp; Haptics", and Privacy / Terms buttons. Derived
    /// from <see cref="BasePopup"/>, opened via <c>PopupService.Instance.Create&lt;SettingsPopup&gt;()</c>.
    ///
    /// <para>Each state persists through <see cref="PlayerWallet"/>. Sound and Music drive real audio;
    /// Vibration and Notifications are persisted stubs — the toggle + storage + change event are wired
    /// so hooking the real feature later is a one-liner off <c>PlayerWallet.On*EnabledChanged</c>.</para>
    /// </summary>
    public class SettingsPopup : BasePopup
    {
        /// <summary>
        /// Flexible visual for an icon-button toggle. Wire whichever fields your button uses — a
        /// sprite swap, a color tint (e.g. blue = on, gray = off), and/or separate on/off child
        /// objects (like a "muted" slash). <see cref="Apply"/> updates all that are assigned.
        /// </summary>
        [Serializable]
        public class ToggleVisual
        {
            public Button button;
            [Tooltip("Icon Image whose sprite/color reflects the state.")]
            public Image icon;
            [Tooltip("Optional. Icon shown when ON / OFF (sprite swap).")]
            public Sprite onSprite;
            public Sprite offSprite;
            [Tooltip("Optional. Tint the icon (on = e.g. blue, off = gray) if Use Color is on.")]
            public bool useColor;
            public Color onColor = Color.white;
            public Color offColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            [Tooltip("Optional child shown only when ON.")]
            public GameObject onVisual;
            [Tooltip("Optional child shown only when OFF (e.g. the crossed-out overlay).")]
            public GameObject offVisual;

            public void Apply(bool on)
            {
                if (icon != null)
                {
                    if (on && onSprite != null) icon.sprite = onSprite;
                    else if (!on && offSprite != null) icon.sprite = offSprite;
                    if (useColor) icon.color = on ? onColor : offColor;
                }
                if (onVisual != null) onVisual.SetActive(on);
                if (offVisual != null) offVisual.SetActive(!on);
            }

            public void Wire(UnityEngine.Events.UnityAction onClick)
            {
                if (button == null) return;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }

        [Header("Notifications")]
        [SerializeField] private ToggleVisual notifications;

        [Header("Audio & Haptics")]
        [SerializeField] private ToggleVisual sound;
        [SerializeField] private ToggleVisual music;
        [SerializeField] private ToggleVisual vibration;

        [Header("Privacy / Terms")]
        [SerializeField] private Button privacyButton;
        [Tooltip("URL opened by the Privacy button. EU users MUST be able to reach this.")]
        [SerializeField] private string privacyUrl = "https://example.com/privacy";
        [SerializeField] private Button termsButton;
        [SerializeField] private string termsUrl = "https://example.com/terms";

        protected override void OnInit()
        {
            // Icon-button toggles (all sprite-swap style, no Unity Toggle).
            notifications.Wire(ToggleNotifications);
            sound.Wire(ToggleSound);
            music.Wire(ToggleMusic);
            vibration.Wire(ToggleVibration);

            // Privacy / Terms.
            if (privacyButton != null) { privacyButton.onClick.RemoveAllListeners(); privacyButton.onClick.AddListener(OpenPrivacy); }
            if (termsButton   != null) { termsButton.onClick.RemoveAllListeners();   termsButton.onClick.AddListener(OpenTerms); }

            RefreshAll();
        }

        private void RefreshAll()
        {
            notifications.Apply(PlayerWallet.NotificationsEnabled);
            sound.Apply(PlayerWallet.SfxEnabled);
            music.Apply(PlayerWallet.MusicEnabled);
            vibration.Apply(PlayerWallet.VibrationEnabled);
        }

        // ── Toggle handlers ──────────────────────────────────────────────────
        private void ToggleNotifications()
        {
            bool on = !PlayerWallet.NotificationsEnabled;
            PlayerWallet.NotificationsEnabled = on; // persisted stub — hook real opt-in later
            notifications.Apply(on);
        }

        private void ToggleSound()
        {
            bool on = !PlayerWallet.SfxEnabled;
            PlayerWallet.SfxEnabled = on; // AudioManager reacts via the change event
            sound.Apply(on);
        }

        private void ToggleMusic()
        {
            bool on = !PlayerWallet.MusicEnabled;
            PlayerWallet.MusicEnabled = on; // AudioManager reacts via the change event
            music.Apply(on);
        }

        private void ToggleVibration()
        {
            bool on = !PlayerWallet.VibrationEnabled;
            PlayerWallet.VibrationEnabled = on; // persisted stub — hook real haptics later
            vibration.Apply(on);
        }

        // ── Links ────────────────────────────────────────────────────────────
        public void OpenPrivacy() => OpenUrl(privacyUrl, "Privacy");
        public void OpenTerms()   => OpenUrl(termsUrl, "Terms");

        private static void OpenUrl(string url, string label)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning($"[SettingsPopup] {label} URL is empty — set it in the inspector.");
                return;
            }
            Application.OpenURL(url);
        }
    }
}
