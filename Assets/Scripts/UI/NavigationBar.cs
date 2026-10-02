using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelShoot.UI
{
    /// <summary>
    /// Bottom navigation bar (Shop / Home / Leaderboard …). Listens to each tab's button; when a
    /// tab is tapped it enables THAT tab's selected image and disables every other tab's, so exactly
    /// one tab reads as selected at a time.
    ///
    /// <para>Add one <see cref="Tab"/> per button in the inspector. <see cref="Select(int)"/> is
    /// public so you can also switch tabs from code or a Button OnClick.</para>
    /// </summary>
    public class NavigationBar : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            [Tooltip("Just a label for the inspector (e.g. Shop / Home / Leaderboard).")]
            public string name;
            [Tooltip("The tab button (BTN_Shop / BTN_Home / BTN_Leaderboard).")]
            public Button button;
            [Tooltip("Selected-state Image — its 'enabled' is turned ON for this tab and OFF for the rest.")]
            public Image highlightImage;
            [Tooltip("Notification badge object — hidden by default; toggle with SetNotification().")]
            public GameObject notification;
        }

        [SerializeField] private Tab[] tabs;
        [Tooltip("Tab shown on start (0 = first). -1 = don't force any.")]
        [SerializeField] private int defaultTab = 1; // Home in the middle, per the hierarchy

        /// <summary>Fired with the selected tab index whenever the active tab changes.</summary>
        public event Action<int> OnTabSelected;

        /// <summary>Index of the currently-selected tab (-1 before the first selection).</summary>
        public int Current { get; private set; } = -1;

        private void Awake()
        {
            if (tabs == null) { Debug.LogWarning("[NavBar] No tabs assigned.", this); return; }
            int wired = 0;
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i];
                if (t?.button == null) { Debug.LogWarning($"[NavBar] Tab {i} ('{t?.name}') has NO button assigned.", this); continue; }
                if (t.highlightImage == null) Debug.LogWarning($"[NavBar] Tab {i} ('{t.name}') has NO highlightImage assigned.", this);
                int idx = i; // capture per-iteration for the closure
                t.button.onClick.RemoveListener(CallbackFor(idx));
                t.button.onClick.AddListener(CallbackFor(idx));
                wired++;
            }
            Debug.Log($"[NavBar] Awake wired {wired}/{tabs.Length} tab buttons.", this);
        }

        // Cache one delegate per index so Remove+Add actually de-dupes on re-Awake.
        private UnityEngine.Events.UnityAction[] cachedCallbacks;
        private UnityEngine.Events.UnityAction CallbackFor(int idx)
        {
            cachedCallbacks ??= new UnityEngine.Events.UnityAction[tabs.Length];
            return cachedCallbacks[idx] ??= () => Select(idx);
        }

        private void Start()
        {
            // Notifications start hidden.
            if (tabs != null)
                foreach (var t in tabs)
                    if (t?.notification != null) t.notification.SetActive(false);

            if (defaultTab >= 0 && defaultTab < (tabs?.Length ?? 0)) Select(defaultTab);
        }

        /// <summary>Show/hide a tab's notification badge (default hidden). Call from wherever a tab
        /// gains/clears something the player should notice.</summary>
        public void SetNotification(int index, bool on)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            if (tabs[index]?.notification != null) tabs[index].notification.SetActive(on);
        }

        /// <summary>Select the tab at <paramref name="index"/> as a USER action: update the highlight
        /// AND fire <see cref="OnTabSelected"/> (e.g. a pager listens and scrolls to that page). Safe
        /// to call from code or a Button OnClick.</summary>
        public void Select(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            Debug.Log($"[NavBar] Select({index}) — '{tabs[index]?.name}'.", this);
            SetHighlight(index);
            OnTabSelected?.Invoke(index);
        }

        /// <summary>Set which tab reads as selected WITHOUT firing <see cref="OnTabSelected"/>. Use this
        /// to reflect state driven elsewhere (e.g. a swipe pager settling on a page) so the highlight
        /// follows the page without looping back into a navigation request.</summary>
        public void SetHighlight(int index)
        {
            if (tabs == null || index < 0 || index >= tabs.Length) return;
            for (int i = 0; i < tabs.Length; i++)
            {
                var img = tabs[i]?.highlightImage;
                if (img == null) continue;
                var c = img.color;
                c.a = (i == index) ? 1f : 0f; // instant, no fade
                img.color = c;
            }
            Current = index;
        }
    }
}
