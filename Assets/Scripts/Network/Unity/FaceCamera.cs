// Naija Streets (Unity) — keeps a world-space name tag readable: turns it to
// face the main camera every frame (yaw and pitch, never mirrored).
// UNVERIFIED (2026-11-06): not compiled or run yet.

using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class FaceCamera : MonoBehaviour
    {
        Camera _cam;

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            // TextMeshPro reads correctly when its forward points away from the camera.
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position, Vector3.up);
        }
    }
}
