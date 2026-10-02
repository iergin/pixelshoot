using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using PixelShoot.Grid;
using PixelShoot.Shooters;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// A powerup delivery cannon. On <see cref="Fire"/> it grows from scale 0, then launches a stickman
    /// at each target box (spawned at <see cref="shootPoint"/>, flown with <c>DOJump</c> + its jump
    /// animation, applying the hit on landing). Each shot gives the cannon a little recoil scale-up.
    /// When every stickman has landed the cannon shrinks back to 0.
    ///
    /// <para>All timings / the jump + recoil feel are inspector-tunable. The stickman's jump animation
    /// trigger is configured on the <see cref="Stickman"/> prefab itself.</para>
    /// </summary>
    public class Cannon : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The cannon model that scales 0 → open → 0. If null, this transform is used.")]
        [SerializeField] private Transform model;
        [Tooltip("Where stickmen spawn from (the muzzle). If null, this transform is used.")]
        [SerializeField] private Transform shootPoint;
        [Tooltip("Stickman prefab launched at each target (pooled).")]
        [SerializeField] private Stickman stickmanPrefab;

        [Header("Grow / shrink")]
        [Tooltip("Open scale the model grows to.")]
        [SerializeField] private Vector3 openScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float growDuration = 0.3f;
        [SerializeField] private Ease growEase = Ease.OutBack;
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.25f;
        [SerializeField] private Ease shrinkEase = Ease.InBack;

        [Header("Shooting")]
        [Tooltip("Seconds between consecutive shots.")]
        [SerializeField, Min(0f)] private float shotInterval = 0.15f;
        [Tooltip("DOJump arc height for the launched stickman.")]
        [SerializeField, Min(0f)] private float jumpPower = 2f;
        [Tooltip("Seconds each stickman takes to reach its target.")]
        [SerializeField, Min(0.05f)] private float jumpDuration = 0.6f;
        [SerializeField] private Ease jumpEase = Ease.OutQuad;
        [Tooltip("Scale stickmen are launched at.")]
        [SerializeField, Min(0.01f)] private float stickmanScale = 1f;

        [Header("Shoot recoil (cannon scale-up per shot)")]
        [Tooltip("Extra scale punched onto the model each time it fires (0 = no recoil).")]
        [SerializeField, Min(0f)] private float shootPunch = 0.15f;
        [SerializeField, Min(0.01f)] private float shootPunchDuration = 0.15f;

        [Header("Aim")]
        [Tooltip("Rotate to face each target before firing.")]
        [SerializeField] private bool aimAtTarget = true;
        [Tooltip("Transform that rotates onto the target. Null = the model (so its shoot point aims too).")]
        [SerializeField] private Transform aimPivot;
        [Tooltip("Seconds to swing onto a new target.")]
        [SerializeField, Min(0f)] private float aimDuration = 0.15f;
        [Tooltip("Y = yaw only (face horizontally); None = full look-at.")]
        [SerializeField] private AxisConstraint aimAxis = AxisConstraint.Y;

        [Header("First-use tutorial")]
        [Tooltip("Shown the FIRST time this cannon fires. Tap anywhere (via the catcher) to close it — " +
                 "THEN the cannon process starts.")]
        [SerializeField] private GameObject tutorial;
        [Tooltip("Full-screen (transparent) button catching a tap anywhere to dismiss the tutorial + fire.")]
        [SerializeField] private Button tutorialTapCatcher;
        [Tooltip("Unique key for the 'seen' flag (e.g. 'Cannon').")]
        [SerializeField] private string tutorialKey = "Cannon";

        private Tween aimTween;
        private Tween scaleTween;
        private Tween recoilTween;
        private bool firing;

        private string TutorialPrefsKey => "PixelShoot.CannonTutorialShown." + tutorialKey;

        /// <summary>True while a fire sequence is running (grow → shots → shrink).</summary>
        public bool IsFiring => firing;

        private void Awake()
        {
            if (model == null) model = transform;
            if (shootPoint == null) shootPoint = transform;
            model.localScale = Vector3.zero; // start hidden
        }

        /// <summary>Grow, launch a stickman at every alive target, then shrink. <paramref name="onDone"/>
        /// fires after the cannon has fully shrunk away.</summary>
        public void Fire(IList<Box> targets, GridController grid, Action onDone = null)
        {
            if (firing) { onDone?.Invoke(); return; }
            firing = true;

            // First time this cannon is used → show its tutorial and wait for a tap, THEN fire.
            if (tutorial != null && PlayerPrefs.GetInt(TutorialPrefsKey, 0) == 0)
                ShowTutorialThenFire(targets, grid, onDone);
            else
                StartCoroutine(FireRoutine(targets, grid, onDone));
        }

        private void ShowTutorialThenFire(IList<Box> targets, GridController grid, Action onDone)
        {
            tutorial.SetActive(true);
            if (tutorialTapCatcher == null)
            {
                // No tap catcher wired → can't wait for a tap; proceed straight to firing.
                MarkTutorialShown();
                tutorial.SetActive(false);
                StartCoroutine(FireRoutine(targets, grid, onDone));
                return;
            }
            tutorialTapCatcher.gameObject.SetActive(true);
            tutorialTapCatcher.onClick.RemoveAllListeners();
            tutorialTapCatcher.onClick.AddListener(() =>
            {
                MarkTutorialShown();
                tutorial.SetActive(false);
                tutorialTapCatcher.gameObject.SetActive(false);
                StartCoroutine(FireRoutine(targets, grid, onDone)); // tap → close → start the cannon
            });
        }

        private void MarkTutorialShown() { PlayerPrefs.SetInt(TutorialPrefsKey, 1); PlayerPrefs.Save(); }

        private IEnumerator FireRoutine(IList<Box> targets, GridController grid, Action onDone)
        {
            firing = true;

            // Grow in.
            scaleTween?.Kill();
            model.localScale = Vector3.zero;
            scaleTween = model.DOScale(openScale, growDuration).SetEase(growEase);
            yield return WaitUnscaled(growDuration);

            int pending = 0;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var box = targets[i];
                    if (box == null || !box.IsAlive) continue;

                    if (aimAtTarget && grid != null) AimAt(grid.GetCellWorldPosition(box.GridX, box.GridZ));
                    Recoil();

                    var s = stickmanPrefab != null ? StickmanPool.Get(stickmanPrefab, null) : null;
                    if (s != null)
                    {
                        s.transform.localScale = Vector3.one * stickmanScale;
                        s.SetColor(box.Color);
                        pending++;
                        s.LaunchJump(shootPoint.position, box, grid, jumpPower, jumpDuration, jumpEase, () => pending--);
                    }
                    else if (grid != null && box.IsAlive)
                    {
                        grid.NotifyBoxHit(box); // no prefab → apply instantly
                    }

                    if (shotInterval > 0f) yield return WaitUnscaled(shotInterval);
                }
            }

            // Wait for the last stickman to land before packing up.
            while (pending > 0) yield return null;

            recoilTween?.Kill();
            scaleTween?.Kill();
            scaleTween = model.DOScale(Vector3.zero, shrinkDuration).SetEase(shrinkEase);
            yield return WaitUnscaled(shrinkDuration);

            firing = false;
            onDone?.Invoke();
        }

        // Rotate the aim pivot onto the target. Only the aim (rotation) tween is killed — never the
        // scale tweens on the model (so aiming and grow/recoil/shrink don't fight even if pivot == model).
        private void AimAt(Vector3 worldPos)
        {
            var pivot = aimPivot != null ? aimPivot : model;
            if (pivot == null) return;
            aimTween?.Kill();
            aimTween = pivot.DOLookAt(worldPos, Mathf.Max(0.001f, aimDuration), aimAxis, Vector3.up);
        }

        // A quick scale-up recoil on each shot; always returns to openScale.
        private void Recoil()
        {
            if (shootPunch <= 0f) return;
            recoilTween?.Kill();
            model.localScale = openScale;
            recoilTween = model.DOPunchScale(Vector3.one * shootPunch, shootPunchDuration, 4, 0.6f)
                 .OnComplete(() => model.localScale = openScale);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }
    }
}
