using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Grid
{
    public class Box : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;

        private ColorData color;
        private bool hit;
        private bool reservedForHit;

        public int GridX { get; private set; }
        public int GridZ { get; private set; }
        public ColorData Color => color;
        public bool IsAlive => !hit;
        public bool IsTargetable => IsAlive && !reservedForHit;

        public void Initialize(int x, int z, ColorData c)
        {
            GridX = x;
            GridZ = z;
            color = c;
            hit = false;
            reservedForHit = false;
            ApplyMaterial(c.BoxUnhitMaterial);
        }

        public void ReserveHit() => reservedForHit = true;

        public void TakeHit()
        {
            if (hit) return;
            hit = true;
            reservedForHit = false;
            ApplyMaterial(color.BoxHitMaterial);
        }

        private void ApplyMaterial(Material m)
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && m != null) meshRenderer.sharedMaterial = m;
        }
    }
}
