// M3 shared road sense (docs/specs/vehicles-m3.md section 8.2, 8.5).
// Web lesson L-83: traffic that only looked for the player on foot drove through
// other vehicles. Here a bot asks a REAL physics box cast at its own height along
// its own path, so it sees every vehicle (bot, parked, the player's car), every
// person and every static thing, and a car on a bridge deck never sees the car
// below it (no separate "same deck" rule needed).
//
// UNVERIFIED: no Unity Editor in Claude's container (2026-11-06).

using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    /// <summary>Anything a bot vehicle can ask to step out of its lane (bot pedestrians).</summary>
    public interface IStepAside
    {
        void StepAside(VehicleController from);
    }

    public struct SenseHit
    {
        public bool Hit;
        /// <summary>Metres from the front bumper to the obstacle along the path.</summary>
        public float Gap;
        public Collider Collider;
        public VehicleController Vehicle;   // when the obstacle is a vehicle
        public bool IsPerson;
        /// <summary>Obstacle speed along the path direction (m/s). 0 for static things.</summary>
        public float SpeedAlong;
        public bool Moving => Mathf.Abs(SpeedAlong) > 0.5f;

        public static SenseHit Clear => new SenseHit { Gap = float.PositiveInfinity };
    }

    public static class TrafficSense
    {
        static readonly HashSet<VehicleController> _vehicles = new HashSet<VehicleController>();
        static readonly Dictionary<Rigidbody, VehicleController> _byBody = new Dictionary<Rigidbody, VehicleController>();
        static readonly RaycastHit[] _hits = new RaycastHit[24];

        /// <summary>Box cast starts this far above the lane so kerbs and the road never count.</summary>
        public const float GroundClearance = 0.35f;

        public static int Count => _vehicles.Count;

        public static void Register(VehicleController v)
        {
            if (v == null) return;
            _vehicles.Add(v);
            if (v.Body != null) _byBody[v.Body] = v;
        }

        public static void Unregister(VehicleController v)
        {
            if (v == null) return;
            _vehicles.Remove(v);
            if (v.Body != null) _byBody.Remove(v.Body);
            foreach (var list in _crossings.Values) list.RemoveAll(r => r.Vehicle == v);
        }

        public static VehicleController VehicleOf(Collider c)
        {
            if (c == null || c.attachedRigidbody == null) return null;
            _byBody.TryGetValue(c.attachedRigidbody, out var v);
            return v;
        }

        /// <summary>
        /// Cast the vehicle's body box along a path (world points, first = ahead of
        /// the bumper, typically the lane's next 2-3 points) up to maxLength metres.
        /// lateralOffset shifts the path sideways (negative = left, for overtaking).
        /// </summary>
        public static SenseHit CastPath(VehicleController self, IList<Vector3> path, float maxLength, float lateralOffset = 0f, float widthMargin = 0.15f)
        {
            var result = SenseHit.Clear;
            if (self == null || path == null || path.Count == 0) return result;

            Vector3 half = self.HalfExtents;
            float hy = Mathf.Max(0.3f, half.y - GroundClearance * 0.5f);
            var box = new Vector3(half.x + widthMargin, hy, 0.1f);
            int mask = VehicleLayers.SenseMask;

            // Start at the front bumper, at lane height + clearance.
            Vector3 nose = self.transform.position + self.transform.forward * (half.z + 0.15f);
            Vector3 from = nose;
            float travelled = 0f;
            for (int i = 0; i < path.Count && travelled < maxLength; i++)
            {
                Vector3 to = path[i];
                Vector3 seg = to - from;
                float len = seg.magnitude;
                if (len < 0.05f) continue;
                Vector3 dir = seg / len;
                len = Mathf.Min(len, maxLength - travelled);
                Vector3 right = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z).normalized);
                // Path points and the vehicle root are at road level (ground-pivot rule):
                // lift the box by clearance + half height so kerbs and the road never count.
                Vector3 centre = from + right * lateralOffset + Vector3.up * (GroundClearance + hy);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                int n = Physics.BoxCastNonAlloc(centre, box, dir, _hits, rot, len, mask, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                RaycastHit bestHit = default;
                for (int h = 0; h < n; h++)
                {
                    var hit = _hits[h];
                    if (hit.collider == null) continue;
                    if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody == self.Body) continue; // self
                    if (hit.collider is WheelCollider) continue;
                    if (hit.distance > 0f && Vector3.Dot(hit.normal, Vector3.up) > 0.7f) continue; // a floor or ramp, not a wall
                    if (hit.distance < best) { best = hit.distance; bestHit = hit; }
                }
                if (best < float.PositiveInfinity)
                {
                    result.Hit = true;
                    result.Gap = travelled + best;
                    result.Collider = bestHit.collider;
                    result.Vehicle = VehicleOf(bestHit.collider);
                    result.IsPerson = VehicleLayers.IsPersonLayer(bestHit.collider.gameObject.layer) || bestHit.collider.CompareTag("Player");
                    Vector3 v = Vector3.zero;
                    if (result.Vehicle != null) v = result.Vehicle.Velocity;
                    else if (bestHit.collider.attachedRigidbody != null) v = VehicleRb.GetVelocity(bestHit.collider.attachedRigidbody);
                    else if (bestHit.collider is CharacterController cc) v = cc.velocity;
                    result.SpeedAlong = Vector3.Dot(v, dir);
                    return result;
                }
                travelled += len;
                from = to;
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Crossings (spec 8.5): first to arrive goes, tie = lower id.
        // ------------------------------------------------------------------

        struct Reservation { public VehicleController Vehicle; public float Arrival; }
        static readonly Dictionary<int, List<Reservation>> _crossings = new Dictionary<int, List<Reservation>>();

        /// <summary>
        /// Ask to enter crossing `id`, arriving in `eta` seconds. Returns true when
        /// this vehicle may go. Call every step while approaching; Release() once
        /// through. Vehicles that stop asking for 1 s are dropped.
        /// </summary>
        public static bool RequestCrossing(int id, VehicleController v, float eta)
        {
            if (!_crossings.TryGetValue(id, out var list)) _crossings[id] = list = new List<Reservation>();
            float now = Time.time;
            float arrival = now + Mathf.Max(0f, eta);
            int mine = list.FindIndex(r => r.Vehicle == v);
            if (mine < 0) list.Add(new Reservation { Vehicle = v, Arrival = arrival });
            else
            {
                // Keep the earliest claimed arrival so a braking car does not lose its turn.
                var r = list[mine];
                if (arrival < r.Arrival) r.Arrival = arrival;
                list[mine] = r;
            }
            list.RemoveAll(r => r.Vehicle == null);
            var me = list.Find(r => r.Vehicle == v);
            foreach (var other in list)
            {
                if (other.Vehicle == v) continue;
                // Only vehicles inside or reaching the box within 2 s compete.
                if (other.Arrival - now > 2f && me.Arrival - now > 2f) continue;
                if (!VehicleMath.HasRightOfWay(me.Arrival, v.Id, other.Arrival, other.Vehicle.Id)) return false;
            }
            return true;
        }

        public static void ReleaseCrossing(int id, VehicleController v)
        {
            if (_crossings.TryGetValue(id, out var list)) list.RemoveAll(r => r.Vehicle == v);
        }

        /// <summary>Test hook: clear all state (Edit/Play mode tests, scene reload).</summary>
        public static void ResetAll()
        {
            _vehicles.Clear(); _byBody.Clear(); _crossings.Clear();
        }
    }
}
