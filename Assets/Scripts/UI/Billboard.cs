using UnityEngine;

namespace PixelShoot.UI
{
    /// <summary>
    /// Keeps a world-space object (e.g. a TMP text over the bus) always facing the camera.
    /// Runs in LateUpdate so it tracks after all movement. Optionally lock to Y-only so a
    /// ground label just spins to face the camera without tilting.
    /// </summary>
    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Camera to face. Defaults to Camera.main.")]
        [SerializeField] private Camera cam;
        [Tooltip("Only rotate around Y (upright label) instead of a full billboard.")]
        [SerializeField] private bool yAxisOnly = false;

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            if (yAxisOnly)
            {
                Vector3 dir = transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) return;
                transform.rotation = Quaternion.LookRotation(dir);
            }
            else
            {
                // Align the object's axes with the camera → text reads flat toward the viewer.
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
