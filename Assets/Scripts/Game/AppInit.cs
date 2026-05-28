using UnityEngine;
using DG.Tweening;

namespace PixelShoot.Game
{
    /// <summary>
    /// Runs before any scene loads to configure global settings (e.g. DOTween capacity).
    /// No GameObject required — driven entirely by RuntimeInitializeOnLoadMethod.
    /// </summary>
    public static class AppInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Pre-allocate DOTween pools to avoid mid-game auto-expansion warnings.
            // 500 tweeners / 50 sequences covers a full board + conveyor animations
            // with comfortable headroom.
            DOTween.SetTweensCapacity(tweenersCapacity: 500, sequencesCapacity: 50);
        }
    }
}
