using UnityEngine;
using PixelShoot.Data;

namespace PixelShoot.Conveyor
{
    /// <summary>
    /// Place on empty GameObjects under a path parent. Ordered by sibling index.
    /// Position is read from this transform; isCanShoot marks shooting zones;
    /// targetSide tells the shooter which grid side it should aim at from here.
    /// </summary>
    public class ConveyorPathNode : MonoBehaviour
    {
        [SerializeField] private bool isCanShoot;
        [SerializeField] private GridSide targetSide = GridSide.Bottom;

        public Vector3 Position => transform.position;
        public bool IsCanShoot => isCanShoot;
        public GridSide TargetSide => targetSide;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isCanShoot ? Color.green : new Color(0.6f, 0.6f, 0.6f, 1f);
            Gizmos.DrawSphere(transform.position, 0.18f);

            // Arrow showing which side this node aims at, only when canShoot.
            if (isCanShoot)
            {
                Vector3 dir = SideToDirection(targetSide);
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
                Vector3 tip = transform.position + dir * 0.9f;
                Gizmos.DrawLine(transform.position, tip);
                // Arrowhead
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.15f;
                Gizmos.DrawLine(tip, tip - dir * 0.25f + right);
                Gizmos.DrawLine(tip, tip - dir * 0.25f - right);
            }
        }

        private static Vector3 SideToDirection(GridSide side)
        {
            switch (side)
            {
                case GridSide.Bottom: return Vector3.forward;  // node at z<grid, aims +Z toward grid
                case GridSide.Top:    return Vector3.back;
                case GridSide.Left:   return Vector3.right;
                case GridSide.Right:  return Vector3.left;
                default:              return Vector3.forward;
            }
        }
#endif
    }
}
