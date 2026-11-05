// Naija Streets (Unity) — when the local pose goes out.
// Spec: docs/specs/netcode-m5.md §4.1 (driving 4 Hz, walking 2 Hz, at once on
// start/stop/vehicle/indoors change, keep-alive every 5 s when still).
// Mirrored in tools/netsim.js. UNVERIFIED (2026-11-05): not compiled yet.

using System;

namespace NaijaLife.Network
{
    public sealed class SendPolicy
    {
        bool _hasSent;
        bool _paused;
        double _lastSent;
        NetPose _lastPose;

        public void SetPaused(bool paused)
        {
            // After a pause, send at once so others see where we are.
            if (_paused && !paused) _hasSent = false;
            _paused = paused;
        }

        /// <summary>Call every frame. True = send this pose now (it is recorded as sent).</summary>
        public bool TryConsume(in NetPose pose, double now)
        {
            if (_paused) return false;
            if (!_hasSent || ModeChanged(pose, _lastPose))
                return Mark(pose, now);

            double elapsed = now - _lastSent;
            if (elapsed >= NetLimits.KeepAliveInterval)
                return Mark(pose, now);

            double interval = pose.IsDriving ? NetLimits.DriveSendInterval : NetLimits.WalkSendInterval;
            if (elapsed >= interval && Changed(pose, _lastPose))
                return Mark(pose, now);

            return false;
        }

        bool Mark(in NetPose pose, double now)
        {
            _hasSent = true;
            _lastSent = now;
            _lastPose = pose;
            return true;
        }

        static bool ModeChanged(in NetPose a, in NetPose b)
        {
            const MoveFlags mask = MoveFlags.Moving | MoveFlags.Driving | MoveFlags.Indoors;
            return (a.Flags & mask) != (b.Flags & mask) || a.VehicleModelId != b.VehicleModelId;
        }

        /// <summary>Moved at least 5 cm or turned at least about 1 degree since the last send.</summary>
        public static bool Changed(in NetPose a, in NetPose b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            if (dx * dx + dy * dy + dz * dz >= NetLimits.StillPosEpsilon * NetLimits.StillPosEpsilon) return true;
            return Math.Abs(DeltaAngle(a.HeadingDeg, b.HeadingDeg)) >= NetLimits.StillHeadingEpsilonDeg;
        }

        /// <summary>Shortest signed difference b - a in degrees, in (-180, 180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }
    }
}
