using UnityEngine;
using UnityEngine.UI;
using PixelShoot.Conveyor;
using TMPro;

namespace PixelShoot.UI
{
    /// <summary>
    /// Drops a "current / capacity" readout onto the HUD for the conveyor.
    /// Plug in the ConveyorController and a Text — the rest is automatic.
    /// </summary>
    public class ConveyorCapacityHUD : MonoBehaviour
    {
        [SerializeField] private ConveyorController conveyor;
        [SerializeField] private TextMeshPro label;
        [Tooltip("Format string. {0} = occupied, {1} = capacity.")]
        [SerializeField] private string format = "Conveyor: {0} / {1}";

        private int lastOcc = -1;
        private int lastCap = -1;

        private void LateUpdate()
        {
            if (conveyor == null || label == null) return;
            int occ = conveyor.OccupiedCount;
            int cap = conveyor.Capacity;
            if (occ == lastOcc && cap == lastCap) return;
            lastOcc = occ;
            lastCap = cap;
            label.text = string.Format(format, occ, cap);
        }
    }
}
