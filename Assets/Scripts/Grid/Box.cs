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

        private Tween heightTween;
        private Tween anchorTween;
        private Tween positionTween;
        private Tween punchTween;
        private Vector3 punchBaseScale;
        private bool punchBaseCaptured;
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
        // Targetable only while on the frontier and not already promised to an incoming bullet
        // or to an in-flight bomb explosion.
        public bool IsShootable => state == BoxState.Frontier && !reservedForHit;
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
            if (bombVisual != null) bombVisual.SetActive(bomb);
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
            ApplyMaterialForState();
            ApplyHeightForState();
            // Dot is the color hint for locked boxes only — hide once the box can be shot or has been shot.
            if (colorDot != null && colorDot.gameObject.activeSelf != (newState == BoxState.Locked))
                colorDot.gameObject.SetActive(newState == BoxState.Locked);
            // Stroke is the shootable-state cue — only on while Frontier.
            if (stroke != null && stroke.gameObject.activeSelf != (newState == BoxState.Frontier))
                stroke.gameObject.SetActive(newState == BoxState.Frontier);
            // Bomb model: on for bomb cells until they detonate (state == Hit).
            if (bombVisual != null)
            {
                bool shouldShow = isBomb && newState != BoxState.Hit;
                if (bombVisual.activeSelf != shouldShow) bombVisual.SetActive(shouldShow);
            }
            // Impact feedback: a quick punch-scale when the box is actually hit.
            if (newState == BoxState.Hit) PlayHitPunch();
        }

        /// <summary>Quick punch-scale on the punch target — the box's "got hit" feedback.</summary>
        private void PlayHitPunch()
        {
            if (!Application.isPlaying) return;
            if (hitPunchScale <= 0f || hitPunchDuration <= 0f) return;

            var t = punchTarget != null ? punchTarget : transform;
            if (!punchBaseCaptured) { punchBaseScale = t.localScale; punchBaseCaptured = true; }

            punchTween?.Kill();
            t.localScale = punchBaseScale; // start clean so repeated hits don't stack
            punchTween = t.DOPunchScale(Vector3.one * hitPunchScale, hitPunchDuration, hitPunchVibrato, hitPunchElasticity)
                          .SetUpdate(false);
        }

        public void ReserveHit() => reservedForHit = true;

        public void TakeHit()
        {
            if (state == BoxState.Hit) return;
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
        }

        private static Color ReadColor(Material m)
        {
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return m.color;
        }
    }
}
