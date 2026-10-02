using UnityEngine;
using PixelShoot.Conveyor;
using PixelShoot.Shooters;
using PixelShoot.UI;

namespace PixelShoot.Game
{
    /// <summary>
    /// Freezes gameplay while ANY popup is on screen. Lives in the Game scene and listens to
    /// <see cref="PopupService.AnyOpenChanged"/>: the instant the first popup starts opening it pauses
    /// the conveyor and suspends bus taps; it only lifts both after the LAST popup has fully closed.
    /// Because it keys off the whole-stack signal, stacked popups keep gameplay frozen and nothing
    /// moves during the open/close animations either.
    ///
    /// <para>Drop one in the Game scene and assign the <see cref="conveyor"/>. Replaces the old
    /// per-panel pause code (booster purchase, shop, settings) — any popup now freezes play for free.</para>
    /// </summary>
    public class PopupGameplayGuard : MonoBehaviour
    {
        [Tooltip("The level conveyor to pause while a popup is up.")]
        [SerializeField] private ConveyorController conveyor;

        private bool suspended; // whether WE currently hold a ClickInputRouter suspend

        private void OnEnable()
        {
            PopupService.AnyOpenChanged += Apply;
            Apply(PopupService.AnyOpen); // sync to whatever is already open
        }

        private void OnDisable()
        {
            PopupService.AnyOpenChanged -= Apply;
            Release(); // balance our suspend on scene unload / disable
            if (conveyor != null) conveyor.IsPaused = false;
        }

        private void Apply(bool anyPopupOpen)
        {
            if (conveyor != null) conveyor.IsPaused = anyPopupOpen;

            if (anyPopupOpen && !suspended)
            {
                ClickInputRouter.PushSuspend(); // block bus taps behind the popup
                suspended = true;
            }
            else if (!anyPopupOpen && suspended)
            {
                Release();
            }
        }

        private void Release()
        {
            if (!suspended) return;
            ClickInputRouter.PopSuspend();
            suspended = false;
        }
    }
}
