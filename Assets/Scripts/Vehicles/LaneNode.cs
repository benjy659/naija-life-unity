// M3 lane graph node (docs/specs/vehicles-m3.md section 8.1). Position INCLUDES
// height, so a bridge deck and the road under it are separate chains of nodes.
// Lanes follow the right-hand side of the road (Nigeria drives on the right).

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    public sealed class LaneNode : MonoBehaviour
    {
        public List<LaneNode> next = new List<LaneNode>();
        public float speedLimitKmh = 50f;

        [Tooltip("Non-zero on the entry node of a crossing (or a one-at-a-time gate). " +
                 "Vehicles reserve it through TrafficSense.RequestCrossing (spec 8.5).")]
        public int crossingId;

        [Tooltip("Where a crossing ends: vehicles release their reservation when they reach this node.")]
        public bool crossingExit;

        public float SpeedLimitMs => speedLimitKmh / VehicleMath.KmhPerMs;

        void OnDrawGizmos()
        {
            Gizmos.color = crossingId != 0 ? new Color(1f, 0.5f, 0f) : new Color(0.2f, 0.8f, 1f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.2f, 0.35f);
            foreach (var n in next)
            {
                if (n == null) continue;
                Vector3 a = transform.position + Vector3.up * 0.2f, b = n.transform.position + Vector3.up * 0.2f;
                Gizmos.DrawLine(a, b);
                Vector3 d = (b - a).normalized;
                Vector3 r = Vector3.Cross(Vector3.up, d) * 0.6f;
                Vector3 tip = Vector3.Lerp(a, b, 0.6f);
                Gizmos.DrawLine(tip, tip - d * 1.2f + r);
                Gizmos.DrawLine(tip, tip - d * 1.2f - r);
            }
        }
    }
}
