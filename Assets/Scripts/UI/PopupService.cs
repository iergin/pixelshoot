using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.UI
{
    /// <summary>
    /// Central popup manager. Lives once in the persistent <b>InitializeScene</b> (so popups survive
    /// the menu ⇆ game scene swaps) and instantiates popup prefabs on demand from a type→prefab map.
    ///
    /// <para><b>Two open behaviours:</b></para>
    /// <list type="bullet">
    /// <item><b>Queue</b> — <see cref="Create{T}"/> is the top-level request. If a popup is already
    /// open, the new one WAITS; it opens only once the whole on-screen stack has closed. Multiple
    /// top-level requests play one after another.</item>
    /// <item><b>Stack (immediate)</b> — <see cref="Push{T}"/> (reached via
    /// <see cref="BasePopup.CreatePopup{T}"/> from inside a popup) opens the child RIGHT AWAY on top
    /// of the current popup, which stays underneath and is revealed again when the child closes.</item>
    /// </list>
    ///
    /// <para>The distinction is intentional and caller-declared: an outside caller (button, game
    /// event) uses <see cref="Create{T}"/> and queues; a popup opening its own child uses
    /// <see cref="BasePopup.CreatePopup{T}"/> and stacks. So a game event arriving while Settings is
    /// open queues politely, but Settings → Message opens instantly.</para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PopupService : MonoBehaviour
    {
        public static PopupService Instance { get; private set; }

        [Tooltip("Parent (a Canvas transform in the InitializeScene) that spawned popups are placed under. " +
                 "If empty, this GameObject's transform is used.")]
        [SerializeField] private Transform popupRoot;
        [Tooltip("Every popup prefab the game can open. Each prefab's root must have a BasePopup-derived " +
                 "component; the service maps that concrete type → this prefab.")]
        [SerializeField] private BasePopup[] prefabs;

        private readonly Dictionary<Type, BasePopup> prefabByType = new Dictionary<Type, BasePopup>();
        private readonly List<BasePopup> activeStack = new List<BasePopup>();   // popups currently on screen (bottom → top)
        private readonly Queue<Pending> queue = new Queue<Pending>();           // top-level requests waiting for the stack to empty
        private bool transitioning;                                            // a top-level open animation is running

        private sealed class Pending
        {
            public Type Type;
            public Action<BasePopup> Configure;
        }

        /// <summary>Number of popups currently on screen (the nested stack depth).</summary>
        public int OpenCount => activeStack.Count;
        /// <summary>The topmost visible popup, or null.</summary>
        public BasePopup Top => activeStack.Count > 0 ? activeStack[activeStack.Count - 1] : null;

        /// <summary>True whenever ANY popup is on screen. Goes true the instant the first popup starts
        /// opening and false only after the LAST one has fully closed — so it stays true across the
        /// whole stack and through open/close animations. Gameplay guards (pause the conveyor, block
        /// bus taps) listen to <see cref="AnyOpenChanged"/>.</summary>
        public static bool AnyOpen { get; private set; }

        /// <summary>Fired when <see cref="AnyOpen"/> flips (true = a popup appeared, false = all gone).</summary>
        public static event System.Action<bool> AnyOpenChanged;

        // Recompute AnyOpen from the live stack and fire on change. Called right after any add/remove.
        private void RefreshAnyOpen()
        {
            bool now = activeStack.Count > 0;
            if (now == AnyOpen) return;
            AnyOpen = now;
            AnyOpenChanged?.Invoke(now);
        }

        /// <summary>True if a popup of type <typeparamref name="T"/> is currently on screen.</summary>
        public bool IsOpen<T>() where T : BasePopup
        {
            for (int i = 0; i < activeStack.Count; i++)
                if (activeStack[i] is T) return true;
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            prefabByType.Clear();
            if (prefabs != null)
                foreach (var p in prefabs)
                {
                    if (p == null) continue;
                    var t = p.GetType();
                    if (prefabByType.ContainsKey(t))
                    {
                        Debug.LogWarning($"[PopupService] Duplicate prefab for {t.Name} — keeping the first.");
                        continue;
                    }
                    prefabByType[t] = p;
                }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ── Top-level request (QUEUED) ───────────────────────────────────────
        /// <summary>Open a popup at the top level. If nothing is open it shows immediately; otherwise
        /// it queues and opens once the on-screen stack has fully closed. Returns the instance if it
        /// opened right away, or null if it was queued.</summary>
        public T Create<T>(Action<T> configure = null) where T : BasePopup
        {
            var wrapped = Wrap(configure);
            if (activeStack.Count == 0 && !transitioning)
                return (T)OpenTopLevel(typeof(T), wrapped);

            queue.Enqueue(new Pending { Type = typeof(T), Configure = wrapped });
            Debug.Log($"[PopupService] Queued {typeof(T).Name} (stack={activeStack.Count}, queue={queue.Count}).");
            return null;
        }

        // ── Nested request (IMMEDIATE / STACKED) ─────────────────────────────
        /// <summary>Open a popup immediately on top of whatever is showing. Reached from
        /// <see cref="BasePopup.CreatePopup{T}"/>; the current popup stays underneath.</summary>
        internal T Push<T>(Action<T> configure) where T : BasePopup
        {
            var p = Spawn(typeof(T));
            if (p == null) return null;
            activeStack.Add(p);
            RefreshAnyOpen();
            var wrapped = Wrap(configure);
            wrapped?.Invoke(p);
            Debug.Log($"[PopupService] Pushed {typeof(T).Name} on top (stack={activeStack.Count}).");
            p.PlayOpen(null);
            return (T)p;
        }

        // ── Close ────────────────────────────────────────────────────────────
        /// <summary>Close a popup (routed from <see cref="BasePopup.Close"/>). When the on-screen
        /// stack empties, the next queued top-level popup opens.</summary>
        internal void Close(BasePopup popup)
        {
            if (popup == null || !activeStack.Contains(popup)) return;
            popup.PlayClose(() =>
            {
                activeStack.Remove(popup);
                Destroy(popup.gameObject);
                Debug.Log($"[PopupService] Closed {popup.GetType().Name} (stack={activeStack.Count}, queue={queue.Count}).");
                if (activeStack.Count == 0) Pump();  // a queued popup may open here (keeps the stack non-empty)
                RefreshAnyOpen();                    // fires AnyOpen=false only when nothing is left / queued
            });
        }

        // ── Internals ────────────────────────────────────────────────────────
        private BasePopup OpenTopLevel(Type type, Action<BasePopup> configure)
        {
            var p = Spawn(type);
            if (p == null) return null;
            transitioning = true;
            activeStack.Add(p);
            RefreshAnyOpen(); // fires AnyOpen=true the instant the first popup starts opening
            configure?.Invoke(p);
            Debug.Log($"[PopupService] Opening {type.Name} (top-level).");
            p.PlayOpen(() => { transitioning = false; Pump(); });
            return p;
        }

        private void Pump()
        {
            if (transitioning) return;
            if (activeStack.Count > 0) return; // something is still on screen → wait
            if (queue.Count == 0) return;
            var next = queue.Dequeue();
            OpenTopLevel(next.Type, next.Configure);
        }

        private BasePopup Spawn(Type type)
        {
            if (!prefabByType.TryGetValue(type, out var prefab) || prefab == null)
            {
                Debug.LogError($"[PopupService] No prefab registered for {type.Name}. " +
                               "Add its prefab to PopupService.prefabs in the inspector.");
                return null;
            }
            var parent = popupRoot != null ? popupRoot : transform;
            var inst = Instantiate(prefab, parent);
            inst.transform.SetAsLastSibling(); // top of the shared canvas (newest popup draws on top)
            inst.Bind(this);
            return inst;
        }

        private static Action<BasePopup> Wrap<T>(Action<T> configure) where T : BasePopup
            => configure == null ? (Action<BasePopup>)null : bp => configure((T)bp);
    }
}
