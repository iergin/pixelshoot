using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Grid
{
    public enum BoxState
    {
        Locked,    // Inside the silhouette. Not shootable. Shown in the shared gray "locked" material.
        Frontier,  // On the silhouette edge. Shootable. Shown in the color's faded BoxUnhitMaterial.
        Hit        // Already destroyed. Stays visible in the color's vivid BoxHitMaterial.
    }

    public class Box : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [Tooltip("Optional small renderer (e.g., a sphere child) that shows a hint of the box's color while it is Locked.")]
        [SerializeField] private MeshRenderer colorDot;

        private ColorData color;
        private BoxState state;
        private bool reservedForHit;
        private Material lockedMat;

        public int GridX { get; private set; }
        public int GridZ { get; private set; }
        public ColorData Color => color;
        public BoxState State => state;
        public bool IsAlive => state != BoxState.Hit;
        // Targetable only while on the frontier and not already promised to an incoming bullet.
        public bool IsShootable => state == BoxState.Frontier && !reservedForHit;

        public void Initialize(int x, int z, ColorData c, Material lockedMaterial)
        {
            GridX = x;
            GridZ = z;
            color = c;
            reservedForHit = false;
            lockedMat = lockedMaterial;
            // The dot reveals the real color of a locked box — uses the vivid unhit material.
            if (colorDot != null && c != null && c.BoxUnhitMaterial != null)
                colorDot.sharedMaterial = c.BoxUnhitMaterial;
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

            Material m = null;
            switch (state)
            {
                case BoxState.Locked: m = lockedMat; break;
                case BoxState.Frontier: m = color != null ? color.BoxUnhitMaterial : null; break;
                case BoxState.Hit: m = color != null ? color.BoxHitMaterial : null; break;
            }
            if (m != null) meshRenderer.sharedMaterial = m;
        }
    }
}
