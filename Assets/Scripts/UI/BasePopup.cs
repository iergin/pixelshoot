using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Base class for every popup in the game. A popup is a self-contained, animated screen
    /// (Settings / Message / Shop / …) that is <b>instantiated on demand</b> by
    /// <see cref="PopupService"/> and destroyed when it closes — it never manages its own place
    /// in the open order.
    ///
    /// <para>Animation reuses the existing <see cref="UiTransition"/> groups (slide + fade), or a
    /// plain CanvasGroup fade, or an instant show if neither is assigned. Close buttons are wired
    /// automatically to <see cref="Close"/>.</para>
    ///
    /// <para><b>Opening another popup from inside this one</b>: call
    /// <see cref="CreatePopup{T}"/> — it opens the child IMMEDIATELY on top of this popup
    /// (this one stays underneath and is revealed again when the child closes). That is different
    /// from a top-level <see cref="PopupService.Create{T}"/>, which QUEUES behind whatever is open.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class BasePopup : MonoBehaviour
    {
        [Header("Base popup")]
        [Tooltip("CanvasGroup used for the fallback fade (auto-added if missing and no transitions are set).")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Optional content groups animated on open/close (slide + fade). If empty, the popup fades via the CanvasGroup.")]
        [SerializeField] private UiTransition[] transitions;
        [Tooltip("Fade duration when falling back to the CanvasGroup (no transitions).")]
        [SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("Buttons that close this popup (the X / 'No thanks' etc.). Wired automatically.")]
        [SerializeField] private Button[] closeButtons;

        /// <summary>Raised after the close animation finishes, right before the popup is destroyed.</summary>
        public event Action Closed;
        /// <summary>Raised after the open animation finishes.</summary>
        public event Action Opened;

        public bool IsOpen { get; private set; }

        private PopupService owner;
        private Tween fadeTween;
        private bool wired;

        /// <summary>The service that spawned this popup (for subclasses that need it directly).</summary>
        protected PopupService Service => owner;

        // ── Lifecycle hooks for subclasses ───────────────────────────────────
        /// <summary>Called once right after spawn, before the open animation. Do inspector-free
        /// setup / event wiring here.</summary>
        protected virtual void OnInit() { }
        /// <summary>Called after the open animation completes.</summary>
        protected virtual void OnPopupOpened() { }
        /// <summary>Called the moment a close is requested, before the close animation plays.</summary>
        protected virtual void OnPopupClosing() { }

        // ── Wiring called by PopupService ────────────────────────────────────
        internal void Bind(PopupService service)
        {
            owner = service;
            EnsureWired();
        }

        private void EnsureWired()
        {
            if (wired) return;
            wired = true;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (closeButtons != null)
                foreach (var b in closeButtons)
                {
                    if (b == null) continue;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(Close);
                }
            OnInit();
        }

        internal void PlayOpen(Action onComplete)
        {
            EnsureWired();
            gameObject.SetActive(true);
            IsOpen = true;
            fadeTween?.Kill();

            // Start from hidden so the open always animates in cleanly.
            SetHidden();

            float dur = 0f;
            if (transitions != null && transitions.Length > 0)
            {
                foreach (var t in transitions)
                {
                    if (t == null) continue;
                    t.SetHidden();
                    t.PlayIn();
                    dur = Mathf.Max(dur, t.InTime);
                }
            }
            else if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad).SetUpdate(true);
                dur = fadeDuration;
            }

            Finish(dur, () => { OnPopupOpened(); Opened?.Invoke(); onComplete?.Invoke(); });
        }

        internal void PlayClose(Action onComplete)
        {
            IsOpen = false;
            OnPopupClosing();
            fadeTween?.Kill();

            float dur = 0f;
            if (transitions != null && transitions.Length > 0)
            {
                foreach (var t in transitions)
                {
                    if (t == null) continue;
                    t.PlayOut();
                    dur = Mathf.Max(dur, t.OutTime);
                }
            }
            else if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                fadeTween = canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InQuad).SetUpdate(true);
                dur = fadeDuration;
            }

            Finish(dur, () => { Closed?.Invoke(); onComplete?.Invoke(); });
        }

        private void SetHidden()
        {
            if (transitions != null) foreach (var t in transitions) if (t != null) t.SetHidden();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        // ── Public API for popups / callers ──────────────────────────────────
        /// <summary>Close this popup. Routed through the service so the stack/queue stays consistent.</summary>
        public void Close()
        {
            if (owner != null) owner.Close(this);
            else PlayClose(() => Destroy(gameObject)); // safety net if spawned without a service
        }

        /// <summary>Open another popup IMMEDIATELY on top of this one (does not wait for this popup
        /// to close). Use this for popup-drives-popup flows (e.g. Settings → Message).</summary>
        protected T CreatePopup<T>(Action<T> configure = null) where T : BasePopup
        {
            if (owner == null) { Debug.LogWarning($"[{name}] No PopupService — cannot open a child popup."); return null; }
            return owner.Push(configure);
        }

        private static void Finish(float delay, Action done)
        {
            if (delay > 0f) DOVirtual.DelayedCall(delay, () => done(), ignoreTimeScale: true);
            else done();
        }

        protected virtual void OnDestroy() => fadeTween?.Kill();
    }
}
