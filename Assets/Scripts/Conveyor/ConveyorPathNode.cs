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
            Gizmos.color = isCanShoot ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }
#endif
    }
}
