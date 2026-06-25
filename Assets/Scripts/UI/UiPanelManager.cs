using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.UI
{
    /// <summary>
    /// Global single-panel coordinator. Guarantees only ONE <see cref="UiPanel"/> is open
    /// at a time and never lets two overlap, whether the panel was opened from the main
    /// menu or from in-game.
    ///
    /// <para><b>Two open modes</b>:
    /// <list type="bullet">
    /// <item><b>Replace</b> (user taps a menu/HUD button): close whatever is open, then open
    /// the requested one.</item>
    /// <item><b>Queue</b> (auto-opened promos): if something is already open, wait — the new
    /// panel opens only after the current one closes, one after another.</item>
    /// </list></para>
    ///
    /// <para>It's a scene-persistent singleton so the same panels are reachable from the menu
    /// and from gameplay. Panels register nothing up-front; they just route their open/close
    /// requests here (see <see cref="UiPanel.RequestOpen"/> / <see cref="UiPanel.RequestClose"/>).</para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class UiPanelManager : MonoBehaviour
    {
        public static UiPanelManager Instance { get; private set; }

        private UiPanel current;
        private bool transitioning;
        private bool wantCloseCurrent;
        private readonly List<UiPanel> queue = new List<UiPanel>();

        public UiPanel Current => current;
        public bool IsAnyOpen => current != null || transitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Request to open a panel. <paramref name="replaceCurrent"/> true → close whatever's
        /// open and show this (user-driven). false → queue behind the current panel and any
        /// already-queued ones (auto promos).
        /// </summary>
        public void Open(UiPanel panel, bool replaceCurrent = true)
        {
            Debug.Log($"[UiPanelManager] Open(panel='{(panel != null ? panel.name : "<null>")}', replace={replaceCurrent}). " +
                      $"current={(current != null ? current.name : "<null>")}, transitioning={transitioning}, queue={queue.Count}.");
            if (panel == null || panel == current) { Debug.Log("[UiPanelManager] Ignored (null or already current)."); return; }
            if (!queue.Contains(panel)) queue.Add(panel);
            if (replaceCurrent && current != null) wantCloseCurrent = true;
            Pump();
        }

        /// <summary>Close a panel (or cancel its queued open). Pumps the queue afterwards.</summary>
        public void Close(UiPanel panel)
        {
            if (panel == null) return;
            if (panel == current) { wantCloseCurrent = true; Pump(); }
            else queue.Remove(panel); // cancel a pending open
        }

        private void Pump()
        {
            if (transitioning) return;

            // Close the current panel if a replace / explicit close asked for it.
            if (current != null)
            {
                if (wantCloseCurrent) CloseCurrentInternal();
                return; // queued panels wait until the current one is gone
            }

            // Nothing open → open the next queued panel.
            if (queue.Count > 0)
            {
                var next = queue[0];
                queue.RemoveAt(0);
                OpenInternal(next);
            }
        }

        private void OpenInternal(UiPanel panel)
        {
            Debug.Log($"[UiPanelManager] OpenInternal('{panel.name}').");
            current = panel;
            transitioning = true;
            panel.PlayOpen(() => { transitioning = false; Pump(); });
        }

        private void CloseCurrentInternal()
        {
            var c = current;
            transitioning = true;
            wantCloseCurrent = false;
            c.PlayClose(() => { current = null; transitioning = false; Pump(); });
        }
    }
}
