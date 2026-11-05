// Naija Streets (Unity) — how one remote player is drawn.
// Spec: docs/specs/netcode-m5.md §4.2: walker 600 ms behind, driver 300 ms,
// delay slides 300 ms/s, extrapolate ≤ 250 ms then hold, drop impossible
// speeds (9 m/s foot, 45 m/s driving), hide after > 5 drops in 10 s, snap on
// gaps over 30 m. Mirrored in tools/netsim.js.
// UNVERIFIED (2026-11-05): not compiled yet.

using System;
using System.Collections.Generic;

namespace NaijaLife.Network
{
    public sealed class SnapshotBuffer
    {
        struct Snap { public NetPose Pose; public double Time; }

        const int Capacity = 8;
        const double StaleAfter = 15.0; // keep-alive is 5 s; three missed = gone

        readonly List<Snap> _snaps = new List<Snap>(Capacity);
        readonly Queue<double> _bad = new Queue<double>();
        bool _hasSeq;
        ushort _lastSeq;
        bool _hidden;
        double _delay = -1;     // current render delay, slides toward target
        double _lastSampleAt = -1;
        double _lastRecv = double.NegativeInfinity;

        public bool IsHidden => _hidden;
        public int Count => _snaps.Count;
        public double CurrentDelay => _delay;

        /// <summary>Latest accepted pose (for nearby sorting). False when none.</summary>
        public bool TryGetLatest(out NetPose pose)
        {
            if (_snaps.Count == 0) { pose = default; return false; }
            pose = _snaps[_snaps.Count - 1].Pose;
            return true;
        }

        /// <summary>
        /// Add a received pose. <paramref name="time"/> is local receive time in seconds.
        /// Returns false when the snapshot was dropped (old, invalid or impossible).
        /// </summary>
        public bool Add(in NetPose pose, ushort sequence, double time)
        {
            if (_hasSeq && !MoveCodec.IsNewer(sequence, _lastSeq)) return false; // old or duplicate: not "bad"
            if (!Finite(pose.X) || !Finite(pose.Y) || !Finite(pose.Z)) return false;
            _hasSeq = true;
            _lastSeq = sequence;
            _lastRecv = time;

            if (_snaps.Count > 0)
            {
                Snap last = _snaps[_snaps.Count - 1];
                float dist = Distance(last.Pose, pose);
                if (dist > NetLimits.SnapDistance)
                {
                    // Big jump (got into a car across a junction, respawn): draw it at once.
                    _snaps.Clear();
                    Push(pose, time);
                    _hidden = false;
                    return true;
                }

                bool driving = last.Pose.IsDriving || pose.IsDriving;
                double minDt = (driving ? NetLimits.DriveSendInterval : NetLimits.WalkSendInterval) * 0.8;
                double dt = Math.Max(time - last.Time, minDt); // bunched packets must not look fast
                float limit = driving ? NetLimits.MaxDriveSpeed : NetLimits.MaxFootSpeed;
                if (dist / dt > limit)
                {
                    MarkBad(time);
                    return false;
                }

                if (time <= last.Time) time = last.Time + 1e-3; // keep times increasing
            }

            Push(pose, time);
            _hidden = false;
            return true;
        }

        /// <summary>Pose to draw at <paramref name="now"/>. False = do not draw.</summary>
        public bool TrySample(double now, out NetPose pose)
        {
            pose = default;
            if (_snaps.Count == 0 || _hidden) return false;
            if (now - _lastRecv > StaleAfter) return false;

            Snap newest = _snaps[_snaps.Count - 1];
            double target = newest.Pose.IsDriving ? NetLimits.DriverRenderDelay : NetLimits.WalkerRenderDelay;
            if (_delay < 0) _delay = target;
            else
            {
                double step = Math.Max(0, now - _lastSampleAt) * NetLimits.DelaySlidePerSecond;
                if (_delay < target) _delay = Math.Min(target, _delay + step);
                else if (_delay > target) _delay = Math.Max(target, _delay - step);
            }
            _lastSampleAt = now;

            double rt = now - _delay;
            if (rt <= _snaps[0].Time) { pose = _snaps[0].Pose; return true; }

            for (int i = 0; i < _snaps.Count - 1; i++)
            {
                Snap a = _snaps[i], b = _snaps[i + 1];
                if (rt >= a.Time && rt < b.Time)
                {
                    float t = (float)((rt - a.Time) / (b.Time - a.Time));
                    pose = Lerp(a.Pose, b.Pose, t);
                    return true;
                }
            }

            // Past the newest snapshot: extrapolate a little, then hold still.
            pose = newest.Pose;
            if (_snaps.Count >= 2 && newest.Pose.IsMoving)
            {
                Snap prev = _snaps[_snaps.Count - 2];
                double span = newest.Time - prev.Time;
                if (span > 1e-4)
                {
                    float ahead = (float)Math.Min(rt - newest.Time, NetLimits.MaxExtrapolation);
                    float k = ahead / (float)span;
                    pose.X += (newest.Pose.X - prev.Pose.X) * k;
                    pose.Y += (newest.Pose.Y - prev.Pose.Y) * k;
                    pose.Z += (newest.Pose.Z - prev.Pose.Z) * k;
                }
            }
            return true;
        }

        void Push(in NetPose pose, double time)
        {
            if (_snaps.Count == Capacity) _snaps.RemoveAt(0);
            _snaps.Add(new Snap { Pose = pose, Time = time });
        }

        void MarkBad(double time)
        {
            _bad.Enqueue(time);
            while (_bad.Count > 0 && time - _bad.Peek() > NetLimits.BadSnapshotWindow) _bad.Dequeue();
            if (_bad.Count > NetLimits.BadSnapshotsToHide) _hidden = true;
        }

        public static NetPose Lerp(in NetPose a, in NetPose b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            NetPose p = t < 0.5f ? a : b; // flags, anim, vehicle from the nearer one
            p.X = a.X + (b.X - a.X) * t;
            p.Y = a.Y + (b.Y - a.Y) * t;
            p.Z = a.Z + (b.Z - a.Z) * t;
            p.Speed = a.Speed + (b.Speed - a.Speed) * t;
            float h = a.HeadingDeg + SendPolicy.DeltaAngle(a.HeadingDeg, b.HeadingDeg) * t;
            h %= 360f;
            if (h < 0f) h += 360f;
            p.HeadingDeg = h;
            return p;
        }

        static float Distance(in NetPose a, in NetPose b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
