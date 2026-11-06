// M3 lane network (docs/specs/vehicles-m3.md section 8.1, 8.7). Holds the
// LaneNodes under it. Also the core of the lane validator: every lane segment is
// swept with a danfo-sized box and every static collider it touches is a map bug
// (web lessons L-83, L-97: "anything in a lane is a map fix, not a traffic fix").

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    [DisallowMultipleComponent]
    public sealed class LaneGraph : MonoBehaviour
    {
        public List<LaneNode> nodes = new List<LaneNode>();

        /// <summary>Danfo footprint for the validator sweep (half extents, m).</summary>
        public static readonly Vector3 SweepHalfExtents = new Vector3(0.95f, 1.0f, 0.1f);

        void Awake() { Refresh(); }

        public void Refresh()
        {
            nodes.Clear();
            GetComponentsInChildren(true, nodes);
        }

        /// <summary>Nearest node within maxDistance and maxDy height difference (bridge-aware).</summary>
        public LaneNode Nearest(Vector3 p, float maxDistance = 30f, float maxDy = 2f)
        {
            LaneNode best = null;
            float bestD = maxDistance * maxDistance;
            foreach (var n in nodes)
            {
                if (n == null) continue;
                Vector3 d = n.transform.position - p;
                if (Mathf.Abs(d.y) > maxDy) continue;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestD) { bestD = sq; best = n; }
            }
            return best;
        }

        public struct Blocker
        {
            public LaneNode From, To;
            public Collider Collider;
            public Vector3 Point;
            public override string ToString() =>
                $"{(From != null ? From.name : "?")} -> {(To != null ? To.name : "?")}: {(Collider != null ? Collider.name : "?")} at {Point}";
        }

        /// <summary>
        /// Sweep every lane segment with a danfo-sized box (bottom 0.35 m above
        /// the lane) and list every STATIC collider it touches. Moving bodies
        /// (vehicles, people) and road surfaces are ignored. 0 results = pass.
        /// </summary>
        public List<Blocker> FindBlockers()
        {
            Refresh();
            var list = new List<Blocker>();
            var hits = new RaycastHit[32];
            int mask = VehicleLayers.SenseMask
                       & ~VehicleLayers.Bit(VehicleLayers.Vehicle) & ~VehicleLayers.Bit(VehicleLayers.DrivenVehicle)
                       & ~VehicleLayers.Bit(VehicleLayers.Pedestrian);
            foreach (var a in nodes)
            {
                if (a == null) continue;
                foreach (var b in a.next)
                {
                    if (b == null) continue;
                    Vector3 lift = Vector3.up * (TrafficSense.GroundClearance + SweepHalfExtents.y);
                    Vector3 from = a.transform.position + lift, to = b.transform.position + lift;
                    Vector3 seg = to - from;
                    float len = seg.magnitude;
                    if (len < 0.05f) continue;
                    Vector3 dir = seg / len;
                    int n = Physics.BoxCastNonAlloc(from, SweepHalfExtents, dir, hits, Quaternion.LookRotation(dir, Vector3.up),
                        len, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var c = hits[i].collider;
                        if (c == null || c.attachedRigidbody != null) continue;
                        if (hits[i].distance > 0f && Vector3.Dot(hits[i].normal, Vector3.up) > 0.7f) continue;
                        list.Add(new Blocker { From = a, To = b, Collider = c, Point = hits[i].point });
                    }
                }
            }
            return list;
        }
    }
}
