using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Game
{
    /// <summary>
    /// One-time app initialisation, run ONCE from the persistent InitializeScene (which never
    /// unloads). Seeds the wallet, stamps first-launch, and bumps the per-launch session counter —
    /// things that must happen once per app start, NOT every time the Game scene (re)loads.
    ///
    /// <para>In single-scene fallback (no InitializeScene / no <see cref="SceneFlow"/>), LevelLoader
    /// does the same init itself, so this component is only needed in the two-scene setup.</para>
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Tooltip("Coins config used to seed the starting balance on a fresh save.")]
        [SerializeField] private CoinsConfig coinsConfig;

        private void Start()
        {
            if (coinsConfig != null) PlayerWallet.EnsureInitialized(coinsConfig.InitialBalance);
            PlayerWallet.StampFirstLaunchIfMissing();
            PlayerWallet.BeginSession(); // one session per app launch
        }
    }
}
