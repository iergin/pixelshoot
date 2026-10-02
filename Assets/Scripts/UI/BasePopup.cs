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
        [Tooltip("Optional content groups animated on open/close (slide + fade). If empty, the popup " +
                 "shows/hides INSTANTLY via the CanvasGroup (no fade).")]
        [SerializeField] private UiTransition[] transitions;
        [Tooltip("Buttons that close this popup (the X / 'No thanks' etc.). Wired automatically.")]
        [SerializeField] private Button[] closeButtons;
        [Tooltip("Optional canvas sorting order for this popup. 0 = inherit the PopupService root canvas " +
                 "(default; renders above the menu chrome — use for full modals like Settings). Set a value " +
                 "BELOW the menu chrome's canvas order (e.g. 50) for a popup that must sit UNDER the nav bar " +
                 "+ HUD, like the Shop. When non-zero, a dedicated Canvas + GraphicRaycaster is added so this " +
                 "order is absolute across all canvases.")]
        [SerializeField] private int sortingOrder = 0;
        [Tooltip("EMBEDDED MODE: tick when this popup is dropped into the scene as a static PAGE (e.g. the " +
                 "Shop inside the menu swipe pager) instead of being spawned by PopupService. It then shows " +
                 "itself instantly with no animation, ignores the sorting override, hides its close buttons " +
                 "(a page has no X — you swipe away), and Close() does nothing. The SAME prefab still works " +
                 "as a normal popup when spawned by PopupService (leave this off for that copy / instance).")]
        [SerializeField] private bool embedded = false;

        /// <summary>Raised after the close animation finishes, right before the popup is destroyed.</summary>
        public event Action Closed;
        /// <summary>Raised after the open animation finishes.</summary>
        public event Action Opened;

        public bool IsOpen { get; private set; }

        private PopupService owner;
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

        // Embedded page: no PopupService owner — set up + show instantly on scene load.
        private void Awake()
        {
            if (!embedded) return;
            EnsureWired();
            ShowInstant();
            IsOpen = true;
            OnPopupOpened();     // page is "open" for its lifetime (state subscriptions, etc.)
            Opened?.Invoke();
        }

        // ── Wiring called by PopupService ────────────────────────────────────
        internal void Bind(PopupService service)
        {
            // Just record the owner here. EnsureWired() (→ OnInit) is deferred to PlayOpen so it runs
            // AFTER the Create(...) configure callback (e.g. PlayPopup.Bind / FailFlowPopup.SetMode),
            // letting OnInit read the mode/refs the caller set up.
            owner = service;
        }

        private void EnsureWired()
        {
            if (wired) return;
            wired = true;
            if (!embedded) ApplySortingOverride(); // a page inherits the pager's canvas
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (closeButtons != null)
                foreach (var b in closeButtons)
                {
                    if (b == null) continue;
                    if (embedded) { b.gameObject.SetActive(false); continue; } // no X on a page
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(Close);
                }
            OnInit();
        }

        // Snap straight to the shown state (embedded page — no open animation).
        private void ShowInstant()
        {
            if (transitions != null) foreach (var t in transitions) if (t != null) t.SetShown();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        internal void PlayOpen(Action onComplete)
        {
            EnsureWired();
            gameObject.SetActive(true);
            IsOpen = true;

            float dur = 0f;
            if (transitions != null && transitions.Length > 0)
            {
                // Only animate when UiTransition groups are assigned.
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
                // No fade — show instantly.
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            Finish(dur, () => { OnPopupOpened(); Opened?.Invoke(); onComplete?.Invoke(); });
        }

        internal void PlayClose(Action onComplete)
        {
            IsOpen = false;
            OnPopupClosing();

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
                // No fade — hide instantly.
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            Finish(dur, () => { Closed?.Invoke(); onComplete?.Invoke(); });
        }

        // Give this popup its own Canvas at an absolute sorting order so it can sit ABOVE or BELOW the
        // menu chrome regardless of the shared PopupService root order (e.g. Shop below the nav bar).
        private void ApplySortingOverride()
        {
            if (sortingOrder == 0) return;
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }

        // ── Public API for popups / callers ──────────────────────────────────
        /// <summary>Close this popup. Routed through the service so the stack/queue stays consistent.
        /// No-op in embedded mode — a page doesn't close itself (you swipe away).</summary>
        public void Close()
        {
            if (embedded) return;
            if (owner != null) owner.Close(this);
            else PlayClose(() => Destroy(gameObject)); // safety net if spawned without a service
        }

        /// <summary>Open another popup on top. From a real popup this stacks immediately over it
        /// (popup-drives-popup, e.g. Settings → Message). From an embedded page it opens a normal
        /// top-level popup via PopupService.</summary>
        protected T CreatePopup<T>(Action<T> configure = null) where T : BasePopup
        {
            if (owner != null) return owner.Push(configure);
            if (PopupService.Instance != null) return PopupService.Instance.Create(configure);
            Debug.LogWarning($"[{name}] No PopupService — cannot open a child popup.");
            return null;
        }

        private static void Finish(float delay, Action done)
        {
            if (delay > 0f) DOVirtual.DelayedCall(delay, () => done(), ignoreTimeScale: true);
            else done();
        }

        protected virtual void OnDestroy()
        {
            if (embedded && IsOpen) OnPopupClosing(); // balance the Awake OnPopupOpened (unsubscribe, etc.)
        }
    }
}
