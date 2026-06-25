using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// A self-contained, animated panel (Settings / Shop / Promo / …) that can be shown
    /// from the main menu OR from gameplay. It never opens itself directly — it routes
    /// through <see cref="UiPanelManager"/> so two panels can never overlap.
    ///
    /// <para>Animation: assign <see cref="UiTransition"/> content groups for slide+fade, or
    /// just a CanvasGroup for a plain fade, or neither for an instant show. Close buttons
    /// are wired automatically.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class UiPanel : MonoBehaviour
    {
        [Tooltip("CanvasGroup used for the fallback fade (added automatically if missing and no transitions are set).")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Optional content groups animated on open/close (slide + fade). If empty, the panel fades via the CanvasGroup.")]
        [SerializeField] private UiTransition[] transitions;
        [Tooltip("Fade duration when falling back to the CanvasGroup (no transitions).")]
        [SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("Buttons that close this panel (the X / 'No thanks' etc.). Wired automatically.")]
        [SerializeField] private Button[] closeButtons;

        public bool IsOpen { get; private set; }
        public event Action OnOpened;
        public event Action OnClosed;

        private Tween fadeTween;
        // True while PlayOpen is mid-activation, so the (first-ever) Awake that
        // SetActive(true) triggers doesn't immediately hide us again.
        private bool opening;
        private bool inited;

        private void Awake() => EnsureInit();

        private void EnsureInit()
        {
            if (inited) return;
            inited = true;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (closeButtons != null)
                foreach (var b in closeButtons)
                {
                    if (b == null) continue;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(RequestClose);
                }
            // Start hidden — UNLESS we're in the middle of opening (Awake fired from
            // PlayOpen's SetActive(true) for a panel that began the scene inactive).
            if (!opening) HideInstant();
        }

        // ── Public requests (go through the manager) ─────────────────────────
        /// <summary>Ask to open. replaceCurrent=true closes any open panel first (user-driven);
        /// false queues behind it (auto promos).</summary>
        public void RequestOpen(bool replaceCurrent = true)
        {
            Debug.Log($"[UiPanel] RequestOpen('{name}', replace={replaceCurrent}). Manager={(UiPanelManager.Instance != null ? "present" : "<null>")}.");
            if (UiPanelManager.Instance != null) UiPanelManager.Instance.Open(this, replaceCurrent);
            else { Debug.LogWarning("[UiPanel] No UiPanelManager in scene — opening directly."); PlayOpen(null); }
        }

        public void RequestClose()
        {
            Debug.Log($"[UiPanel] RequestClose('{name}').");
            if (UiPanelManager.Instance != null) UiPanelManager.Instance.Close(this);
            else PlayClose(null);
        }

        // ── Called by the manager ────────────────────────────────────────────
        public void PlayOpen(Action onComplete)
        {
            int tc = transitions != null ? transitions.Length : 0;
            Debug.Log($"[UiPanel] PlayOpen('{name}'). transitions={tc}, canvasGroup={(canvasGroup != null ? "set" : "<null>")}, wasActive={gameObject.activeSelf}.");
            // Guard the first-activation Awake from re-hiding us, then make sure init ran.
            opening = true;
            gameObject.SetActive(true);
            EnsureInit();
            opening = false;
            IsOpen = true;
            fadeTween?.Kill();

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

            Finish(dur, () => { OnOpened?.Invoke(); onComplete?.Invoke(); });
        }

        public void PlayClose(Action onComplete)
        {
            IsOpen = false;
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

            Finish(dur, () =>
            {
                gameObject.SetActive(false);
                OnClosed?.Invoke();
                onComplete?.Invoke();
            });
        }

        private void HideInstant()
        {
            IsOpen = false;
            if (transitions != null) foreach (var t in transitions) if (t != null) t.SetHidden();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        private static void Finish(float delay, Action done)
        {
            if (delay > 0f) DOVirtual.DelayedCall(delay, () => done(), ignoreTimeScale: true);
            else done();
        }

        private void OnDestroy() => fadeTween?.Kill();
    }
}
