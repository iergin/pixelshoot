using UnityEngine;
using PixelShoot.Shooters;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drop this on ANY panel / popup root. While the GameObject is active, world shooter/bus
    /// taps are suspended (ClickInputRouter ignores them), so opening any modal automatically
    /// blocks gameplay input — no per-panel code needed. Balanced via OnEnable / OnDisable
    /// (Unity also fires OnDisable when the object is destroyed while active).
    /// </summary>
    public class GameplayInputBlocker : MonoBehaviour
    {
        private bool pushed;

        private void OnEnable()
        {
            if (pushed) return;
            ClickInputRouter.PushSuspend();
            pushed = true;
        }

        private void OnDisable()
        {
            if (!pushed) return;
            ClickInputRouter.PopSuspend();
            pushed = false;
        }
    }
}
