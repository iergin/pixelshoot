using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using PixelShoot.Grid;

namespace PixelShoot.Boosters
{
    /// <summary>
    /// Bomb-delivery sibling of <see cref="Cannon"/>: grows from scale 0, then lobs a bomb prefab at
    /// each target (spawned at <see cref="shootPoint"/>, flown with <c>DOJump</c>, optionally spinning),
    /// invoking <c>onBombLanded</c> on arrival so the caller applies the bomb effect. Recoils per shot,
    /// then shrinks away once every bomb has landed. All timings inspector-tunable.
    /// </summary>
    public class BombCannon : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform shootPoint;
        [Tooltip("Bomb visual launched at each target. Destroyed on landing (after onBombLanded).")]
        [SerializeField] private GameObject bombPrefab;

        [Header("Grow / shrink")]
        [SerializeField] private Vector3 openScale = Vector3.one;
        [SerializeField, Min(0.01f)] private float growDuration = 0.3f;
        [SerializeField] private Ease growEase = Ease.OutBack;
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.25f;
        [SerializeField] private Ease shrinkEase = Ease.InBack;

        [Header("Shooting")]
        [SerializeField, Min(0f)] private float shotInterval = 0.15f;
        [SerializeField, Min(0f)] private float jumpPower = 2.5f;
        [SerializeField, Min(0.05f)] private float jumpDuration = 0.6f;
        [SerializeField] private Ease jumpEase = Ease.OutQuad;
        [SerializeField, Min(0.01f)] private float bombScale = 1f;
        [Tooltip("Bomb spin while flying (deg/sec) — the bomb's 'do-jump animation'. 0 = no spin.")]
        [SerializeField] private float bombSpinDegPerSec = 360f;
        [Tooltip("Seconds the FLYING bomb model takes to scale-close away after it lands (the cell's own " +
                 "bomb pops in via MakeBomb). 0 = destroy instantly.")]
        [SerializeField, Min(0f)] private float bombCloseDuration = 0.15f;

        [Header("Shoot recoil (cannon scale-up per shot)")]
        [SerializeField, Min(0f)] private float shootPunch = 0.15f;
        [SerializeField, Min(0.01f)] private float shootPunchDuration = 0.15f;

        [Header("Aim")]
        [Tooltip("Rotate to face each target before firing.")]
        [SerializeField] private bool aimAtTarget = true;
        [Tooltip("Transform that rotates onto the target. Null = the model.")]
        [SerializeField] private Transform aimPivot;
        [Tooltip("Seconds to swing onto a new target.")]
        [SerializeField, Min(0f)] private float aimDuration = 0.15f;
        [Tooltip("Y = yaw only (face horizontally); None = full look-at.")]
        [SerializeField] private AxisConstraint aimAxis = AxisConstraint.Y;

        private Tween aimTween;
        private Tween scaleTween;
        private Tween recoilTween;
        private bool firing;
        private bool warnedNoPrefab;
        public bool IsFiring => firing;

        private void Awake()
        {
            if (model == null) model = transform;
            if (shootPoint == null) shootPoint = transform;
            model.localScale = Vector3.zero;
        }

        /// <summary>Grow, lob a bomb at each target's cell, then shrink. <paramref name="onBombLanded"/>
        /// runs per bomb on landing (apply the effect there); <paramref name="onDone"/> after shrink.</summary>
        public void Fire(IList<Box> targets, GridController grid, Action<Box> onBombLanded, Action onDone = null)
        {
            if (firing) { onDone?.Invoke(); return; }
            StartCoroutine(FireRoutine(targets, grid, onBombLanded, onDone));
        }

        private IEnumerator FireRoutine(IList<Box> targets, GridController grid, Action<Box> onBombLanded, Action onDone)
        {
            firing = true;
            Debug.Log($"[BombCannon] FireRoutine start — targets={targets?.Count ?? 0}, bombPrefab={(bombPrefab != null ? "OK" : "NULL")}, model={(model != null ? "OK" : "NULL")}, shootPoint={(shootPoint != null ? "OK" : "NULL")}");

            if (bombPrefab == null && !warnedNoPrefab)
            {
                warnedNoPrefab = true;
                Debug.LogWarning("[BombCannon] Bomb Prefab is NOT assigned — bombs are applied instantly with no flying model. Assign a bomb prefab to see it lobbed.", this);
            }

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

                    Vector3 endPos = grid != null ? grid.GetCellWorldPosition(box.GridX, box.GridZ) : shootPoint.position;
                    if (aimAtTarget) AimAt(endPos);
                    Recoil();

                    if (bombPrefab != null)
                    {
                        pending++;
                        var bomb = Instantiate(bombPrefab, shootPoint.position, Quaternion.identity);
                        bomb.transform.localScale = Vector3.one * bombScale;
                        var spin = bombSpinDegPerSec != 0f ? bomb.AddComponent<SpinWhileAlive>() : null;
                        if (spin != null) spin.degPerSec = bombSpinDegPerSec;

                        var captured = box;
                        var capturedBomb = bomb;
                        var capturedSpin = spin;
                        bomb.transform.DOJump(endPos, jumpPower, 1, Mathf.Max(0.05f, jumpDuration))
                            .SetEase(jumpEase)
                            .OnComplete(() =>
                            {
                                onBombLanded?.Invoke(captured);          // the cell becomes a bomb (its own model pops)
                                if (capturedSpin != null) Destroy(capturedSpin); // stop the tumble
                                // Close the flying bomb model away, then destroy it.
                                if (bombCloseDuration > 0f)
                                    capturedBomb.transform.DOScale(Vector3.zero, bombCloseDuration)
                                        .SetEase(Ease.InBack)
                                        .OnComplete(() => Destroy(capturedBomb));
                                else
                                    Destroy(capturedBomb);
                                pending--;
                            });
                    }
                    else
                    {
                        onBombLanded?.Invoke(box); // no prefab → apply instantly
                    }

                    if (shotInterval > 0f) yield return WaitUnscaled(shotInterval);
                }
            }

            while (pending > 0) yield return null;

            recoilTween?.Kill();
            scaleTween?.Kill();
            scaleTween = model.DOScale(Vector3.zero, shrinkDuration).SetEase(shrinkEase);
            yield return WaitUnscaled(shrinkDuration);

            firing = false;
            onDone?.Invoke();
        }

        private void AimAt(Vector3 worldPos)
        {
            var pivot = aimPivot != null ? aimPivot : model;
            if (pivot == null) return;
            aimTween?.Kill();
            aimTween = pivot.DOLookAt(worldPos, Mathf.Max(0.001f, aimDuration), aimAxis, Vector3.up);
        }

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

        /// <summary>Spins a transform every frame until destroyed — the flying bomb's tumble.</summary>
        private sealed class SpinWhileAlive : MonoBehaviour
        {
            public float degPerSec = 360f;
            private void Update() => transform.Rotate(0f, 0f, degPerSec * Time.deltaTime);
        }
    }
}
