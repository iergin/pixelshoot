using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop-in settings panel: sound on/off toggle and a privacy-policy button.
    /// GDPR / Apple App Store: shipping with a reachable privacy policy is
    /// a hard requirement for the EU and most stores — wire that URL in
    /// the inspector before launch.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;

        [Header("Sound")]
        [SerializeField] private Toggle soundToggle;
        [Tooltip("Optional. Label that reads 'Sound: ON' / 'Sound: OFF' on top of the toggle.")]
        [SerializeField] private TMP_Text soundLabel;

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
            if (soundToggle != null)
            {
                soundToggle.isOn = !PlayerWallet.SoundMuted; // ON = sound playing
                soundToggle.onValueChanged.RemoveAllListeners();
                soundToggle.onValueChanged.AddListener(OnSoundToggle);
            }
            // Push the persisted mute state into the AudioListener.
            AudioListener.volume = PlayerWallet.SoundMuted ? 0f : 1f;
            RefreshSoundLabel();
        }

        public void OpenPanel()
        {
            if (panel != null) panel.SetActive(true);
        }

        public void ClosePanel()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnSoundToggle(bool soundOn)
        {
            PlayerWallet.SoundMuted = !soundOn;
            RefreshSoundLabel();
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel == null) return;
            soundLabel.text = PlayerWallet.SoundMuted ? "Sound: OFF" : "Sound: ON";
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
