using UnityEngine;
using PixelShoot.Game;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Single MonoBehaviour entry point for ads. Survives scene loads, exposes
    /// the active <see cref="IAdsService"/> as a static <see cref="Service"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AdsManager : MonoBehaviour
    {
        private static AdsManager instance;
        public static IAdsService Service { get; private set; }

        [Tooltip("If true, the banner is shown automatically once the SDK initialises.")]
        [SerializeField] private bool autoShowBanner = true;
        [SerializeField] private BannerPosition bannerPosition = BannerPosition.Bottom;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            // Persistence comes from living in the never-unloaded InitializeScene (no DontDestroyOnLoad).

#if PIXELSHOOT_ADMOB
            Service = new AdMobAdsService();
#else
            Service = new NullAdsService();
#endif
            Service.Initialize();

            // NoAds purchase hides the banner too.
            // if (autoShowBanner && !PlayerWallet.HasNoAds)
            //     Service.ShowBanner(bannerPosition);
        }

        /// <summary>Hide the banner permanently — called when NoAds is purchased mid-session.</summary>
        public static void SuppressAdsAfterNoAdsPurchase()
        {
            if (Service == null) return;
            Service.HideBanner();
            Debug.Log("[AdsManager] NoAds active — banner hidden.");
        }

        // (Auto-bootstrap removed — place an AdsManager in the InitializeScene instead.)
    }
}
