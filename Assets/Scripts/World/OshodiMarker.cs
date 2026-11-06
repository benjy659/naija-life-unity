// OshodiMarker — gizmo for M2 job slots, viewpoints and spawn points (spec 8.5).
// The GameObject itself stays empty (no mesh, no collider) so M4 and QA can
// find it by name; this component only draws a wire sphere + a vertical line
// in the Scene view. The gizmo floats 0.5 m above the marker so it stays
// visible even when the marker sits on (or just under) a raised surface.
using UnityEngine;

namespace NaijaLife.World
{
    public class OshodiMarker : MonoBehaviour
    {
        public float gizmoRadius = 0.4f;
        public Color gizmoColor = new Color(0.2f, 0.8f, 0.4f);

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 p = transform.position + Vector3.up * 0.5f;
            Gizmos.DrawWireSphere(p, gizmoRadius);
            Gizmos.DrawLine(transform.position, p + Vector3.up * 0.5f);
        }
    }
}
