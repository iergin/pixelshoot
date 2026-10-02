using UnityEngine;
using DG.Tweening;
using PixelShoot.Data;

namespace PixelShoot.Grid
{
    public enum BoxState
    {
        Locked,    // Inside the silhouette. Not shootable. Shown in the shared gray "locked" material.
        Frontier,  // On the silhouette edge. Shootable. Shown in the shared "unhit" material (no color tint).
        Hit        // Already destroyed. Stays visible in the color's BoxHitMaterial.
    }

    public class Box : MonoBehaviour
    {
        // Shader property ids — cached to avoid string lookup every state change.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId     = Shader.PropertyToID("_Color");

        [SerializeField] private MeshRenderer boxMesh;
        [Tooltip("Optional small renderer (e.g., a sphere child) that shows a hint of the box's color while it is Locked.")]
        [SerializeField] private MeshRenderer colorDot;
        [Tooltip("Optional outline/stroke renderer that is enabled ONLY while the box is on the Frontier (shootable). Its material is set to the color's BoxHitMaterial at init.")]
        [SerializeField] private MeshRenderer stroke;
        [Tooltip("Optional visual (e.g. bomb mesh) toggled on for bomb cells.")]
        [SerializeField] private GameObject bombVisual;
        [Tooltip("Local Y the bomb model is lifted to ONLY while the box is Frontier (outermost, " +
                 "shootable). In other states it keeps its prefab-authored default Y.")]
        [SerializeField] private float bombVisualFrontierY = 1.1f;
        [Tooltip("Optional outline child (stencil-mask + BoxOutline materials) enabled ONLY while the box is Hit. Outlines clip against neighbouring hit boxes so shared edges don't double up.")]
        [SerializeField] private GameObject outline;
        [Tooltip("Layer the HINT Free Outline (index 1 in Free Outline Settings) filters. Separate from " +
                 "the shooter/booster outline layer so the two use different Outline entries/colours. " +
                 "SetHintOutline() moves the hint renderers here, then restores them.")]
        [SerializeField] private string hintOutlineLayer = "HintOutline";
        [Tooltip("Renderers whose layer is switched to light the idle-hint outline. Empty = just the box mesh.")]
        [SerializeField] private Renderer[] hintOutlineRenderers;
        [Tooltip("Optional sheen overlay child (BoxSheen additive material) enabled ONLY while the box is Hit, so the looping screen-space shine sweep shimmers across the painted picture.")]
        [SerializeField] private GameObject sheen;
        [Tooltip("Optional particle that plays when this bomb explodes. Instantiated at the bomb position and auto-destroyed by its own ParticleSystem.")]
        [SerializeField] private GameObject explosionParticlePrefab;
        [Tooltip("Duration of the colour fade between state transitions. The material swaps instantly; only the tint colour interpolates.")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.3f;

        [Header("Per-state height")]
        [Tooltip("Root whose vertical scale (localScale.y) is driven by the box state. If null, falls back to this transform. Give it a bottom-pivoted mesh so it grows upward.")]
        [SerializeField] private Transform heightRoot;
        [Tooltip("Optional root whose Y POSITION (localPosition.y) is set to the per-state value. Use it to lift/lower the box independently of its scale.")]
        [SerializeField] private Transform positionRoot;
        [Tooltip("Height (localScale.y) / Y offset while Locked — not yet shootable.")]
        [SerializeField] private float lockedHeight = 0.05f;
        [Tooltip("Height (localScale.y) / Y offset while Frontier — waiting to be shot.")]
        [SerializeField] private float frontierHeight = 1f;
        [Tooltip("Height (localScale.y) / Y offset while Hit — already cleared / coloured.")]
        [SerializeField] private float hitHeight = 0.02f;
        [Tooltip("Seconds for the height/position to tween between states. 0 = snap.")]
        [SerializeField, Min(0f)] private float heightTweenDuration = 0.2f;
        [Tooltip("Scale the height root from its BOTTOM edge instead of its centre — the box grows upward only. Assumes a centre-pivot mesh whose height is meshUnitHeight at scale.y=1.")]
        [SerializeField] private bool scaleFromBottom = true;
        [Tooltip("Local height of the height-root mesh at scale.y = 1 (standard Unity cube = 1). Used to compute the bottom anchor.")]
        [SerializeField] private float meshUnitHeight = 1f;

        [Header("Hit punch")]
        [Tooltip("Transform that plays a quick punch-scale when the box is hit. If null, falls back to this transform. Keep it OFF the height root so it doesn't fight the height tween.")]
        [SerializeField] private Transform punchTarget;
        [Tooltip("Punch strength added to scale on hit (0 = disabled).")]
        [SerializeField, Min(0f)] private float hitPunchScale = 0.2f;
        [Tooltip("Duration of the hit punch-scale.")]
        [SerializeField, Min(0f)] private float hitPunchDuration = 0.2f;
        [Tooltip("How many times the punch oscillates before settling.")]
        [SerializeField, Min(0)] private int hitPunchVibrato = 6;
        [Tooltip("0 = stiff/no overshoot, 1 = springy overshoot.")]
        [SerializeField, Range(0f, 1f)] private float hitPunchElasticity = 0.5f;

        [Header("Hit mesh shrink")]
        [Tooltip("Horizontal (X/Z) scale multiplier applied to the box model once it is Hit — shrink it a touch so the outline reads cleanly around it. 1 = no change. Y (height) is left to the height system.")]
        [SerializeField, Min(0.01f)] private float hitMeshScaleMultiplier = 1f;
        [Tooltip("Seconds to tween the mesh shrink between states. 0 = snap.")]
        [SerializeField, Min(0f)] private float hitMeshScaleDuration = 0.2f;

        [Header("Shootable pulse")]
        [Tooltip("Gently pulse (grow/shrink) shootable Frontier boxes so they read as tappable. Only " +
                 "Frontier boxes pulse; the tween stops the instant a box is locked / hit / key-hidden.")]
        [SerializeField] private bool pulseEnabled = true;
        [Tooltip("Transform the pulse scales. Null = this box's transform.")]
        [SerializeField] private Transform pulseTarget;
        [Tooltip("Scale at the SMALL end of the pulse (e.g. 0.95 = 95%).")]
        [SerializeField, Min(0.01f)] private float pulseScaleMin = 0.95f;
        [Tooltip("Scale at the BIG end of the pulse (e.g. 1.05 = 105%).")]
        [SerializeField, Min(0.01f)] private float pulseScaleMax = 1.05f;
        [Tooltip("Seconds for ONE full grow+shrink cycle (min → max → min). ALL boxes share one central " +
                 "clock, so same-period boxes breathe perfectly in sync regardless of when they became " +
                 "shootable.")]
        [SerializeField, Min(0.05f)] private float pulsePeriod = 2f;

        private Tween heightTween;
        private Tween anchorTween;
        private Tween positionTween;
        private Tween punchTween;
        private Tween meshScaleTween;
        private Tween outlineRevealTween;
        private Vector3 pulseBaseScale = Vector3.one;
        private bool pulseBaseCaptured;
        private bool pulsing;
        private Vector3 punchBaseScale;
        private bool punchBaseCaptured;
        private Vector3 meshBaseScale = Vector3.one;
        private bool meshBaseCaptured;
        private bool hasInitialHeight;
        private Vector3 heightRootBasePos;
        private float heightRootBaseScaleY = 1f;
        private bool heightBaseCaptured;

        private ColorData color;
        private BoxState state;
        private bool reservedForHit;
        private Material lockedMat;
        private Material unhitMat;
        private Tone tone;
        private bool isBomb;
        public GameObject ExplosionParticlePrefab => explosionParticlePrefab;

        // Reused — avoids allocating per state change.
        private MaterialPropertyBlock propsCache;
        private MaterialPropertyBlock Props => propsCache ?? (propsCache = new MaterialPropertyBlock());

        // Tint animation state: the current shown colour follows shownColor; transitions
        // tween from old to new via DOTween. hasInitialTint=false skips the first frame
        // so the spawn-in moment doesn't visibly flash from white to the locked grey.
        private UnityEngine.Color shownColor = UnityEngine.Color.white;
        private bool hasInitialTint;
        private Tween tintTween;

        public int GridX { get; private set; }
        public int GridZ { get; private set; }
        public ColorData Color => color;
        public BoxState State => state;
        public Tone Tone => tone;
        public bool IsBomb => isBomb;
        public int KeyId { get; private set; }
        public bool IsAlive => state != BoxState.Hit;
        // Targetable only while on the frontier, not already promised to an incoming bullet or
        // bomb, AND not hidden under an uncollected key (those boxes are locked away until the
        // key's lock opens — a bullet must not paint a box the player can't even see).
        public bool IsShootable => state == BoxState.Frontier && !reservedForHit && !hiddenByKey;
        public bool IsReserved => reservedForHit;

        public void Initialize(int x, int z, ColorData c, Material lockedMaterial, Material unhitMaterial, Tone cellTone = Tone.Normal, bool bomb = false, int keyId = 0)
        {
            GridX = x;
            GridZ = z;
            color = c;
            reservedForHit = false;
            lockedMat = lockedMaterial;
            unhitMat = unhitMaterial;
            tone = cellTone;
            isBomb = bomb;
            KeyId = keyId;
            ShowBombVisual(bomb);
            // The dot reveals the real color of a locked box — use the per-color Hit material
            // (the only remaining color-tinted material on ColorData).
            if (colorDot != null && c != null && c.BoxHitMaterial != null)
                colorDot.sharedMaterial = c.BoxHitMaterial;
            // Stroke paints the per-color hit material; it only shows while the box is
            // on the frontier (shootable), giving the player a clear color cue.
            if (stroke != null && c != null && c.BoxHitMaterial != null)
                stroke.sharedMaterial = c.BoxHitMaterial;
            SetState(BoxState.Locked);
        }

        public void SetState(BoxState newState)
        {
            state = newState;
            if (newState == BoxState.Hit) reservedForHit = false;

            // While covered by an uncollected key, the box is invisible regardless of state.
            if (hiddenByKey)
            {
                HideKeyVisuals();
            }
            else
            {
                ApplyMaterialForState();
                // Dot is the color hint for locked boxes only — hide once the box can be shot or has been shot.
                if (colorDot != null && colorDot.gameObject.activeSelf != (newState == BoxState.Locked))
                    colorDot.gameObject.SetActive(newState == BoxState.Locked);
                // Stroke is the shootable-state cue — only on while Frontier.
                if (stroke != null && stroke.gameObject.activeSelf != (newState == BoxState.Frontier))
                    stroke.gameObject.SetActive(newState == BoxState.Frontier);
                // Bomb model: on for bomb cells until they detonate (state == Hit).
                ShowBombVisual(isBomb && newState != BoxState.Hit);
                // Outline turns OFF immediately for any non-Hit state. For Hit it is revealed
                // only AFTER the hit scale movements (punch + height + mesh shrink) settle —
                // see ScheduleOutlineReveal.
                if (newState != BoxState.Hit)
                {
                    outlineRevealTween?.Kill(); outlineRevealTween = null;
                    if (outline != null && outline.activeSelf) outline.SetActive(false);
                }
                // Sheen overlay: also Hit-only, so only painted cells catch the looping shine.
                if (sheen != null && sheen.activeSelf != (newState == BoxState.Hit))
                    sheen.SetActive(newState == BoxState.Hit);
            }

            // (Hit sound is played in TakeHit, which knows whether a bomb opened it.)

            // Shootable pulse: on only while this box is a live Frontier target.
            UpdatePulse();

            // On hit: punch FIRST, then apply height + mesh scale, then reveal the
            // outline once those tweens finish. Other transitions apply immediately.
            if (newState == BoxState.Hit)
            {
                if (TryPlayHitPunch(ApplyHitTransformsThenOutline)) return;
                ApplyHitTransformsThenOutline();
                return;
            }
            ApplyStateTransforms();
        }

        // ── Key cover: while an uncollected key sits on this cell, the box exists in the
        // grid (counts toward the win, participates in frontier) but shows nothing — the
        // floating key visual stands in for it. Revealed when the key is collected. ──
        private bool hiddenByKey;
        public bool IsHiddenByKey => hiddenByKey;

        /// <summary>Hide or reveal this box because a key is (un)covering it. Purely visual;
        /// state / gameplay are untouched. Revealing re-applies the current state's visuals.</summary>
        public void SetKeyHidden(bool hidden)
        {
            hiddenByKey = hidden;
            if (boxMesh != null) boxMesh.enabled = !hidden;
            if (hidden) HideKeyVisuals();
            else SetState(state); // restore the state-appropriate visuals now that it's exposed
        }

        private void HideKeyVisuals()
        {
            if (boxMesh != null) boxMesh.enabled = false;
            if (colorDot != null && colorDot.gameObject.activeSelf) colorDot.gameObject.SetActive(false);
            if (stroke != null && stroke.gameObject.activeSelf) stroke.gameObject.SetActive(false);
            if (bombVisual != null && bombVisual.activeSelf) bombVisual.SetActive(false);
            if (outline != null && outline.activeSelf) outline.SetActive(false);
            if (sheen != null && sheen.activeSelf) sheen.SetActive(false);
        }

        /// <summary>Turn an already-spawned box into a BOMB at runtime (streak gift). Pops the bomb
        /// model in. No-op if it's already a bomb or has been hit. Returns true if it became a bomb.</summary>
        /// <summary>True if this bomb was placed by the streak gift (vs authored). Streak bombs
        /// skip linked-colour cells when they detonate so they can't strand a linked pair.</summary>
        public bool IsStreakBomb { get; private set; }

        public bool MakeBomb()
        {
            if (isBomb || state == BoxState.Hit) return false;
            isBomb = true;
            IsStreakBomb = true;
            if (bombVisual != null)
            {
                ShowBombVisual(true);
                if (Application.isPlaying)
                {
                    var t = bombVisual.transform;
                    Vector3 target = t.localScale;
                    t.localScale = Vector3.zero;
                    t.DOScale(target, 0.3f).SetEase(Ease.OutBack);
                }
            }
            return true;
        }

        /// <summary>Temporarily hide (or restore) the Hit-state outline. The idle hint hides all Hit
        /// outlines while it is up so the two outline systems don't overlap / fight (the Hit outline's
        /// stencil mask otherwise blocks the Free Outline). Restoring only re-shows it if still Hit.</summary>
        public void SetHitOutlineVisible(bool visible)
        {
            if (outline == null) return;
            if (visible)
            {
                if (state == BoxState.Hit && !hiddenByKey && !outline.activeSelf) outline.SetActive(true);
            }
            else if (outline.activeSelf)
            {
                outline.SetActive(false);
            }
        }

        // ── Idle hint outline (Free Outline layer, same trick as the shooter claw highlight) ──
        private int hintLayerIdx = -2;
        private Renderer[] hintRends;
        private int[] hintOrigLayers;

        /// <summary>Light the box's outline (idle hint) by moving the hint renderers onto the Free
        /// Outline layer; restore their original layers when off.</summary>
        public void SetHintOutline(bool on)
        {
            if (hintLayerIdx == -2) hintLayerIdx = LayerMask.NameToLayer(hintOutlineLayer);
            if (hintLayerIdx < 0) return; // layer doesn't exist

            if (hintRends == null)
            {
                if (hintOutlineRenderers != null && hintOutlineRenderers.Length > 0)
                    hintRends = hintOutlineRenderers;
                else
                {
                    if (boxMesh == null) boxMesh = GetComponent<MeshRenderer>();
                    hintRends = boxMesh != null ? new Renderer[] { boxMesh } : new Renderer[0];
                }
                hintOrigLayers = new int[hintRends.Length];
                for (int i = 0; i < hintRends.Length; i++)
                    if (hintRends[i] != null) hintOrigLayers[i] = hintRends[i].gameObject.layer;
            }

            for (int i = 0; i < hintRends.Length; i++)
            {
                if (hintRends[i] == null) continue;
                hintRends[i].gameObject.layer = on ? hintLayerIdx : hintOrigLayers[i];
            }
        }

        private bool bombBaseYCaptured;
        private float bombBaseY; // prefab-authored default Y, used in every state except Frontier

        // Show/hide the bomb model. When shown, lift it to bombVisualFrontierY ONLY while Frontier
        // (outermost / shootable); otherwise sit it at its default authored Y.
        private void ShowBombVisual(bool show)
        {
            if (bombVisual == null) return;
            if (!bombBaseYCaptured) { bombBaseY = bombVisual.transform.localPosition.y; bombBaseYCaptured = true; }
            if (show)
            {
                float y = state == BoxState.Frontier ? bombVisualFrontierY : bombBaseY;
                var p = bombVisual.transform.localPosition;
                bombVisual.transform.localPosition = new Vector3(p.x, y, p.z);
            }
            if (bombVisual.activeSelf != show) bombVisual.SetActive(show);
        }

        private void ApplyStateTransforms()
        {
            ApplyHeightForState();
            ApplyMeshScaleForState();
        }

        private void ApplyHitTransformsThenOutline()
        {
            ApplyStateTransforms();
            ScheduleOutlineReveal();
        }

        /// <summary>Turn the outline on once the height / mesh-scale tweens have finished.</summary>
        private void ScheduleOutlineReveal()
        {
            if (outline == null || hiddenByKey) return;
            outlineRevealTween?.Kill(); outlineRevealTween = null;

            float delay = Mathf.Max(heightTweenDuration, hitMeshScaleDuration);
            if (!Application.isPlaying || delay <= 0f)
            {
                if (!outline.activeSelf) outline.SetActive(true);
                return;
            }
            outlineRevealTween = DOVirtual.DelayedCall(delay, () =>
            {
                if (this != null && outline != null && state == BoxState.Hit && !outline.activeSelf)
                    outline.SetActive(true);
            }, ignoreTimeScale: true);
        }

        /// <summary>
        /// Shrink the box model horizontally (X/Z) by <see cref="hitMeshScaleMultiplier"/>
        /// while Hit so the outline has room; restore full size in every other state.
        /// Y is left untouched (the height system owns it).
        /// </summary>
        private void ApplyMeshScaleForState()
        {
            if (boxMesh == null) boxMesh = GetComponent<MeshRenderer>();
            if (boxMesh == null) return;
            var t = boxMesh.transform;

            bool first = !meshBaseCaptured;
            if (!meshBaseCaptured) { meshBaseScale = t.localScale; meshBaseCaptured = true; }

            float m = state == BoxState.Hit ? hitMeshScaleMultiplier : 1f;

            meshScaleTween?.Kill(); meshScaleTween = null;
            bool instant = first || hitMeshScaleDuration <= 0f || !Application.isPlaying;
            if (instant) { SetMeshXZ(m); return; }

            // Tween ONLY a scalar multiplier and apply it to X/Z each frame — never touch Y.
            // boxMesh and heightRoot can be the same transform, so the height system owns Y;
            // a full DOScale here would fight it and pin the box at the wrong height.
            float startM = Mathf.Abs(meshBaseScale.x) > 1e-5f ? t.localScale.x / meshBaseScale.x : 1f;
            meshScaleTween = DOTween.To(() => startM, SetMeshXZ, m, hitMeshScaleDuration).SetEase(Ease.OutBack);
        }

        /// <summary>Apply the horizontal multiplier to X/Z, leaving Y (height) as-is.</summary>
        private void SetMeshXZ(float m)
        {
            if (boxMesh == null) return;
            var t = boxMesh.transform;
            var s = t.localScale;
            t.localScale = new Vector3(meshBaseScale.x * m, s.y, meshBaseScale.z * m);
        }

        /// <summary>
        /// Quick punch-scale on the punch target — the box's "got hit" feedback.
        /// Returns true if a punch was started (and <paramref name="onComplete"/> will run
        /// when it finishes); false if punching is disabled / not in play mode.
        /// </summary>
        private bool TryPlayHitPunch(System.Action onComplete)
        {
            if (!Application.isPlaying) return false;
            if (hitPunchScale <= 0f || hitPunchDuration <= 0f) return false;

            var t = punchTarget != null ? punchTarget : transform;
            if (!punchBaseCaptured) { punchBaseScale = t.localScale; punchBaseCaptured = true; }

            punchTween?.Kill();
            t.localScale = punchBaseScale; // start clean so repeated hits don't stack
            punchTween = t.DOPunchScale(Vector3.one * hitPunchScale, hitPunchDuration, hitPunchVibrato, hitPunchElasticity)
                          .SetUpdate(false)
                          .OnComplete(() => onComplete?.Invoke());
            return true;
        }

        public void ReserveHit() => reservedForHit = true;

        public void TakeHit(bool fromBomb = false)
        {
            if (state == BoxState.Hit) return;
            // Play the clear sound here (SetState is also used for non-hit transitions):
            // bomb blasts get their own clip, stickman hits the normal one.
            var am = PixelShoot.Audio.AudioManager.Instance;
            if (am != null)
            {
                if (fromBomb) am.PlayBoxHitBomb();
                else          am.PlayBoxHit();
            }
            SetState(BoxState.Hit);
        }

        private void ApplyMaterialForState()
        {
            if (boxMesh == null) boxMesh = GetComponent<MeshRenderer>();
            if (boxMesh == null) return;

            Material m;
            Color baseColor;
            switch (state)
            {
                case BoxState.Locked:
                    m = lockedMat;
                    baseColor = m != null ? ReadColor(m) : UnityEngine.Color.gray;
                    break;
                case BoxState.Frontier:
                    // Frontier (shootable, stroke shown) → box model takes the per-color
                    // Hit material too, so it reads in its real colour like the stroke.
                    m = color != null && color.BoxHitMaterial != null ? color.BoxHitMaterial : unhitMat;
                    baseColor = m != null ? ReadColor(m) : UnityEngine.Color.gray;
                    break;
                case BoxState.Hit:
                    m = color != null ? color.BoxHitMaterial : null;
                    baseColor = m != null ? ReadColor(m) : (color != null ? color.DisplayColor : UnityEngine.Color.white);
                    break;
                default:
                    return;
            }
            if (m == null) return;

            // Material swap is instant; only the per-cell tint colour interpolates.
            boxMesh.sharedMaterial = m;
            Color target = ToneShifter.Apply(baseColor, tone);

            // Kill an in-flight tween before starting a new one — picks up from current shownColor.
            tintTween?.Kill();
            tintTween = null;

            // First call (initial Locked state at spawn) OR edit mode preview OR duration=0 → snap.
            bool instant = !hasInitialTint || transitionDuration <= 0f || !Application.isPlaying;
            if (instant)
            {
                shownColor = target;
                hasInitialTint = true;
                WriteTint();
                return;
            }

            tintTween = DOTween.To(
                () => shownColor,
                v => { shownColor = v; WriteTint(); },
                target,
                transitionDuration
            ).SetEase(Ease.OutQuad);
        }

        private void WriteTint()
        {
            if (boxMesh == null) return;
            Props.Clear();
            Props.SetColor(BaseColorId, shownColor);
            Props.SetColor(ColorId, shownColor);
            boxMesh.SetPropertyBlock(Props);
        }

        /// <summary>
        /// Drive the per-state value: scale the height root's Y and offset the position
        /// root's local Y to the same value (locked / frontier / hit).
        /// </summary>
        private void ApplyHeightForState()
        {
            float h;
            switch (state)
            {
                case BoxState.Locked:   h = lockedHeight;   break;
                case BoxState.Frontier: h = frontierHeight; break;
                case BoxState.Hit:      h = hitHeight;      break;
                default: return;
            }

            heightTween?.Kill();   heightTween = null;
            anchorTween?.Kill();   anchorTween = null;
            positionTween?.Kill(); positionTween = null;

            // Snap on the initial Locked setup at spawn, in edit mode, or when duration is 0.
            bool instant = !hasInitialHeight || heightTweenDuration <= 0f || !Application.isPlaying;
            hasInitialHeight = true;

            // Height root → scale.y (optionally anchored to its bottom edge so it grows upward).
            var ht = heightRoot != null ? heightRoot : transform;
            if (!heightBaseCaptured)
            {
                heightRootBasePos = ht.localPosition;
                heightRootBaseScaleY = ht.localScale.y;
                heightBaseCaptured = true;
            }

            Vector3 s = ht.localScale;
            // Bottom of the design pose: where the mesh's lower edge sits at the prefab scale.
            float designBottom = heightRootBasePos.y - heightRootBaseScaleY * meshUnitHeight * 0.5f;
            // Position that keeps that bottom fixed for the new scale.
            float anchoredY = designBottom + h * meshUnitHeight * 0.5f;

            if (instant)
            {
                ht.localScale = new Vector3(s.x, h, s.z);
                if (scaleFromBottom)
                    ht.localPosition = new Vector3(heightRootBasePos.x, anchoredY, heightRootBasePos.z);
            }
            else
            {
                heightTween = ht.DOScaleY(h, heightTweenDuration).SetEase(Ease.OutBack);
                if (scaleFromBottom)
                    anchorTween = ht.DOLocalMoveY(anchoredY, heightTweenDuration).SetEase(Ease.OutBack);
            }

            // Position root → localPosition.y (independent of the height root).
            if (positionRoot != null)
            {
                Vector3 p = positionRoot.localPosition;
                if (instant) positionRoot.localPosition = new Vector3(p.x, h, p.z);
                else         positionTween = positionRoot.DOLocalMoveY(h+0.1f, heightTweenDuration).SetEase(Ease.OutBack);
            }
        }

        private void OnDestroy()
        {
            tintTween?.Kill();
            heightTween?.Kill();
            anchorTween?.Kill();
            positionTween?.Kill();
            punchTween?.Kill();
            meshScaleTween?.Kill();
            outlineRevealTween?.Kill();
            if (pulsing) BoxPulseDriver.Remove(this);
        }

        // ── Shootable pulse ──────────────────────────────────────────────────
        // Gentle breathing scale on Frontier (shootable) boxes. Started/stopped by SetState via
        // UpdatePulse(); only live on the moving frontier, so at most a few dozen tweens run at once.
        private void UpdatePulse()
        {
            if (pulseEnabled && state == BoxState.Frontier && !hiddenByKey) StartPulse();
            else StopPulse();
        }

        private void StartPulse()
        {
            if (pulsing) return;
            var t = pulseTarget != null ? pulseTarget : transform;
            if (!pulseBaseCaptured) { pulseBaseScale = t.localScale; pulseBaseCaptured = true; }
            pulsing = true;
            BoxPulseDriver.Add(this); // one central clock drives every pulsing box in sync
        }

        private void StopPulse()
        {
            if (!pulsing) return;
            pulsing = false;
            BoxPulseDriver.Remove(this);
            if (pulseBaseCaptured)
            {
                var t = pulseTarget != null ? pulseTarget : transform;
                t.localScale = pulseBaseScale; // snap back to the exact base scale
            }
        }

        /// <summary>Called every frame by the shared <see cref="BoxPulseDriver"/> while this box is a live
        /// Frontier target. Scale is derived from ABSOLUTE time, so all same-period boxes are in phase.</summary>
        internal void PulseTick()
        {
            if (!pulsing) return;
            var t = pulseTarget != null ? pulseTarget : transform;
            // Sinusoidal 0..1 from absolute time → shared phase across all boxes (starts at 0 = min).
            float phase = Time.time * (Mathf.PI * 2f / Mathf.Max(0.05f, pulsePeriod)) - Mathf.PI * 0.5f;
            float t01 = (Mathf.Sin(phase) + 1f) * 0.5f;
            t.localScale = pulseBaseScale * Mathf.Lerp(pulseScaleMin, pulseScaleMax, t01);
        }

        private static Color ReadColor(Material m)
        {
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return m.color;
        }
    }

    /// <summary>
    /// One shared driver that ticks EVERY pulsing (Frontier) box each frame from a single clock, so
    /// their grow/shrink stays perfectly in phase — instead of each box running its own loop from its
    /// own start time. Lazily spawns a hidden updater; iterates only the active (frontier) boxes.
    /// </summary>
    internal sealed class BoxPulseDriver : MonoBehaviour
    {
        private static BoxPulseDriver instance;
        private static readonly System.Collections.Generic.List<Box> active = new System.Collections.Generic.List<Box>();

        internal static void Add(Box b)
        {
            if (b == null || active.Contains(b)) return;
            active.Add(b);
            if (instance == null)
            {
                var go = new GameObject("[BoxPulseDriver]") { hideFlags = HideFlags.HideAndDontSave };
                instance = go.AddComponent<BoxPulseDriver>();
            }
        }

        internal static void Remove(Box b) => active.Remove(b);

        private void LateUpdate()
        {
            // Iterate backwards so a null/destroyed entry can be pruned safely.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                if (b == null) { active.RemoveAt(i); continue; }
                b.PulseTick();
            }
        }
    }
}
