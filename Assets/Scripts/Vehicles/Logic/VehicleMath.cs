// M3 pure maths (docs/specs/vehicles-m3.md sections 6 and 8). No UnityEngine:
// compiled and unit-tested outside Unity (Assets/Tests/EditMode/Vehicles).
// Units: metres, seconds, m/s unless a name says Kmh.

using System;

namespace NaijaLife.Vehicles
{
    public static class VehicleMath
    {
        public const float KmhPerMs = 3.6f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Move current toward target by at most maxDelta.</summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            float d = target - current;
            if (Math.Abs(d) <= maxDelta) return target;
            return current + Math.Sign(d) * maxDelta;
        }

        /// <summary>
        /// Share of full motor torque at this speed (spec 6): full torque up to
        /// 60% of top speed, falling linearly to zero at top speed, zero above.
        /// </summary>
        public static float TorqueFactor(float speedMs, float topSpeedMs)
        {
            if (topSpeedMs <= 0f) return 0f;
            float s = Math.Abs(speedMs) / topSpeedMs;
            if (s <= 0.6f) return 1f;
            if (s >= 1f) return 0f;
            return (1f - s) / 0.4f;
        }

        /// <summary>
        /// Speed-sensitive steering limit (spec 6): lerp from the 0 km/h angle to
        /// the top-speed angle by speed / topSpeed.
        /// </summary>
        public static float MaxSteerAngle(float speedMs, float topSpeedMs, float lowSpeedDeg, float topSpeedDeg)
        {
            float t = topSpeedMs <= 0f ? 0f : Math.Abs(speedMs) / topSpeedMs;
            return Lerp(lowSpeedDeg, topSpeedDeg, t);
        }

        /// <summary>
        /// Motor torque (N m, at the driven axle) a VehicleDefinition needs to
        /// reach 50 km/h in the given time, ignoring drag: F = m * a, T = F * r.
        /// Used to set motorTorque from the spec's 0-50 time.
        /// </summary>
        public static float TorqueFor0To50(float massKg, float seconds, float wheelRadius)
        {
            if (seconds <= 0f) return 0f;
            float a = (50f / KmhPerMs) / seconds;
            return massKg * a * wheelRadius;
        }

        /// <summary>
        /// Brake torque (N m, total over all wheels) for a target deceleration.
        /// </summary>
        public static float BrakeTorqueFor(float massKg, float decel, float wheelRadius)
        {
            return massKg * decel * wheelRadius;
        }

        /// <summary>
        /// Distance needed to stop from speed at decel (v^2 / 2a).
        /// </summary>
        public static float StoppingDistance(float speedMs, float decel)
        {
            if (decel <= 0f) return float.PositiveInfinity;
            return speedMs * speedMs / (2f * decel);
        }

        /// <summary>
        /// Traffic target speed for a gap (spec 8.2): the speed from which the
        /// vehicle can stop `standoff` metres short of the obstacle at `decel`,
        /// capped by vmax. A gap at or under the standoff gives 0.
        /// </summary>
        public static float SpeedForGap(float gap, float vmax, float standoff = 4.5f, float decel = 5f)
        {
            float room = gap - standoff;
            if (room <= 0f) return 0f;
            float v = (float)Math.Sqrt(2f * decel * room);
            return v < vmax ? v : vmax;
        }

        /// <summary>
        /// Follow a slower leader at a time gap (spec 8.2: 2 s). Returns the
        /// target speed so the gap grows toward speed * timeGap.
        /// </summary>
        public static float SpeedForFollow(float gap, float leaderSpeed, float vmax, float timeGap = 2f, float standoff = 4.5f)
        {
            float wanted = standoff + Math.Max(leaderSpeed, 0f) * timeGap;
            if (gap <= standoff) return 0f;
            // Proportional: at the wanted gap match the leader; closer is slower, further is faster.
            float v = leaderSpeed + (gap - wanted) * 0.5f;
            return Clamp(v, 0f, vmax);
        }

        /// <summary>
        /// Crossing rule (spec 8.5): first to arrive goes; on a tie (within
        /// tieWindow seconds) the lower registration id goes.
        /// </summary>
        public static bool HasRightOfWay(float myArrival, int myId, float otherArrival, int otherId, float tieWindow = 0.25f)
        {
            if (Math.Abs(myArrival - otherArrival) <= tieWindow) return myId < otherId;
            return myArrival < otherArrival;
        }

        /// <summary>Damage points for a collision impulse (spec 10).</summary>
        public static float DamageFromImpulse(float impulse, float threshold, float damageScale)
        {
            if (impulse <= threshold || damageScale <= 0f) return 0f;
            return impulse / damageScale;
        }

        /// <summary>
        /// Exit-point order (spec 7.2): the indices of candidate exit points in
        /// try order, skipping ones the caller marked blocked. Returns -1 when
        /// all are blocked (player stays in the seat).
        /// </summary>
        public static int FirstFreeExit(bool[] blocked)
        {
            if (blocked == null) return -1;
            for (int i = 0; i < blocked.Length; i++)
                if (!blocked[i]) return i;
            return -1;
        }

        /// <summary>Exit is allowed only below this forward speed (spec 7.2).</summary>
        public const float MaxExitSpeedMs = 2f;

        public static bool CanExit(float forwardSpeedMs) => Math.Abs(forwardSpeedMs) < MaxExitSpeedMs;

        /// <summary>
        /// Anti-roll force (N) for one axle from the two suspension extensions
        /// (0 = fully compressed, 1 = fully extended), the classic anti-roll bar.
        /// Apply -F (along the wheel's up) at the left wheel and +F at the right:
        /// a hanging left wheel is pushed down and the compressed right side up.
        /// </summary>
        public static float AntiRollForce(float leftExtension, float rightExtension, float stiffness)
        {
            return (leftExtension - rightExtension) * stiffness;
        }
    }
}
