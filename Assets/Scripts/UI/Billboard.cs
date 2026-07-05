using UnityEngine;

namespace PixelShoot.UI
{
    /// <summary>
    /// Keeps a world-space object (e.g. a TMP text over the bus) always facing the camera.
    /// It OVERWRITES the world rotation every LateUpdate, so the object never inherits the
    /// bus's rotation — parent it to the bus (so it follows position) and its facing stays
    /// locked to the camera regardless of how the bus turns.
    ///
    /// <para>High execution order so it runs AFTER any script that rotates the bus, even if
    /// that also happens in LateUpdate.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
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
