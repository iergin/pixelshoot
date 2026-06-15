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

        [SerializeField] private MeshRenderer meshRenderer;
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
        }

        public void ReserveHit() => reservedForHit = true;

        public void TakeHit()
        {
            if (state == BoxState.Hit) return;
            SetState(BoxState.Hit);
        }

        private void ApplyMaterialForState()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            Material m;
            Color baseColor;
            switch (state)
            {
                case BoxState.Locked:
                    m = lockedMat;
                    baseColor = m != null ? ReadColor(m) : UnityEngine.Color.gray;
                    break;
                case BoxState.Frontier:
                    m = unhitMat;
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
            meshRenderer.sharedMaterial = m;
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
            if (meshRenderer == null) return;
            Props.Clear();
            Props.SetColor(BaseColorId, shownColor);
            Props.SetColor(ColorId, shownColor);
            meshRenderer.SetPropertyBlock(Props);
        }

        private void OnDestroy()
        {
            tintTween?.Kill();
        }

        private static Color ReadColor(Material m)
        {
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            return m.color;
        }
    }
}
