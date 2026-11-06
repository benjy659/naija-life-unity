// M3 bot driver (docs/specs/vehicles-m3.md section 8). Drives a VehicleController
// along the LaneGraph through the same DriveInput the player uses.
//   - sees what is ahead with TrafficSense.CastPath (real physics, own height)
//   - brakes for people (2 m short), queues behind vehicles (2 s gap),
//     stops a car length short of anything still
//   - crossings: first to arrive goes, tie = lower id; creeps after 8 s
//   - goes round parked things on the left when the offset path is clear,
//     else waits, reverses, reroutes (StuckLogic). NEVER drives through anything.
//   - far from the player it runs kinematic along the lane (cheaper on WebGL)
//
// UNVERIFIED: no Unity Editor in Claude's container (2026-11-06).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    public sealed class TrafficDriver : MonoBehaviour
    {
        public LaneGraph graph;

        [Header("Sensing")]
        public float vehicleStandoff = 4.5f;
        public float personStandoff = 2f;
        public float comfortDecel = 4f;
        public float maxAccel = 2f;

        [Header("Overtaking (right-hand traffic: pass on the left)")]
        public float overtakeOffset = 3.5f;

        [Header("Dynamic near / kinematic far (spec 8.3)")]
        public float dynamicBelow = 110f;
        public float kinematicAbove = 130f;

        /// <summary>Set when rerouting failed; the spawner removes it once out of view.</summary>
        public bool WantsDespawn { get; private set; }
        public StuckState Stuck => _stuck.State;
        public SenseHit LastSense { get; private set; }
        public float TargetSpeed { get; private set; }

        /// <summary>Raised once when the bot honks at a person standing in its lane (audio later).</summary>
        public static event Action<TrafficDriver> Honked;

        VehicleController _v;
        readonly List<LaneNode> _route = new List<LaneNode>(8); // _route[0] = node we are driving toward
        LaneNode _prev;
        readonly StuckLogic _stuck = new StuckLogic();
        readonly List<Vector3> _path = new List<Vector3>(8);
        float _overtakeBlend;
        float _crossingWait;
        int _heldCrossing;
        float _personStill;
        bool _honked;
        Transform _player;
        float _kinematicSpeed;

        void Awake() { _v = GetComponent<VehicleController>(); }

        /// <summary>Spawner hook: start at a node, heading to its next.</summary>
        public void Begin(LaneGraph g, LaneNode start)
        {
            graph = g;
            _prev = start;
            _route.Clear();
            WantsDespawn = false;
            _stuck.Reset();
            _overtakeBlend = 0f;
            _heldCrossing = 0;
            _honked = false;
            _kinematicSpeed = 0f;
            if (start != null && start.next.Count > 0) _route.Add(Pick(start, null));
            FillRoute();
            _v.playerDrivable = false;
            _v.SetDriver(VehicleDriver.Bot);
        }

        void OnDisable()
        {
            if (_heldCrossing != 0) TrafficSense.ReleaseCrossing(_heldCrossing, _v);
            _heldCrossing = 0;
        }

        void Start()
        {
            if (_route.Count == 0 && graph != null)
            {
                var n = graph.Nearest(transform.position);
                if (n != null) Begin(graph, n);
            }
        }

        static LaneNode Pick(LaneNode at, LaneNode avoid)
        {
            if (at == null || at.next.Count == 0) return null;
            if (at.next.Count == 1) return at.next[0];
            for (int tries = 0; tries < 4; tries++)
            {
                var n = at.next[UnityEngine.Random.Range(0, at.next.Count)];
                if (n != avoid) return n;
            }
            return at.next[0];
        }

        void FillRoute()
        {
            while (_route.Count > 0 && _route.Count < 5)
            {
                var last = _route[_route.Count - 1];
                var n = Pick(last, _route.Count > 1 ? _route[_route.Count - 2] : _prev);
                if (n == null) break;
                _route.Add(n);
            }
        }

        // ------------------------------------------------------------------

        void FixedUpdate()
        {
            if (_v == null || _v.Driver != VehicleDriver.Bot || _route.Count == 0 || _route[0] == null) return;
            float dt = Time.fixedDeltaTime;

            AdvanceRoute();
            if (_route.Count == 0) { _v.SetInput(new DriveInput { Handbrake = true }); return; }
            UpdateMode();

            float fwd = _v.IsKinematicMover ? _kinematicSpeed : _v.ForwardSpeed;
            float target = LaneSpeed(fwd);

            // ---- Sense ahead along the route ----
            BuildPath();
            float look = Mathf.Max(14f, VehicleMath.StoppingDistance(Mathf.Abs(fwd), comfortDecel) + vehicleStandoff + 3f);
            float offset = -overtakeOffset * _overtakeBlend;
            var sense = TrafficSense.CastPath(_v, _path, look, offset);
            LastSense = sense;
            if (sense.Hit)
            {
                if (sense.IsPerson)
                    target = Mathf.Min(target, VehicleMath.SpeedForGap(sense.Gap, target, personStandoff, comfortDecel));
                else if (sense.Moving && sense.SpeedAlong > 0f)
                    target = Mathf.Min(target, VehicleMath.SpeedForFollow(sense.Gap, sense.SpeedAlong, target));
                else
                    target = Mathf.Min(target, VehicleMath.SpeedForGap(sense.Gap, target, vehicleStandoff, comfortDecel));
            }
            People(sense, fwd, dt);

            // ---- Crossings ----
            target = Mathf.Min(target, CrossingSpeed(fwd, dt));

            // ---- Stuck: overtake / wait / reverse / reroute ----
            bool offsetClear = false;
            if (sense.Hit && !sense.Moving && !sense.IsPerson && sense.Gap < StuckLogic.BlockGap * 1.5f)
            {
                var side = TrafficSense.CastPath(_v, _path, sense.Gap + 15f, -overtakeOffset);
                offsetClear = !side.Hit;
            }
            float reverseStep = _v.ForwardSpeed < 0f ? -_v.ForwardSpeed * dt : 0f;
            var state = _stuck.Step(sense.Hit ? sense.Gap : float.PositiveInfinity,
                                    sense.Moving || sense.IsPerson, offsetClear, dt, reverseStep);
            _overtakeBlend = Mathf.MoveTowards(_overtakeBlend, state == StuckState.TryOvertake ? 1f : 0f, dt / 1.5f);
            if (state == StuckState.TryOvertake)
                target = Mathf.Min(LaneSpeed(fwd), 6f); // pass slowly, still braking for the side cast below
            if (state == StuckState.Reroute) Reroute();

            TargetSpeed = target;

            if (_v.IsKinematicMover) { KinematicStep(target, dt); return; }

            // ---- Drive input ----
            var input = new DriveInput { Steer = SteerToward(LookPoint(fwd, offset)) };
            if (state == StuckState.Reversing)
            {
                input.Brake = 1f; input.Steer = 0f; // PedalLogic engages reverse after 0.25 s at rest
            }
            else
            {
                float err = target - fwd;
                if (target < 0.1f && Mathf.Abs(fwd) < 0.3f) input.Handbrake = true;      // hold still, never creep into reverse
                else if (err > 0.2f) input.Throttle = Mathf.Clamp(err * 0.5f, 0.15f, 1f);
                else if (err < -0.4f) input.Brake = Mathf.Clamp(-err * 0.35f, 0.15f, 1f);
            }
            _v.SetInput(input);
        }

        // ------------------------------------------------------------------
        // Route and steering
        // ------------------------------------------------------------------

        void AdvanceRoute()
        {
            int guard = 0;
            while (_route.Count > 0 && guard++ < 4)
            {
                var t = _route[0];
                Vector3 to = t.transform.position - transform.position; to.y = 0f;
                Vector3 segDir = _prev != null ? (t.transform.position - _prev.transform.position) : transform.forward;
                segDir.y = 0f;
                bool passed = Vector3.Dot(to, segDir) < 0f;
                if (to.magnitude > 4f && !passed) break;
                // Reached t.
                if (t.crossingExit && _heldCrossing != 0) { TrafficSense.ReleaseCrossing(_heldCrossing, _v); _heldCrossing = 0; _crossingWait = 0f; }
                _prev = t;
                _route.RemoveAt(0);
                FillRoute();
            }
        }

        void BuildPath()
        {
            _path.Clear();
            for (int i = 0; i < _route.Count && i < 4; i++) _path.Add(_route[i].transform.position);
        }

        float LaneSpeed(float fwd)
        {
            var t = _route[0];
            float limit = t.SpeedLimitMs;
            // Slow for the corner at the next node (or the one after).
            for (int i = 0; i < 2 && i + 1 < _route.Count; i++)
            {
                Vector3 a = (i == 0 ? (_prev != null ? _prev.transform.position : transform.position) : _route[i - 1].transform.position);
                Vector3 b = _route[i].transform.position, c = _route[i + 1].transform.position;
                Vector3 d1 = b - a, d2 = c - b; d1.y = 0f; d2.y = 0f;
                if (d1.sqrMagnitude < 0.01f || d2.sqrMagnitude < 0.01f) continue;
                float ang = Vector3.Angle(d1, d2);
                if (ang < 15f) continue;
                float dist = Vector3.Distance(transform.position, b);
                float cornerV = Mathf.Lerp(limit, 5f, Mathf.Clamp01(ang / 90f));
                // Brake in time for it.
                float v = Mathf.Sqrt(cornerV * cornerV + 2f * comfortDecel * Mathf.Max(0f, dist - 4f));
                limit = Mathf.Min(limit, v);
            }
            return limit;
        }

        Vector3 LookPoint(float fwd, float lateral)
        {
            float l = Mathf.Clamp(6f + Mathf.Abs(fwd) * 0.6f, 6f, 14f);
            Vector3 a = _prev != null ? _prev.transform.position : transform.position;
            Vector3 p = transform.position;
            // Project onto the current segment, then walk l metres along the route.
            Vector3 b = _route[0].transform.position;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 1f;
            Vector3 cur = a + ab * t;
            float left = l;
            Vector3 dir = ab.normalized;
            int i = 0;
            Vector3 segEnd = b;
            while (true)
            {
                float seg = Vector3.Distance(cur, segEnd);
                if (seg >= left || i + 1 >= _route.Count) { dir = (segEnd - cur).sqrMagnitude > 0.01f ? (segEnd - cur).normalized : dir; cur += dir * Mathf.Min(left, seg); break; }
                left -= seg; cur = segEnd; i++; segEnd = _route[i].transform.position;
            }
            if (lateral != 0f)
            {
                Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
                cur += Vector3.Cross(Vector3.up, flat) * lateral;
            }
            return cur;
        }

        float SteerToward(Vector3 look)
        {
            Vector3 local = transform.InverseTransformPoint(look);
            float ang = Mathf.Atan2(local.x, Mathf.Max(0.5f, local.z)) * Mathf.Rad2Deg;
            var def = _v.definition;
            float max = def != null ? VehicleMath.MaxSteerAngle(_v.ForwardSpeed, def.TopSpeedMs, def.maxSteerLowSpeedDeg, def.maxSteerTopSpeedDeg) : 30f;
            return Mathf.Clamp(ang / Mathf.Max(1f, max), -1f, 1f);
        }

        void Reroute()
        {
            // Pick a different branch at the node we are heading to, if it has one.
            var at = _prev;
            LaneNode alt = null;
            if (at != null)
                foreach (var n in at.next) if (_route.Count == 0 || n != _route[0]) { alt = n; break; }
            if (alt == null) { WantsDespawn = true; return; }
            _route.Clear();
            _route.Add(alt);
            FillRoute();
            _stuck.Reset();
        }

        // ------------------------------------------------------------------
        // People and crossings
        // ------------------------------------------------------------------

        void People(SenseHit sense, float fwd, float dt)
        {
            if (sense.Hit && sense.IsPerson && sense.Gap < 8f && Mathf.Abs(fwd) < 0.5f)
            {
                _personStill += dt;
                if (_personStill > 3f && !_honked)
                {
                    _honked = true; // one beep, not a loop (spec 8.4)
                    Honked?.Invoke(this);
                    var aside = sense.Collider != null ? sense.Collider.GetComponentInParent<IStepAside>() : null;
                    aside?.StepAside(_v);
                }
            }
            else
            {
                _personStill = 0f;
                if (!sense.Hit || !sense.IsPerson) _honked = false;
            }
        }

        float CrossingSpeed(float fwd, float dt)
        {
            if (_heldCrossing != 0) return float.PositiveInfinity; // already ours
            float dist = 0f;
            Vector3 from = transform.position;
            for (int i = 0; i < _route.Count && i < 3; i++)
            {
                var n = _route[i];
                dist += Vector3.Distance(from, n.transform.position);
                from = n.transform.position;
                if (dist > 25f) break;
                if (n.crossingId == 0) continue;
                float eta = dist / Mathf.Max(2f, Mathf.Abs(fwd));
                if (TrafficSense.RequestCrossing(n.crossingId, _v, eta))
                {
                    if (dist < 6f) _heldCrossing = n.crossingId; // committed: keep it until the exit node
                    _crossingWait = 0f;
                    return float.PositiveInfinity;
                }
                _crossingWait += dt;
                if (_crossingWait > 8f) return 1f; // creep, still braking for anything in the cast
                return VehicleMath.SpeedForGap(dist, 14f, 2f, comfortDecel);
            }
            return float.PositiveInfinity;
        }

        // ------------------------------------------------------------------
        // Dynamic near, kinematic far (spec 8.3)
        // ------------------------------------------------------------------

        void UpdateMode()
        {
            if (_player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _player = go.transform;
                if (_player == null) return;
            }
            float d = Vector3.Distance(_player.position, transform.position);
            if (!_v.IsKinematicMover && d > kinematicAbove && (Mathf.Abs(_v.ForwardSpeed) < 1f || Mathf.Abs(_v.LastInput.Steer) < 0.1f))
            {
                _kinematicSpeed = Mathf.Max(0f, _v.ForwardSpeed);
                _v.SetKinematic(true);
            }
            else if (_v.IsKinematicMover && d < dynamicBelow)
            {
                _v.SetKinematic(false);
            }
        }

        void KinematicStep(float target, float dt)
        {
            float accel = target > _kinematicSpeed ? maxAccel : comfortDecel * 1.5f;
            _kinematicSpeed = Mathf.MoveTowards(_kinematicSpeed, Mathf.Max(0f, target), accel * dt);
            Vector3 a = _prev != null ? _prev.transform.position : transform.position;
            Vector3 b = _route[0].transform.position;
            Vector3 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.01f) return;
            Vector3 dir = ab / len;
            float t = Mathf.Clamp(Vector3.Dot(transform.position - a, dir), 0f, len);
            Vector3 pos = a + dir * Mathf.Min(len, t + _kinematicSpeed * dt);
            var rot = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Mathf.Clamp01(4f * dt));
            _v.KinematicMove(pos, rot, dt);
        }
    }
}
