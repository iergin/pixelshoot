using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Game;

namespace PixelShoot.Shop
{
    /// <summary>
    /// Per-scene trigger for the No Ads promo. The promo CORE (panels + show logic + state + the
    /// interstitial triggers) lives once in the persistent InitializeScene as
    /// <see cref="NoAdsPromoController"/>; this thin component forwards scene-local events to it via
    /// <see cref="NoAdsPromoController.Instance"/> — so nothing needs a cross-scene reference.
    ///
    /// <para>Place one wherever a trigger belongs:</para>
    /// <list type="bullet">
    /// <item><b>MainMenu scene</b> — assign <see cref="openButton"/> (the "No Ads" menu button).</item>
    /// <item><b>Game scene</b> — assign <see cref="gameController"/> (per-session promo on level win).</item>
    /// </list>
    /// A single component can carry both refs, or you can drop one per scene with just the ref it needs.
    /// </summary>
    public class NoAdsPromoTrigger : MonoBehaviour
    {
        [Tooltip("MENU: the 'No Ads' button. Opens the promo panel on click.")]
        [SerializeField] private Button openButton;
        [Tooltip("GAME: GameController whose OnLevelWon drives the per-session / starter promo.")]
        [SerializeField] private GameController gameController;

        private void Awake()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveAllListeners();
                openButton.onClick.AddListener(OpenPromo);
            }
        }

        private void OnEnable()  { if (gameController != null) gameController.OnLevelWon += NotifyWin; }
        private void OnDisable() { if (gameController != null) gameController.OnLevelWon -= NotifyWin; }

        private void OpenPromo() => NoAdsPromoController.Instance?.ShowNoAdsPanel();
        private void NotifyWin() => NoAdsPromoController.Instance?.NotifyLevelWon();
    }
}
