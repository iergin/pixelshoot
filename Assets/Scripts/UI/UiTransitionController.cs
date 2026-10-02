using System;
using UnityEngine;
using DG.Tweening;

namespace PixelShoot.UI
{
    /// <summary>
    /// Owns and drives a set of <see cref="UiTransition"/> groups (e.g. the menu's left / right /
    /// top / bottom button parents) as one unit: play them all IN, all OUT, or snap them hidden.
    /// Callers (MainMenuController, any screen) just say <see cref="PlayIn"/> / <see cref="PlayOut"/>
    /// and get a callback when the OUT finishes — the transition mechanics live here, not in them.
    /// </summary>
    public class UiTransitionController : MonoBehaviour
    {
        [Tooltip("Animated content groups played together (slide + fade). Order doesn't matter.")]
        [SerializeField] private UiTransition[] groups;

        /// <summary>Longest OUT duration across the groups (0 if none).</summary>
        public float OutTime
        {
            get
            {
                float longest = 0f;
                if (groups != null)
                    foreach (var g in groups) if (g != null) longest = Mathf.Max(longest, g.OutTime);
                return longest;
            }
        }

        /// <summary>Snap every group to its hidden pose (no animation).</summary>
        public void SetHidden()
        {
            if (groups == null) return;
            foreach (var g in groups) if (g != null) g.SetHidden();
        }

        /// <summary>Reset to hidden, then animate every group IN.</summary>
        public void PlayIn()
        {
            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g == null) continue;
                g.SetHidden();
                g.PlayIn();
            }
        }

        /// <summary>Animate every group OUT; invoke <paramref name="onComplete"/> once the longest
        /// one finishes (immediately if there are no groups). Time-scale independent.</summary>
        public void PlayOut(Action onComplete = null)
        {
            float longest = 0f;
            if (groups != null)
                foreach (var g in groups)
                {
                    if (g == null) continue;
                    g.PlayOut();
                    longest = Mathf.Max(longest, g.OutTime);
                }

            if (longest > 0f) DOVirtual.DelayedCall(longest, () => onComplete?.Invoke(), ignoreTimeScale: true);
            else onComplete?.Invoke();
        }
    }
}
