using UnityEngine;

namespace PixelShoot.Ads
{
    /// <summary>
    /// Single MonoBehaviour entry point for ads. Survives scene loads, exposes
    /// the active <see cref="IAdsService"/> as a static <see cref="Instance"/>.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AdsManager : MonoBehaviour
    {
        private static AdsManager instance;
        public static IAdsService Service { get; private set; }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);

#if PIXELSHOOT_ADMOB
            Service = new AdMobAdsService();
#else
            Service = new NullAdsService();
#endif
            Service.Initialize();
        }

        /// <summary>Static auto-bootstrap so AdsManager exists even if no GameObject in the scene has it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("[AdsManager]");
            go.AddComponent<AdsManager>();
        }
    }
}
