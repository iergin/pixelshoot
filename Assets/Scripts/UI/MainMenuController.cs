using System;
using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Shop;
using PixelShoot.Game;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drives the main menu: shows the coin balance, animates the menu open/closed via a
    /// set of <see cref="UiTransition"/> groups (left / right / top / bottom button parents),
    /// and on Start fades everything out, reveals the game panel, and enables gameplay input.
    ///
    /// <para><b>Extensible by design</b>: the animated groups are just a list of UiTransition —
    /// add a new button parent, drop a UiTransition on it, and add it to <see cref="groups"/>.
    /// The menu buttons (Shop / Settings / No-Ads) are wired to existing controllers; new
    /// buttons follow the same pattern. <see cref="OnGameStarted"/> lets other systems react
    /// to the menu→game transition.</para>
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Roots")]
        [Tooltip("Whole menu canvas/root, deactivated after the start transition completes.")]
        [SerializeField] private GameObject menuRoot;
        [Tooltip("Game HUD/world panel, activated when the player presses Start.")]
        [SerializeField] private GameObject gamePanel;
  
        [Header("Animated groups (left / right / top / bottom button parents)")]
        [SerializeField] private UiTransition[] groups;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button noAdsButton;

        [Header("Linked controllers (optional)")]
        [SerializeField] private ShopManager shop;
        [SerializeField] private SettingsController settings;
        [SerializeField] private NoAdsPromoController noAdsPromo;

        [Header("Start transition")]
        [Tooltip("Open the menu automatically on scene start.")]
        [SerializeField] private bool openOnStart = true;

        [Header("Lives")]
        [Tooltip("Shown when Start is pressed with no lives left (refill with coins / rewarded ad).")]
        [SerializeField] private OutOfLivesController outOfLives;

        [Header("Gameplay input")]
        [Tooltip("Behaviours that must NOT run while the menu is up (e.g. ClickInputRouter). " +
                 "Disabled on Awake / when the menu opens, enabled only after Start begins gameplay.")]
        [SerializeField] private Behaviour[] gameplayInput;

        /// <summary>Fired the moment the start transition finishes and gameplay begins.</summary>
        public event Action OnGameStarted;
        /// <summary>Fired when Start is pressed but the player has no lives left.</summary>
        public event Action OnOutOfLives;

        private bool starting;

        private void Awake()
        {
            WireButtons();
            if (gamePanel != null) gamePanel.SetActive(false);
            SetGameplayInput(false); // no bus clicks until the player presses Start
        }

        private void SetGameplayInput(bool enabled)
        {
            if (gameplayInput == null) return;
            foreach (var b in gameplayInput)
                if (b != null) b.enabled = enabled;
        }

        private void Start()
        {
            if (openOnStart) Open();
        }

        private void WireButtons()
        {
            Hook(startButton, "Start", StartGame);
            Hook(shopButton, "Shop", () =>
            {
                Debug.Log($"[MainMenu] Shop button clicked. shop={(shop != null ? "set" : "<null>")}.");
                shop?.OpenShop();
            });
            Hook(settingsButton, "Settings", () =>
            {
                Debug.Log($"[MainMenu] Settings button clicked. settings={(settings != null ? "set" : "<null>")}.");
                settings?.OpenPanel();
            });
            Hook(noAdsButton, "NoAds", () =>
            {
                Debug.Log($"[MainMenu] NoAds button clicked. noAdsPromo={(noAdsPromo != null ? "set" : "<null>")}.");
                noAdsPromo?.ShowNoAdsPanel();
            });
        }

        private static void Hook(Button b, string label, UnityEngine.Events.UnityAction action)
        {
            if (b == null) { Debug.LogWarning($"[MainMenu] '{label}' button is NOT assigned in the inspector."); return; }
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(action);
            Debug.Log($"[MainMenu] Wired '{label}' onto button '{b.name}' (interactable={b.interactable}).");
        }

        // ── Open / close ──────────────────────────────────────────────────────
        public void Open()
        {
            if (menuRoot != null) menuRoot.SetActive(true);
            starting = false;
            SetGameplayInput(false); // menu is up → gameplay input off
            foreach (var g in groups)
            {
                if (g == null) continue;
                g.SetHidden();
                g.PlayIn();
            }
        }

        /// <summary>Player pressed Start: slide/fade the whole menu out, then hand off to gameplay.</summary>
        public void StartGame()
        {
            if (starting) return;

            // A life is spent the instant the level starts — once committed it's gone even
            // if the player quits or backs out. Block the start (and show the out-of-lives
            // UI) when there are none left.
            if (!PlayerLives.TryConsumeForLevelStart())
            {
                if (outOfLives != null)
                {
                    Debug.Log($"[MainMenu] Start blocked — out of lives. Opening '{outOfLives.name}'.");
                    outOfLives.Open();
                }
                else
                {
                    Debug.LogWarning("[MainMenu] Start blocked — out of lives, but the 'Out Of Lives' " +
                                     "reference is NOT assigned. Run Generator ▶ Create Out Of Lives Panel UI, " +
                                     "or drag the OutOfLivesController onto MainMenuController.outOfLives.");
                }
                OnOutOfLives?.Invoke();
                return;
            }

            starting = true;

            // Reveal the game panel underneath right away so the fade-out uncovers live gameplay.
            if (gamePanel != null) gamePanel.SetActive(true);

            float longest = 0f;
            foreach (var g in groups)
            {
                if (g == null) continue;
                g.PlayOut();
                longest = Mathf.Max(longest, g.OutTime);
            }

            // After the out animation finishes, drop the menu and enable input.
            if (longest > 0f)
                DG.Tweening.DOVirtual.DelayedCall(longest, FinishStart, ignoreTimeScale: true);
            else
                FinishStart();
        }

        private void FinishStart()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
            SetGameplayInput(true); // gameplay begins → allow bus clicks
            OnGameStarted?.Invoke();
        }
    }
}
