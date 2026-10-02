using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelShoot.UI
{
    /// <summary>
    /// A dark full-screen UI overlay (RawImage + PixelShoot/SpotlightOverlay material) that
    /// punches rectangular "spotlight" holes over target RectTransforms — including
    /// WORLD-space ones (e.g. a world-space TMP), which a plain overlay would cover. Feed it
    /// the targets; it recomputes their screen rects each frame while active.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class SpotlightOverlay : MonoBehaviour
    {
        private const int MaxHoles = 8;
        private static readonly int HolesId     = Shader.PropertyToID("_Holes");
        private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");

        [Tooltip("Camera used to project world targets to the screen. Defaults to Camera.main.")]
        [SerializeField] private Camera cam;
        [Tooltip("Targets kept lit (holes cut around them). Works for UI or world-space RectTransforms.")]
        [SerializeField] private List<RectTransform> holes = new List<RectTransform>();
        [Tooltip("Extra screen-pixel padding around each hole.")]
        [SerializeField] private Vector2 padding = new Vector2(24f, 24f);

        private Material mat;
        private readonly Vector4[] holeData = new Vector4[MaxHoles];
        private readonly Vector3[] corners = new Vector3[4];

        private void Awake()
        {
            var g = GetComponent<Graphic>();
            // Own material instance so we don't write into the shared asset.
            mat = new Material(g.material);
            g.material = mat;
        }

        private void OnEnable() => UpdateHoles();
        private void LateUpdate() => UpdateHoles();

        /// <summary>Replace the highlighted targets at runtime.</summary>
        public void SetHoles(IEnumerable<RectTransform> targets)
        {
            holes.Clear();
            if (targets != null) holes.AddRange(targets);
            UpdateHoles();
        }

        private void UpdateHoles()
        {
            if (mat == null) return;
            var c = cam != null ? cam : Camera.main;
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);

            int n = 0;
            for (int i = 0; i < holes.Count && n < MaxHoles; i++)
            {
                var rt = holes[i];
                if (rt == null) continue;

                rt.GetWorldCorners(corners); // 4 world corners
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    // World-space RectTransform → screen via camera; UI-overlay corners are already screen.
                    Vector3 sp = c != null ? c.WorldToScreenPoint(corners[k]) : corners[k];
                    minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                    minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
                }
                minX -= padding.x; maxX += padding.x;
                minY -= padding.y; maxY += padding.y;
                holeData[n++] = new Vector4(minX / sw, minY / sh, maxX / sw, maxY / sh);
            }

            mat.SetInt(HoleCountId, n);
            if (n > 0) mat.SetVectorArray(HolesId, holeData);
        }

        private void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
