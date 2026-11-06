// Naija Streets (Unity) - when does the player let go of a job?
// (docs/specs/jobs-m4.md section 4.3). Pure decision; PlayerJobs feeds it each frame.

namespace NaijaLife.Jobs
{
    public struct ReleaseInput
    {
        public float DistanceFromWork;   // metres from the slot's work point
        public bool OnTrip;              // riding the bus: distance and idle do not count
        public float IdleSeconds;        // since the last shout, tap or trip
        public float ShiftSecondsLeft;   // from the server's ends-by time
        public float Pawa;
        public bool EndRequested;        // End shift tapped, or another action needs the player
    }

    public static class ReleaseWatch
    {
        public const float LeaveRadius = 8f;
        public const float IdleLimitSeconds = 180f;

        /// <summary>Null while the player keeps the job. During a trip only Pawa can end it at once.</summary>
        public static ReleaseReason? Check(ReleaseInput i)
        {
            if (i.Pawa <= 0f) return ReleaseReason.PawaEmpty;
            if (i.OnTrip) return null;   // "We go finish this trip first."
            if (i.EndRequested) return ReleaseReason.ShiftEnded;
            if (i.ShiftSecondsLeft <= 0f) return ReleaseReason.ShiftEnded;
            if (i.DistanceFromWork > LeaveRadius) return ReleaseReason.WalkedAway;
            if (i.IdleSeconds > IdleLimitSeconds) return ReleaseReason.Idle;
            return null;
        }

        public static string PlayerText(ReleaseReason r)
        {
            switch (r)
            {
                case ReleaseReason.WalkedAway: return "You left the bus. The conductor takes it back.";
                case ReleaseReason.Idle: return "The conductor takes his bus back.";
                case ReleaseReason.PawaEmpty: return "You don tire. Shift over.";
                default: return null;
            }
        }
    }
}
