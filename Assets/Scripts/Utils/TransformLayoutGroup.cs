using System.Collections.Generic;
using UnityEngine;

namespace PixelShoot.Utils
{
    /// <summary>
    /// A 3D-Transform equivalent of UGUI's GridLayoutGroup. Drop it on a parent and its
    /// child transforms snap into a tidy line or grid laid out on a chosen plane. Runs in
    /// edit mode so you see the arrangement while authoring, and re-arranges automatically
    /// when children are added/removed or fields change. Call <see cref="Arrange"/> at
    /// runtime after spawning/removing children.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TransformLayoutGroup : MonoBehaviour
    {
        public enum Plane { XZ, XY, YZ }      // which two local axes the layout spans
        public enum Mode { Grid, Horizontal, Vertical }

        [Header("Layout")]
        [Tooltip("Plane the children are laid out on (XZ = flat on the ground, the usual top-down choice).")]
        [SerializeField] private Plane plane = Plane.XZ;
        [SerializeField] private Mode mode = Mode.Grid;
        [Tooltip("Grid only: number of columns (Horizontal fill) or rows (Vertical fill) before wrapping.")]
        [SerializeField, Min(1)] private int constraintCount = 3;
        [Tooltip("Grid only: fill row-by-row (Horizontal) or column-by-column (Vertical).")]
        [SerializeField] private bool fillRowByRow = true;

        [Header("Spacing")]
        [Tooltip("Center-to-center step. x = along the primary axis, y = along the secondary axis.")]
        [SerializeField] private Vector2 spacing = new Vector2(1.1f, 1.1f);

        [Header("Alignment")]
        [Tooltip("Center the whole block around the parent's local origin (else it starts at the origin and grows out).")]
        [SerializeField] private bool centered = true;
        [Tooltip("Flip the primary-axis direction (columns).")]
        [SerializeField] private bool flipPrimary;
        [Tooltip("Flip the secondary-axis direction (rows). Default rows grow toward -axis so it reads top→bottom.")]
        [SerializeField] private bool flipSecondary = true;

        [Header("Options")]
        [Tooltip("Include inactive children in the layout.")]
        [SerializeField] private bool includeInactive;
        [Tooltip("Also zero each child's local rotation.")]
        [SerializeField] private bool resetRotation;

        private readonly List<Transform> buffer = new List<Transform>();

        private void OnEnable() => Arrange();
        private void OnTransformChildrenChanged() => Arrange();
#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += SafeArrange;
        private void SafeArrange() { if (this != null) Arrange(); }
#endif

        [ContextMenu("Arrange Now")]
        public void Arrange()
        {
            CollectChildren();
            int n = buffer.Count;
            if (n == 0) return;

            // Work out the grid dimensions.
            int cols, rows;
            switch (mode)
            {
                case Mode.Horizontal: cols = n; rows = 1; break;
                case Mode.Vertical:   cols = 1; rows = n; break;
                default: // Grid
                    if (fillRowByRow) { cols = Mathf.Max(1, constraintCount); rows = Mathf.CeilToInt(n / (float)cols); }
                    else              { rows = Mathf.Max(1, constraintCount); cols = Mathf.CeilToInt(n / (float)rows); }
                    break;
            }

            // Centering offset.
            float u0 = centered ? -(cols - 1) * spacing.x * 0.5f : 0f;
            float v0 = centered ? -(rows - 1) * spacing.y * 0.5f : 0f;
            float us = flipPrimary   ? -1f : 1f;
            float vs = flipSecondary ? -1f : 1f;

            for (int i = 0; i < n; i++)
            {
                int col, row;
                if (mode == Mode.Horizontal)                 { col = i;        row = 0; }
                else if (mode == Mode.Vertical)              { col = 0;        row = i; }
                else if (fillRowByRow)                       { col = i % cols; row = i / cols; }
                else /* grid, column-major */                { col = i / rows; row = i % rows; }

                float u = (u0 + col * spacing.x) * us;
                float v = (v0 + row * spacing.y) * vs;
                buffer[i].localPosition = ToLocal(u, v);
                if (resetRotation) buffer[i].localRotation = Quaternion.identity;
            }
        }

        private void CollectChildren()
        {
            buffer.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (!includeInactive && !c.gameObject.activeSelf) continue;
                buffer.Add(c);
            }
        }

        private Vector3 ToLocal(float u, float v)
        {
            switch (plane)
            {
                case Plane.XY: return new Vector3(u, v, 0f);
                case Plane.YZ: return new Vector3(0f, v, u);
                default:       return new Vector3(u, 0f, v); // XZ
            }
        }
    }
}
