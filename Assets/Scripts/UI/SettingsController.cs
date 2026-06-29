using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop-in settings panel: independent Music / Effects toggles and a privacy-policy
    /// button. GDPR / Apple App Store: shipping with a reachable privacy policy is
    /// a hard requirement for the EU and most stores — wire that URL in
    /// the inspector before launch.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        [Header("Music")]
        [SerializeField] private Toggle musicToggle;
        [Tooltip("Optional. Label that reads 'Music: ON' / 'Music: OFF'.")]
        [SerializeField] private TMP_Text musicLabel;

        [Header("Effects (SFX)")]
        [SerializeField] private Toggle sfxToggle;
        [Tooltip("Optional. Label that reads 'Effects: ON' / 'Effects: OFF'.")]
        [SerializeField] private TMP_Text sfxLabel;

        [Header("Privacy policy")]
        [SerializeField] private Button privacyPolicyButton;
        [Tooltip("URL opened by the privacy-policy button. EU users MUST be able to reach this.")]
        [SerializeField] private string privacyPolicyUrl = "https://example.com/privacy";

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);

            if (openButton  != null) { openButton.onClick.RemoveAllListeners();  openButton.onClick.AddListener(OpenPanel); }
            if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(ClosePanel); }
            if (privacyPolicyButton != null)
            {
                privacyPolicyButton.onClick.RemoveAllListeners();
                privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
            }
            if (musicToggle != null)
            {
                musicToggle.isOn = PlayerWallet.MusicEnabled;
                musicToggle.onValueChanged.RemoveAllListeners();
                musicToggle.onValueChanged.AddListener(OnMusicToggle);
            }
            if (sfxToggle != null)
            {
                sfxToggle.isOn = PlayerWallet.SfxEnabled;
                sfxToggle.onValueChanged.RemoveAllListeners();
                sfxToggle.onValueChanged.AddListener(OnSfxToggle);
            }
            RefreshMusicLabel();
            RefreshSfxLabel();
        }

        [Tooltip("If set, open/close routes through the global UiPanelManager so it never overlaps another panel. Falls back to plain SetActive otherwise.")]
        [SerializeField] private UiPanel uiPanel;

        private UiPanel ResolvePanel()
        {
            if (uiPanel != null) return uiPanel;
            if (panel != null) uiPanel = panel.GetComponent<UiPanel>();
            return uiPanel;
        }

        public void OpenPanel()
        {
            var p = ResolvePanel();
            Debug.Log($"[Settings] OpenPanel() called. uiPanel={(p != null ? "set" : "<null>")}, panel={(panel != null ? panel.name : "<null>")}.");
            if (p != null) { p.RequestOpen(replaceCurrent: true); return; }
            if (panel != null) panel.SetActive(true);
            else Debug.LogWarning("[Settings] OpenPanel: both uiPanel and panel are null — nothing to open.");
        }

        public void ClosePanel()
        {
            var p = ResolvePanel();
            if (p != null) { p.RequestClose(); return; }
            if (panel != null) panel.SetActive(false);
        }

        private void OnMusicToggle(bool musicOn)
        {
            PlayerWallet.MusicEnabled = musicOn; // AudioManager reacts via the change event
            RefreshMusicLabel();
        }

        private void OnSfxToggle(bool sfxOn)
        {
            PlayerWallet.SfxEnabled = sfxOn;
            RefreshSfxLabel();
        }

        private void RefreshMusicLabel()
        {
            if (musicLabel == null) return;
            musicLabel.text = PlayerWallet.MusicEnabled ? "Music: ON" : "Music: OFF";
        }

        private void RefreshSfxLabel()
        {
            if (sfxLabel == null) return;
            sfxLabel.text = PlayerWallet.SfxEnabled ? "Effects: ON" : "Effects: OFF";
        }

        public void OpenPrivacyPolicy()
        {
            if (string.IsNullOrWhiteSpace(privacyPolicyUrl))
            {
                Debug.LogWarning("[Settings] Privacy policy URL is empty — set it in the inspector.");
                return;
            }
            Application.OpenURL(privacyPolicyUrl);
        }
    }
}
