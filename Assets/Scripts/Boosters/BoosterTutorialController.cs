using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelShoot.Data;
using PixelShoot.Game;
using PixelShoot.UI;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// One-time forced tutorial for a booster: on the level it unlocks (and only if never
    /// shown), everything dims EXCEPT the booster button (raised above the dark overlay and
    /// still tappable) and any extra highlights, plus an explanation panel. Using the
    /// booster closes the tutorial and the use is FREE (no count consumed).
    /// </summary>
    public class BoosterTutorialController : MonoBehaviour
    {
        [SerializeField] private BoosterManager manager;
        [Tooltip("Full-screen dark overlay (raycast target on, so it blocks everything below).")]
        [SerializeField] private GameObject darkOverlay;
        [Tooltip("Instruction panel (child of the tutorial canvas, above the overlay).")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TMP_Text tutorialText;
        [Tooltip("Sorting order applied to highlighted UI so they render ABOVE the dark overlay.")]
        [SerializeField] private int raiseSortingOrder = 500;
        [Tooltip("Extra UI objects to keep lit (raised above the dark). E.g. a UI marker over the conveyor text.")]
        [SerializeField] private List<GameObject> extraHighlights = new List<GameObject>();

        [Header("Spotlight holes (per booster)")]
        [Tooltip("Spotlight overlay whose world-space holes are set per tutorial. If null, the one on the dark overlay is used.")]
        [SerializeField] private SpotlightOverlay spotlight;
        [Tooltip("Which world targets stay LIT (holes) for each booster's tutorial. A booster with no entry (e.g. Claw) gets NO holes — only its button is lit.")]
        [SerializeField] private List<TutorialHoleSet> holeSets = new List<TutorialHoleSet>();

        /// <summary>Per-booster set of world-space targets kept lit (holes) during its tutorial.</summary>
        [System.Serializable]
        public class TutorialHoleSet
        {
            public BoosterData booster;
            public List<RectTransform> holes = new List<RectTransform>();
        }

        private BoosterData activeBooster;
        private readonly List<GameObject> raised = new List<GameObject>();
        private readonly List<bool> raisedAddedCanvas = new List<bool>();

        public bool IsActiveFor(BoosterData data) => activeBooster != null && activeBooster == data;

        /// <summary>Called by a booster button; starts the tutorial if this booster is eligible.</summary>
        public void CheckFor(BoosterButton btn, BoosterData data)
        {
            if (btn == null || data == null || !data.HasTutorial) return;
            if (data.TutorialStyle != BoosterTutorialStyle.Spotlight) return;    // glow-style handled by the button itself
            if (activeBooster != null) return;                                  // one at a time
            if (PlayerProgress.DisplayLevel != data.UnlockLevel) return;         // only on the unlock level
            if (PlayerBoosters.IsTutorialShown(data.Id)) return;                 // once ever
            Begin(btn, data);
        }

        private void Begin(BoosterButton btn, BoosterData data)
        {
            activeBooster = data;
            if (darkOverlay != null) darkOverlay.SetActive(true);
            ApplyHolesFor(data); // per-booster: booster 1 lights the conveyor text, Claw lights nothing
            Shooters.ClickInputRouter.PushSuspend(); // block world bus taps behind the overlay

            Raise(btn.gameObject);
            foreach (var h in extraHighlights) if (h != null) Raise(h);

            if (tutorialText != null) tutorialText.text = data.TutorialText;
            if (tutorialPanel != null) tutorialPanel.SetActive(true);
        }

        /// <summary>The tutorial booster was tapped — use it free and end the tutorial.</summary>
        public void CompleteWithUse(BoosterButton btn, Transform startPoint)
        {
            if (activeBooster == null) return;
            var data = activeBooster;
            End(markShown: true);
            if (manager != null) manager.UseBoosterFree(data, startPoint);
        }

        private void End(bool markShown)
        {
            if (markShown && activeBooster != null) PlayerBoosters.MarkTutorialShown(activeBooster.Id);
            RestoreAll();
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            if (darkOverlay != null) darkOverlay.SetActive(false);
            Shooters.ClickInputRouter.PopSuspend();
            activeBooster = null;
        }

        // Drive the spotlight holes from the active booster: its configured set, or none.
        private void ApplyHolesFor(BoosterData data)
        {
            var spot = spotlight != null ? spotlight
                     : (darkOverlay != null ? darkOverlay.GetComponent<SpotlightOverlay>() : null);
            if (spot == null) return;

            List<RectTransform> holes = null;
            foreach (var set in holeSets)
                if (set != null && set.booster == data) { holes = set.holes; break; }
            spot.SetHoles(holes); // null / empty → fully dark except the raised button
        }

        // Raise a UI object above the dark overlay (temporary overrideSorting canvas + raycaster).
        private void Raise(GameObject go)
        {
            var c = go.GetComponent<Canvas>();
            bool addedCanvas = c == null;
            if (addedCanvas) c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = raiseSortingOrder;
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
            raised.Add(go);
            raisedAddedCanvas.Add(addedCanvas);
        }

        private void RestoreAll()
        {
            for (int i = 0; i < raised.Count; i++)
            {
                var go = raised[i];
                if (go == null) continue;
                if (raisedAddedCanvas[i])
                {
                    var gr = go.GetComponent<GraphicRaycaster>(); if (gr != null) Destroy(gr);
                    var c = go.GetComponent<Canvas>();            if (c != null)  Destroy(c);
                }
                else
                {
                    var c = go.GetComponent<Canvas>(); if (c != null) c.overrideSorting = false;
                }
            }
            raised.Clear();
            raisedAddedCanvas.Clear();
        }
    }
}
